// Mock data for the admin dashboard. Deterministic: every value is a pure function of the site,
// the hour and a fixed seed, so a reload shows the same picture. Replace with exported runs (T-13).
//
// Physics follows the case where it can (heating formula, 0.8 kW heat per 1 kW server, heat-led
// load); network layout, weather, jobs and noise are MOCK values chosen to look plausible.

import { cantonElectricityChfPerKwh } from '../params'

export const MS_PER_HOUR = 3_600_000
export const HOURS_PER_DAY = 24
const DAYS_PER_YEAR = 365

export const PHYSICS = {
  serverElectricKw: 1, // docs/case/challenge.md: 1 kW rack server
  recoveredShare: 0.8, // docs/case/challenge.md: 80 % recoverable into water
  baseTemperatureC: 20, // docs/case/challenge.md: heating formula
  formulaDivisorC: 25.6, // docs/case/challenge.md: heating formula
  heatingCutoffC: 12, // docs/case/challenge.md: heating day below 12 °C daily mean (used hourly here)
  computePriceChfPerServerHour: 0.66, // decision 0018: pinned computing price for scored figures
} as const

// MOCK: design heating power and hot water per site type, home = case reference house.
export const SITE_TYPES = {
  home: { label: 'Home', designKw: 10, hotWaterKwhPerDay: 12, servers: [3, 9] },
  hotel: { label: 'Hotel', designKw: 45, hotWaterKwhPerDay: 140, servers: [8, 13] },
  office: { label: 'Office', designKw: 25, hotWaterKwhPerDay: 10, servers: [6, 13] },
} as const
export type SiteType = keyof typeof SITE_TYPES

export interface Site {
  id: string
  name: string
  type: SiteType
  canton: string
  city: string
  lat: number
  lon: number
  servers: number
  tankKwh: number
  insulation: number // MOCK: multiplies the design heating power, 0.6 (new) to 1.3 (old)
}

// ---------- deterministic randomness ----------

function hash(...values: number[]): number {
  let h = 2166136261
  for (const v of values) {
    h ^= Math.floor(v) | 0
    h = Math.imul(h, 16777619)
    h ^= h >>> 13
    h = Math.imul(h, 0x5bd1e995)
    h ^= h >>> 15
  }
  return (h >>> 0) / 4294967296
}

function rng(seed: number) {
  let a = seed >>> 0
  return () => {
    a = (a + 0x6d2b79f5) >>> 0
    let t = a
    t = Math.imul(t ^ (t >>> 15), t | 1)
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61)
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296
  }
}

/** Smooth noise in [-1, 1] over hours, knots every `periodH` hours. */
function smoothNoise(seed: number, hour: number, periodH: number) {
  const k = Math.floor(hour / periodH)
  const f = hour / periodH - k
  const a = hash(seed, k) * 2 - 1
  const b = hash(seed, k + 1) * 2 - 1
  const s = f * f * (3 - 2 * f)
  return a + (b - a) * s
}

const clamp = (v: number, lo: number, hi: number) => Math.min(hi, Math.max(lo, v))

// ---------- network ----------

const CITIES: { city: string; canton: string; lat: number; lon: number; spread: number; sites: number }[] = [
  { city: 'Basel', canton: 'BS', lat: 47.559, lon: 7.588, spread: 0.035, sites: 30 },
  { city: 'Riehen', canton: 'BS', lat: 47.584, lon: 7.648, spread: 0.012, sites: 5 },
  { city: 'Allschwil', canton: 'BL', lat: 47.551, lon: 7.536, spread: 0.01, sites: 4 },
  { city: 'Liestal', canton: 'BL', lat: 47.484, lon: 7.734, spread: 0.012, sites: 4 },
  { city: 'Zürich', canton: 'ZH', lat: 47.377, lon: 8.54, spread: 0.04, sites: 12 },
  { city: 'Winterthur', canton: 'ZH', lat: 47.5, lon: 8.724, spread: 0.02, sites: 4 },
  { city: 'Bern', canton: 'BE', lat: 46.948, lon: 7.447, spread: 0.03, sites: 7 },
  { city: 'Luzern', canton: 'LU', lat: 47.05, lon: 8.309, spread: 0.02, sites: 5 },
  { city: 'St. Gallen', canton: 'SG', lat: 47.424, lon: 9.377, spread: 0.02, sites: 4 },
  { city: 'Genève', canton: 'GE', lat: 46.204, lon: 6.143, spread: 0.03, sites: 6 },
  { city: 'Lausanne', canton: 'VD', lat: 46.52, lon: 6.633, spread: 0.025, sites: 5 },
  { city: 'Lugano', canton: 'TI', lat: 46.005, lon: 8.952, spread: 0.02, sites: 3 },
  { city: 'Chur', canton: 'GR', lat: 46.85, lon: 9.532, spread: 0.015, sites: 3 },
  { city: 'Sion', canton: 'VS', lat: 46.233, lon: 7.36, spread: 0.015, sites: 2 },
  { city: 'Aarau', canton: 'AG', lat: 47.392, lon: 8.044, spread: 0.015, sites: 3 },
]

