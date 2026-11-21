// T-18 reads the T-13 export. It aggregates existing hourly results for display;
// it never schedules jobs or recalculates the heat balance.
const showcase = '../../data/derived/reference-house-v1/';
const byId = (id) => document.getElementById(id);
const number = (value, digits = 0) => new Intl.NumberFormat('en-CH', {
  minimumFractionDigits: digits, maximumFractionDigits: digits
}).format(value);
const kwh = (value, digits = 0) => `${number(value, digits)} kWh`;
let bundle;
let activeDay;
let activeHour = 18;

function parseCsv(text) {
  const rows = [];
  let row = [], cell = '', quoted = false;
  for (let i = 0; i < text.length; i++) {
    const char = text[i];
    if (quoted) {
      if (char === '"' && text[i + 1] === '"') { cell += '"'; i++; }
      else if (char === '"') quoted = false;
      else cell += char;
    } else if (char === '"') quoted = true;
    else if (char === ',') { row.push(cell); cell = ''; }
    else if (char === '\n') { row.push(cell.replace(/\r$/, '')); rows.push(row); row = []; cell = ''; }
    else cell += char;
  }
  if (cell || row.length) { row.push(cell); rows.push(row); }
  if (quoted) throw new Error('The hourly CSV has an unfinished quoted value.');
  const [header, ...values] = rows;
  if (!header || !header.includes('local_time')) throw new Error('The hourly CSV has no T-13 header.');
  if (values.some((r) => r.length !== header.length))
    throw new Error('The hourly CSV contains an incomplete row.');
  return values.map((r) => {
    const out = {};
    header.forEach((key, i) => out[key] = key === 'local_time' ? r[i] : Number(r[i]));
    return out;
  });
}

function validate(data) {
  const { summary, parameters, run, rows } = data;
  if (summary.schema_version !== 1 || parameters.schema_version !== 1 || run.schema_version !== 1)
    throw new Error('This view supports T-13 result bundle schema version 1.');
  if (!summary.sites?.length) throw new Error('The run contains no sites.');
  if (!Number.isFinite(run.step_hours) || run.step_hours <= 0)
    throw new Error('The run has no valid hourly step length.');
  const site = summary.sites[0];
  if (!Number.isFinite(site.summary?.coverage) || !Number.isFinite(site.summary?.heat_value_chf)
      || !Number.isFinite(site.summary?.server_heat_used_kwh)
      || !Number.isFinite(site.summary?.backup_heat_kwh))
    throw new Error('The run summary has missing or invalid heat figures.');
  const required = ['direct_used_kwh', 'tank_discharge_kwh', 'backup_heat_kwh',
    'tank_stored_kwh', 'actual_load_per_server', 'local_time'];
  if (!rows.length || required.some((key) => !(key in rows[0])))
    throw new Error('The selected hourly file lacks required columns.');
  if (rows.some((r) => required.slice(0, -1).some((key) => !Number.isFinite(r[key]))))
    throw new Error('The selected hourly file has an invalid numeric value.');
  return { ...data, site };
}

async function loadShowcase() {
  const read = async (path) => {
    const response = await fetch(showcase + path);
    if (!response.ok) throw new Error(`Could not load ${path} (${response.status}).`);
    return response.text();
  };
  const [summaryText, parametersText, runText] = await Promise.all([
    read('summary.json'), read('parameters.json'), read('run.json')
  ]);
  const summary = JSON.parse(summaryText);
  const hourlyPath = summary.sites?.[0]?.hourly_path;
  if (!hourlyPath || hourlyPath.includes('..')) throw new Error('Invalid hourly path in summary.json.');
  return validate({ summary, parameters: JSON.parse(parametersText), run: JSON.parse(runText),
    rows: parseCsv(await read(hourlyPath)), label: 'Reference-house example · Basel 2025' });
}

async function loadFolder(fileList) {
  const files = Array.from(fileList);
  const find = (path) => files.find((f) =>
    (f.webkitRelativePath || f.name).replaceAll('\\', '/').endsWith('/' + path)
    || f.name === path);
  const required = ['summary.json', 'parameters.json', 'run.json'];
  const missing = required.filter((name) => !find(name));
  if (missing.length) throw new Error(`The selected folder is missing ${missing.join(', ')}.`);
  const [summary, parameters, run] = await Promise.all(required.map(async (name) =>
    JSON.parse(await find(name).text())));
  const hourlyPath = summary.sites?.[0]?.hourly_path;
  if (!hourlyPath || hourlyPath.includes('..') || !find(hourlyPath))
    throw new Error(`The selected folder is missing ${hourlyPath || 'the hourly CSV'}.`);
  return validate({ summary, parameters, run, rows: parseCsv(await find(hourlyPath).text()),
    label: files[0].webkitRelativePath?.split('/')[0] || 'Selected run' });
}

