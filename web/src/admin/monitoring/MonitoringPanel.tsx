import { useEffect, useRef, useState } from 'react'
import type { Metric, MonitorConfig, MonitorInputs, MonitorSite, MonitorSnapshot } from './types'
import './monitoring.css'

const number = new Intl.NumberFormat('en-CH', { maximumFractionDigits: 2 })
const display = (metric: Metric) => metric.value === null ? 'Unavailable' : number.format(metric.value)
const explain = (metric: Metric) => `${metric.meaning}\nUnit: ${metric.unit} · ${metric.kind}\nSource: ${metric.source}`
const siteFields: [keyof Omit<MonitorSite, 'site_id' | 'name'>, string][] = [
  ['heat_needed_kwh', 'Heat needed'], ['planned_heat_kwh', 'Planned heat'],
  ['tank_stored_kwh', 'Tank at hour start'], ['tank_capacity_kwh', 'Tank capacity'],
  ['heat_headroom_kwh', 'Heat headroom'], ['wasted_heat_kwh', 'Wasted heat'],
  ['backup_heat_kwh', 'Backup heating'], ['temperature_c', 'Outside temperature'],
  ['electricity_price_chf_per_kwh', 'Electricity tariff'], ['heat_value_chf_per_kwh', 'Displaced heat value'],
  ['potential_heat_value_chf_per_hour', 'Potential heat value'],
]
async function json<T>(url: string, signal: AbortSignal, inputs?: MonitorInputs): Promise<T> {
  const response = await fetch(url, { signal, ...(inputs ? {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(inputs),
  } : {}) })
  if (!response.ok) {
    const detail = await response.json().catch(() => ({})) as { error?: string }
    throw new Error(detail.error ?? `Monitoring request failed (${response.status}). Check the local API.`)
  }
  const body: unknown = await response.json()
  if (!body || typeof body !== 'object' || (body as { schema_version?: number }).schema_version !== 1)
    throw new Error('Unsupported monitoring response. No fallback/mock readings substituted.')
  return body as T
}
function download(name: string, value: unknown) {
  const url = URL.createObjectURL(new Blob([JSON.stringify(value, null, 2)], { type: 'application/json' }))
  const a = document.createElement('a'); a.href = url; a.download = name; a.click(); URL.revokeObjectURL(url)
}
function MetricValue({ metric }: { metric: Metric }) {
  return <span title={explain(metric)}>{display(metric)} <small>{metric.unit}</small></span>
}

