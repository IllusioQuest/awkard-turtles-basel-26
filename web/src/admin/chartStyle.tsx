// Shared Recharts styling for all dashboard pages.

export const axis = { stroke: 'var(--muted)', tickLine: false, fontSize: 12 }
export const tooltip = {
  contentStyle: { background: 'var(--surface-2)', border: 'none', borderRadius: 8 },
  labelStyle: { color: 'var(--ink-2)' },
  itemStyle: { color: 'var(--ink)' },
  cursor: { stroke: 'var(--axis)' },
}
export const legend = {
  wrapperStyle: { paddingTop: 8 },
  formatter: (name: string) => <span style={{ color: 'var(--ink-2)' }}>{name}</span>,
}
