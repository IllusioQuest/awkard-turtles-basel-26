import { useMemo } from 'react'
import { Line, LineChart, ReferenceLine, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { chf, dateTime, dec1, num, pct, pct1 } from './format'
import { HOURS_PER_DAY, MS_PER_HOUR, PHYSICS, SITE_TYPES, siteState, type Site, type SiteState } from './mock'

interface Props {
  site: Site
  state: SiteState
  t: number
  now: number
  onClose: () => void
}

const PANEL_WINDOW_H = 48

export default function SitePanel({ site, state, t, now, onClose }: Props) {
  const history = useMemo(() => {
    const out = []
    for (let h = -PANEL_WINDOW_H; h <= 0; h++) {
      const at = t + h * MS_PER_HOUR
      out.push({ t: at, load: siteState(site, at, at > now).load })
    }
    return out
  }, [site, t, now])

  const day = useMemo(() => {
    const totals = { used: 0, dumped: 0, jobs: 0, missed: 0, income: 0, cost: 0 }
    for (let h = 0; h < HOURS_PER_DAY; h++) {
      const s = siteState(site, t - h * MS_PER_HOUR, t > now)
      totals.used += s.heatUsedKw
      totals.dumped += s.heatDumpedKw
      totals.jobs += s.jobsPerHour
      totals.missed += s.missedPerHour
      totals.income += s.incomeChfPerHour
      totals.cost += s.electricityChfPerHour
    }
    return totals
  }, [site, t, now])

  const rows: [string, string][] = [
    ['Type', SITE_TYPES[site.type].label],
    ['Location', `${site.city}, ${site.canton}`],
    ['Servers', `${num.format(state.serversRunning)} of ${site.servers} running`],
    ['Heat capacity', `${dec1.format(site.servers * PHYSICS.recoveredShare)} kW`],
    ['Outdoor temperature', `${dec1.format(state.temperatureC)} °C`],
    ['Heat demand', `${dec1.format(state.heatDemandKw)} kW`],
    ['Heat delivered', `${dec1.format(state.heatUsedKw)} kW`],
    ['Tank', `${pct.format(state.tankLevel)} of ${site.tankKwh} kWh`],
  ]
  const dayRows: [string, string][] = [
    ['Heat delivered', `${num.format(day.used)} kWh`],
    ['Heat dumped', `${num.format(day.dumped)} kWh`],
    ['Jobs', num.format(day.jobs)],
    ['Missed deadlines', `${num.format(day.missed)} (${pct1.format(day.missed / Math.max(1, day.jobs))})`],
    ['Computing income', chf.format(day.income)],
    ['Electricity cost', chf.format(day.cost)],
  ]

  return (
    <aside className="panel">
      <div className="panel-head">
        <div>
          <h2>{site.name}</h2>
          <span className="note">{site.id}</span>
        </div>
        <button type="button" className="close" onClick={onClose} aria-label="Close">
          ×
        </button>
      </div>

      <div className="panel-hero">
        <span className="stat-value">{state.online ? pct.format(state.load) : 'Offline'}</span>
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
      <span className="note">last 48 h</span>

      <dl className="kv">
        {rows.map(([k, v]) => (
          <div key={k}>
            <dt>{k}</dt>
            <dd>{v}</dd>
          </div>
        ))}
      </dl>

      <span className="label">Last 24 h</span>
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
