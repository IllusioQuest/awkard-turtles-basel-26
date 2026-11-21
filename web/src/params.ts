import cantonMedians from '../../data/raw/elcom/canton-median-h4-2026.json'

// Shared sourced parameters for the dashboards.

// ElCom 2026, H4, cantonal median of the standard product (data/raw/elcom/SOURCE.md).
const RP_PER_CHF = 100
const ELECTRICITY_BY_BFS = new Map(
  cantonMedians.response.data.cantonMedianObservations.map((o) => [Number(o.canton), o.total]),
)

/** Electricity price in CHF/kWh for a canton (ElCom 2026, H4, cantonal median). */
export function cantonElectricityChfPerKwh(code: string): number | undefined {
  const bfs = CANTONS.find((c) => c.code === code)?.bfs
  const rpPerKwh = bfs === undefined ? undefined : ELECTRICITY_BY_BFS.get(bfs)
  return rpPerKwh === undefined ? undefined : rpPerKwh / RP_PER_CHF
}

// bfs = official BFS canton number, as used by ElCom.
export const CANTONS: { code: string; name: string; bfs: number }[] = [
  { code: 'AG', name: 'Aargau', bfs: 19 },
  { code: 'AI', name: 'Appenzell Innerrhoden', bfs: 16 },
  { code: 'AR', name: 'Appenzell Ausserrhoden', bfs: 15 },
  { code: 'BE', name: 'Bern', bfs: 2 },
  { code: 'BL', name: 'Basel-Landschaft', bfs: 13 },
  { code: 'BS', name: 'Basel-Stadt', bfs: 12 },
  { code: 'FR', name: 'Fribourg', bfs: 10 },
  { code: 'GE', name: 'Genève', bfs: 25 },
  { code: 'GL', name: 'Glarus', bfs: 8 },
  { code: 'GR', name: 'Graubünden', bfs: 18 },
  { code: 'JU', name: 'Jura', bfs: 26 },
  { code: 'LU', name: 'Luzern', bfs: 3 },
  { code: 'NE', name: 'Neuchâtel', bfs: 24 },
  { code: 'NW', name: 'Nidwalden', bfs: 7 },
  { code: 'OW', name: 'Obwalden', bfs: 6 },
  { code: 'SG', name: 'St. Gallen', bfs: 17 },
  { code: 'SH', name: 'Schaffhausen', bfs: 14 },
  { code: 'SO', name: 'Solothurn', bfs: 11 },
  { code: 'SZ', name: 'Schwyz', bfs: 5 },
  { code: 'TG', name: 'Thurgau', bfs: 20 },
  { code: 'TI', name: 'Ticino', bfs: 21 },
  { code: 'UR', name: 'Uri', bfs: 4 },
  { code: 'VD', name: 'Vaud', bfs: 22 },
  { code: 'VS', name: 'Valais', bfs: 23 },
  { code: 'ZG', name: 'Zug', bfs: 9 },
  { code: 'ZH', name: 'Zürich', bfs: 1 },
]
