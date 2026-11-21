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
import { DAY_MS, chf, dateTime, dayTick, hourTick, monthTick, num, pct } from './format'
import { axis, legend, tooltip } from './chartStyle'

/**
 * One bucket of the selected window (see AdminPage); t is the bucket start in wall ms (see format.ts).
 * Load and heat are means over the bucket's hours (kWh per hour = kW); misses are summed.
 */
export interface NetworkBucket {
  t: number
  label: string // bucket start, formatted for the bar chart's category axis
  load: number
  heatUsedKw: number
  heatDumpedKw: number
  heatDemandKw: number
  misses: number
}

export interface MonthlyPoint {
  month: string
  start: number
  incomeChf: number
  electricityChf: number
}

const HEIGHT = 240

/** Time axis over the data; tick labels by span like the household view (hours, days, months). */
const timeAxis = (data: NetworkBucket[]) => {
  const span = data.length ? data[data.length - 1].t - data[0].t : 0
  const fmt = span <= 2 * DAY_MS ? hourTick : span <= 180 * DAY_MS ? dayTick : monthTick
  return (
    <XAxis
      dataKey="t"
      type="number"
      scale="time"
      domain={['dataMin', 'dataMax']}
      tickFormatter={(v) => fmt.format(v)}
      minTickGap={32}
      axisLine={{ stroke: 'var(--axis)' }}
      {...axis}
    />
  )
}

export function LoadChart({ data, t }: { data: NetworkBucket[]; t: number }) {
  return (
    <ResponsiveContainer width="100%" height={HEIGHT}>
      <ComposedChart data={data} margin={{ top: 12, right: 8, bottom: 0, left: 0 }}>
        <CartesianGrid stroke="var(--grid)" vertical={false} />
        {timeAxis(data)}
        <YAxis domain={[0, 1]} tickFormatter={(v) => pct.format(v)} axisLine={false} width={44} {...axis} />
        <Tooltip {...tooltip} labelFormatter={(v) => dateTime.format(Number(v))} formatter={(v, name) => [pct.format(Number(v)), name]} />
        <Line legendType="plainline" dataKey="load" name="Load" stroke="var(--series-1)" strokeWidth={2} dot={false} isAnimationActive={false} />
        <ReferenceLine x={t} stroke="var(--ink)" ifOverflow="extendDomain" />
      </ComposedChart>
    </ResponsiveContainer>
  )
}

export function HeatChart({ data, t }: { data: NetworkBucket[]; t: number }) {
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
        <ReferenceLine x={t} stroke="var(--ink)" ifOverflow="extendDomain" />
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

export function MissedChart({ data }: { data: NetworkBucket[] }) {
  return (
    <ResponsiveContainer width="100%" height={HEIGHT - 40}>
      <ComposedChart data={data} margin={{ top: 12, right: 8, bottom: 0, left: 0 }}>
        <CartesianGrid stroke="var(--grid)" vertical={false} />
        <XAxis dataKey="label" axisLine={{ stroke: 'var(--axis)' }} minTickGap={16} {...axis} />
        <YAxis allowDecimals={false} tickFormatter={(v) => num.format(v)} axisLine={false} width={44} {...axis} />
        <Tooltip {...tooltip} cursor={{ fill: 'rgba(255,255,255,0.04)' }} formatter={(v) => [num.format(Number(v)), 'Missed deadlines']} />
        <Bar dataKey="misses" name="Missed deadlines" fill="var(--series-2)" radius={[4, 4, 0, 0]} isAnimationActive={false} />
      </ComposedChart>
    </ResponsiveContainer>
  )
}
