import cantonMedians from '../../data/raw/elcom/canton-median-h4-2026.json'

// Central defaults for the client calculator. Every value has a unit and a source.
// The reference house of the case (docs/case/key-figures.md) is the default parameter set.

export const HOURS_PER_DAY = 24
export const DAYS_PER_YEAR = 365

export interface Param {
  value: number
  unit: string
  source: string
}

// Internal parameters (not shown to the client).
export const INTERNAL = {
  heatingDaysPerYear: {
    value: 206,
    unit: 'days/year',
    source: 'docs/case/challenge.md: 206 heating days in Basel 2025',
  },
  recoveredHeatKwPerServer: {
    value: 0.8,
    unit: 'kW/server',
    source: 'docs/case/challenge.md: 1 kW server, 80 % recoverable into the water system',
  },
  heatFeeChfPerKwh: {
    value: 0,
    unit: 'CHF/kWh',
    source: 'decision 0020: operator model, heat fee is an open parameter (default: free heat)',
  },
  connectionCostChfPerServer: {
    value: 500,
    unit: 'CHF/server',
    source: 'TODO(source): team guess for water block, heat exchanger share and plumbing, paid once by the host',
  },
} satisfies Record<string, Param>

// Reference household of the case: 4 persons, ≈ 27,000 kWh space heating, 12 kWh/day hot water.
export const REFERENCE_PERSONS = 4
export const REFERENCE_SPACE_HEATING_KWH_PER_YEAR: Param = {
  value: 27000,
  unit: 'kWh/year',
  source: 'docs/case/challenge.md: ≈ 27,000 kWh space heating per year (household of four)',
}
export const HOT_WATER_KWH_PER_PERSON_DAY: Param = {
  value: 3,
  unit: 'kWh/(person·day)',
  source: 'docs/case/challenge.md: 12 kWh/day for a household of four',
}

export interface Household {
  id: string
  label: string
  persons: number
}

// Space heating scales linearly with persons from the reference household.
// TODO(source): our assumption; real demand depends on floor area and insulation.
export const HOUSEHOLDS: Household[] = [
  { id: 'small', label: 'Small', persons: 2 },
  { id: 'medium', label: 'Medium', persons: REFERENCE_PERSONS },
  { id: 'large', label: 'Large', persons: 6 },
]
export const DEFAULT_HOUSEHOLD = 'medium'

export const SERVERS: Param & { min: number; max: number } = {
  value: 4,
  min: 1,
  max: 13,
  unit: 'servers',
  source:
    'Default: draft main variant (config/reference-house-results.json). Max: coldest-hour capacity, ceil(10 kW / 0.8 kW)',
}

export const HEAT_PRICE: Param & { min: number; max: number; step: number } = {
  value: 0.133,
  min: 0.05,
  max: 0.3,
  step: 0.001,
  unit: 'CHF/kWh',
  source:
    'docs/case/key-figures.md: electricity 0.3325 CHF/kWh (ElCom 2026, IWB Basel, H4) ÷ heat pump efficiency 2.5. Other cantons: cantonHeatPriceChfPerKwh. Slider range is a UI choice.',
}

export const YEARS_SHOWN = 15

export const DEFAULT_CANTON = 'BS'

export const HEAT_PUMP_EFFICIENCY: Param = {
  value: 2.5,
  unit: 'kWh heat/kWh electricity',
  source: 'docs/case/challenge.md: value of heat = electricity price ÷ 2.5',
}

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

/** Heat price a household pays today, CHF/kWh = electricity price ÷ heat pump efficiency (case rule). */
export function cantonHeatPriceChfPerKwh(code: string): number | undefined {
  const chfPerKwh = cantonElectricityChfPerKwh(code)
  if (chfPerKwh === undefined) return undefined
  return Math.round((chfPerKwh / HEAT_PUMP_EFFICIENCY.value) * 1000) / 1000
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
