// Pitch mode configuration (decision 0069). Loaded from /pitch/pitch.json (web/public/pitch/), with
// these built-in defaults when the file is missing or invalid. Scene order, runs, hours and texts are
// all here, so the pitch can change without code.

export interface NumberCheck {
  /** Fleet run and house whose summary must match the number shown in a strip. */
  run: string
  house: string
  field: 'coverage' | 'utilisation' | 'heatDumpedKwh' | 'breakEvenChfPerServerHour'
  /** Value as shown on screen (percent for coverage and utilisation, CHF, kWh). */
  shown: number
  /** Allowed difference in the shown unit (rounding on screen). */
  tolerance: number
}

export interface Step {
  label?: string
  /** Map scenes: run hour to show (hours since the run start, 2025-01-01 00:00). */
  h?: number
  /** Map scenes: zoom out to the turning globe. */
  globe?: boolean
  /** Text strip at the bottom of the screen. */
  strip?: string
}

interface SceneBase {
  id: string
  title: string
  /** Optional screen recording played instead of the scene (key V). */
  fallback?: string
}

export type Scene =
  | (SceneBase & { type: 'video'; src: string; strip?: { text: string; fromS: number; toS: number } })
  | (SceneBase & { type: 'page'; url: string; steps?: Step[] })
  | (SceneBase & { type: 'demo'; liveUrl: string; steps: Step[] })
  | (SceneBase & { type: 'slide'; text: string; sub?: string })
  /** A large QR code for the audience, with a caption; the address is shown under it. */
  | (SceneBase & { type: 'qr'; url: string; text: string })
  /** A page of another local service (other origin), e.g. the audience demo's operator view. */
  | (SceneBase & { type: 'external'; url: string })

export interface PitchConfig {
  scenes: Scene[]
  checks: NumberCheck[]
}

const JAN_15_NOON = 14 * 24 + 12
const JUL_15_NOON = 195 * 24 + 12

export const DEFAULT_CONFIG: PitchConfig = {
  scenes: [
    {
      id: 'film', type: 'video', title: 'Intro film', src: '/pitch/intro.mp4',
      strip: {
        text: "Bob's house · 2025 · one server: 18.9 % of heat covered · 0 kWh dumped · 84 % utilisation · 5.9 % late jobs · break-even CHF 0.23 per server-hour",
        fromS: 0, toS: 7,
      },
    },
    { id: 'key', type: 'page', title: 'Key figures, one server', url: '/admin/reference-1-server/houses/reference' },
    { id: 'real', type: 'page', title: 'Real house', url: '/admin/real-house-1-server/houses/real-house' },
    {
      id: 'map', type: 'page', title: 'Map: January vs July', url: '/admin/dreispitz-5-pool-1-server?fit=1&layer=load',
      steps: [{ label: '15 January', h: JAN_15_NOON }, { label: '15 July', h: JUL_15_NOON }],
    },
    {
      id: 'money', type: 'page', title: 'Does one server pay?', url: '/admin/reference-1-server/houses/reference',
      steps: [
        { label: 'money' },
        { label: 'split', strip: 'One server: +CHF 5,252 a year after hardware · payback 3.4 years for DigitalHeat · Bob: CHF 784 of free heat' },
      ],
    },
    {
      id: 'globe', type: 'page', title: 'Globe', url: '/admin/dreispitz-5-pool-1-server?fit=1&layer=load',
      steps: [{ label: 'globe', globe: true }],
    },
    // The presenter speaks into their own phone (/speak through the ngrok tunnel); no QR on screen.
    { id: 'demo', type: 'demo', title: 'Live demo', liveUrl: '/live', steps: [{ label: 'live' }] },
    { id: 'close', type: 'slide', title: 'Close', text: 'Let the house decide.' },
    // After the pitch, optional: the audience demo (decision 0074, DigitalHeat.AudienceDemo). It runs on
    // another laptop, which shows its own operator view; add an 'external' scene here if it runs on this one.
    { id: 'audience-qr', type: 'qr', title: 'Audience: QR', url: 'https://swiss-heat.lovable.app', text: 'Ask Apertus a question' },
  ],
  checks: [
    { run: 'reference-1-server', house: 'reference', field: 'coverage', shown: 18.9, tolerance: 0.05 },
    { run: 'reference-1-server', house: 'reference', field: 'utilisation', shown: 84, tolerance: 0.5 },
    { run: 'reference-1-server', house: 'reference', field: 'heatDumpedKwh', shown: 0, tolerance: 0.5 },
    { run: 'reference-1-server', house: 'reference', field: 'breakEvenChfPerServerHour', shown: 0.23, tolerance: 0.005 },
  ],
}

function valid(c: unknown): c is PitchConfig {
  const x = c as PitchConfig
  return !!x && Array.isArray(x.scenes) && x.scenes.length > 0 && x.scenes.every((s) => typeof s?.id === 'string' && typeof s?.type === 'string')
}

export async function loadConfig(): Promise<{ config: PitchConfig; fromFile: boolean }> {
  try {
    const res = await fetch('/pitch/pitch.json', { cache: 'no-store' })
    if (res.ok) {
      const c = await res.json()
      if (valid(c)) return { config: { ...c, checks: c.checks ?? [] }, fromFile: true }
    }
  } catch { /* use defaults */ }
  return { config: DEFAULT_CONFIG, fromFile: false }
}
