// Network map (T-12 step 3, decision 0025 §7). Plain ES module, no dependencies, works offline.
// Reads <data>/<strategy>/{run,sites,site-hours,flows,summary}.json for strategies B0, B1, B2, LP.

const STRATEGIES = ["B0", "B1", "B2", "LP"];
const CANDIDATE_DATA = [new URLSearchParams(location.search).get("data"),
  "../../results/network/switzerland-test", "../../data/derived/network/switzerland-test/mock-export"].filter(Boolean);
const CITY_NAMES = { basel: "Basel", zurich: "Zurich", bern: "Bern", geneva: "Geneva", lausanne: "Lausanne",
  lucerne: "Lucerne", "st-gallen": "St. Gallen", lugano: "Lugano", chur: "Chur" };
const KIND = { "reference-house-case": "Reference house (case)", "residential-sfh": "Single-family house",
  "residential-mfh": "Apartment building", "hotel-spa": "Hotel with spa", "indoor-pool": "Indoor pool" };
// Sequential heat ramp for the dark surface: low recedes toward the surface, high is bright (one hue).
const RAMP = ["#3b2a22", "#7c3a1e", "#b8471f", "#d95926", "#f0814c", "#f9ae86", "#ffd9c0"];
const NS = "http://www.w3.org/2000/svg";
const W = 1000, H = 600;

const params = new URLSearchParams(location.search);
const state = { runs: {}, strategy: null, metric: params.get("metric") === "coverage" ? "coverage" : "load",
  step: "day", t: Math.max(0, Math.min(364, Number(params.get("day") ?? 14) || 0)), playing: false, timer: null };
const $ = (id) => document.getElementById(id);
const el = (tag, attrs = {}, parent) => {
  const n = document.createElementNS(NS, tag);
  for (const [k, v] of Object.entries(attrs)) n.setAttribute(k, v);
  if (parent) parent.appendChild(n);
  return n;
};
const fmt = (x, d = 0) => x == null || Number.isNaN(x) ? "–" : x.toLocaleString("en-CH", { maximumFractionDigits: d, minimumFractionDigits: d });

function rampColor(v) {
  const x = Math.max(0, Math.min(1, v)) * (RAMP.length - 1);
  const i = Math.min(RAMP.length - 2, Math.floor(x)), f = x - i;
  const a = hex(RAMP[i]), b = hex(RAMP[i + 1]);
  return `rgb(${a.map((c, k) => Math.round(c + (b[k] - c) * f)).join(",")})`;
}
function hex(h) { return [1, 3, 5].map((i) => parseInt(h.slice(i, i + 2), 16)); }

async function getJson(url) { const r = await fetch(url); if (!r.ok) throw new Error(url); return r.json(); }

async function loadRuns() {
  for (const base of CANDIDATE_DATA) {
    const runs = {};
    await Promise.all(STRATEGIES.map(async (id) => {
      try {
        const run = await getJson(`${base}/${id}/run.json`);
        const [sites, hours, flows, summary] = await Promise.all(["sites", "site-hours", "flows", "summary"]
          .map((f) => getJson(`${base}/${id}/${f}.json`)));
        runs[id] = prepare({ id, base, run, sites, hours, flows, summary });
      } catch { /* strategy not exported */ }
    }));
    if (Object.keys(runs).length) return runs;
  }
  throw new Error("No exported runs found. Pass ?data=<folder> or run tools/make_mock.py.");
}

