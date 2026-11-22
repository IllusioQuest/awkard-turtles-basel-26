import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import QRCode from 'qrcode'
import { DEFAULT_CONFIG, loadConfig, type PitchConfig, type Scene, type Step } from './config'
import './pitch.css'

// Pitch mode (decision 0069): one browser tab plays the whole pitch. Pages are preloaded in stacked,
// full-size iframes and only made visible, so nothing loads on stage. Keys: → / Space / PageDown next,
// ← / PageUp back, 1–9 scene, G globe, B or . black, F fullscreen, H checks, V fallback recording,
// T type a request, Q set the QR address, R reload the current page.

type ConsoleLine = { level: string; text: string }
type Live = { status: string; processStatus: string; updatedUtc: string; voice?: { console?: ConsoleLine[] } }
type Check = { name: string; ok: boolean | null; detail: string }

const MSG = 'digitalheat-pitch'
const DEBOUNCE_MS = 250
const params = new URLSearchParams(window.location.search)
const TOKEN = params.get('t') ?? ''

function withPitchParam(url: string) {
  return url + (url.includes('?') ? '&' : '?') + 'pitch=1'
}
function stepsOf(s: Scene): Step[] {
  return s.type === 'page' || s.type === 'demo' ? (s.steps?.length ? s.steps : [{}]) : [{}]
}
function parseHash(): [number, number] {
  const m = /s=(\d+)\.(\d+)/.exec(window.location.hash)
  return m ? [Number(m[1]), Number(m[2])] : [0, 0]
}

