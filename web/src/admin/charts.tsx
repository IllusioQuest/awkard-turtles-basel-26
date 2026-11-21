import {
  Area,
  Bar,
  CartesianGrid,
  Cell,
  ComposedChart,
  Legend,
  Line,
  ReferenceLine,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import { chf, dateTime, dayTick, num, pct } from './format'
import { axis, legend, tooltip } from './chartStyle'
import type { monthlyEconomics, networkSeries } from './mock'

type Series = ReturnType<typeof networkSeries>
type Monthly = ReturnType<typeof monthlyEconomics>

const HEIGHT = 240
function TimeMarkers({ t, now }: { t: number; now: number }) {
  return (
    <>
      <ReferenceLine x={now} stroke="var(--muted)" strokeDasharray="2 4" />
      {t !== now && <ReferenceLine x={t} stroke="var(--ink)" />}
    </>
  )
}

const DAY_MS = 86_400_000
/** One tick per local midnight (Europe/Zurich, approximated as UTC+1). */
function dayTicks(data: Series) {
  if (data.length === 0) return []
  const offset = 3_600_000
  const first = Math.ceil((data[0].t + offset) / DAY_MS) * DAY_MS - offset
  const ticks = []
  for (let t = first; t <= data[data.length - 1].t; t += DAY_MS) ticks.push(t)
  return ticks
}

const timeAxis = (data: Series) => (
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

export function LoadChart({ data, t, now }: { data: Series; t: number; now: number }) {
  return (
    <ResponsiveContainer width="100%" height={HEIGHT}>
      <ComposedChart data={data} margin={{ top: 12, right: 8, bottom: 0, left: 0 }}>
        <CartesianGrid stroke="var(--grid)" vertical={false} />
        {timeAxis(data)}
        <YAxis domain={[0, 1]} tickFormatter={(v) => pct.format(v)} axisLine={false} width={44} {...axis} />
        <Tooltip
          {...tooltip}
          labelFormatter={(v) => dateTime.format(Number(v))}
          formatter={(v, name) => [Array.isArray(v) ? v.map((x) => pct.format(Number(x))).join(' – ') : pct.format(Number(v)), name]}
        />
        <Legend {...legend} />
        <Area legendType="square" dataKey="forecastBand" name="Forecast range" stroke="none" fill="var(--series-1)" fillOpacity={0.15} isAnimationActive={false} />
        <Line legendType="plainline" dataKey="actualLoad" name="Actual" stroke="var(--series-1)" strokeWidth={2} dot={false} isAnimationActive={false} />
        <Line legendType="plainline" dataKey="forecastLoad" name="Forecast" stroke="var(--series-1)" strokeWidth={2} strokeDasharray="4 4" dot={false} isAnimationActive={false} />
        <TimeMarkers t={t} now={now} />
      </ComposedChart>
    </ResponsiveContainer>
  )
}

export function HeatChart({ data, t, now }: { data: Series; t: number; now: number }) {
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
        <TimeMarkers t={t} now={now} />
      </ComposedChart>
    </ResponsiveContainer>
  )
}

export function EconomicsChart({ data }: { data: Monthly }) {
  return (
    <ResponsiveContainer width="100%" height={HEIGHT + 40}>
      <ComposedChart data={data} margin={{ top: 12, right: 8, bottom: 0, left: 0 }} barGap={2}>
        <CartesianGrid stroke="var(--grid)" vertical={false} />
        <XAxis dataKey="month" axisLine={{ stroke: 'var(--axis)' }} interval={1} {...axis} />
        <YAxis tickFormatter={(v) => num.format(v)} axisLine={false} width={64} {...axis} />
        <Tooltip
          {...tooltip}
          cursor={{ fill: 'rgba(255,255,255,0.04)' }}
          formatter={(v, name) => [chf.format(Number(v)), name]}
          labelFormatter={(label, items) => `${label}${items?.[0]?.payload?.forecast ? ' (forecast)' : ''}`}
        />
        <Legend {...legend} />
        <Bar legendType="square" dataKey="incomeChf" name="Computing income" fill="var(--series-1)" radius={[4, 4, 0, 0]} isAnimationActive={false}>
          {data.map((d) => (
            <Cell key={d.start} fillOpacity={d.forecast ? 0.4 : 1} />
          ))}
        </Bar>
        <Bar legendType="square" dataKey="electricityChf" name="Electricity cost" fill="var(--series-2)" radius={[4, 4, 0, 0]} isAnimationActive={false}>
          {data.map((d) => (
            <Cell key={d.start} fillOpacity={d.forecast ? 0.4 : 1} />
          ))}
        </Bar>
      </ComposedChart>
    </ResponsiveContainer>
  )
}