// Daily aggregates per site and for the network; flows indexed by hour and by day.
function prepare(r) {
  const n = r.hours.timeAxis.hours, days = Math.floor(n / 24);
  const siteIds = r.sites.sites.map((s) => s.id);
  const day = {}, net = { demand: new Float64Array(days), used: new Float64Array(days), moved: new Float64Array(days),
    demandH: new Float64Array(n), usedH: new Float64Array(n), movedH: new Float64Array(n) };
  for (const id of siteIds) {
    const h = r.hours.sites[id];
    const d = { demand: new Float64Array(days), used: new Float64Array(days), load: new Float64Array(days),
      t1: new Float64Array(days), missed: new Float64Array(days) };
    for (let i = 0; i < n; i++) {
      const k = Math.floor(i / 24);
      d.demand[k] += h.demandKwh[i]; d.used[k] += h.usedKwh[i]; d.load[k] += h.loadShare[i] / 24;
      d.t1[k] += h.type1ServerHours[i]; d.missed[k] += h.missedServerHours[i];
      net.demandH[i] += h.demandKwh[i]; net.usedH[i] += h.usedKwh[i];
    }
    for (let k = 0; k < days; k++) { net.demand[k] += d.demand[k]; net.used[k] += d.used[k]; }
    day[id] = d;
  }
  const byHour = new Map(), byDay = new Map(), movedOut = {}, movedIn = {};
  const col = Object.fromEntries(r.flows.columns.map((c, i) => [c, i]));
  for (const row of r.flows.rows) {
    const hr = row[col.hour], from = row[col.fromSiteId], to = row[col.toSiteId], sh = row[col.serverHours];
    for (const [map, key] of [[byHour, hr], [byDay, Math.floor(hr / 24)]]) {
      if (!map.has(key)) map.set(key, new Map());
      const m = map.get(key), kk = `${from}|${to}`;
      m.set(kk, (m.get(kk) || 0) + sh);
    }
    net.moved[Math.floor(hr / 24)] += sh; net.movedH[hr] += sh;
    movedOut[from] = (movedOut[from] || 0) + sh; movedIn[to] = (movedIn[to] || 0) + sh;
  }
  return { ...r, n, days, day, net, byHour, byDay, movedOut, movedIn };
}

// ---------- map ----------
let proj, siteXY = {}, cityXY = {};
function setupMap(cantons, sites) {
  const svg = $("map");
  svg.setAttribute("viewBox", `0 0 ${W} ${H}`);
  const lat0 = 46.8, kx = Math.cos(lat0 * Math.PI / 180);
  let minX = Infinity, maxX = -Infinity, minY = Infinity, maxY = -Infinity;
  const walk = (c, f) => (typeof c[0] === "number" ? f(c) : c.forEach((x) => walk(x, f)));
  for (const f of cantons.features) walk(f.geometry.coordinates, ([lon, lat]) => {
    minX = Math.min(minX, lon * kx); maxX = Math.max(maxX, lon * kx); minY = Math.min(minY, -lat); maxY = Math.max(maxY, -lat);
  });
  const pad = 24, s = Math.min((W - 2 * pad) / (maxX - minX), (H - 2 * pad) / (maxY - minY));
  const ox = (W - s * (maxX - minX)) / 2, oy = (H - s * (maxY - minY)) / 2;
  proj = ([lon, lat]) => [ox + s * (lon * kx - minX), oy + s * (-lat - minY)];

  const gC = el("g", {}, svg);
  for (const f of cantons.features) {
    const polys = f.geometry.type === "Polygon" ? [f.geometry.coordinates] : f.geometry.coordinates;
    const d = polys.map((p) => p.map((ring) => "M" + ring.map((c) => proj(c).map((v) => v.toFixed(1)).join(",")).join("L") + "Z").join("")).join("");
    el("path", { d, class: "canton" }, gC).appendChild(el("title")).textContent = f.properties.name;
  }
  // Sites of one city are drawn on a small ring around the city centre (their positions are synthetic anyway).
  const byCity = {};
  for (const st of sites) (byCity[st.id.split("-").slice(0, st.id.startsWith("st-gallen") ? 2 : 1).join("-")] ||= []).push(st);
  for (const [city, list] of Object.entries(byCity)) {
    const lat = list.reduce((a, s) => a + s.latitudeDeg.value, 0) / list.length;
    const lon = list.reduce((a, s) => a + s.longitudeDeg.value, 0) / list.length;
    const [cx, cy] = proj([lon, lat]);
    cityXY[city] = [cx, cy];
    list.forEach((st, i) => {
      const a = -Math.PI / 2 + (2 * Math.PI * i) / list.length, rr = list.length > 1 ? 22 : 0;
      siteXY[st.id] = [cx + rr * Math.cos(a), cy + rr * Math.sin(a)];
    });
  }
  el("g", { id: "flows" }, svg);
  el("g", { id: "sites" }, svg);
  const gL = el("g", {}, svg);
  for (const [city, [x, y]] of Object.entries(cityXY))
    el("text", { x: x, y: y + 44, class: "city-label", "text-anchor": "middle" }, gL).textContent = CITY_NAMES[city] || city;
}

const radius = (servers) => 4 + 1.5 * Math.sqrt(servers);