export default function MonitoringPanel() {
  const [config, setConfig] = useState<MonitorConfig | null>(null)
  const [inputs, setInputs] = useState<MonitorInputs | null>(null)
  const [snapshot, setSnapshot] = useState<MonitorSnapshot | null>(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(true)
  const request = useRef<AbortController | null>(null)
  const generation = useRef(0)
  useEffect(() => {
    const controller = new AbortController(); request.current = controller
    const id = ++generation.current
    void (async () => {
      try {
        const metadata = await json<MonitorConfig>('/api/monitor/config', controller.signal)
        if (id !== generation.current) return
        setConfig(metadata); setInputs(metadata.default_inputs)
        const initial = await json<MonitorSnapshot>('/api/monitor/snapshot', controller.signal, metadata.default_inputs)
        if (id === generation.current) setSnapshot(initial)
      } catch (e) {
        if (!controller.signal.aborted && id === generation.current) setError(e instanceof Error ? e.message : 'Monitoring unavailable')
      } finally { if (id === generation.current) setBusy(false) }
    })()
    return () => controller.abort()
  }, [])
  async function refresh() {
    if (!inputs) { window.location.reload(); return }
    request.current?.abort()
    const controller = new AbortController(); request.current = controller
    const id = ++generation.current
    setBusy(true); setError('')
    try {
      const next = await json<MonitorSnapshot>('/api/monitor/snapshot', controller.signal, inputs)
      if (id === generation.current) setSnapshot(next)
    } catch (e) {
      if (!controller.signal.aborted && id === generation.current) setError(e instanceof Error ? e.message : 'Monitoring unavailable')
    } finally { if (id === generation.current) setBusy(false) }
  }
  const changed = snapshot && inputs && JSON.stringify({ ...snapshot.inputs, show_details: inputs.show_details }) !== JSON.stringify(inputs)
  return <section className="monitoring" aria-labelledby="monitor-heading">
    <div className="monitor-heading">
      <div><p className="monitor-eyebrow">Local infrastructure · shared C# model</p><h2 id="monitor-heading">Compute & useful heat</h2></div>
      <span className="monitor-badge">Read-only · no game control</span>
    </div>
    <p className="monitor-intro">Real Docker observations and a separate hourly heat preview. Reservations are planned sessions, not connected players. Changing these controls never launches, pauses or stops a game.</p>
    {error && <div className="monitor-error" role="alert"><strong>Monitoring unavailable.</strong> {error} {snapshot && 'The previous snapshot below is stale.'}<br />Start <code>dotnet run --project gaming/api</code> alongside this web app. <button onClick={() => void refresh()} type="button">Retry</button></div>}
    {inputs && config && <form className="monitor-controls" onSubmit={e => { e.preventDefault(); void refresh() }}>
      <label>Data mode<select value={inputs.mode} onChange={e => setInputs({ ...inputs, mode: e.target.value as MonitorInputs['mode'], selected_hour_local: e.target.value === 'live' ? null : config.default_inputs.selected_hour_local })}><option value="replay">Historical replay (offline)</option><option value="live">Current weather + model</option></select></label>
      <label>Model hour<input type="datetime-local" step="3600" disabled={inputs.mode === 'live'} min={config.available_hours[0]} max={config.available_hours.at(-1)} value={inputs.selected_hour_local ?? ''} onChange={e => setInputs({ ...inputs, selected_hour_local: e.target.value })} /></label>
      <label>Planned sessions<input type="number" min="0" max={config.max_reservations} step="1" value={inputs.initial_reservations} onChange={e => setInputs({ ...inputs, initial_reservations: Number(e.target.value) })} required /></label>
      <div className="monitor-presets"><button type="button" onClick={() => setInputs({ ...inputs, mode: 'replay', selected_hour_local: config.winter_hour_local })}>January</button><button type="button" onClick={() => setInputs({ ...inputs, mode: 'replay', selected_hour_local: config.summer_hour_local })}>July</button></div>
      <label className="monitor-check"><input type="checkbox" checked={inputs.observe_docker} onChange={e => setInputs({ ...inputs, observe_docker: e.target.checked })} />Read Docker status</label>
      <label className="monitor-check"><input type="checkbox" checked={inputs.bypass_capabilities} onChange={e => setInputs({ ...inputs, bypass_capabilities: e.target.checked })} />What-if: bypass app setup</label>
      <button className="monitor-primary" type="submit" disabled={busy}>{busy ? 'Preparing snapshot…' : 'Update preview'}</button>
      <button type="button" onClick={() => download('monitor-inputs.json', inputs)}>Export inputs JSON</button>
    </form>}
    {busy && !snapshot && <p role="status">Loading shared monitoring data…</p>}
    {snapshot && <>
      <div className="monitor-context"><strong>{snapshot.local_hour} · {snapshot.time_basis}</strong><span>{snapshot.application_id} desktops · generated {new Date(snapshot.generated_at_utc).toLocaleTimeString()} · {snapshot.mode}</span></div>
      {changed && <p className="monitor-pending" role="status">Inputs changed. Values below still belong to the previous snapshot; select Update preview.</p>}
      <details className="monitor-warnings" open><summary>Model boundaries & data quality</summary><ul>{snapshot.warnings.map(w => <li key={w}>{w}</li>)}</ul><p>Assumed heat per session: <MetricValue metric={snapshot.session_heat_kwh_per_hour} />. Not measured Minecraft/Xonotic power.</p></details>
      <h3>Docker observations</h3>
      <p className="monitor-help">CPU: 100% means one core; multiple cores can exceed 100%. RAM: Docker-reported used / limit. Unavailable is not zero. App process checks only {snapshot.application_id}, not the Minecraft server. Status is a snapshot, not a live feed.</p>
      <div className="monitor-scroll"><table><caption>Desktop container observations and planned claims</caption><thead><tr><th scope="col">Node / site</th><th scope="col">Container</th><th scope="col">App process</th><th scope="col">CPU used</th><th scope="col">RAM used / limit</th><th scope="col">Planned / slots</th><th scope="col">New-claim precheck</th></tr></thead><tbody>{snapshot.nodes.map(n => <tr key={n.node_id}>
        <th scope="row">{n.node_id}<small>{snapshot.sites.find(s => s.site_id === n.site_id)?.name ?? n.site_id}</small></th>
        <td><span title={n.observation_detail}>{n.observation_status !== 'observed' ? n.observation_status : n.container_running ? 'Running' : 'Stopped / paused'}</span><small>{n.observed_at_utc ? new Date(n.observed_at_utc).toLocaleTimeString() : 'Not observed'}</small></td>
        <td>{n.application_running === null ? 'Unknown' : n.application_running ? 'Detected' : 'Not detected'}</td><td>{n.cpu_percent ?? 'Unavailable'}</td><td>{n.memory_used_and_limit ?? 'Unavailable'}</td>
        <td title={`${explain(n.planned_sessions)}\n${explain(n.session_slots)}`}>{display(n.planned_sessions)} / {display(n.session_slots)}</td>
        <td><strong>{n.ready_for_new_claims ? 'Passes prechecks' : 'Not ready'}</strong><small>{n.readiness_reason}</small></td>
      </tr>)}</tbody></table></div>
      <h3>Heat preview <small>kWh for one full hour</small></h3>
      <p className="monitor-help">Tank = stored energy at hour start, not water temperature. Headroom = remaining useful-heat acceptance room, not computing capacity. Backup heating = energy another heater supplies, not a standby server.</p>
      <div className="monitor-sites">{snapshot.sites.map(site => <article key={site.site_id} className="monitor-site"><h4>{site.name}</h4><dl>{siteFields.slice(0, 7).map(([key, label]) => <div key={key}><dt>{label}</dt><dd><MetricValue metric={site[key]} /></dd></div>)}</dl></article>)}</div>
      <details className="monitor-guide" open={inputs?.show_details ?? false} onToggle={e => { if (inputs && e.currentTarget.open !== inputs.show_details) setInputs({ ...inputs, show_details: e.currentTarget.open }) }}><summary>What every value means · units, assumptions & sources</summary>
        <p>Snapshot values come directly from the existing C# engine. No second heat or scheduling model runs in the browser. Source scenario: <code>{snapshot.scenario_source}</code>.</p>
        {snapshot.sites.map(site => <div key={site.site_id}><h4>{site.name}</h4><dl>{siteFields.map(([key, label]) => <div key={key}><dt>{label} · <MetricValue metric={site[key]} /></dt><dd>{site[key].meaning}<small>{site[key].kind} · Source: {site[key].source}</small></dd></div>)}</dl></div>)}
      </details>
      <div className="monitor-footer"><span>{snapshot.placed_reservations} of {snapshot.inputs.initial_reservations} requested claims placed. Heat and slots still constrain admission.</span><button type="button" onClick={() => download('monitor-snapshot.json', snapshot)}>Export snapshot JSON</button></div>
    </>}
    <p className="monitor-separation">This monitoring section uses the local C# data adapter. The existing network dashboard below retains its own data source and controls; its mock figures are not Docker readings.</p>
  </section>
}
