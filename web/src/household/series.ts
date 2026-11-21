// Time series for one household, aggregated into buckets that fit the selected range.
// Values come from the same mock site model as the admin dashboard (admin/mock.ts).

import { cantonHeatPriceChfPerKwh } from '../params'
import { HOURS_PER_DAY, MS_PER_HOUR, siteState, type Site } from '../admin/mock'

const DAY_MS = HOURS_PER_DAY * MS_PER_HOUR

/** Bucket and sampling step for a range: fine for short ranges, coarse for long ones. */
export function resolution(spanMs: number) {
  if (spanMs <= 3 * DAY_MS) return { bucketMs: MS_PER_HOUR, sampleMs: MS_PER_HOUR, unit: 'hour' as const }
  if (spanMs <= 14 * DAY_MS) return { bucketMs: 3 * MS_PER_HOUR, sampleMs: MS_PER_HOUR, unit: '3 hours' as const }
  if (spanMs <= 120 * DAY_MS) return { bucketMs: DAY_MS, sampleMs: 2 * MS_PER_HOUR, unit: 'day' as const }
  return { bucketMs: 7 * DAY_MS, sampleMs: 6 * MS_PER_HOUR, unit: 'week' as const }
}

export interface Bucket {
  t: number
  forecast: boolean
  load: number
  loadBand: [number, number] | null
  powerKwh: number
  heatUsedKwh: number
  heatDemandKwh: number
  heatDumpedKwh: number
  savingsChf: number
  tankLevel: number
  temperatureC: number
}

/** Forecast uncertainty (MOCK): grows with distance from now, capped. */
const uncertainty = (daysAhead: number) => Math.min(0.35, 0.04 + 0.01 * daysAhead)

export function householdSeries(site: Site, from: number, to: number, now: number): Bucket[] {
  const { bucketMs, sampleMs } = resolution(to - from)
  const heatPrice = cantonHeatPriceChfPerKwh(site.canton) ?? 0
  const sampleHours = sampleMs / MS_PER_HOUR
  const start = Math.floor(from / bucketMs) * bucketMs
  const out: Bucket[] = []
  for (let b = start; b < to; b += bucketMs) {
    let load = 0
    let power = 0
    let used = 0
    let demand = 0
    let dumped = 0
    let tank = 0
    let temp = 0
    let n = 0
    for (let ts = b; ts < b + bucketMs; ts += sampleMs) {
      const s = siteState(site, ts, ts > now)
      load += s.load
      power += s.serversRunning * sampleHours // 1 kW per running server (case)
      used += s.heatUsedKw * sampleHours
      demand += s.heatDemandKw * sampleHours
      dumped += s.heatDumpedKw * sampleHours
      tank += s.tankLevel
      temp += s.temperatureC
      n++
    }
    const forecast = b >= now
    const avgLoad = load / n
    const u = uncertainty((b - now) / DAY_MS)
    out.push({
      t: b,
      forecast,
      load: avgLoad,
      loadBand: forecast ? [Math.max(0, avgLoad * (1 - u)), Math.min(1, avgLoad * (1 + u))] : null,
      powerKwh: power,
      heatUsedKwh: used,
      heatDemandKwh: demand,
      heatDumpedKwh: dumped,
      savingsChf: used * heatPrice,
      tankLevel: tank / n,
      temperatureC: temp / n,
    })
  }
  // Live point: at hourly resolution, add the current instant so the line ends at "now".
  if (bucketMs === MS_PER_HOUR && now > from && now < to && now % bucketMs !== 0) {
    const s = siteState(site, now)
    const live: Bucket = {
      t: now,
      forecast: false,
      load: s.load,
      loadBand: null,
      powerKwh: s.serversRunning,
      heatUsedKwh: s.heatUsedKw,
      heatDemandKwh: s.heatDemandKw,
      heatDumpedKwh: s.heatDumpedKw,
      savingsChf: s.heatUsedKw * heatPrice,
      tankLevel: s.tankLevel,
      temperatureC: s.temperatureC,
    }
    const at = out.findIndex((b) => b.t > now)
    if (at === -1) out.push(live)
    else out.splice(at, 0, live)
  }
  return out
}

export function totals(buckets: Bucket[]) {
  const sum = (f: (b: Bucket) => number) => buckets.reduce((s, b) => s + f(b), 0)
  const heatUsedKwh = sum((b) => b.heatUsedKwh)
  const heatDemandKwh = sum((b) => b.heatDemandKwh)
  return {
    heatUsedKwh,
    heatDemandKwh,
    heatDumpedKwh: sum((b) => b.heatDumpedKwh),
    coveredShare: heatDemandKwh > 0 ? Math.min(1, heatUsedKwh / heatDemandKwh) : 0,
    powerKwh: sum((b) => b.powerKwh),
    savingsChf: sum((b) => b.savingsChf),
    avgLoad: buckets.length ? sum((b) => b.load) / buckets.length : 0,
    avgTemperatureC: buckets.length ? sum((b) => b.temperatureC) / buckets.length : 0,
  }
}
