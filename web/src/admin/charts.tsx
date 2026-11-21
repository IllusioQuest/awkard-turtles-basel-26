import {
  Area,
  Bar,
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
import { DAY_MS, chf, dateTime, dayTick, num, pct } from './format'
import { axis, legend, tooltip } from './chartStyle'

/** One hour of the network; t is wall ms (see format.ts). Energies in kWh = kW over the hour. */
export interface SeriesPoint {
  t: number
  load: number
  heatUsedKw: number
  heatDumpedKw: number
  heatDemandKw: number
}

export interface MonthlyPoint {
  month: string
  start: number
  incomeChf: number
  electricityChf: number
}

export interface MissedPoint {
  day: string
  start: number
  misses: number
}

const HEIGHT = 240

/** One tick per midnight of the wall time. */
function dayTicks(data: SeriesPoint[]) {
  if (data.length === 0) return []
  const ticks = []
  for (let t = Math.ceil(data[0].t / DAY_MS) * DAY_MS; t <= data[data.length - 1].t; t += DAY_MS) ticks.push(t)
  return ticks
}

const timeAxis = (data: SeriesPoint[]) => (
  <XAxis
    ticks={dayTicks(data)}
    dataKey="t"
    type="number"
    scale="time"
    domain={['dataMin', 'dataMax']}
    tickFormatter={(v) => dayTick.format(v)}
    axisLine={{ stroke: 'var(--axis)' }}
    {...axis}
  />
)

export function LoadChart({ data, t }: { data: SeriesPoint[]; t: number }) {
  return (
    <ResponsiveContainer width="100%" height={HEIGHT}>
      <ComposedChart data={data} margin={{ top: 12, right: 8, bottom: 0, left: 0 }}>
        <CartesianGrid stroke="var(--grid)" vertical={false} />
        {timeAxis(data)}
        <YAxis domain={[0, 1]} tickFormatter={(v) => pct.format(v)} axisLine={false} width={44} {...axis} />
        <Tooltip {...tooltip} labelFormatter={(v) => dateTime.format(Number(v))} formatter={(v, name) => [pct.format(Number(v)), name]} />
        <Line legendType="plainline" dataKey="load" name="Load" stroke="var(--series-1)" strokeWidth={2} dot={false} isAnimationActive={false} />
        <ReferenceLine x={t} stroke="var(--ink)" />
      </ComposedChart>
    </ResponsiveContainer>
  )
}

export function HeatChart({ data, t }: { data: SeriesPoint[]; t: number }) {
  return (
    <ResponsiveContainer width="100%" height={HEIGHT}>
      <ComposedChart data={data} margin={{ top: 12, right: 8, bottom: 0, left: 0 }}>
        <CartesianGrid stroke="var(--grid)" vertical={false} />
        {timeAxis(data)}
        <YAxis tickFormatter={(v) => num.format(v)} axisLine={false} width={44} unit="" {...axis} />
        <Tooltip {...tooltip} labelFormatter={(v) => dateTime.format(Number(v))} formatter={(v, name) => [`${num.format(Number(v))} kW`, name]} />
        <Legend {...legend} />
        <Area legendType="square" dataKey="heatUsedKw" name="Delivered (kW)" stackId="h" stroke="var(--series-1)" fill="var(--series-1)" fillOpacity={0.35} strokeWidth={2} isAnimationActive={false} />
        <Area legendType="square" dataKey="heatDumpedKw" name="Dumped (kW)" stackId="h" stroke="none" fill="var(--series-2)" fillOpacity={0.6} isAnimationActive={false} />
        <Line legendType="plainline" dataKey="heatDemandKw" name="Demand (kW)" stroke="var(--ink-2)" strokeWidth={1.5} strokeDasharray="4 4" dot={false} isAnimationActive={false} />
        <ReferenceLine x={t} stroke="var(--ink)" />
      </ComposedChart>
    </ResponsiveContainer>
  )
}

export function EconomicsChart({ data }: { data: MonthlyPoint[] }) {
  return (
    <ResponsiveContainer width="100%" height={HEIGHT + 40}>
      <ComposedChart data={data} margin={{ top: 12, right: 8, bottom: 0, left: 0 }} barGap={2}>
        <CartesianGrid stroke="var(--grid)" vertical={false} />
        <XAxis dataKey="month" axisLine={{ stroke: 'var(--axis)' }} interval={data.length > 14 ? 1 : 0} {...axis} />
        <YAxis tickFormatter={(v) => num.format(v)} axisLine={false} width={64} {...axis} />
        <Tooltip {...tooltip} cursor={{ fill: 'rgba(255,255,255,0.04)' }} formatter={(v, name) => [chf.format(Number(v)), name]} />
        <Legend {...legend} />
        <Bar legendType="square" dataKey="incomeChf" name="Computing income" fill="var(--series-1)" radius={[4, 4, 0, 0]} isAnimationActive={false} />
        <Bar legendType="square" dataKey="electricityChf" name="Electricity cost" fill="var(--series-2)" radius={[4, 4, 0, 0]} isAnimationActive={false} />
      </ComposedChart>
    </ResponsiveContainer>
  )
}

export function MissedChart({ data }: { data: MissedPoint[] }) {
  return (
    <ResponsiveContainer width="100%" height={HEIGHT - 40}>
      <ComposedChart data={data} margin={{ top: 12, right: 8, bottom: 0, left: 0 }}>
        <CartesianGrid stroke="var(--grid)" vertical={false} />
        <XAxis dataKey="day" axisLine={{ stroke: 'var(--axis)' }} {...axis} />
        <YAxis allowDecimals={false} tickFormatter={(v) => num.format(v)} axisLine={false} width={44} {...axis} />
        <Tooltip {...tooltip} cursor={{ fill: 'rgba(255,255,255,0.04)' }} formatter={(v) => [num.format(Number(v)), 'Missed deadlines']} />
        <Bar dataKey="misses" name="Missed deadlines" fill="var(--series-2)" radius={[4, 4, 0, 0]} isAnimationActive={false} />
      </ComposedChart>
    </ResponsiveContainer>
  )
}