export default function PitchPage() {
  const [config, setConfig] = useState<PitchConfig>(DEFAULT_CONFIG)
  const [fromFile, setFromFile] = useState(false)
  const [[scene, step], setPos] = useState<[number, number]>(parseHash)
  // A reload during the pitch resumes at the scene in the URL hash, without the start screen.
  const [started, setStarted] = useState(() => /s=\d+\.\d+/.test(window.location.hash))
  const [black, setBlack] = useState(false)
  const [help, setHelp] = useState(false)
  const [fallback, setFallback] = useState(false)
  const [typing, setTyping] = useState(false)
  const [typed, setTyped] = useState('')
  const [sent, setSent] = useState('')
  const [hud, setHud] = useState(false)
  const [videoTime, setVideoTime] = useState(0)
  const [videoError, setVideoError] = useState(false)
  const [speakOverride, setSpeakOverride] = useState(() => localStorage.getItem('digitalheat.pitch.speak') ?? '')
  const [qrImages, setQrImages] = useState<Record<string, string>>({})
  const [live, setLive] = useState<Live | null>(null)
  const [mapOnline, setMapOnline] = useState<boolean | null>(null)
  const [checks, setChecks] = useState<Check[]>([])
  const [reloads, setReloads] = useState<Record<string, number>>({})
  const frames = useRef<Record<string, HTMLIFrameElement | null>>({})
  const video = useRef<HTMLVideoElement>(null)
  const lastKey = useRef(0)

  const scenes = config.scenes
  const current = scenes[Math.min(scene, scenes.length - 1)]
  const currentStep = stepsOf(current)[Math.min(step, stepsOf(current).length - 1)]
  const videoSrc = useMemo(() => {
    const v = scenes.find((s) => s.type === 'video')
    return v && v.type === 'video' ? v.src : undefined
  }, [scenes])
  const pageUrls = useMemo(() => [...new Set(scenes.flatMap((s) => (s.type === 'page' ? [s.url] : s.type === 'demo' ? [s.liveUrl] : [])))], [scenes])

  useEffect(() => { loadConfig().then(({ config, fromFile }) => { setConfig(config); setFromFile(fromFile) }) }, [])
  useEffect(() => { window.location.hash = `s=${scene}.${step}` }, [scene, step])

  // The presenter's phone page: a manual override (Q), else the running ngrok tunnel + /speak with
  // the token of this page's URL. Its QR is shown only in the checks screen (H), for the presenter.
  const [tunnel, setTunnel] = useState<{ url: string; addr: string | null } | null>(null)
  useEffect(() => {
    fetch('/api/voice-demo/tunnel').then((r) => (r.ok ? r.json() : null)).then(setTunnel).catch(() => setTunnel(null))
  }, [help])
  const speakUrl = useMemo(() => {
    if (speakOverride) return speakOverride
    const t = TOKEN ? `?t=${encodeURIComponent(TOKEN)}` : ''
    return tunnel?.url ? `${tunnel.url}/speak${t}` : ''
  }, [speakOverride, tunnel])
  // QR images: the speak page and every audience QR scene.
  const qrTargets = useMemo(() => [speakUrl, ...scenes.flatMap((s) => (s.type === 'qr' ? [s.url] : []))].filter(Boolean), [speakUrl, scenes])
  useEffect(() => {
    let stop = false
    Promise.all(qrTargets.map(async (u) => [u, await QRCode.toDataURL(u, { margin: 1, width: 560, color: { dark: '#0d0d0d', light: '#ffffff' } }).catch(() => '')] as const))
      .then((pairs) => { if (!stop) setQrImages(Object.fromEntries(pairs)) })
    return () => { stop = true }
  }, [qrTargets])
  const externalUrls = useMemo(() => [...new Set(scenes.flatMap((s) => (s.type === 'external' ? [s.url] : [])))], [scenes])

  const post = useCallback((url: string, msg: object) => {
    frames.current[url]?.contentWindow?.postMessage({ source: MSG, ...msg }, window.location.origin)
  }, [])

  // Apply a scene's step to its page: map hour or globe.
  useEffect(() => {
    if (current.type !== 'page') return
    if (currentStep.h !== undefined) post(current.url, { type: 'setHour', h: currentStep.h })
    if (currentStep.globe) post(current.url, { type: 'globe' })
    window.focus()
  }, [current, currentStep, post])

  // Video: start from the beginning when its scene is entered (a key press is the user gesture).
  useEffect(() => {
    const v = video.current
    if (!v) return
    if (current.type === 'video' && started && !black) { v.currentTime = 0; v.play().catch(() => {}) }
    else v.pause()
  }, [current, started, black])

  // Live demo data: poll while the demo is on screen or the checks are open.
  useEffect(() => {
    if (current.type !== 'demo' && !help) return
    let stop = false
    const tick = async () => {
      try {
        const r = await fetch('/api/live-household', { cache: 'no-store' })
        if (!stop) setLive(r.ok ? await r.json() : null)
      } catch { if (!stop) setLive(null) }
    }
    void tick()
    const timer = setInterval(tick, 500)
    return () => { stop = true; clearInterval(timer) }
  }, [current, help])

  // Messages from the map page: which style it uses (online with streets, or bundled land).
  useEffect(() => {
    const onMessage = (e: MessageEvent) => {
      if (e.origin !== window.location.origin) return
      const m = e.data as { source?: string; type?: string; online?: boolean }
      if (m?.source === 'digitalheat-map' && m.type === 'style') setMapOnline(!!m.online)
    }
    window.addEventListener('message', onMessage)
    return () => window.removeEventListener('message', onMessage)
  }, [])

  const go = useCallback((dir: 1 | -1) => {
    setFallback(false)
    setPos(([s, st]) => {
      const n = stepsOf(scenes[s]).length
      if (dir === 1) {
        if (st + 1 < n) return [s, st + 1]
        return s + 1 < scenes.length ? [s + 1, 0] : [s, st]
      }
      if (st > 0) return [s, st - 1]
      if (s === 0) return [0, 0]
      // Leaving the globe backwards: reload the map so it is flat again.
      const prev = scenes[s - 1]
      if (scenes[s].type === 'page' && stepsOf(scenes[s]).some((x) => x.globe)) {
        const url = (scenes[s] as { url: string }).url
        setReloads((r) => ({ ...r, [url]: (r[url] ?? 0) + 1 }))
      }
      return [s - 1, stepsOf(prev).length - 1]
    })
  }, [scenes])

  const onKey = useCallback((e: KeyboardEvent) => {
    if (typing) { if (e.key === 'Escape') setTyping(false); return }
    const k = e.key
    if (k === 'F5' || ((e.ctrlKey || e.metaKey) && k.toLowerCase() === 'r')) { e.preventDefault(); return }
    const now = performance.now()
    const nav = ['ArrowRight', ' ', 'PageDown', 'ArrowLeft', 'PageUp'].includes(k)
    if (nav && now - lastKey.current < DEBOUNCE_MS) { e.preventDefault(); return }
    if (nav) lastKey.current = now
    setHud(true)
    if (k === 'ArrowRight' || k === ' ' || k === 'PageDown') { e.preventDefault(); if (!started) setStarted(true); else go(1) }
    else if (k === 'ArrowLeft' || k === 'PageUp') { e.preventDefault(); go(-1) }
    else if (/^[1-9]$/.test(k) && Number(k) <= scenes.length) { setStarted(true); setFallback(false); setPos([Number(k) - 1, 0]) }
    else if (k === 'g' || k === 'G') {
      const i = scenes.findIndex((s) => s.type === 'page' && stepsOf(s).some((x) => x.globe))
      if (i >= 0) { setStarted(true); setPos([i, 0]) }
    }
    else if (k === 'b' || k === 'B' || k === '.') setBlack((b) => !b)
    else if (k === 'f' || k === 'F') { if (document.fullscreenElement) void document.exitFullscreen(); else void document.documentElement.requestFullscreen() }
    else if (k === 'h' || k === 'H' || k === '?') setHelp((h) => !h)
    else if (k === 'v' || k === 'V') setFallback((f) => !f)
    else if (k === 't' || k === 'T') { e.preventDefault(); setTyping(true) }
    else if (k === 'q' || k === 'Q') {
      const u = window.prompt("Presenter's speak page (empty = ngrok tunnel + /speak)", speakUrl)
      if (u !== null) { localStorage.setItem('digitalheat.pitch.speak', u.trim()); setSpeakOverride(u.trim()) }
    }
    else if (k === 'r' || k === 'R') {
      const url = current.type === 'page' || current.type === 'external' ? current.url : current.type === 'demo' ? current.liveUrl : ''
      if (url) setReloads((r) => ({ ...r, [url]: (r[url] ?? 0) + 1 }))
    }
    else if (k === 'Escape') { setHelp(false); setFallback(false) }
  }, [typing, started, go, scenes, current, speakUrl])

  useEffect(() => {
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onKey])
  useEffect(() => {
    if (!hud) return
    const timer = setTimeout(() => setHud(false), 2500)
    return () => clearTimeout(timer)
  }, [hud, scene, step])

  const onKeyRef = useRef(onKey)
  useEffect(() => { onKeyRef.current = onKey }, [onKey])
  // The scene and step on screen, for pages that finish loading later (a reload mid-pitch).
  const posRef = useRef({ current, currentStep })
  useEffect(() => { posRef.current = { current, currentStep } }, [current, currentStep])

  // Keys pressed while a page iframe has focus still drive the pitch (same origin).
  const attachKeys = useCallback((url: string) => {
    const w = frames.current[url]?.contentWindow
    if (!w) return
    w.addEventListener('keydown', (e) => { onKeyRef.current(e) })
    window.focus()
    // Preload every hour this page will show, so its data is cached before the pitch; then put the
    // page into the state of the step that is on screen now (if any).
    const hours = [...new Set(scenes.flatMap((s) => (s.type === 'page' && s.url === url ? stepsOf(s) : []))
      .map((x) => x.h).filter((h): h is number => h !== undefined))]
    hours.forEach((h, i) => setTimeout(() => post(url, { type: 'setHour', h }), 1200 + i * 1200))
    setTimeout(() => {
      const { current: c, currentStep: st } = posRef.current
      if (c.type !== 'page' || c.url !== url) {
        if (hours.length) post(url, { type: 'setHour', h: hours[0] })
        return
      }
      if (st.h !== undefined) post(url, { type: 'setHour', h: st.h })
      else if (st.globe) {
        const last = scenes.filter((s) => s.type === 'page' && s.url === url).flatMap(stepsOf).filter((x) => x.h !== undefined).pop()
        if (last?.h !== undefined) post(url, { type: 'setHour', h: last.h })
        post(url, { type: 'globe' })
      }
    }, 1200 + hours.length * 1200 + 400)
  }, [scenes, post])

  // Checks (key H): everything the pitch needs, green or red.
  useEffect(() => {
    if (!help) return
    let stop = false
    ;(async () => {
      const out: Check[] = []
      const get = async (u: string) => { try { const r = await fetch(u, { cache: 'no-store' }); return r.ok ? r : null } catch { return null } }
      const runsRes = await get('/api/runs')
      const runs: { id: string }[] = runsRes ? await runsRes.json() : []
      out.push({ name: 'Fleet API', ok: !!runsRes, detail: runsRes ? `${runs.length} runs` : 'not reachable: start src/DigitalHeat.FleetApi' })
      const needed = [...new Set(scenes.flatMap((s) => (s.type === 'page' ? [/\/admin\/([^/?]+)/.exec(s.url)?.[1]] : [])).filter(Boolean))] as string[]
      for (const id of needed) out.push({ name: `Run ${id}`, ok: runs.some((r) => r.id === id), detail: runs.some((r) => r.id === id) ? 'ok' : 'missing: run it with FleetRun' })
      const vid = scenes.find((s) => s.type === 'video')
      if (vid && vid.type === 'video') {
        // The dev server answers a missing file with the app page (200), so check the content type.
        const r = await get(vid.src)
        const isVideo = !!r && (r.headers.get('content-type') ?? '').startsWith('video/')
        out.push({ name: 'Intro film', ok: isVideo, detail: isVideo ? vid.src : `missing web/public${vid.src} (a title card is shown instead)` })
      }
      const fresh = live ? Date.now() - Date.parse(live.updatedUtc) < 15000 : false
      out.push({ name: 'Voice runner', ok: !!live && (fresh || live.status !== 'running'), detail: live ? `${live.status}, updated ${Math.round((Date.now() - Date.parse(live.updatedUtc)) / 1000)} s ago` : 'no live snapshot: start src/DigitalHeat.VoiceDemo' })
      const webTunnel = !!tunnel && /:?5173$/.test(tunnel.addr ?? '')
      out.push({ name: 'Speak page (presenter phone)', ok: !!speakUrl && (webTunnel || !!speakOverride), detail: speakUrl ? `${speakUrl.replace(/\?t=.*/, '')}${tunnel && !webTunnel && !speakOverride ? ` (tunnel goes to ${tunnel.addr}, should be 5173)` : ''} · scan the code on the right` : 'no ngrok tunnel: run "ngrok http 5173", or press Q' })
      out.push({ name: 'Token', ok: !TOKEN ? null : true, detail: TOKEN ? 'set (from ?t= of this page)' : 'none: open /pitch?t=<VOICE_DEMO_TOKEN> if the API uses one' })
      for (const u of externalUrls) {
        // Other origin: an opaque no-cors request only tells whether the service answers.
        const up = await fetch(u, { mode: 'no-cors', cache: 'no-store' }).then(() => true, () => false)
        out.push({ name: 'Audience demo', ok: up, detail: up ? u : `${u} not reachable: run scripts/Start-AudienceApertus.ps1 (optional segment)` })
      }
      out.push({ name: 'Map style', ok: mapOnline, detail: mapOnline === null ? 'map not loaded yet' : mapOnline ? 'online (streets)' : 'offline (bundled land)' })
      out.push({ name: 'Config', ok: true, detail: fromFile ? '/pitch/pitch.json' : 'built-in defaults' })
      for (const c of config.checks) {
        const r = await get(`/api/runs/${encodeURIComponent(c.run)}/houses`)
        const houses: { id: string; summary: Record<string, number | null> }[] = r ? await r.json() : []
        const raw = houses.find((h) => h.id === c.house)?.summary[c.field]
        const value = raw == null ? null : c.field === 'coverage' || c.field === 'utilisation' ? raw * 100 : raw
        const ok = value !== null && Math.abs(value - c.shown) <= c.tolerance
        out.push({ name: `Number ${c.run} ${c.field}`, ok: value === null ? false : ok, detail: value === null ? 'not available' : `shown ${c.shown}, run ${value.toFixed(3)}` })
      }
      if (!stop) setChecks(out)
    })()
    return () => { stop = true }
  }, [help, scenes, config.checks, live, speakUrl, speakOverride, mapOnline, fromFile, tunnel, externalUrls])

  const sendTyped = async () => {
    const text = typed.trim()
    if (!text) return
    try {
      const r = await fetch('/api/voice-demo/requests', { method: 'POST', body: text, headers: { 'Content-Type': 'text/plain', ...(TOKEN ? { 'X-Voice-Token': TOKEN } : {}) } })
      setSent(r.ok ? 'sent' : `failed (${r.status})`)
      if (r.ok) { setTyped(''); setTyping(false); setPos(([s]) => [s, stepsOf(scenes[s]).length - 1]) }
    } catch { setSent('failed') }
  }

  const strip = current.type === 'video'
    ? (current.strip && videoTime >= current.strip.fromS && videoTime < current.strip.toS ? current.strip.text : '')
    : currentStep.strip ?? ''
  const consoleLines = live?.voice?.console?.slice(-26) ?? []

  return (
    <div className="pitch">
      {pageUrls.map((url) => {
        const active = (current.type === 'page' && current.url === url) || (current.type === 'demo' && current.liveUrl === url)
        return (
          <iframe
            key={`${url}#${reloads[url] ?? 0}`}
            ref={(el) => { frames.current[url] = el }}
            title={url}
            src={withPitchParam(url)}
            className={`pitch-frame${active ? ' on' : ''}${current.type === 'demo' ? ' split' : ''}`}
            onLoad={() => attachKeys(url)}
            tabIndex={-1}
          />
        )
      })}

      {externalUrls.map((url) => (
        <iframe
          key={`${url}#${reloads[url] ?? 0}`}
          title={url}
          src={url}
          className={`pitch-frame${current.type === 'external' && current.url === url ? ' on' : ''}`}
          onLoad={() => window.focus()}
          tabIndex={-1}
        />
      ))}

      <video
        ref={video}
        className={`pitch-video${current.type === 'video' && !videoError ? ' on' : ''}`}
        src={videoSrc}
        preload="auto"
        playsInline
        onTimeUpdate={(e) => setVideoTime(e.currentTarget.currentTime)}
        onError={() => setVideoError(true)}
      />
      {current.type === 'video' && videoError && <div className="pitch-card on"><h1>Bob's house</h1><p>Basel · January 2025</p></div>}

      {current.type === 'qr' && (
        <div className="pitch-qr on">
          {qrImages[current.url] ? <img src={qrImages[current.url]} alt="QR code" /> : <div className="pitch-qr-missing">No QR code</div>}
          <p className="pitch-qr-text">{current.text}</p>
          <p className="pitch-qr-url">{current.url.replace(/^https?:\/\//, '').replace(/\/$/, '')}</p>
        </div>
      )}
      {current.type === 'demo' && (
        <div className="pitch-console">
          {consoleLines.length === 0 && <div className="pitch-console-line dim">waiting for a request …</div>}
          {consoleLines.map((l, i) => <div key={i} className={`pitch-console-line ${l.level}`}>{l.text}</div>)}
        </div>
      )}
      {current.type === 'slide' && <div className="pitch-card on"><h1>{current.text}</h1>{current.sub && <p>{current.sub}</p>}</div>}

      {strip && <div className="pitch-strip">{strip}</div>}
      {fallback && current.fallback && <video className="pitch-fallback" src={current.fallback} autoPlay controls={false} />}
      {fallback && !current.fallback && <div className="pitch-toast">No fallback recording for this scene</div>}
      {!started && <div className="pitch-start"><p>Press → to start · H for checks</p></div>}
      {black && <div className="pitch-black" />}

      {typing && (
        <div className="pitch-type">
          <input autoFocus value={typed} onChange={(e) => setTyped(e.target.value)} maxLength={500}
            onKeyDown={(e) => { if (e.key === 'Enter') void sendTyped(); if (e.key === 'Escape') setTyping(false) }}
            placeholder="Type the request, Enter to send (same path as the phone)" />
          {sent && <span>{sent}</span>}
        </div>
      )}

      {hud && (
        <div className="pitch-hud">
          {scene + 1}/{scenes.length} · {current.title}{stepsOf(current).length > 1 ? ` · ${currentStep.label ?? step + 1}` : ''}
        </div>
      )}

      {help && (
        <div className="pitch-help">
          <h2>Pitch checks</h2>
          {speakUrl && qrImages[speakUrl] && <img className="pitch-help-qr" src={qrImages[speakUrl]} alt="Speak page QR for the presenter's phone" />}
          <ul>{checks.map((c) => <li key={c.name} className={c.ok === null ? 'na' : c.ok ? 'ok' : 'bad'}><b>{c.name}</b> {c.detail}</li>)}</ul>
          <h2>Keys</h2>
          <p>→ Space PageDown next · ← PageUp back · 1–{Math.min(9, scenes.length)} scene · G globe · B or . black · F fullscreen ·
            V fallback recording · T type a request · Q speak page address · R reload page · H close</p>
          <p className="dim">Scenes: {scenes.map((s, i) => `${i + 1} ${s.title}`).join(' · ')}</p>
        </div>
      )}
    </div>
  )
}
