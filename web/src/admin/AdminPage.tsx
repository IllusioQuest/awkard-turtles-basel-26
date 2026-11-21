import { useEffect, useMemo, useState } from 'react'
import MapView, { type HeatLayer } from './MapView'
import SitePanel from './SitePanel'
import { EconomicsChart, HeatChart, LoadChart } from './charts'
import {
  HOURS_PER_DAY,
  MS_PER_HOUR,
  SITES,
  missedJobs,
  networkAt,
  networkSeries,
  networkTotals,
  monthlyEconomics,
  siteState,
  weatherField,
} from './mock'
import { chf, dateTime, dec1, num, pct } from './format'
import './admin.css'

const HISTORY_DAYS = 30
const FORECAST_DAYS = 7
const CHART_WINDOW_DAYS = 3
const DAY_MS = HOURS_PER_DAY * MS_PER_HOUR
const PLAY_STEP_MS = 120 // one simulated hour per tick

const NOW = Math.floor(Date.now() / MS_PER_HOUR) * MS_PER_HOUR
const MIN_T = NOW - HISTORY_DAYS * DAY_MS
const MAX_T = NOW + FORECAST_DAYS * DAY_MS
const TOTAL_SERVERS = SITES.reduce((n, s) => n + s.servers, 0)

const RANGES = [
  { id: '24h', label: '24 h', hours: 24 },
  { id: '7d', label: '7 days', hours: 7 * 24 },
  { id: '30d', label: '30 days', hours: 30 * 24 },
] as const
type RangeId = (typeof RANGES)[number]['id']

const HEAT_LAYERS: { id: HeatLayer; label: string }[] = [
  { id: 'none', label: 'Sites' },
  { id: 'load', label: 'Load' },
  { id: 'capacity', label: 'Free capacity' },
]

