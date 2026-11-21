export const chf = new Intl.NumberFormat('de-CH', { style: 'currency', currency: 'CHF', maximumFractionDigits: 0 })
export const num = new Intl.NumberFormat('de-CH', { maximumFractionDigits: 0 })
export const dec1 = new Intl.NumberFormat('de-CH', { maximumFractionDigits: 1, minimumFractionDigits: 1 })
export const pct = new Intl.NumberFormat('de-CH', { style: 'percent', maximumFractionDigits: 0 })
export const pct1 = new Intl.NumberFormat('de-CH', { style: 'percent', maximumFractionDigits: 1 })
export const dateTime = new Intl.DateTimeFormat('de-CH', {
  timeZone: 'Europe/Zurich',
  weekday: 'short',
  day: '2-digit',
  month: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
})
export const dayTick = new Intl.DateTimeFormat('de-CH', { timeZone: 'Europe/Zurich', day: '2-digit', month: '2-digit' })