function drawSites() {
  const run = state.runs[state.strategy], g = $("sites");
  g.textContent = "";
  const sites = [...run.sites.sites].sort((a, b) => b.serverCount.value - a.serverCount.value);
  for (const st of sites) {
    const [x, y] = siteXY[st.id];
    const c = el("circle", { cx: x, cy: y, r: radius(st.serverCount.value), class: "site" + (st.kind === "reference-house-case" ? " site-ref" : ""), "data-id": st.id, tabindex: 0 }, g);
    c.addEventListener("mouseenter", (e) => showSiteTip(st, e));
    c.addEventListener("mousemove", (e) => moveTip(e));
    c.addEventListener("mouseleave", hideTip);
    c.addEventListener("focus", (e) => showSiteTip(st, e));
    c.addEventListener("blur", hideTip);
  }
}

function valueAt(run, id) {
  const h = run.hours.sites[id];
  if (state.step === "hour") {
    const i = state.t;
    return { demand: h.demandKwh[i], used: h.usedKwh[i], load: h.loadShare[i], t1: h.type1ServerHours[i], missed: h.missedServerHours[i] };
  }
  const d = run.day[id], k = state.t;
  return { demand: d.demand[k], used: d.used[k], load: d.load[k], t1: d.t1[k], missed: d.missed[k] };
}

function update() {
  const run = state.runs[state.strategy];
  for (const c of $("sites").querySelectorAll("circle")) {
    const v = valueAt(run, c.dataset.id);
    const m = state.metric === "load" ? v.load : (v.demand > 0 ? v.used / v.demand : 1);
    c.setAttribute("fill", rampColor(m));
  }
  // flows for the current step
  const gF = $("flows"); gF.textContent = "";
  const m = (state.step === "hour" ? run.byHour.get(state.t) : run.byDay.get(state.t)) || new Map();
  const max = Math.max(1e-9, ...m.values());
  for (const [key, sh] of m) {
    const [from, to] = key.split("|");
    const [x1, y1] = siteXY[from], [x2, y2] = siteXY[to];
    const mx = (x1 + x2) / 2 - (y2 - y1) * 0.35, my = (y1 + y2) / 2 + (x2 - x1) * 0.35;
    const sw = 1.8 + 3.5 * Math.sqrt(sh / max), d = `M${x1},${y1} Q${mx},${my} ${x2},${y2}`;
    el("path", { d, class: "flow-halo", "stroke-width": (sw + 5).toFixed(2) }, gF);
    const p = el("path", { d, class: "flow", "stroke-width": sw.toFixed(2) }, gF);
    el("title", {}, p).textContent = `${fmt(sh, 1)} server-hours of Type 1 work moved`;
  }
  // now panel
  const t = state.t, net = run.net;
  const demand = state.step === "hour" ? net.demandH[t] : net.demand[t];
  const used = state.step === "hour" ? net.usedH[t] : net.used[t];
  const moved = state.step === "hour" ? net.movedH[t] : net.moved[t];
  const per = state.step === "hour" ? "kWh/h" : "MWh/day", div = state.step === "hour" ? 1 : 1000;
  $("now-date").textContent = label(t);
  $("now-demand").innerHTML = `${fmt(demand / div, 1)}<small> ${per}</small>`;
  $("now-used").innerHTML = `${fmt(used / div, 1)}<small> ${per}</small>`;
  $("now-moved").innerHTML = `${fmt(moved, 0)}<small> server-h</small>`;
  $("slider").value = t;
  drawPlayhead();
  if (tipSite) showSiteTip(tipSite);
}

function label(t) {
  const d = new Date(Date.UTC(2025, 0, 1) + (state.step === "hour" ? t * 3600e3 : t * 86400e3));
  const s = d.toLocaleDateString("en-GB", { weekday: "short", day: "numeric", month: "long", timeZone: "UTC" });
  return state.step === "hour" ? `${s}, ${String(d.getUTCHours()).padStart(2, "0")}:00` : s;
}