export default function AdminPage() {
  const [t, setT] = useState(NOW)
  const [heatLayer, setHeatLayer] = useState<HeatLayer>('none')
  const [showWeather, setShowWeather] = useState(false)
  const [rangeId, setRangeId] = useState<RangeId>('24h')
  const rangeHours = RANGES.find((r) => r.id === rangeId)!.hours
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [playing, setPlaying] = useState(false)

  useEffect(() => {
    if (!playing) return
    const timer = setInterval(() => {
      setT((prev) => {
        if (prev + MS_PER_HOUR > MAX_T) {
          setPlaying(false)
          return prev
        }
        return prev + MS_PER_HOUR
      })
    }, PLAY_STEP_MS)
    return () => clearInterval(timer)
  }, [playing])

  const togglePlay = () => {
    if (!playing && t >= MAX_T) setT(MIN_T)
    setPlaying((p) => !p)
  }

  const isForecast = t > NOW
  const states = useMemo(() => new Map(SITES.map((s) => [s.id, siteState(s, t, isForecast)])), [t, isForecast])
  const weather = useMemo(() => (showWeather ? weatherField(t) : null), [t, showWeather])
  const current = useMemo(() => networkAt(t, NOW), [t])
  const totals = useMemo(() => networkTotals(t - rangeHours * MS_PER_HOUR, t, NOW), [t, rangeHours])
  const series = useMemo(
    () => networkSeries(Math.max(MIN_T, t - CHART_WINDOW_DAYS * DAY_MS), Math.min(MAX_T, t + CHART_WINDOW_DAYS * DAY_MS), NOW),
    [t],
  )
  const monthly = useMemo(() => monthlyEconomics(NOW), [])
  const missed = useMemo(() => (isForecast ? [] : missedJobs(t)), [t, isForecast])
  const selected = SITES.find((s) => s.id === selectedId) ?? null

  const dumpedShare = totals.heatDumpedKwh / Math.max(1, totals.heatUsedKwh + totals.heatDumpedKwh)
  const missedShare = totals.missed / Math.max(1, totals.jobs)
  const avgLoad = totals.serverHours / (TOTAL_SERVERS * rangeHours)

  return (
    <main className="page admin">
      <div className="page-head">
        <h1>Network</h1>
        <div className="segmented compact" role="radiogroup" aria-label="Time range">
          {RANGES.map((r) => (
            <button key={r.id} type="button" role="radio" aria-checked={rangeId === r.id} className={rangeId === r.id ? 'active' : ''} onClick={() => setRangeId(r.id)}>
              {r.label}
            </button>
          ))}
        </div>
      </div>

      <section className="stats">
        <Stat label="Sites online" value={`${current.sitesOnline} / ${SITES.length}`} note={`${num.format(TOTAL_SERVERS)} servers`} />
        <Stat label="Average load" value={pct.format(avgLoad)} note={`now ${pct.format(current.load)}`} />
        <Stat label="Heat delivered" value={`${num.format(totals.heatUsedKwh / 1000)} MWh`} />
        <Stat label="Heat dumped" value={pct.format(dumpedShare)} note={`${num.format(totals.heatDumpedKwh)} kWh`} />
        <Stat label="Missed deadlines" value={pct.format(missedShare)} note={`${num.format(totals.missed)} of ${num.format(totals.jobs)} jobs`} />
        <Stat label="Computing income" value={chf.format(totals.incomeChf)} />
        <Stat label="Electricity cost" value={chf.format(totals.electricityChf)} />
      </section>

      <section className="map-card">
        <MapView
          sites={SITES}
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

        {weather && (
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
          <SitePanel site={selected} state={states.get(selected.id)!} t={t} now={NOW} onClose={() => setSelectedId(null)} />
        )}
      </section>

      <section className="timeline">
        <div className="timeline-head">
          <span className="timeline-time">{dateTime.format(t)}</span>
          <span className="timeline-mode">{t === NOW ? 'Live' : isForecast ? 'Forecast' : 'History'}</span>
          {t !== NOW && (
            <button
              type="button"
              className="link"
              onClick={() => {
                setPlaying(false)
                setT(NOW)
              }}
            >
              Back to now
            </button>
          )}
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
              min={MIN_T}
              max={MAX_T}
              step={MS_PER_HOUR}
              value={t}
              onChange={(e) => {
                setPlaying(false)
                setT(Number(e.target.value))
              }}
              style={{ '--now': `${((NOW - MIN_T) / (MAX_T - MIN_T)) * 100}%` } as React.CSSProperties}
              className="timeline-slider"
            />
            <div className="timeline-ticks">
              <span>−{HISTORY_DAYS} days</span>
              <span style={{ left: `${((NOW - MIN_T) / (MAX_T - MIN_T)) * 100}%` }}>Now</span>
              <span>+{FORECAST_DAYS} days</span>
            </div>
          </div>
        </div>
      </section>

      <section className="charts">
        <div className="card">
          <span className="label">Network load</span>
          <LoadChart data={series} t={t} now={NOW} />
        </div>
        <div className="card">
          <span className="label">Heat</span>
          <HeatChart data={series} t={t} now={NOW} />
        </div>
        <div className="card wide">
          <span className="label">Computing income and electricity cost per month</span>
          <EconomicsChart data={monthly} />
        </div>
        <div className="card wide">
          <div className="card-head">
            <span className="label">Missed deadlines</span>
            <span className="note">{isForecast ? 'No events in forecast mode' : `last 24 h until ${dateTime.format(t)}`}</span>
          </div>
          {missed.length > 0 && (
            <table className="table">
              <thead>
                <tr>
                  <th>Job</th>
                  <th>Type</th>
                  <th>Site</th>
                  <th>Deadline</th>
                  <th className="num">Late</th>
                </tr>
              </thead>
              <tbody>
                {missed.map((j) => (
                  <tr key={j.id + j.siteId} onClick={() => setSelectedId(j.siteId)}>
                    <td>{j.id}</td>
                    <td>{j.jobType}</td>
                    <td>{j.siteName}</td>
                    <td>{dateTime.format(j.deadline)}</td>
                    <td className="num">{j.lateMinutes} min</td>
                  </tr>
                ))}
              </tbody>
            </table>
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
