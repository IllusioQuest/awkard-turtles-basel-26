import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { CartesianGrid, ComposedChart, Legend, Line, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { axis, legend, tooltip } from '../admin/chartStyle'
import { DAY_MS, dec1 } from '../admin/format'
import '../admin/admin.css'
import './household.css'

type Point = { simulatedMs: number; demandKw: number; heatProducedKw: number; tankKwh: number; targetLoad: number }
type LiveEvent = { simulatedMs: number; kind: string; jobId: string; observed: boolean }
type LiveState = {
  runId: string; season: string; busy: boolean; startLocal: string; status: string; processStatus: string
  jobId: string; error: string | null; simulatedMs: number; demandKw: number; tankKwh: number; cpuCores: number
  tankCapacityKwh: number; targetLoad: number; tasksDone: number; latestEnergyPerSpin: number | null
  waitingJobs: number; runningJobs: number; pausedJobs: number; eventsTruncated: boolean
  lagSeconds: number; updatedUtc: string; history: Point[]; events: LiveEvent[]
  // T-78 voice demo (DigitalHeat.VoiceDemo): container output, CPU time and energy are measured.
  voice?: { stage: string; transcript: string; answer: string; usefulHeatKwh: number; requestDeclaredHeatKwhPerHour: number
    requestPowerW: number; requestCpuSeconds: number; requestEnergyKwh: number | null; hotWaterProfile: string; scheduler: string }
}

const clock = new Intl.DateTimeFormat('de-CH', { timeZone: 'UTC', hour: '2-digit', minute: '2-digit' })
const localTime = (start: string, ms: number) => clock.format(Date.parse(`${start}Z`) + ms)

export default function LiveHouseholdPage() {
  const [state, setState] = useState<LiveState | null>(null)
  const [unavailable, setUnavailable] = useState(false)
  const [now, setNow] = useState(0)
  useEffect(() => {
    let stopped = false
    async function refresh() {
      try {
        const res = await fetch('/api/live-household', { cache: 'no-store' })
        if (!res.ok) throw new Error('not available')
        const next = await res.json() as LiveState
        if (!stopped) { setState(next); setUnavailable(false) }
      } catch { if (!stopped) setUnavailable(true) }
      finally { if (!stopped) setNow(Date.now()) }
    }
    void refresh()
    const timer = window.setInterval(() => void refresh(), 1000)
    return () => { stopped = true; window.clearInterval(timer) }
  }, [])

  const stale = state?.status === 'running' && now > 0 && now - Date.parse(state.updatedUtc) > 10000
  const tick = (ms: number) => ms === DAY_MS ? '24:00' : state ? localTime(state.startLocal, ms) : ''
  return <main className="page admin household">
    <Link className="link back" to="/admin">← Network</Link>
    <h1>Live household</h1>
    {!state && <p className="note">Start a live request from the console to see it here.</p>}
    {unavailable && state && <p className="banner">Live connection unavailable; showing the last update.</p>}
    {stale && <p className="banner">Live updates stopped; showing the last update.</p>}
    {state && <>
      {state.error && <p className="banner">{state.error}</p>}
      {state.lagSeconds > 1 && <p className="note">Replay is {dec1.format(state.lagSeconds)} seconds behind its target pace.</p>}
      <p className="note">Basel · {state.season} · simulated time {tick(state.simulatedMs)}</p>
      <section className="stats">
        <LiveStat label="Replay" value={state.status} />
        <LiveStat label="Real request" value={state.processStatus} />
        <LiveStat label="Real CPU limit" value={state.cpuCores == null ? '–' : `${dec1.format(state.cpuCores)} cores`} />
        {state.voice
          ? <LiveStat label="Job power (measured)" value={`${Math.round(state.voice.requestPowerW)} W`} />
          : <LiveStat label="Tasks completed" value={String(state.tasksDone)} />}
        {state.voice && <LiveStat label="Job energy (measured)" value={state.voice.requestEnergyKwh == null ? '–' : `${state.voice.requestEnergyKwh.toFixed(6)} kWh`} />}
        {state.voice && <LiveStat label="Useful heat now" value={`${state.voice.usefulHeatKwh.toFixed(3)} kWh`} />}
        <LiveStat label="Modelled heat demand" value={`${dec1.format(state.demandKw)} kW`} />
        <LiveStat label="Modelled tank" value={`${dec1.format(state.tankKwh)} / ${dec1.format(state.tankCapacityKwh)} kWh`} />
        <LiveStat label="Requested server load" value={`${Math.round(state.targetLoad * 100)}%`} />
        {state.busy && <LiveStat label={state.status === 'completed' ? 'Model jobs waiting at end' : 'Model jobs waiting'} value={String(state.waitingJobs)} />}
        {state.busy && <LiveStat label={state.status === 'completed' ? 'Model jobs active at end' : 'Model jobs running'} value={String(state.runningJobs)} />}
        {state.busy && <LiveStat label={state.status === 'completed' ? 'Model jobs paused at end' : 'Model jobs paused'} value={String(state.pausedJobs)} />}
      </section>
      {!state.voice && <p className="note">Heat, tank and time are simulated. The real request runs until the replay ends; its task count is observed, and electrical power is not measured.</p>}
      <section className="charts">
        <div className="card"><span className="label">Modelled tank (kWh)</span>
          <ResponsiveContainer width="100%" height={220}><ComposedChart data={state.history}>
            <CartesianGrid stroke="var(--grid)" vertical={false} /><XAxis dataKey="simulatedMs" type="number" domain={[0, DAY_MS]} tickCount={5} tickFormatter={tick} {...axis} />
            <YAxis domain={[0, state.tankCapacityKwh || 'auto']} {...axis} /><Tooltip {...tooltip} labelFormatter={(v) => tick(Number(v))} />
            <Line dataKey="tankKwh" name="Tank" stroke="var(--series-1)" strokeWidth={2} dot={false} isAnimationActive={false} />
          </ComposedChart></ResponsiveContainer>
        </div>
        <div className="card"><span className="label">Requested load</span>
          <ResponsiveContainer width="100%" height={220}><ComposedChart data={state.history}>
            <CartesianGrid stroke="var(--grid)" vertical={false} /><XAxis dataKey="simulatedMs" type="number" domain={[0, DAY_MS]} tickCount={5} tickFormatter={tick} {...axis} />
            <YAxis domain={[0, 1]} tickFormatter={(v) => `${v * 100}%`} {...axis} /><Tooltip {...tooltip} labelFormatter={(v) => tick(Number(v))} />
            <Line type="stepAfter" dataKey="targetLoad" name="Load" stroke="var(--series-1)" strokeWidth={2} dot={false} isAnimationActive={false} />
          </ComposedChart></ResponsiveContainer>
        </div>
        <div className="card wide"><span className="label">Modelled heat demand and server heat (kW)</span>
          <ResponsiveContainer width="100%" height={220}><ComposedChart data={state.history}>
            <CartesianGrid stroke="var(--grid)" vertical={false} /><XAxis dataKey="simulatedMs" type="number" domain={[0, DAY_MS]} tickCount={5} tickFormatter={tick} {...axis} />
            <YAxis {...axis} /><Tooltip {...tooltip} labelFormatter={(v) => tick(Number(v))} />
            <Line type="stepAfter" dataKey="demandKw" name="Demand" stroke="var(--ink-2)" strokeDasharray="4 4" dot={false} isAnimationActive={false} />
            <Line type="stepAfter" dataKey="heatProducedKw" name="Server heat" stroke="var(--series-1)" strokeWidth={2} dot={false} isAnimationActive={false} />
            <Legend {...legend} />
          </ComposedChart></ResponsiveContainer>
        </div>
      </section>
      <section className="card job-log"><h2>Job logs</h2>
        {state.eventsTruncated && <p className="note">Showing the most recent events.</p>}
        <ol className="job-log-list">{[...state.events].reverse().map((e, i) => <li className="job-log-entry" key={`${e.simulatedMs}-${e.kind}-${i}`}>
          <span className={`job-log-dot job-log-dot-${e.kind}`} aria-hidden="true" />
          <div className="job-log-message"><span><strong>{e.jobId ?? state.jobId}</strong> {e.kind} {state.busy && <span className="note">· {e.observed ? 'real' : 'simulated'}</span>}</span></div>
          <time className="note">{tick(e.simulatedMs)}</time>
        </li>)}</ol>
        {state.latestEnergyPerSpin !== null && <p className="note">Latest real result: mean energy per spin {dec1.format(state.latestEnergyPerSpin)}.</p>}
      </section>
    </>}
  </main>
}

function LiveStat({ label, value }: { label: string; value: string }) {
  return <div className="stat"><span className="label">{label}</span><span className="stat-value">{value}</span></div>
}