function buildSites(): Site[] {
  const rand = rng(20261121)
  const sites: Site[] = []
  for (const c of CITIES) {
    for (let i = 0; i < c.sites; i++) {
      const r = rand()
      const type: SiteType = r < 0.78 ? 'home' : r < 0.9 ? 'office' : 'hotel'
      const [lo, hi] = SITE_TYPES[type].servers
      const angle = rand() * 2 * Math.PI
      const dist = Math.sqrt(rand()) * c.spread
      const n = String(sites.length + 1).padStart(3, '0')
      sites.push({
        id: `${c.canton}-${n}`,
        name: `${SITE_TYPES[type].label} ${c.city} ${n}`,
        type,
        canton: c.canton,
        city: c.city,
        lat: c.lat + dist * Math.sin(angle) * 0.7,
        lon: c.lon + dist * Math.cos(angle),
        servers: lo + Math.floor(rand() * (hi - lo + 1)),
        insulation: 0.6 + rand() * 0.7,
        tankKwh: type === 'home' ? 35 : type === 'office' ? 120 : 300, // home: case tank; others MOCK
      })
    }
  }
  return sites
}

export const SITES = buildSites()

// ---------- weather (MOCK) ----------

// MOCK relief: a few Gaussian ridges (Jura, Bernese and Valais Alps, Central Alps, Graubünden).
const RIDGES = [
  { lat: 47.25, lon: 7.1, sLat: 0.18, sLon: 0.45, m: 500 },
  { lat: 46.5, lon: 7.7, sLat: 0.2, sLon: 0.5, m: 1700 },
  { lat: 46.05, lon: 7.6, sLat: 0.15, sLon: 0.45, m: 1900 },
  { lat: 46.65, lon: 8.6, sLat: 0.25, sLon: 0.45, m: 1500 },
  { lat: 46.65, lon: 9.8, sLat: 0.3, sLon: 0.45, m: 1700 },
]

/** Rough elevation in metres. MOCK. */
function elevationM(lat: number, lon: number) {
  let m = 300
  for (const r of RIDGES) m += r.m * Math.exp(-(((lat - r.lat) / r.sLat) ** 2 + ((lon - r.lon) / r.sLon) ** 2))
  return m
}

/** Value noise that is smooth in space (smoothstep-interpolated grid of `cellDeg`) and in time. */
function spatialNoise(seed: number, lat: number, lon: number, hour: number, cellDeg: number) {
  const gx = lon / cellDeg
  const gy = lat / cellDeg
  const x0 = Math.floor(gx)
  const y0 = Math.floor(gy)
  const fx = (gx - x0) ** 2 * (3 - 2 * (gx - x0))
  const fy = (gy - y0) ** 2 * (3 - 2 * (gy - y0))
  const at = (x: number, y: number) => smoothNoise(seed + x * 31 + y * 7919, hour, 18)
  const top = at(x0, y0) * (1 - fx) + at(x0 + 1, y0) * fx
  const bottom = at(x0, y0 + 1) * (1 - fx) + at(x0 + 1, y0 + 1) * fx
  return top * (1 - fy) + bottom * fy
}

