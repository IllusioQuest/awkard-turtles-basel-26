import { useEffect, useRef, useState } from 'react'
import { DayPicker, type DateRange } from 'react-day-picker'
import 'react-day-picker/style.css'

export interface TimeRange {
  from: number
  to: number
  preset: string | null
}

export interface Preset {
  id: string
  label: string
  range: (now: number) => { from: number; to: number }
}

interface Props {
  value: TimeRange
  onChange: (range: TimeRange) => void
  presets: Preset[]
  now: number
  min: number
  max: number
}

const DAY_MS = 86_400_000
const dateFmt = new Intl.DateTimeFormat('de-CH', { timeZone: 'UTC', day: '2-digit', month: '2-digit', year: 'numeric' })

// The picker works in wall ms (local wall time read as UTC); DayPicker works with local dates.
const startOfDay = (d: Date) => Date.UTC(d.getFullYear(), d.getMonth(), d.getDate())
const toLocalDate = (ms: number) => {
  const d = new Date(ms)
  return new Date(d.getUTCFullYear(), d.getUTCMonth(), d.getUTCDate())
}

export default function DateRangePicker({ value, onChange, presets, now, min, max }: Props) {
  const [open, setOpen] = useState(false)
  const [draft, setDraft] = useState<DateRange | undefined>()
  const root = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!open) return
    const close = (e: MouseEvent) => {
      if (!root.current?.contains(e.target as Node)) setOpen(false)
    }
    const esc = (e: KeyboardEvent) => e.key === 'Escape' && setOpen(false)
    document.addEventListener('mousedown', close)
    document.addEventListener('keydown', esc)
    return () => {
      document.removeEventListener('mousedown', close)
      document.removeEventListener('keydown', esc)
    }
  }, [open])

  const preset = presets.find((p) => p.id === value.preset)
  const label = preset ? preset.label : `${dateFmt.format(value.from)} – ${dateFmt.format(value.to - 1)}`
  const selected: DateRange = draft ?? { from: toLocalDate(value.from), to: toLocalDate(value.to - 1) }

  const pick = (range: DateRange | undefined) => {
    setDraft(range)
    if (range?.from && range.to) {
      onChange({ from: startOfDay(range.from), to: startOfDay(range.to) + DAY_MS, preset: null })
    }
  }

  return (
    <div className="drp" ref={root}>
      <button
        type="button"
        className="drp-trigger"
        onClick={() => {
          setDraft(undefined)
          setOpen((o) => !o)
        }}
        aria-expanded={open}
      >
        <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2">
          <rect x="3" y="5" width="18" height="16" rx="2" />
          <path d="M3 10h18M8 3v4M16 3v4" />
        </svg>
        {label}
      </button>

      {open && (
        <div className="drp-popover">
          <div className="drp-presets">
            {presets.map((p) => (
              <button
                key={p.id}
                type="button"
                className={p.id === value.preset ? 'active' : ''}
                onClick={() => {
                  onChange({ ...p.range(now), preset: p.id })
                  setOpen(false)
                }}
              >
                {p.label}
              </button>
            ))}
          </div>
          <DayPicker
            mode="range"
            numberOfMonths={2}
            weekStartsOn={1}
            selected={selected}
            onSelect={pick}
            defaultMonth={toLocalDate(Math.max(min, value.to - DAY_MS * 31))}
            startMonth={toLocalDate(min)}
            endMonth={toLocalDate(max)}
            disabled={[{ before: toLocalDate(min) }, { after: toLocalDate(max - 1) }]}
          />
        </div>
      )}
    </div>
  )
}
