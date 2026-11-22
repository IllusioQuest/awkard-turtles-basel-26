// Contract between the dashboard and its data sources: the fleet-results API
// (src/DigitalHeat.FleetApi, docs/model/fleet-api.md) and the mock fallback (admin/mock.ts).
// JSON field names are exactly these (camelCase). Units are in the names.
//
// Time: every series is indexed by the run hour h (0 = run start, `RunInfo.startLocal`, local
// Europe/Zurich wall time, 24 values per local day, DST days not shifted). The web converts h to
// "wall ms" = Date.UTC(start wall time) + h × 3,600,000 and formats it with timeZone 'UTC', so the
// labels show the wall time exactly.

export interface RunInfo {
  id: string // folder name under data/derived/fleet/
  scenario: string
  startLocal: string // "2025-01-01T00:00"
  hours: number // run length in hours
  periodHours: number // house timeseries granularity: 1 (hourly) or 24 (daily)
  houses: number
  servers: number
  maxElectricKwPerServer: number
  heatRecoveryShare: number
  mock: boolean // true only for the mock fallback; the UI shows a "mock data" banner
  note: string // scenario _note, may be empty
}

export interface HouseSummary {
  heatDemandKwh: number
  heatUsedKwh: number
  heatDumpedKwh: number
  heatBackupKwh: number
  electricKwh: number
  coverage: number // heat used ÷ demand
  utilisation: number // mean load over installed servers
  incomeChf: number
  electricityCostChf: number
  heatValueChf: number
  marginChf: number
  breakEvenChfPerServerHour: number | null
  lateWorkKwh: number
  pauses: number
}

export interface HouseInfo {
  id: string // house_id
  municipality: string // from data/raw/swiss-grid/grid-10km.csv via cellId (may be "")
  canton: string
  cellId: string
  lat: number
  lon: number
  servers: number
  tankKwh: number
  electricityPriceChfPerKwh: number
  summary: HouseSummary
  siteType: string // display category from the house file ("house" if not set, "pool", ...)
}

/** One hour of the whole network (network-hourly.csv). Energies are kWh in that hour. */
export interface NetworkHour {
  h: number
  heatDemandKwh: number
  heatProducedKwh: number
  heatUsedKwh: number // demand − backup
  heatDumpedKwh: number
  heatBackupKwh: number
  electricKwh: number
  load: number // electricKwh ÷ (all servers × max kW × 1 h)
  incomeChf: number
  electricityCostChf: number
  arrivals: number
  completions: number
  rejections: number
  misses: number // deadlines passed while the job was not done, in this hour
  queueT1: number
  queueT2: number
}

/** One house in one timeseries period [h, h + periodHours). kW = kWh ÷ period hours. */
export interface HousePeriod {
  h: number
  load: number // electricKwh ÷ (servers × max kW × period hours)
  temperatureC: number // mean outdoor temperature of the house's weather cell over the period
  heatDemandKwh: number
  heatProducedKwh: number
  heatUsedKwh: number
  heatDumpedKwh: number
  heatBackupKwh: number
  electricKwh: number
  tankEndKwh: number
  incomeChf: number
  electricityCostChf: number
  lateWorkKwh: number
  heatValueChf: number // heat used × house electricity price ÷ heat-pump efficiency (case §10)
  // Jobs attributed to this house (the house that last ran a chunk of the job; never-started jobs count nowhere):
  jobsOnTime: number // completed by their deadline, counted in the period of completion
  deadlinesMissed: number // deadline passed while not done, counted in the period of the deadline
}

/** Map snapshot: every house's period containing hour h. houses[i] belongs to HouseInfo[i] of /houses. */
export interface Snapshot {
  h: number // requested hour
  periodStartH: number
  periodHours: number
  houses: HousePeriod[]
}

export interface WeatherPoint {
  id: string // grid point_id
  lat: number
  lon: number
}

/** Temperatures at the weather points for consecutive run hours [fromH, fromH + temperatureC.length). */
export interface WeatherHours {
  fromH: number
  temperatureC: number[][] // [hour][point], point order of /api/weather/points
}

export interface JobEvent {
  timeMs: number
  jobId: string
  jobType: string
  chunkId: number | null
  kind: 'started' | 'paused' | 'resumed' | 'stopped' | 'completed'
}

export interface JobEventLog {
  available: boolean
  truncated: boolean
  events: JobEvent[]
}

/** What the pages read; implemented by the API client and by the mock fallback. */
export interface DataSource {
  runs(): Promise<RunInfo[]>
  houses(runId: string): Promise<HouseInfo[]>
  network(runId: string): Promise<NetworkHour[]>
  snapshots(runId: string, fromH: number, toH: number): Promise<Snapshot[]> // one per hour in [fromH, toH)
  houseSeries(runId: string, houseId: string): Promise<HousePeriod[]>
  jobEvents(runId: string, houseId: string, fromMs: number, toMs: number): Promise<JobEventLog>
  weatherPoints(): Promise<WeatherPoint[]>
  weather(runId: string, fromH: number, toH: number): Promise<WeatherHours>
}