function regionalNoise(lat: number, lon: number, hour: number) {
  return 0.7 * spatialNoise(11, lat, lon, hour, 0.7) + 0.3 * spatialNoise(29, lat, lon, hour, 0.25)
}

/** Air temperature in °C at a place and hour. Seasonal + daily cycle + regional weather + lapse rate. */
export function temperatureC(lat: number, lon: number, t: number) {
  const hour = t / MS_PER_HOUR
  const date = new Date(t)
  const start = Date.UTC(date.getUTCFullYear(), 0, 1)
  const dayOfYear = (t - start) / (MS_PER_HOUR * HOURS_PER_DAY)
  const localHour = (date.getUTCHours() + 1) % HOURS_PER_DAY
  const seasonal = 11 - 9 * Math.cos((2 * Math.PI * (dayOfYear - 20)) / DAYS_PER_YEAR)
  const daily = 4 * Math.cos((2 * Math.PI * (localHour - 15)) / HOURS_PER_DAY)
  const weather = 3.5 * smoothNoise(7, hour, 60) + 2.5 * regionalNoise(lat, lon, hour)
  const south = 2 * Math.min(1, Math.max(0, (46.45 - lat) / 0.4)) // warmer south of the Alps
  const lapse = -0.0065 * (elevationM(lat, lon) - 300)
  return seasonal + daily + weather + south + lapse
}

export const WEATHER_BOUNDS = { west: 5.8, east: 10.6, south: 45.75, north: 47.9 }
const WEATHER_COLS = 160
const WEATHER_ROWS = 96

export interface WeatherField {
  cols: number
  rows: number
  temperatureC: Float32Array // row-major, north row first
  lowC: number // 5th percentile
  medianC: number
  highC: number // 95th percentile
}

/** Temperature field over Switzerland for the weather overlay. */
export function weatherField(t: number): WeatherField {
  const { west, east, south, north } = WEATHER_BOUNDS
  const temperature = new Float32Array(WEATHER_COLS * WEATHER_ROWS)
  for (let r = 0; r < WEATHER_ROWS; r++) {
    const lat = north - ((r + 0.5) / WEATHER_ROWS) * (north - south)
    for (let c = 0; c < WEATHER_COLS; c++) {
      const lon = west + ((c + 0.5) / WEATHER_COLS) * (east - west)
      temperature[r * WEATHER_COLS + c] = temperatureC(lat, lon, t)
    }
  }
  const sorted = Float32Array.from(temperature).sort()
  const lowC = sorted[Math.floor(sorted.length * 0.05)]
  const medianC = sorted[Math.floor(sorted.length * 0.5)]
  const highC = sorted[Math.floor(sorted.length * 0.95)]
  return { cols: WEATHER_COLS, rows: WEATHER_ROWS, temperatureC: temperature, lowC, medianC, highC }
}

// ---------- site state ----------

export interface SiteState {
  online: boolean
  temperatureC: number
  heatDemandKw: number
  load: number // 0..1 share of servers running
  serversRunning: number
  heatProducedKw: number
  heatUsedKw: number
  heatDumpedKw: number
  tankLevel: number // 0..1
  jobsPerHour: number
  missedPerHour: number
  electricityChfPerHour: number
  incomeChfPerHour: number
}

/**
 * Heat demand follows the case formula max(0, design × (20 − T) ÷ 25.6) when T < 12 °C, plus hot
 * water with morning and evening peaks. Load is heat-led: servers run to cover demand, with a
 * minimum share for deadline jobs (MOCK) whose surplus heat is partly dumped once the tank is full.
 */
