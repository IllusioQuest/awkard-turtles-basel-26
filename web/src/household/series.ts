// Time series for one house, aggregated into buckets that fit the selected range.
// Values come from the simulation's house series (HousePeriod); times are wall ms (admin/format.ts).

import type { HouseInfo, HousePeriod, RunInfo } from '../data/types'
import { DAY_MS, MS_PER_HOUR, wallMs } from '../admin/format'

/** Bucket size for a range: fine for short ranges, coarse for long ones (never finer than the run's period). */
export function resolution(spanMs: number, periodHours: number) {
  const hours = spanMs <= 3 * DAY_MS ? 1 : spanMs <= 14 * DAY_MS ? 3 : spanMs <= 120 * DAY_MS ? 24 : 168
  const bucketHours = Math.max(hours, periodHours)
  const unit = bucketHours === 1 ? 'hour' : bucketHours === 24 ? 'day' : bucketHours === 168 ? 'week' : `${bucketHours} hours`
  return { bucketMs: bucketHours * MS_PER_HOUR, unit }
}

export interface Bucket {
  t: number
  load: number
  powerKwh: number
  heatUsedKwh: number
  heatDemandKwh: number
  heatDumpedKwh: number
  tankLevel: number
  temperatureC: number
  electricityCostChf: number
  heatValueChf: number
  jobsOnTime: number
  deadlinesMissed: number
}

/** Periods in [fromMs, toMs) summed (energies) or averaged (load, tank level, temperature) per bucket. */
export function householdSeries(run: RunInfo, house: HouseInfo, periods: HousePeriod[], fromMs: number, toMs: number): Bucket[] {
  const { bucketMs } = resolution(toMs - fromMs, run.periodHours)
  const buckets = new Map<number, Bucket & { n: number }>()
  for (const p of periods) {
    const ms = wallMs(run, p.h)
    if (ms < fromMs || ms >= toMs) continue
    const start = fromMs + Math.floor((ms - fromMs) / bucketMs) * bucketMs
    let b = buckets.get(start)
    if (!b) {
      b = {
        t: start, load: 0, powerKwh: 0, heatUsedKwh: 0, heatDemandKwh: 0, heatDumpedKwh: 0, tankLevel: 0, temperatureC: 0,
        electricityCostChf: 0, heatValueChf: 0, jobsOnTime: 0, deadlinesMissed: 0, n: 0,
      }
      buckets.set(start, b)
    }
    b.load += p.load
    b.powerKwh += p.electricKwh
    b.heatUsedKwh += p.heatUsedKwh
    b.heatDemandKwh += p.heatDemandKwh
    b.heatDumpedKwh += p.heatDumpedKwh
    b.tankLevel += house.tankKwh > 0 ? p.tankEndKwh / house.tankKwh : 0
    b.temperatureC += p.temperatureC
    b.electricityCostChf += p.electricityCostChf
    b.heatValueChf += p.heatValueChf
    b.jobsOnTime += p.jobsOnTime
    b.deadlinesMissed += p.deadlinesMissed
    b.n++
  }
  return [...buckets.values()].map(({ n, ...b }) => ({ ...b, load: b.load / n, tankLevel: b.tankLevel / n, temperatureC: b.temperatureC / n }))
}

/** Range totals. `maxElectricKw` (kW per server) turns electric kWh into server-hours sold. */
export function totals(buckets: Bucket[], maxElectricKw: number) {
  const sum = (f: (b: Bucket) => number) => buckets.reduce((s, b) => s + f(b), 0)
  const heatUsedKwh = sum((b) => b.heatUsedKwh)
  const heatDemandKwh = sum((b) => b.heatDemandKwh)
  // Runs or API builds older than these fields send nothing: the sums are NaN and the cards show "–".
  const deadlinesMissed = sum((b) => b.deadlinesMissed)
  const jobsDue = deadlinesMissed + sum((b) => b.jobsOnTime)
  const serverHours = maxElectricKw > 0 ? sum((b) => b.powerKwh) / maxElectricKw : 0
  const breakEven = (sum((b) => b.electricityCostChf) - sum((b) => b.heatValueChf)) / serverHours
  return {
    deadlinesKnown: Number.isFinite(jobsDue),
    deadlinesMissed,
    jobsDue,
    /** Missed ÷ (missed + on time) of the jobs attributed to this house; null without any (or without data). */
    missedShare: Number.isFinite(jobsDue) && jobsDue > 0 ? deadlinesMissed / jobsDue : null,
    serverHours,
    /** Break-even price (case §10): (electricity cost − value of heat used) ÷ server-hours sold, CHF per server-hour. */
    breakEvenChfPerServerHour: serverHours > 0 && Number.isFinite(breakEven) ? breakEven : null,
    heatUsedKwh,
    heatDemandKwh,
    heatDumpedKwh: sum((b) => b.heatDumpedKwh),
    coveredShare: heatDemandKwh > 0 ? Math.min(1, heatUsedKwh / heatDemandKwh) : 0,
    powerKwh: sum((b) => b.powerKwh),
    avgLoad: buckets.length ? sum((b) => b.load) / buckets.length : 0,
    avgTemperatureC: buckets.length ? sum((b) => b.temperatureC) / buckets.length : 0,
  }
}
