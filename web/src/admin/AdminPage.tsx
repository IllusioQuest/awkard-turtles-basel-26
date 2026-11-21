import { useEffect, useMemo, useState } from 'react'
import { Navigate, useNavigate, useParams, useSearchParams } from 'react-router'
import { useAsync, useSource } from '../data/source'
import type { DataSource, HouseInfo, HousePeriod, NetworkHour, RunInfo } from '../data/types'
import MapView, { type HeatLayer, type MapState } from './MapView'
import MockBanner from './MockBanner'
import SitePanel from './SitePanel'
import { EconomicsChart, HeatChart, LoadChart, MissedChart, type MissedPoint, type MonthlyPoint, type SeriesPoint } from './charts'
import { DAY_MS, HOURS_PER_DAY, alignHour, chf, dateTime, dayTick, dec1, lastHour, monthTick, num, pct, wallMs } from './format'
import { makeInterpolator, type WeatherField, type WeatherInterpolator } from './weather'
import './admin.css'

const CHART_WINDOW_DAYS = 3
const PLAY_STEP_MS = 120 // one period (hour or day) per tick

const RANGES = [
  { id: '24h', label: '24 h', hours: 24 },
  { id: '7d', label: '7 days', hours: 7 * 24 },
  { id: '30d', label: '30 days', hours: 30 * 24 },
] as const
type RangeId = (typeof RANGES)[number]['id']

const HEAT_LAYERS: { id: HeatLayer; label: string }[] = [
  { id: 'none', label: 'Houses' },
  { id: 'load', label: 'Load' },
  { id: 'capacity', label: 'Free capacity' },
]

export default function AdminPage() {
  const { runId = '' } = useParams()
  const source = useSource()
  const runs = useAsync(source ? 'runs' : null, () => source!.runs())
  const houses = useAsync(source ? `houses:${runId}` : null, () => source!.houses(runId))
  const network = useAsync(source ? `network:${runId}` : null, () => source!.network(runId))

  const error = runs.error ?? houses.error ?? network.error
  if (runs.data && !runs.data.some((r) => r.id === runId)) return <Navigate to="/admin" replace />
  if (error) return <main className="page admin"><p className="banner">{error.message}</p></main>
  if (!source || !runs.data || !houses.data || !network.data || houses.loading || network.loading)
    return <main className="page admin"><p className="note">Loading…</p></main>
  const run = runs.data.find((r) => r.id === runId)!
  return <RunView key={run.id} source={source} run={run} runs={runs.data} houses={houses.data} network={network.data} />
}

interface Props {
  source: DataSource
  run: RunInfo
  runs: RunInfo[]
  houses: HouseInfo[]
  network: NetworkHour[]
}

const interpolators = new WeakMap<object, WeatherInterpolator>()

