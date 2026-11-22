import { useEffect, useRef, useState } from 'react'
import './speak.css'

// T-78 phone page: one button records a spoken request and posts it to the demo inbox
// (FleetApi POST /api/voice-demo/requests). The scheduling itself happens in DigitalHeat.VoiceDemo.
type Live = { status: string; processStatus: string; voice?: { stage: string; transcript: string; answer: string } }
type Phase = 'idle' | 'recording' | 'sending' | 'sent' | 'error'

const token = new URLSearchParams(window.location.search).get('t') ?? ''
const preferredTypes = ['audio/webm;codecs=opus', 'audio/webm', 'audio/mp4', 'audio/ogg']

async function send(body: Blob | string, type: string) {
  const res = await fetch('/api/voice-demo/requests', {
    method: 'POST', body, headers: { 'Content-Type': type, ...(token ? { 'X-Voice-Token': token } : {}) },
  })
  if (!res.ok) throw new Error(`Upload failed (${res.status})`)
}

export default function SpeakPage() {
  const [phase, setPhase] = useState<Phase>('idle')
  const [error, setError] = useState('')
  const [text, setText] = useState('')
  const [live, setLive] = useState<Live | null>(null)
  const recorder = useRef<MediaRecorder | null>(null)
  const chunks = useRef<Blob[]>([])

  useEffect(() => {
    if (phase !== 'sent') return
    const timer = window.setInterval(async () => {
      try { const res = await fetch('/api/live-household', { cache: 'no-store' }); if (res.ok) setLive(await res.json()) } catch { /* keep last */ }
    }, 1000)
    return () => window.clearInterval(timer)
  }, [phase])

  async function start() {
    setError('')
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: { channelCount: 1, echoCancellation: true, noiseSuppression: true } })
      const mimeType = preferredTypes.find((t) => MediaRecorder.isTypeSupported(t))
      const r = new MediaRecorder(stream, mimeType ? { mimeType } : undefined)
      chunks.current = []
      r.ondataavailable = (e) => { if (e.data.size > 0) chunks.current.push(e.data) }
      r.onstop = async () => {
        stream.getTracks().forEach((t) => t.stop())
        const type = (r.mimeType || 'audio/webm').split(';')[0]
        setPhase('sending')
        try { await send(new Blob(chunks.current, { type }), type); setLive(null); setPhase('sent') }
        catch (e) { setError(String((e as Error).message)); setPhase('error') }
      }
      recorder.current = r
      r.start()
      setPhase('recording')
    } catch {
      setError('Microphone not available. Open this page over https (ngrok) or on localhost, and allow the microphone.')
      setPhase('error')
    }
  }

  function stop() { recorder.current?.stop() }

  async function sendText() {
    if (!text.trim()) return
    setPhase('sending')
    try { await send(text.trim(), 'text/plain'); setText(''); setLive(null); setPhase('sent') }
    catch (e) { setError(String((e as Error).message)); setPhase('error') }
  }

  const recording = phase === 'recording'
  const status = live?.voice?.stage === 'done' ? 'Answered'
    : live?.processStatus === 'paused' ? 'Paused — waiting for useful heat'
    : live?.processStatus === 'running' ? (live.voice?.stage === 'answering' ? 'Answering …' : 'Transcribing …')
    : 'Job scheduled'
  return <main className="speak">
    <h1>DigitalHeat</h1>
    <p className="speak-sub">Ask the server in the basement.</p>
    <button className={`speak-button${recording ? ' recording' : ''}`} disabled={phase === 'sending'}
      onClick={recording ? stop : start} aria-pressed={recording}>
      {recording ? 'Stop & send' : phase === 'sending' ? 'Sending …' : 'Speak'}
    </button>
    {phase === 'sent' && <section className="speak-status">
      <p className="speak-state">{status}</p>
      {live?.voice?.transcript && <p className="speak-transcript">“{live.voice.transcript}”</p>}
      {live?.voice?.answer && <p className="speak-answer">{live.voice.answer}</p>}
    </section>}
    {error && <p className="speak-error">{error}</p>}
    <details className="speak-text">
      <summary>Type instead</summary>
      <textarea value={text} onChange={(e) => setText(e.target.value)} rows={3} maxLength={500} placeholder="Your request" />
      <button onClick={sendText} disabled={phase === 'sending' || !text.trim()}>Send</button>
    </details>
  </main>
}