// ---------- tooltip ----------
let tipSite = null, lastEvt = null;
function showSiteTip(st, e) {
  tipSite = st; if (e) lastEvt = e;
  const run = state.runs[state.strategy], v = valueAt(run, st.id), tip = $("tooltip");
  const unit = state.step === "hour" ? "kWh" : "kWh/day";
  const syn = ["serverCount", "tankCapacityKwh", "latitudeDeg"].filter((k) => st[k]?.synthetic);
  tip.innerHTML = `<div class="t-title">${st.name}</div>
    <div class="row"><span>${KIND[st.kind] || st.kind} · ${st.canton}</span></div>
    <div class="row"><span>Servers</span><b>${fmt(st.serverCount.value)}</b></div>
    <div class="row"><span>Heat demand</span><b>${fmt(v.demand, 1)} ${unit}</b></div>
    <div class="row"><span>Covered by servers</span><b>${v.demand > 0 ? fmt(100 * v.used / v.demand, 0) : "–"} %</b></div>
    <div class="row"><span>Server load</span><b>${fmt(100 * v.load, 0)} %</b></div>
    <div class="row"><span>Type 1 work here</span><b>${fmt(v.t1, 1)} server-h</b></div>
    ${v.missed > 1e-6 ? `<div class="row"><span>Missed deadlines</span><b>${fmt(v.missed, 1)} server-h</b></div>` : ""}
    <div class="row"><span>Year: moved out / in</span><b>${fmt(run.movedOut[st.id] || 0)} / ${fmt(run.movedIn[st.id] || 0)} server-h</b></div>
    ${syn.length ? `<div class="syn">Synthetic: ${syn.map((k) => ({ serverCount: "servers", tankCapacityKwh: "tank", latitudeDeg: "position" })[k]).join(", ")}</div>` : ""}`;
  tip.hidden = false;
  if (lastEvt) moveTip(lastEvt);
}
function moveTip(e) {
  lastEvt = e;
  const tip = $("tooltip"), box = $("map").parentElement.getBoundingClientRect();
  const x = (e.clientX ?? box.left + 40) - box.left + 14, y = (e.clientY ?? box.top + 40) - box.top + 14;
  tip.style.left = Math.min(x, box.width - tip.offsetWidth - 8) + "px";
  tip.style.top = Math.min(y, box.height - tip.offsetHeight - 8) + "px";
}
function hideTip() { tipSite = null; $("tooltip").hidden = true; }

// ---------- side panel ----------
function drawKpis() {
  const run = state.runs[state.strategy], s = run.summary.network;
  $("kpi-strategy").textContent = run.run.strategyLabel || run.id;
  const tiles = [
    ["Heat demand covered", `${fmt(100 * s.coverage, 1)} %`, "rest from backup heaters"],
    ["Heat dumped", `${fmt((s.dumpedHeatKwh || 0) / 1000, 1)} MWh`, "heat nobody needed"],
    ["Server utilisation", `${fmt(100 * s.utilisation, 1)} %`, "running hours ÷ installed"],
    ["Missed deadlines", s.missedDeadlinePercent == null ? "–" : `${fmt(s.missedDeadlinePercent, 1)} %`, "of Type 1 work due"],
    ["Work moved", `${fmt((s.movedServerHours || 0) / 1000, 1)}k`, "server-hours between sites"],
    ["Sites", fmt(run.sites.sites.length), `${fmt(run.sites.sites.reduce((a, x) => a + x.serverCount.value, 0))} servers`],
  ];
  $("kpis").innerHTML = tiles.map(([k, v, d]) => `<div class="kpi"><div class="k">${k}</div><div class="v">${v}</div><div class="d">${d}</div></div>`).join("");
  const r = run.run;
  $("run-note").textContent = r.mock ? "Mock data for layout only. Run the network engine export to see real results."
    : `Forecast: ${r.forecast?.label ?? "–"} · horizon ${r.horizonHours ?? "–"} h · commit ${String(r.gitCommit ?? "–").slice(0, 7)}`;
  $("badge-mock").hidden = !r.mock;
}

function drawStrategyButtons() {
  const seg = $("strategy-seg");
  seg.innerHTML = Object.keys(state.runs).sort((a, b) => STRATEGIES.indexOf(a) - STRATEGIES.indexOf(b)).map((id) =>
    `<button data-s="${id}" role="radio" aria-checked="${id === state.strategy}" class="${id === state.strategy ? "on" : ""}" title="${state.runs[id].run.strategyLabel || id}">${id}</button>`).join("");
  seg.onclick = (e) => { const id = e.target.dataset.s; if (!id) return; state.strategy = id; drawStrategyButtons(); drawSites(); drawKpis(); drawChart(); drawTable(); update(); };
}