function RunView({ source, run, runs, houses, network }: Props) {
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const step = run.periodHours
  const maxT = lastHour(run)
  const [t, setT] = useState(() => alignHour(run, Number(params.get('h') ?? 0)))
  const [heatLayer, setHeatLayer] = useState<HeatLayer>('none')
  const [showWeather, setShowWeather] = useState(false)
  const [rangeId, setRangeId] = useState<RangeId>('24h')
  const rangeHours = RANGES.find((r) => r.id === rangeId)!.hours
  const [selectedId, setSelectedId] = useState<string | null>(() => {
    const id = params.get('house')
    return houses.some((h) => h.id === id) ? id : null
  })
  const [playing, setPlaying] = useState(false)

  useEffect(() => {
    if (!playing) return
    const timer = setInterval(() => {
      setT((prev) => {
        if (prev + step > maxT) {
          setPlaying(false)
          return prev
        }
        return prev + step
      })
    }, PLAY_STEP_MS)
    return () => clearInterval(timer)
  }, [playing, step, maxT])

  const togglePlay = () => {
    if (!playing && t >= maxT) setT(0)
    setPlaying((p) => !p)
  }

  // Map states: every house's period at hour t.
  const snapshot = useAsync(`snap:${run.id}:${t}`, () => source.snapshots(run.id, t, t + 1))
  const states = useMemo(() => {
    const map = new Map<string, MapState>()
    const snap = snapshot.data?.[0]
    if (!snap) return map
    houses.forEach((h, i) => {
      const p = snap.houses[i]
      if (!p) return
      map.set(h.id, {
        load: p.load,
        tankLevel: h.tankKwh > 0 ? p.tankEndKwh / h.tankKwh : 0,
        heatProducedKw: p.heatProducedKwh / snap.periodHours,
        heatUsedKw: p.heatUsedKwh / snap.periodHours,
        heatDumpedKw: p.heatDumpedKwh / snap.periodHours,
      })
    })
    return map
  }, [snapshot.data, houses])
  const snapshotOf = (id: string): HousePeriod | undefined => snapshot.data?.[0]?.houses[houses.findIndex((h) => h.id === id)]

  const weather = useWeatherField(source, run, t, showWeather)

  // Stats over the hours (t − range, t].
  const totals = useMemo(() => {
    const sum = { load: 0, used: 0, dumped: 0, completions: 0, misses: 0, income: 0, cost: 0, n: 0 }
    for (let h = Math.max(0, t - rangeHours + 1); h <= t && h < network.length; h++) {
      const n = network[h]
      sum.load += n.load
      sum.used += n.heatUsedKwh
      sum.dumped += n.heatDumpedKwh
      sum.completions += n.completions
      sum.misses += n.misses
      sum.income += n.incomeChf
      sum.cost += n.electricityCostChf
      sum.n++
    }
    return sum
  }, [network, t, rangeHours])

  const series = useMemo<SeriesPoint[]>(() => {
    const from = Math.max(0, t - CHART_WINDOW_DAYS * HOURS_PER_DAY)
    const to = Math.min(network.length - 1, t + CHART_WINDOW_DAYS * HOURS_PER_DAY)
    return network.slice(from, to + 1).map((n) => ({
      t: wallMs(run, n.h),
      load: n.load,
      heatUsedKw: n.heatUsedKwh,
      heatDumpedKw: n.heatDumpedKwh,
      heatDemandKw: n.heatDemandKwh,
    }))
  }, [network, t, run])

  const monthly = useMemo<MonthlyPoint[]>(() => {
    const byMonth = new Map<number, MonthlyPoint>()
    for (const n of network) {
      const d = new Date(wallMs(run, n.h))
      const start = Date.UTC(d.getUTCFullYear(), d.getUTCMonth(), 1)
      let m = byMonth.get(start)
      if (!m) byMonth.set(start, (m = { month: monthTick.format(start), start, incomeChf: 0, electricityChf: 0 }))
      m.incomeChf += n.incomeChf
      m.electricityChf += n.electricityCostChf
    }
    return [...byMonth.values()]
  }, [network, run])

  const missed = useMemo<MissedPoint[]>(() => {
    const from = Math.max(0, t - CHART_WINDOW_DAYS * HOURS_PER_DAY)
    const to = Math.min(network.length - 1, t + CHART_WINDOW_DAYS * HOURS_PER_DAY)
    const byDay = new Map<number, MissedPoint>()
    for (let h = from; h <= to; h++) {
      const start = Math.floor(wallMs(run, h) / DAY_MS) * DAY_MS
      let d = byDay.get(start)
      if (!d) byDay.set(start, (d = { day: dayTick.format(start), start, misses: 0 }))
      d.misses += network[h].misses
    }
    return [...byDay.values()]
  }, [network, t, run])

  const selected = houses.find((h) => h.id === selectedId) ?? null
  const houseSeries = useAsync(selected ? `series:${run.id}:${selected.id}` : null, () => source.houseSeries(run.id, selected!.id))

  const dumpedShare = totals.dumped / Math.max(1, totals.used + totals.dumped)
  const missedShare = totals.misses / Math.max(1, totals.completions + totals.misses)
  const mapHouses = useMemo(() => houses.map((h) => ({ id: h.id, name: `${h.municipality || 'House'} ${h.id}`, lat: h.lat, lon: h.lon, servers: h.servers })), [houses])
  const wall = wallMs(run, t)

  return (
    <main className="page admin">
      {run.mock && <MockBanner />}
      <div className="page-head">
        <div>
          <h1>Network</h1>
          {run.note && <span className="note">{run.note}</span>}
        </div>
        <div className="head-controls">
          <select aria-label="Run" value={run.id} onChange={(e) => navigate(`/admin/${encodeURIComponent(e.target.value)}`)}>
            {runs.map((r) => (
              <option key={r.id} value={r.id}>
                {r.id}
              </option>
            ))}
          </select>
          <div className="segmented compact" role="radiogroup" aria-label="Time range">
            {RANGES.map((r) => (
              <button key={r.id} type="button" role="radio" aria-checked={rangeId === r.id} className={rangeId === r.id ? 'active' : ''} onClick={() => setRangeId(r.id)}>
                {r.label}
              </button>
            ))}
          </div>
        </div>
      </div>

      <section className="stats">
        <Stat label="Houses" value={num.format(run.houses)} note={`${num.format(run.servers)} servers`} />
        <Stat label="Average load" value={pct.format(totals.n ? totals.load / totals.n : 0)} note={`at selected hour ${pct.format(network[t]?.load ?? 0)}`} />
        <Stat label="Heat delivered" value={`${dec1.format(totals.used / 1000)} MWh`} />
        <Stat label="Heat dumped" value={pct.format(dumpedShare)} note={`${num.format(totals.dumped)} kWh`} />
        <Stat label="Missed deadlines" value={pct.format(missedShare)} note={`${num.format(totals.misses)} of ${num.format(totals.completions + totals.misses)} deadlines`} />
        <Stat label="Computing income" value={chf.format(totals.income)} />
        <Stat label="Electricity cost" value={chf.format(totals.cost)} />
      </section>

      <section className="map-card">
        <MapView
          houses={mapHouses}
          states={states}
          weather={weather}
          heatLayer={heatLayer}
          showWeather={showWeather}
          selectedId={selectedId}
          onSelect={setSelectedId}
        />

        <div className="map-controls">
          <div className="segmented compact">
            {HEAT_LAYERS.map((l) => (
              <button key={l.id} type="button" className={heatLayer === l.id ? 'active' : ''} onClick={() => setHeatLayer(l.id)}>
                {l.label}
              </button>
            ))}
          </div>
          <button type="button" role="switch" aria-checked={showWeather} className="switch" onClick={() => setShowWeather((v) => !v)}>
            <span className="switch-track">
              <span className="switch-knob" />
            </span>
            Weather
          </button>
        </div>

        {weather && showWeather && (
          <div className="map-legend">
            <span>Temperature</span>
            <div className="legend-bar" />
            <div className="legend-ticks">
              <span>{dec1.format(weather.lowC)} °C</span>
              <span>{dec1.format(weather.medianC)} °C</span>
              <span>{dec1.format(weather.highC)} °C</span>
            </div>
          </div>
        )}

        {selected && (
          <SitePanel
            run={run}
            house={selected}
            state={snapshotOf(selected.id)}
            series={houseSeries.data}
            t={t}
            onMore={() => navigate(`/admin/${encodeURIComponent(run.id)}/houses/${encodeURIComponent(selected.id)}?h=${t}`)}
            onClose={() => setSelectedId(null)}
          />
        )}
      </section>

      <section className="timeline">
        <div className="timeline-head">
          <span className="timeline-time">{dateTime.format(wall)}</span>
          <span className="timeline-mode">
            {step === HOURS_PER_DAY ? 'Day' : 'Hour'} {num.format(t / step + 1)} of {num.format(maxT / step + 1)}
          </span>
        </div>
        <div className="timeline-row">
          <button type="button" className="play" onClick={togglePlay} aria-label={playing ? 'Pause' : 'Play'}>
            {playing ? (
              <svg viewBox="0 0 24 24" width="16" height="16" fill="currentColor">
                <path d="M7 5h4v14H7zm6 0h4v14h-4z" />
              </svg>
            ) : (
              <svg viewBox="0 0 24 24" width="16" height="16" fill="currentColor">
                <path d="M8 5v14l11-7z" />
              </svg>
            )}
          </button>
          <div className="timeline-track">
            <input
              type="range"
              min={0}
              max={maxT}
              step={step}
              value={t}
              onChange={(e) => {
                setPlaying(false)
                setT(Number(e.target.value))
              }}
              className="timeline-slider"
            />
            <div className="timeline-ticks">
              <span>{dateTime.format(wallMs(run, 0))}</span>
              <span>{dateTime.format(wallMs(run, maxT))}</span>
            </div>
          </div>
        </div>
      </section>

      <section className="charts">
        <div className="card">
          <span className="label">Network load</span>
          <LoadChart data={series} t={wall} />
        </div>
        <div className="card">
          <span className="label">Heat</span>
          <HeatChart data={series} t={wall} />
        </div>
        <div className="card wide">
          <span className="label">Computing income and electricity cost per month</span>
          <EconomicsChart data={monthly} />
        </div>
        <div className="card wide">
          <div className="card-head">
            <span className="label">Missed deadlines per day</span>
            <span className="note">
              {num.format(totals.misses)} of {num.format(totals.completions + totals.misses)} in the selected range
            </span>
          </div>
          <MissedChart data={missed} />
        </div>
      </section>
    </main>
  )
}

/** Temperature field at hour t from the real weather points; null while loading or when the overlay is off. */
function useWeatherField(source: DataSource, run: RunInfo, t: number, enabled: boolean): WeatherField | null {
  const points = useAsync(enabled ? 'weather-points' : null, () => source.weatherPoints())
  const hours = useAsync(enabled ? `weather:${run.id}:${t}` : null, () => source.weather(run.id, t, t + 1))
  return useMemo(() => {
    if (!enabled || !points.data || !hours.data?.temperatureC[0]) return null
    let interpolator = interpolators.get(points.data)
    if (!interpolator) interpolators.set(points.data, (interpolator = makeInterpolator(points.data)))
    return interpolator.fill(hours.data.temperatureC[0])
  }, [enabled, points.data, hours.data])
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
