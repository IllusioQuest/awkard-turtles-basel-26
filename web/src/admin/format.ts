import type { RunInfo } from '../data/types'

export const MS_PER_HOUR = 3_600_000
export const HOURS_PER_DAY = 24
export const DAY_MS = HOURS_PER_DAY * MS_PER_HOUR

/** Run hour h as "wall ms": the local wall time read as UTC (see data/types.ts). Format with timeZone 'UTC'. */
export const runStartMs = (run: Pick<RunInfo, 'startLocal'>) => Date.parse(`${run.startLocal}Z`)
export const wallMs = (run: Pick<RunInfo, 'startLocal'>, h: number) => runStartMs(run) + h * MS_PER_HOUR
export const runHour = (run: Pick<RunInfo, 'startLocal'>, ms: number) => Math.round((ms - runStartMs(run)) / MS_PER_HOUR)

export const chf = new Intl.NumberFormat('de-CH', { style: 'currency', currency: 'CHF', maximumFractionDigits: 0 })
export const num = new Intl.NumberFormat('de-CH', { maximumFractionDigits: 0 })
export const dec1 = new Intl.NumberFormat('de-CH', { maximumFractionDigits: 1, minimumFractionDigits: 1 })
export const pct = new Intl.NumberFormat('de-CH', { style: 'percent', maximumFractionDigits: 0 })
export const pct1 = new Intl.NumberFormat('de-CH', { style: 'percent', maximumFractionDigits: 1 })
export const dateTime = new Intl.DateTimeFormat('de-CH', {
  timeZone: 'UTC',
  weekday: 'short',
  day: '2-digit',
  month: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
})
export const dayTick = new Intl.DateTimeFormat('de-CH', { timeZone: 'UTC', day: '2-digit', month: '2-digit' })
export const dayHourTick = new Intl.DateTimeFormat('de-CH', { timeZone: 'UTC', day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' })
export const hourTick = new Intl.DateTimeFormat('de-CH', { timeZone: 'UTC', hour: '2-digit', minute: '2-digit' })
export const monthTick = new Intl.DateTimeFormat('de-CH', { timeZone: 'UTC', month: 'short', year: '2-digit' })

/** Last run hour the timeline can select: the start of the final period. */
export const lastHour = (run: Pick<RunInfo, 'hours' | 'periodHours'>) => Math.floor((run.hours - 1) / run.periodHours) * run.periodHours

/** Clamps h to the run and aligns it to the start of its period. Non-numbers give 0. */
export const alignHour = (run: Pick<RunInfo, 'hours' | 'periodHours'>, h: number) =>
  Number.isFinite(h) ? Math.min(lastHour(run), Math.max(0, Math.floor(h / run.periodHours) * run.periodHours)) : 0