function drawLegend() {
  const name = state.metric === "load" ? "Server load" : "Heat demand covered by servers";
  const grad = `linear-gradient(90deg, ${RAMP.join(",")})`;
  $("legend").innerHTML = `<span>${name}: 0 %<span class="ramp" style="background:${grad}"></span>100 %</span>
    <span><svg width="46" height="18"><circle cx="8" cy="9" r="${radius(4)}" fill="#7c3a1e"/><circle cx="32" cy="9" r="${Math.min(9, radius(30))}" fill="#7c3a1e"/></svg>size = servers</span>
    <span><svg width="34" height="10"><path d="M2,5 L32,5" class="flow" stroke-width="3"/></svg>Type 1 work moving</span>
    <span>Sites are drawn around their city</span>`;
}

// ---------- timeline ----------
function drawChart() {
  const run = state.runs[state.strategy], svg = $("chart"), w = svg.clientWidth || 900, h = 110;
  svg.setAttribute("viewBox", `0 0 ${w} ${h}`); svg.textContent = "";
  const padL = 44, padR = 8, top = 6, plotH = 70, stripY = 84, stripH = 14, days = run.days;
  const max = Math.max(...run.net.demand) / 1000;
  const x = (k) => padL + (k / (days - 1)) * (w - padL - padR), y = (v) => top + plotH - (v / max) * plotH;
  const ax = el("g", { class: "axis" }, svg);
  for (const v of [0, max / 2, max]) {
    el("line", { x1: padL, x2: w - padR, y1: y(v), y2: y(v), class: "gridline" }, ax);
    el("text", { x: padL - 6, y: y(v) + 3, "text-anchor": "end" }, ax).textContent = fmt(v, 0);
  }
  el("text", { x: padL - 6, y: stripY + stripH - 3, "text-anchor": "end" }, ax).textContent = "moved";
  el("text", { x: padL + 6, y: top + 10 }, ax).textContent = "MWh per day";
  ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"].forEach((m, i) => {
    const k = Math.round(i * 30.42);
    el("text", { x: x(k) + 2, y: h - 1 }, ax).textContent = m;
  });
  let area = `M${x(0)},${y(0)}`; let line = "";
  for (let k = 0; k < days; k++) { area += `L${x(k)},${y(run.net.demand[k] / 1000)}`; line += `${k ? "L" : "M"}${x(k)},${y(run.net.used[k] / 1000)}`; }
  el("path", { d: area + `L${x(days - 1)},${y(0)}Z`, fill: "var(--demand)", opacity: 0.55 }, svg);
  el("path", { d: line, fill: "none", stroke: "var(--heat)", "stroke-width": 2 }, svg);
  const maxMoved = Math.max(1e-9, ...run.net.moved), bw = Math.max(1, (w - padL - padR) / days - 0.5);
  for (let k = 0; k < days; k++) if (run.net.moved[k] > 0)
    el("rect", { x: x(k) - bw / 2, y: stripY, width: bw, height: stripH, rx: 1, fill: "var(--flow)", opacity: (0.25 + 0.75 * run.net.moved[k] / maxMoved).toFixed(2) }, svg);
  el("line", { id: "playhead", y1: top - 2, y2: stripY + stripH + 2, stroke: "var(--text-primary)", "stroke-width": 1.5 }, svg);
  const hit = el("rect", { x: padL, y: 0, width: w - padL - padR, height: h, fill: "transparent", style: "cursor:pointer" }, svg);
  const pick = (e) => Math.round(((e.offsetX - padL) / (w - padL - padR)) * (days - 1));
  hit.addEventListener("click", (e) => { const k = Math.max(0, Math.min(days - 1, pick(e))); state.t = state.step === "hour" ? k * 24 + 12 : k; update(); });
  hit.addEventListener("mousemove", (e) => {
    const k = Math.max(0, Math.min(days - 1, pick(e)));
    hit.setAttribute("title", "");
    const tip = $("tooltip"), box = $("map").parentElement.getBoundingClientRect();
    tip.innerHTML = `<div class="t-title">${new Date(Date.UTC(2025, 0, 1) + k * 86400e3).toLocaleDateString("en-GB", { day: "numeric", month: "long", timeZone: "UTC" })}</div>
      <div class="row"><span>Heat demand</span><b>${fmt(run.net.demand[k] / 1000, 1)} MWh</b></div>
      <div class="row"><span>Covered by servers</span><b>${fmt(run.net.used[k] / 1000, 1)} MWh</b></div>
      <div class="row"><span>Work moved</span><b>${fmt(run.net.moved[k], 0)} server-h</b></div>`;
    tip.hidden = false;
    tip.style.left = Math.min(e.clientX - box.left + 12, box.width - tip.offsetWidth - 8) + "px";
    tip.style.top = (box.height - tip.offsetHeight - 8) + "px";
  });
  hit.addEventListener("mouseleave", hideTip);
  state.chart = { x, days };
  drawPlayhead();
}
function drawPlayhead() {
  const p = $("playhead"); if (!p || !state.chart) return;
  const k = state.step === "hour" ? state.t / 24 : state.t, x = state.chart.x(k);
  p.setAttribute("x1", x); p.setAttribute("x2", x);
}