export function siteState(site: Site, t: number, forecast = false): SiteState {
  const hour = Math.floor(t / MS_PER_HOUR)
  const type = SITE_TYPES[site.type]
  const seed = hash(site.lat * 1e4, site.lon * 1e4) * 1e6
  const online = forecast || hash(seed, Math.floor(hour / 30)) > 0.03
  const temp = temperatureC(site.lat, site.lon, t)
  const localHour = (new Date(t).getUTCHours() + 1) % HOURS_PER_DAY
  const space =
    temp < PHYSICS.heatingCutoffC
      ? Math.max(0, (type.designKw * site.insulation * (PHYSICS.baseTemperatureC - temp)) / PHYSICS.formulaDivisorC)
      : 0
  const hwShape = 1 + 0.9 * Math.exp(-((localHour - 7) ** 2) / 3) + 0.6 * Math.exp(-((localHour - 20) ** 2) / 4) - 0.45
  const hotWater = (type.hotWaterKwhPerDay / HOURS_PER_DAY) * hwShape
  const demand = space + hotWater

  const capacityKw = site.servers * PHYSICS.recoveredShare
  // Continuous in time (fractional hours), plus a minute-scale jitter so a live view moves. MOCK.
  const hours = t / MS_PER_HOUR
  const minutes = t / 60_000
  const noise = forecast ? 0 : 0.08 * smoothNoise(seed, hours, 5)
  // Pulls load down by 0–4 %, so it is visible even when the servers are at full load.
  const jitter = forecast ? 0 : 0.02 * (1 + smoothNoise(seed + 2, minutes, 0.5))
  const minLoad = 0.12 // MOCK: deadline jobs keep a few servers busy
  const load = online ? clamp(clamp(demand / capacityKw + noise, minLoad, 1) - jitter, minLoad, 1) : 0
  const produced = load * capacityKw
  const surplus = Math.max(0, produced - demand)
  const tankLevel = clamp(0.35 + 0.5 * smoothNoise(seed + 1, hours, 9) * 0.5 + surplus / capacityKw, 0.05, 1)
  const dumped = tankLevel > 0.9 ? surplus * 0.7 : surplus * 0.15
  const jobsPerHour = online ? site.servers * (3 + 2 * hash(seed, hour)) : 0
  const missRate = 0.004 + 0.05 * Math.max(0, 0.3 - load) + (hash(seed, hour, 3) > 0.985 ? 0.08 : 0)
  const price = cantonElectricityChfPerKwh(site.canton) ?? 0.3
  const serversRunning = load * site.servers
  return {
    online,
    temperatureC: temp,
    heatDemandKw: demand,
    load,
    serversRunning,
    heatProducedKw: produced,
    heatUsedKw: produced - dumped,
    heatDumpedKw: dumped,
    tankLevel,
    jobsPerHour,
    missedPerHour: jobsPerHour * (forecast ? 0.01 : missRate),
    electricityChfPerHour: serversRunning * PHYSICS.serverElectricKw * price,
    incomeChfPerHour: serversRunning * PHYSICS.computePriceChfPerServerHour,
  }
}

// ---------- aggregates ----------

export interface NetworkPoint {
  t: number
  load: number
  serversRunning: number
  heatProducedKw: number
  heatUsedKw: number
  heatDumpedKw: number
  heatDemandKw: number
  jobs: number
  missed: number
  electricityChf: number
  incomeChf: number
  sitesOnline: number
}

export function networkAt(t: number, now: number): NetworkPoint {
  const forecast = t > now
  let servers = 0
  const p: NetworkPoint = {
    t,
    load: 0,
    serversRunning: 0,
    heatProducedKw: 0,
    heatUsedKw: 0,
    heatDumpedKw: 0,
    heatDemandKw: 0,
    jobs: 0,
    missed: 0,
    electricityChf: 0,
    incomeChf: 0,
    sitesOnline: 0,
  }
  for (const site of SITES) {
    const s = siteState(site, t, forecast)
    servers += site.servers
    p.load += s.serversRunning
    p.heatProducedKw += s.heatProducedKw
    p.heatUsedKw += s.heatUsedKw
    p.heatDumpedKw += s.heatDumpedKw
    p.heatDemandKw += s.heatDemandKw
    p.jobs += s.jobsPerHour
    p.missed += s.missedPerHour
    p.electricityChf += s.electricityChfPerHour
    p.incomeChf += s.incomeChfPerHour
    p.sitesOnline += s.online ? 1 : 0
  }
  p.serversRunning = p.load
  p.load /= servers
  return p
}

