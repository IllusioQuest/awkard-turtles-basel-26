import { useMemo, useState, type ReactNode } from 'react'
import { Link, Navigate, useParams, useSearchParams } from 'react-router'
import {
  Area,
  CartesianGrid,
  ComposedChart,
  Legend,
  Line,
  ReferenceLine,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import MockBanner from '../admin/MockBanner'
import { axis, legend, tooltip } from '../admin/chartStyle'
import { DAY_MS, alignHour, chf, dec1, num, pct, pct1, runStartMs, wallMs } from '../admin/format'
import '../admin/admin.css'
import { useAsync, useSource } from '../data/source'
import type { HouseInfo, HousePeriod, RunInfo } from '../data/types'
import DateRangePicker, { type Preset, type TimeRange } from './DateRangePicker'
import { householdSeries, resolution, totals } from './series'
import './household.css'

const chf3 = new Intl.NumberFormat('de-CH', { minimumFractionDigits: 3, maximumFractionDigits: 3 })
// Missed shares are often far below 1 %: two significant digits instead of a rounded 0 %.
const pctSmall = new Intl.NumberFormat('de-CH', { style: 'percent', maximumSignificantDigits: 2 })
const fmtHour = new Intl.DateTimeFormat('de-CH', { timeZone: 'UTC', hour: '2-digit', minute: '2-digit' })
const fmtDay = new Intl.DateTimeFormat('de-CH', { timeZone: 'UTC', day: '2-digit', month: '2-digit' })
const fmtMonth = new Intl.DateTimeFormat('de-CH', { timeZone: 'UTC', month: 'short', year: '2-digit' })
const fmtFull = new Intl.DateTimeFormat('de-CH', {
  timeZone: 'UTC',
  weekday: 'short',
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
})

export default function HouseholdPage() {
  const { runId = '', houseId = '' } = useParams()
  const source = useSource()
  const runs = useAsync(source ? 'runs' : null, () => source!.runs())
  const houses = useAsync(source ? `houses:${runId}` : null, () => source!.houses(runId))
  const series = useAsync(source ? `series:${runId}:${houseId}` : null, () => source!.houseSeries(runId, houseId))

  const run = runs.data?.find((r) => r.id === runId)
  if (runs.data && !run) return <Navigate to="/admin" replace />
  const house = houses.data?.find((h) => h.id === houseId)
  if (houses.data && !houses.loading && !house) return <Navigate to={`/admin/${encodeURIComponent(runId)}`} replace />
  const error = runs.error ?? houses.error ?? series.error
  if (error) return <main className="page admin"><p className="banner">{error.message}</p></main>
  if (!run || !house || !series.data || series.loading || houses.loading) return <main className="page admin"><p className="note">Loading…</p></main>
  return <HouseView key={`${run.id}/${house.id}`} run={run} house={house} periods={series.data} />
}

function HouseView({ run, house, periods }: { run: RunInfo; house: HouseInfo; periods: HousePeriod[] }) {
  const [params] = useSearchParams()
  const selectedH = alignHour(run, Number(params.get('h') ?? 0))
  const selectedMs = wallMs(run, selectedH)
  const minMs = runStartMs(run)
  const maxMs = wallMs(run, run.hours)

  const presets = useMemo<Preset[]>(() => {
    const year = new Date(minMs).getUTCFullYear()
    const month = (m: number) => ({ from: Date.UTC(year, m, 1), to: Date.UTC(year, m + 1, 1) })
    const all: Preset[] = [
      { id: 'run', label: 'Whole run', range: () => ({ from: minMs, to: maxMs }) },
      { id: 'jan', label: 'January', range: () => month(0) },
      { id: 'jul', label: 'July', range: () => month(6) },
      { id: 'first7', label: 'First 7 days', range: () => ({ from: minMs, to: minMs + 7 * DAY_MS }) },
      { id: 'around', label: 'Around selected hour (±3 days)', range: (n) => ({ from: n - 3 * DAY_MS, to: n + 3 * DAY_MS }) },
    ]
    // Clamp to the run window and drop presets that do not overlap it.
    return all
      .map((p): Preset => ({ ...p, range: (n) => ({ from: Math.max(minMs, p.range(n).from), to: Math.min(maxMs, p.range(n).to) }) }))
      .filter((p) => {
        const r = p.range(selectedMs)
        return r.to > r.from
      })
  }, [minMs, maxMs, selectedMs])

  const [range, setRange] = useState<TimeRange>({ from: minMs, to: maxMs, preset: 'run' })
  const source = useSource()
  const fromMs = Math.max(0, range.from - minMs)
  const toMs = Math.min(run.hours * 3_600_000, range.to - minMs)
  const jobLog = useAsync(source ? `job-events:${run.id}:${house.id}:${fromMs}:${toMs}` : null,
    () => source!.jobEvents(run.id, house.id, fromMs, toMs))

  // A single-house run: its missed-deadline share is the network count over all jobs that arrived,
  // including jobs that never started (as in docs/model/fleet-simulation.md and the paper); the
  // per-house count below counts only jobs that ran in the house.
  const network = useAsync(source && run.houses === 1 ? `network:${run.id}` : null, () => source!.network(run.id))
  const runMissed = useMemo(() => {
    if (!network.data) return null
    const h0 = fromMs / 3_600_000, h1 = toMs / 3_600_000
    let misses = 0, arrivals = 0
    for (const n of network.data) if (n.h >= h0 && n.h < h1) { misses += n.misses; arrivals += n.arrivals }
    return arrivals > 0 ? { misses, arrivals } : null
  }, [network.data, fromMs, toMs])

  const data = useMemo(() => householdSeries(run, house, periods, range.from, range.to), [run, house, periods, range])
  const sum = useMemo(() => totals(data, run.maxElectricKwPerServer), [data, run.maxElectricKwPerServer])
  const span = range.to - range.from
  const { unit } = resolution(span, run.periodHours)
  const tickFormat = (v: number) => (span <= 2 * DAY_MS ? fmtHour : span <= 180 * DAY_MS ? fmtDay : fmtMonth).format(v)
  const selectedInRange = selectedMs >= range.from && selectedMs < range.to

  const chart = (children: ReactNode, yFormat: (v: number) => string, domain?: [number, number], showLegend = false, integerY = false) => (
    <ResponsiveContainer width="100%" height={220}>
      <ComposedChart data={data} margin={{ top: 12, right: 8, bottom: 0, left: 0 }}>
        <CartesianGrid stroke="var(--grid)" vertical={false} />
        <XAxis
          dataKey="t"
          type="number"
          scale="time"
          domain={['dataMin', 'dataMax']}
          tickFormatter={tickFormat}
          axisLine={{ stroke: 'var(--axis)' }}
          minTickGap={32}
          {...axis}
        />
        <YAxis tickFormatter={yFormat} domain={domain} allowDecimals={!integerY} axisLine={false} width={52} {...axis} />
        <Tooltip
          {...tooltip}
          labelFormatter={(v) => fmtFull.format(Number(v))}
          formatter={(v, name) => [yFormat(Number(v)), name]}
        />
        {selectedInRange && <ReferenceLine x={Math.max(selectedMs, data[0]?.t ?? selectedMs)} stroke="var(--muted)" strokeDasharray="2 4" />}
        {children}
        {showLegend && <Legend {...legend} />}
      </ComposedChart>
    </ResponsiveContainer>
  )

  const kwh = (v: number) => num.format(v)
  const capacityKw = house.servers * run.maxElectricKwPerServer * run.heatRecoveryShare
  const place = [house.municipality, house.canton].filter(Boolean).join(', ')

  return (
    <main className="page admin household">
      {run.mock && <MockBanner />}
      <div className="page-head">
        <div>
          <Link className="link back" to={`/admin/${encodeURIComponent(run.id)}?h=${selectedH}&house=${encodeURIComponent(house.id)}`}>
            ← Network
          </Link>
          <h1>{house.id}</h1>
          <span className="note">{[place, run.id].filter(Boolean).join(' · ')}</span>
        </div>
        <div className="head-controls">
          <DateRangePicker
            value={range}
            onChange={(r) => setRange({ ...r, from: Math.max(minMs, r.from), to: Math.min(maxMs, r.to) })}
            presets={presets}
            now={selectedMs}
            min={minMs}
            max={maxMs}
          />
        </div>
      </div>


      <section className="stats">
        <Stat label="Servers" value={String(house.servers)} note={`heat capacity ${dec1.format(capacityKw)} kW`} />
        <Stat label="Average load" value={pct1.format(sum.avgLoad)} />
        <Stat label="Heat delivered" value={`${num.format(sum.heatUsedKwh)} kWh`} />
        <Stat label="Heat demand covered" value={pct.format(sum.coveredShare)} note={`of ${num.format(sum.heatDemandKwh)} kWh`} />
        <Stat label="Power used by servers" value={`${num.format(sum.powerKwh)} kWh`} note="paid by DigitalHeat" />
        <Stat label="Electricity cost" value={chf.format(sum.electricityCostChf)} note="paid by DigitalHeat" />
        <Stat label="Computing income" value={chf.format(sum.incomeChf)} note="jobs run in this house" />
        <Stat label="Heat dumped" value={`${num.format(sum.heatDumpedKwh)} kWh`} />
        <Stat label="Outdoor temperature" value={`${dec1.format(sum.avgTemperatureC)} °C`} note="average" />
        <Stat
          label="Deadlines missed"
          value={runMissed ? pctSmall.format(runMissed.misses / runMissed.arrivals) : sum.missedShare === null ? '–' : pctSmall.format(sum.missedShare)}
          note={runMissed
            ? `${num.format(runMissed.misses)} of ${num.format(runMissed.arrivals)} jobs, incl. never started`
            : sum.deadlinesKnown ? `${num.format(sum.deadlinesMissed)} of ${num.format(sum.jobsDue)} jobs` : 'not in this run (re-run or restart the API)'}
        />
        <Stat
          label="Break even kWh price"
          value={sum.breakEvenChfPerServerHour === null ? '–' : `CHF ${chf3.format(sum.breakEvenChfPerServerHour)}`}
          note={
            sum.breakEvenChfPerServerHour === null && sum.serverHours > 0
              ? 'no heat value in this run (re-run or restart the API)'
              : `per server-hour, ${num.format(sum.serverHours)} sold`
          }
        />
      </section>

      <p className="note forecast-note">Dashed line: hour selected on the network page.</p>

      <section className="charts">
        <div className="card">
          <span className="label">Load</span>
          {chart(<Line legendType="plainline" dataKey="load" name="Load" stroke="var(--series-1)" strokeWidth={2} dot={false} isAnimationActive={false} />, (v) => pct.format(v), [0, 1])}
        </div>
        <div className="card">
          <span className="label">Power used by servers (kWh)</span>
          {chart(
            <Area legendType="square" dataKey="powerKwh" name="Electricity" stroke="var(--series-1)" fill="var(--series-1)" fillOpacity={0.3} strokeWidth={2} isAnimationActive={false} />,
            kwh,
          )}
        </div>
        <div className="card">
          <span className="label">Heat (kWh)</span>
          {chart(
            <>
              <Area legendType="square" dataKey="heatUsedKwh" name="Delivered" stackId="h" stroke="var(--series-1)" fill="var(--series-1)" fillOpacity={0.3} strokeWidth={2} isAnimationActive={false} />
              <Area legendType="square" dataKey="heatDumpedKwh" name="Dumped" stackId="h" stroke="none" fill="var(--series-2)" fillOpacity={0.6} isAnimationActive={false} />
              <Line legendType="plainline" dataKey="heatDemandKwh" name="Demand" stroke="var(--ink-2)" strokeWidth={1.5} strokeDasharray="4 4" dot={false} isAnimationActive={false} />
            </>,
            kwh,
            undefined,
            true,
          )}
        </div>
        <div className="card">
          <span className="label">Heat storage tank ({house.tankKwh} kWh)</span>
          {chart(
            <Line legendType="plainline" dataKey="tankLevel" name="Fill level" stroke="var(--series-1)" strokeWidth={2} dot={false} isAnimationActive={false} />,
            (v) => pct.format(v),
            [0, 1],
          )}
        </div>
        <div className="card wide">
          <span className="label">Outdoor temperature (°C)</span>
          {chart(
            <Line legendType="plainline" dataKey="temperatureC" name="Temperature" stroke="var(--series-1)" strokeWidth={2} dot={false} isAnimationActive={false} />,
            (v) => dec1.format(v),
          )}
        </div>
        <div className="card wide">
          <span className="label">Deadlines missed (jobs per {unit})</span>
          {chart(
            <Area legendType="square" dataKey="deadlinesMissed" name="Missed" stroke="var(--series-2)" fill="var(--series-2)" fillOpacity={0.4} strokeWidth={2} isAnimationActive={false} />,
            (v) => num.format(v),
            undefined,
            false,
            true,
          )}
        </div>
      </section>
      <section className="card job-log" aria-labelledby="job-log-heading">
        <h2 id="job-log-heading">Job logs</h2>
        {jobLog.loading && <p className="note">Loading activity…</p>}
        {jobLog.error && <p className="note">Could not load job activity: {jobLog.error.message}</p>}
        {!jobLog.loading && jobLog.data && !jobLog.data.available && <p className="note">Job activity was not recorded for this run.</p>}
        {!jobLog.loading && jobLog.data?.available && jobLog.data.events.length === 0 && <p className="note">No job activity in this period.</p>}
        {!jobLog.loading && !jobLog.error && jobLog.data?.truncated && <p className="note">Showing the most recent 100 events. Select a shorter period to see earlier activity.</p>}
        {!jobLog.loading && !jobLog.error && jobLog.data?.available && jobLog.data.events.length > 0 && (
          <ol className="job-log-list">
            {[...jobLog.data.events].reverse().map((event, index) => (
              <li key={`${event.timeMs}-${event.jobId}-${event.chunkId}-${event.kind}-${index}`} className="job-log-entry">
                <span className={`job-log-dot job-log-dot-${event.kind}`} aria-hidden="true" />
                <div className="job-log-message">
                  <span><strong>{event.jobId}</strong> {event.chunkId === null ? event.kind : `· chunk ${event.chunkId} ${event.kind}`}</span>
                </div>
                <time className="note">{fmtFull.format(minMs + event.timeMs)}</time>
              </li>
            ))}
          </ol>
        )}
      </section>
    </main>
  )
}

function Stat({ label, value, note }: { label: string; value: string; note?: string }) {
  return (
    <div className="stat">
      <span className="label">{label}</span>
      <span className="stat-value">{value}</span>
      {note && <span className="note">{note}</span>}
    </div>
  )
}