// ---------- table ----------
function drawTable() {
  const run = state.runs[state.strategy];
  const rows = run.sites.sites.map((st) => {
    const d = run.day[st.id], sum = (a) => a.reduce((x, y) => x + y, 0);
    const dem = sum(d.demand), used = sum(d.used);
    return `<tr><td>${st.name}</td><td>${KIND[st.kind] || st.kind}</td><td>${fmt(st.serverCount.value)}${st.serverCount.synthetic ? "*" : ""}</td>
      <td>${fmt(dem / 1000, 1)}</td><td>${fmt(100 * used / dem, 1)}</td><td>${fmt(sum(d.t1), 0)}</td>
      <td>${fmt(run.movedOut[st.id] || 0)}</td><td>${fmt(run.movedIn[st.id] || 0)}</td><td>${fmt(sum(d.missed), 1)}</td></tr>`;
  }).join("");
  $("site-table").innerHTML = `<thead><tr><th>Site</th><th>Kind</th><th>Servers</th><th>Demand MWh</th><th>Covered %</th>
    <th>Type 1 server-h</th><th>Moved out</th><th>Moved in</th><th>Missed server-h</th></tr></thead><tbody>${rows}</tbody>
    <caption style="caption-side:bottom;text-align:left;color:var(--text-muted);padding-top:6px">* synthetic value (config/network/README.md)</caption>`;
}

// ---------- controls ----------
function setStep(step) {
  if (step === state.step) return;
  state.t = step === "hour" ? state.t * 24 + 12 : Math.floor(state.t / 24);
  state.step = step;
  $("slider").max = step === "hour" ? state.runs[state.strategy].n - 1 : state.runs[state.strategy].days - 1;
  for (const b of $("step-seg").children) { const on = b.dataset.step === step; b.classList.toggle("on", on); b.setAttribute("aria-checked", on); }
  update();
}
function togglePlay() {
  state.playing = !state.playing;
  $("play").textContent = state.playing ? "❚❚" : "▶";
  $("play").setAttribute("aria-label", state.playing ? "Pause" : "Play");
  clearInterval(state.timer);
  if (state.playing) state.timer = setInterval(() => {
    state.t = (state.t + 1) % (Number($("slider").max) + 1); update();
  }, state.step === "hour" ? 120 : 220);
}

async function main() {
  try {
    const [cantons, runs] = await Promise.all([getJson("data/switzerland-cantons.geojson"), loadRuns()]);
    state.runs = runs;
    state.strategy = [params.get("strategy"), "B2", "LP", "B1", "B0"].find((s) => s && runs[s]) || Object.keys(runs)[0];
    setupMap(cantons, runs[state.strategy].sites.sites);
    drawStrategyButtons(); drawSites(); drawKpis(); drawLegend(); drawChart(); drawTable(); update();
  } catch (err) {
    $("now-date").textContent = String(err.message || err);
    return;
  }
  $("metric-seg").onclick = (e) => {
    const m = e.target.dataset.metric; if (!m) return; state.metric = m;
    for (const b of $("metric-seg").children) { const on = b.dataset.metric === m; b.classList.toggle("on", on); b.setAttribute("aria-checked", on); }
    drawLegend(); update();
  };
  $("step-seg").onclick = (e) => { const s = e.target.dataset.step; if (s) { if (state.playing) togglePlay(); setStep(s); } };
  $("slider").oninput = (e) => { state.t = Number(e.target.value); update(); };
  $("play").onclick = togglePlay;
  $("toggle-table").onclick = () => {
    const tv = $("table-view"), open = tv.hidden; tv.hidden = !open;
    $("toggle-table").setAttribute("aria-expanded", open); $("toggle-table").textContent = open ? "Hide table" : "Show sites as table";
  };
  window.addEventListener("resize", () => drawChart());
  document.addEventListener("keydown", (e) => { if (e.code === "Space" && e.target === document.body) { e.preventDefault(); togglePlay(); } });
}
main();