/** Hourly network series. Forecast hours carry a widening uncertainty band (MOCK). */
export function networkSeries(from: number, to: number, now: number) {
  const out = []
  for (let t = from; t <= to; t += MS_PER_HOUR) {
    const p = networkAt(t, now)
    const daysAhead = Math.max(0, (t - now) / (MS_PER_HOUR * HOURS_PER_DAY))
    const band = t > now ? 0.03 + 0.015 * daysAhead : 0
    out.push({
      ...p,
      actualLoad: t <= now ? p.load : null,
      forecastLoad: t >= now ? p.load : null,
      forecastBand: t >= now ? [clamp(p.load - band, 0, 1), clamp(p.load + band, 0, 1)] : null,
    })
  }
  return out
}

/** Sum of a network series over a period (hourly steps). */
export function networkTotals(from: number, to: number, now: number) {
  const totals = { serverHours: 0, heatUsedKwh: 0, heatDumpedKwh: 0, jobs: 0, missed: 0, electricityChf: 0, incomeChf: 0 }
  for (let t = from; t < to; t += MS_PER_HOUR) {
    const p = networkAt(t, now)
    totals.serverHours += p.serversRunning
    totals.heatUsedKwh += p.heatUsedKw
    totals.heatDumpedKwh += p.heatDumpedKw
    totals.jobs += p.jobs
    totals.missed += p.missed
    totals.electricityChf += p.electricityChf
    totals.incomeChf += p.incomeChf
  }
  return totals
}

/** Monthly income and electricity cost, from 12 months back to 12 months ahead. Sampled every 6 h. */
export function monthlyEconomics(now: number) {
  const SAMPLE_H = 6
  const nowDate = new Date(now)
  const out = []
  for (let m = -12; m <= 12; m++) {
    const start = Date.UTC(nowDate.getUTCFullYear(), nowDate.getUTCMonth() + m, 1)
    const end = Date.UTC(nowDate.getUTCFullYear(), nowDate.getUTCMonth() + m + 1, 1)
    let income = 0
    let cost = 0
    let used = 0
    let dumped = 0
    for (let t = start; t < end; t += SAMPLE_H * MS_PER_HOUR) {
      const p = networkAt(t, t > now ? t - 1 : now)
      income += p.incomeChf * SAMPLE_H
      cost += p.electricityChf * SAMPLE_H
      used += p.heatUsedKw * SAMPLE_H
      dumped += p.heatDumpedKw * SAMPLE_H
    }
    out.push({
      month: new Date(start).toLocaleDateString('de-CH', { month: 'short', year: '2-digit' }),
      start,
      forecast: start > now,
      incomeChf: income,
      electricityChf: cost,
      heatUsedMwh: used / 1000,
      heatDumpedMwh: dumped / 1000,
    })
  }
  return out
}

// ---------- missed deadlines ----------

export const JOB_TYPES = ['Batch LLM', 'Research simulation'] as const

export interface MissedJob {
  id: string
  siteId: string
  siteName: string
  jobType: (typeof JOB_TYPES)[number]
  deadline: number
  lateMinutes: number
}

/** Missed jobs in the 24 hours before `t`, newest first (MOCK, drawn from the site miss rates). */
export function missedJobs(t: number, limit = 12): MissedJob[] {
  const out: MissedJob[] = []
  const end = Math.floor(t / MS_PER_HOUR)
  for (let h = end; h > end - HOURS_PER_DAY && out.length < limit; h--) {
    for (const site of SITES) {
      const s = siteState(site, h * MS_PER_HOUR)
      const r = hash(h, site.servers, site.lat * 1e3)
      if (r < s.missedPerHour / 4) {
        out.push({
          id: `J-${(h % 46656).toString(36).toUpperCase()}${Math.floor(r * 1e4).toString(36).toUpperCase().padStart(3, '0')}`,
          siteId: site.id,
          siteName: site.name,
          jobType: JOB_TYPES[r < s.missedPerHour / 8 ? 0 : 1],
          deadline: h * MS_PER_HOUR - Math.floor(hash(h, site.servers, 9) * 60) * 60000,
          lateMinutes: 5 + Math.floor(hash(h, site.servers, 2) * 180),
        })
        if (out.length >= limit) break
      }
    }
  }
  return out
}
