import { useMemo } from 'react'
import { Line, LineChart, ReferenceLine, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import type { HouseInfo, HousePeriod, RunInfo } from '../data/types'
import { chf, dateTime, dec1, num, pct, wallMs } from './format'

const pctSmall = new Intl.NumberFormat('de-CH', { style: 'percent', maximumSignificantDigits: 2 })

interface Props {
  run: RunInfo
  house: HouseInfo
  state: HousePeriod | undefined // the house's period at the selected hour
  series: HousePeriod[] | undefined // all periods of the house (undefined while loading or unavailable)
  t: number
  onMore: () => void
  onClose: () => void
}

const HOURS_PER_DAY = 24
const SPARK_HOURLY = 48
const SPARK_DAILY = 14

export default function SitePanel({ run, house, state, series, t, onMore, onClose }: Props) {
  const daily = run.periodHours === HOURS_PER_DAY
  const sparkPeriods = daily ? SPARK_DAILY : SPARK_HOURLY

  const history = useMemo(
    () => (series ?? []).filter((p) => p.h <= t && p.h > t - sparkPeriods * run.periodHours).map((p) => ({ t: wallMs(run, p.h), load: p.load })),
    [series, t, run, sparkPeriods],
  )

  // Hourly runs: the 24 hours up to t; daily runs: the day starting at t.
  const day = useMemo(() => {
    const totals = { used: 0, dumped: 0, missed: 0, onTime: 0, income: 0, cost: 0 }
    for (const p of series ?? []) {
      if (p.h > t || p.h <= t - HOURS_PER_DAY) continue
      totals.used += p.heatUsedKwh
      totals.dumped += p.heatDumpedKwh
      totals.missed += p.deadlinesMissed
      totals.onTime += p.jobsOnTime
      totals.income += p.incomeChf
      totals.cost += p.electricityCostChf
    }
    return totals
  }, [series, t])

  const kw = (kwh: number) => `${dec1.format(kwh / run.periodHours)} kW`
  const rows: [string, string][] = state
    ? [
        ['Location', [house.municipality, house.canton].filter(Boolean).join(', ') || '–'],
        ['Servers', `${dec1.format(state.load * house.servers)} of ${house.servers} running`],
        ['Heat capacity', `${dec1.format(house.servers * run.maxElectricKwPerServer * run.heatRecoveryShare)} kW`],
        ['Outdoor temperature', `${dec1.format(state.temperatureC)} °C`],
        ['Heat demand', kw(state.heatDemandKwh)],
        ['Heat delivered', kw(state.heatUsedKwh)],
        ['Heat produced', kw(state.heatProducedKwh)],
        ['Tank', `${pct.format(house.tankKwh > 0 ? state.tankEndKwh / house.tankKwh : 0)} of ${num.format(house.tankKwh)} kWh`],
      ]
    : [['Location', [house.municipality, house.canton].filter(Boolean).join(', ') || '–']]
  const dayRows: [string, string][] = [
    ['Heat delivered', `${num.format(day.used)} kWh`],
    ['Heat dumped', `${num.format(day.dumped)} kWh`],
    // Jobs attributed to this house (last house that ran them): missed ÷ (missed + on time).
    ['Deadlines missed', Number.isFinite(day.missed + day.onTime) && day.missed + day.onTime > 0 ? `${pctSmall.format(day.missed / (day.missed + day.onTime))} (${num.format(day.missed)} of ${num.format(day.missed + day.onTime)})` : '–'],
    ['Computing income', chf.format(day.income)],
    ['Electricity cost', chf.format(day.cost)],
  ]

  return (
    <aside className="panel">
      <div className="panel-head">
        <div>
          <h2>{house.id}</h2>
          <span className="note">{[house.municipality, house.canton].filter(Boolean).join(', ')}</span>
        </div>
        <button type="button" className="close" onClick={onClose} aria-label="Close">
          ×
        </button>
      </div>

      <button type="button" className="more" onClick={onMore}>
        More
      </button>

      <div className="panel-hero">
        <span className="stat-value">{state ? pct.format(state.load) : '–'}</span>
        <span className="note">load</span>
      </div>

      <ResponsiveContainer width="100%" height={80}>
        <LineChart data={history} margin={{ top: 4, right: 0, bottom: 0, left: 0 }}>
          <XAxis dataKey="t" type="number" domain={['dataMin', 'dataMax']} hide />
          <YAxis domain={[0, 1]} hide />
          <ReferenceLine y={1} stroke="var(--grid)" />
          <Tooltip
            contentStyle={{ background: 'var(--surface-2)', border: 'none', borderRadius: 8 }}
            labelStyle={{ color: 'var(--ink-2)' }}
            itemStyle={{ color: 'var(--ink)' }}
            labelFormatter={(v) => dateTime.format(Number(v))}
            formatter={(v) => [pct.format(Number(v)), 'Load']}
          />
          <Line dataKey="load" stroke="var(--series-1)" strokeWidth={2} dot={false} isAnimationActive={false} />
        </LineChart>
      </ResponsiveContainer>
      <span className="note">{daily ? `last ${SPARK_DAILY} days` : `last ${SPARK_HOURLY} h`}</span>

      <dl className="kv">
        {rows.map(([k, v]) => (
          <div key={k}>
            <dt>{k}</dt>
            <dd>{v}</dd>
          </div>
        ))}
      </dl>

      <span className="label">{daily ? 'This day' : 'Last 24 h'}</span>
      <dl className="kv">
        {dayRows.map(([k, v]) => (
          <div key={k}>
            <dt>{k}</dt>
            <dd>{v}</dd>
          </div>
        ))}
      </dl>
    </aside>
  )
}