function useful(row) { return row.direct_used_kwh + row.tank_discharge_kwh; }
function days() { return [...new Set(bundle.rows.map((r) => r.local_time.slice(0, 10)))]; }
function setDay(date) {
  activeDay = date;
  const dayRows = bundle.rows.filter((r) => r.local_time.startsWith(date));
  if (!dayRows.length) return;
  activeHour = Math.min(activeHour, dayRows.length - 1);
  byId('hour-range').max = String(dayRows.length - 1);
  byId('hour-range').value = String(activeHour);
  const dateLabel = new Date(`${date}T12:00:00`).toLocaleDateString('en-GB',
    { day: 'numeric', month: 'long', year: 'numeric' });
  byId('selected-day').textContent = dateLabel;
  for (const [id, monthDay] of [['january', '-01-15'], ['july', '-07-15']]) {
    const button = byId(id);
    button.classList.toggle('active', date.endsWith(monthDay));
    button.disabled = !days().some((d) => d.endsWith(monthDay));
  }
  const max = Math.max(...dayRows.map((r) => r.heat_demand_kwh), 0.01);
  const chart = byId('day-chart');
  chart.replaceChildren();
  dayRows.forEach((row, index) => {
    const col = document.createElement('button');
    col.type = 'button';
    col.className = 'chart-col';
    col.setAttribute('aria-label', `${row.local_time.slice(11)}: ${kwh(useful(row), 2)} computing, ${kwh(row.backup_heat_kwh, 2)} regular heating`);
    col.title = col.getAttribute('aria-label');
    for (const [type, value] of [['compute', useful(row)], ['backup', row.backup_heat_kwh]]) {
      const segment = document.createElement('span');
      segment.className = type;
      segment.style.height = `${Math.max(0, value) / max * 100}%`;
      col.append(segment);
    }
    col.addEventListener('click', () => setHour(index));
    chart.append(col);
  });
  chart.setAttribute('aria-label', `${dateLabel}: teal is computing heat; tan is regular heating. Select a bar for one hour.`);
  setHour(activeHour);
}

function setHour(index) {
  const rows = bundle.rows.filter((r) => r.local_time.startsWith(activeDay));
  activeHour = Math.max(0, Math.min(index, rows.length - 1));
  const row = rows[activeHour];
  byId('hour-range').value = String(activeHour);
  const time = row.local_time.slice(11);
  byId('hour-output').textContent = time;
  byId('hour-time').textContent = `${byId('selected-day').textContent.toUpperCase()} · ${time}`;
  const used = useful(row);
  byId('hour-message').textContent = row.backup_heat_kwh > 0.01
    ? 'Computing helps. The regular heater covers the rest.'
    : row.actual_load_per_server < 0.01
      ? 'The home needs no server heat right now.'
      : 'Computing covers this hour’s heating need.';
  byId('hour-computing').textContent = kwh(used, 2);
  byId('hour-rate').textContent = `${number(used / bundle.run.step_hours, 2)} kW`;
  byId('hour-backup').textContent = kwh(row.backup_heat_kwh, 2);
  byId('hour-load').textContent = `${number(row.actual_load_per_server * 100)}% of installed capacity`;
  const capacity = bundle.parameters.reference_parameters?.tank_capacity_kwh?.value;
  byId('hour-tank').textContent = Number.isFinite(capacity)
    ? `${kwh(row.tank_stored_kwh, 1)} / ${kwh(capacity, 0)}` : kwh(row.tank_stored_kwh, 1);
  byId('day-computing').textContent = kwh(rows.reduce((sum, r) => sum + useful(r), 0), 1);
  [...byId('day-chart').children].forEach((bar, i) => bar.classList.toggle('selected', i === activeHour));
}

function render(data) {
  bundle = data;
  const summary = data.site.summary;
  byId('run-note').textContent = `${data.label} · ${summary.server_count} servers · ${data.run.timezone} · ${summary.job_demand_assumption?.startsWith('unlimited') ? 'unlimited-job comparison' : 'job scenario'}`;
  byId('job-note').textContent = summary.job_demand_assumption?.startsWith('unlimited')
    ? 'This example assumes unlimited available jobs.'
    : 'Job supply and deadlines follow the selected run’s scenario.';
  byId('coverage').textContent = number(summary.coverage * 100);
  byId('year-computing').textContent = kwh(summary.server_heat_used_kwh);
  byId('year-backup').textContent = kwh(summary.backup_heat_kwh);
  byId('delivered-year').textContent = kwh(summary.server_heat_used_kwh);
  byId('heat-value').textContent = `CHF ${number(summary.heat_value_chf)}`;
  byId('server-count').textContent = number(summary.server_count);
  byId('bar-computing').style.width = `${summary.coverage * 100}%`;
  byId('bar-backup').style.width = `${(1 - summary.coverage) * 100}%`;
  byId('provenance').textContent = data.run.git_commit
    ? `Run revision ${data.run.git_commit.slice(0, 8)}${data.run.git_dirty ? ' · modified code' : ''}`
    : 'Run revision unknown';
  const availableDays = days();
  setDay(availableDays.find((d) => d.endsWith('-01-15')) || availableDays[0]);
  setHour(Math.min(18, bundle.rows.filter((r) => r.local_time.startsWith(activeDay)).length - 1));
}

byId('january').addEventListener('click', () => {
  const date = days().find((d) => d.endsWith('-01-15')); if (date) setDay(date);
});
byId('july').addEventListener('click', () => {
  const date = days().find((d) => d.endsWith('-07-15')); if (date) setDay(date);
});
byId('hour-range').addEventListener('input', (event) => setHour(Number(event.target.value)));
byId('run-folder').addEventListener('change', async (event) => {
  try { render(await loadFolder(event.target.files)); }
  catch (error) { byId('run-note').textContent = `Could not open run: ${error.message}`; }
});
loadShowcase().then(render).catch((error) => {
  byId('run-note').textContent = `Could not load the example: ${error.message} Run “python -m http.server 8000” from the repository root and open the page via localhost.`;
});
