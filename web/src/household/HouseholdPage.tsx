import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { useParams } from 'react-router'
import {
  Area,
  CartesianGrid,
  ComposedChart,
  Legend,
  Line,
  ReferenceArea,
  ReferenceLine,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import { axis, legend, tooltip } from '../admin/chartStyle'
import { dec1, num, pct, pct1 } from '../admin/format'
import { HOURS_PER_DAY, MS_PER_HOUR, PHYSICS, SITES, siteState } from '../admin/mock'
import '../admin/admin.css'
import DateRangePicker, { type Preset, type TimeRange } from './DateRangePicker'
import { householdSeries, resolution, totals, type Bucket } from './series'
import './household.css'

const DAY_MS = HOURS_PER_DAY * MS_PER_HOUR
const START = Date.now()
const MIN_T = START - 2 * 365 * DAY_MS
const MAX_T = START + 365 * DAY_MS
const LIVE_TICK_MS = 2000
const DEFAULT_SITE = SITES.find((s) => s.type === 'home' && s.city === 'Basel')!

const PRESETS: Preset[] = [
  { id: '24h', label: 'Last 24 hours', range: (n) => ({ from: n - DAY_MS, to: n }) },
  { id: '7d', label: 'Last 7 days', range: (n) => ({ from: n - 7 * DAY_MS, to: n }) },
  { id: '30d', label: 'Last 30 days', range: (n) => ({ from: n - 30 * DAY_MS, to: n }) },
  { id: '12m', label: 'Last 12 months', range: (n) => ({ from: n - 365 * DAY_MS, to: n }) },
  { id: 'n7d', label: 'Next 7 days', range: (n) => ({ from: n, to: n + 7 * DAY_MS }) },
  { id: 'n12m', label: 'Next 12 months', range: (n) => ({ from: n, to: n + 365 * DAY_MS }) },
  { id: 'pm12m', label: 'Past and next 12 months', range: (n) => ({ from: n - 365 * DAY_MS, to: n + 365 * DAY_MS }) },
]

const fmtHour = new Intl.DateTimeFormat('de-CH', { timeZone: 'Europe/Zurich', hour: '2-digit', minute: '2-digit' })
const fmtDay = new Intl.DateTimeFormat('de-CH', { timeZone: 'Europe/Zurich', day: '2-digit', month: '2-digit' })
const fmtMonth = new Intl.DateTimeFormat('de-CH', { timeZone: 'Europe/Zurich', month: 'short', year: '2-digit' })
const fmtFull = new Intl.DateTimeFormat('de-CH', {
  timeZone: 'Europe/Zurich',
  weekday: 'short',
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
})

export default function HouseholdPage() {
  const { siteId } = useParams()
  const site = SITES.find((s) => s.id === siteId) ?? DEFAULT_SITE
  const [selected, setSelected] = useState<TimeRange>({ ...PRESETS[0].range(START), preset: PRESETS[0].id })
  const [now, setNow] = useState(START)

  // Live mode: preset ranges are relative to now and follow the clock; custom ranges stay fixed.
  const preset = PRESETS.find((p) => p.id === selected.preset)
  const live = preset !== undefined
  useEffect(() => {
    if (!live) return
    const timer = setInterval(() => setNow(Date.now()), LIVE_TICK_MS)
    return () => clearInterval(timer)
  }, [live])
  const range = useMemo(() => (preset ? { ...preset.range(now), preset: preset.id } : selected), [preset, now, selected])

  const data = useMemo(() => householdSeries(site, range.from, range.to, now), [site, range, now])
  const current = useMemo(() => siteState(site, now), [site, now])
  const sum = useMemo(() => totals(data), [data])
  const span = range.to - range.from
  const { unit } = resolution(span)
  const tickFormat = (v: number) => (span <= 2 * DAY_MS ? fmtHour : span <= 180 * DAY_MS ? fmtDay : fmtMonth).format(v)
  const hasForecast = range.to > now

  const chart = (children: ReactNode, yFormat: (v: number) => string, domain?: [number, number], showLegend = false) => (
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
        <YAxis tickFormatter={yFormat} domain={domain} axisLine={false} width={52} {...axis} />
        <Tooltip
          {...tooltip}
          labelFormatter={(v, items) => `${fmtFull.format(Number(v))}${(items?.[0]?.payload as Bucket | undefined)?.forecast ? ' · forecast' : ''}`}
          formatter={(v, name) => [Array.isArray(v) ? v.map((x) => yFormat(Number(x))).join(' – ') : yFormat(Number(v)), name]}
        />
        {hasForecast && now > range.from && <ReferenceLine x={now} stroke="var(--muted)" strokeDasharray="2 4" />}
        {hasForecast && <ReferenceArea x1={Math.max(now, data[0]?.t ?? now)} x2={data[data.length - 1]?.t} fill="#ffffff" fillOpacity={0.03} />}
        {children}
        {showLegend && <Legend {...legend} />}
      </ComposedChart>
    </ResponsiveContainer>
  )

  const kwh = (v: number) => num.format(v)

  return (
    <main className="page admin household">
      <div className="page-head">
        <div>
          <h1>{site.name}</h1>
          <span className="note">
            {site.city}, {site.canton}
          </span>
        </div>
        <div className="head-controls">
          {live && (
            <span className="live">
              <span className="live-dot" />
              Live
            </span>
          )}
          <DateRangePicker value={range} onChange={setSelected} presets={PRESETS} now={now} min={MIN_T} max={MAX_T} />
        </div>
      </div>

      <section className="stats">
        <Stat label="Servers" value={String(site.servers)} note={`${dec1.format(current.serversRunning)} running now`} />
        <Stat label="Load now" value={pct1.format(current.load)} note={`average ${pct.format(sum.avgLoad)}`} />
        <Stat label="Heat delivered" value={`${num.format(sum.heatUsedKwh)} kWh`} note={`now ${dec1.format(current.heatUsedKw)} kW`} />
        <Stat label="Heat demand covered" value={pct.format(sum.coveredShare)} note={`of ${num.format(sum.heatDemandKwh)} kWh`} />
        <Stat label="Power used by servers" value={`${num.format(sum.powerKwh)} kWh`} note={`now ${dec1.format(current.serversRunning * PHYSICS.serverElectricKw)} kW, paid by DigitalHeat`} />
        <Stat label="Heat dumped" value={`${num.format(sum.heatDumpedKwh)} kWh`} />
        <Stat label="Outdoor temperature" value={`${dec1.format(current.temperatureC)} °C`} note={`average ${dec1.format(sum.avgTemperatureC)} °C`} />
      </section>

      {hasForecast && <p className="note forecast-note">Shaded area: forecast. Values per {unit}.</p>}
      {!hasForecast && <p className="note forecast-note">Values per {unit}.</p>}

      <section className="charts">
        <div className="card">
          <span className="label">Load</span>
          {chart(
            <>
              {hasForecast && <Area legendType="square" dataKey="loadBand" name="Forecast range" stroke="none" fill="var(--series-1)" fillOpacity={0.15} isAnimationActive={false} />}
              <Line legendType="plainline" dataKey="load" name="Load" stroke="var(--series-1)" strokeWidth={2} dot={false} isAnimationActive={false} />
            </>,
            (v) => pct.format(v),
            [0, 1],
            hasForecast,
          )}
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
          <span className="label">Heat storage tank ({site.tankKwh} kWh)</span>
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
