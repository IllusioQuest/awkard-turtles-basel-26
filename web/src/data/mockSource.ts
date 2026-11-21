// Mock fallback: a DataSource over the deterministic mock model (admin/mock.ts), used only when the
// API is not reachable. One run, flagged `mock: true`; the UI shows a banner for it.

import { SITES, networkAt, siteState, temperatureC, type Site } from '../admin/mock'
import { WEATHER_BOUNDS } from '../admin/weather'
import type { DataSource, HouseInfo, HousePeriod, NetworkHour, RunInfo, WeatherPoint } from './types'

const MS_PER_HOUR = 3_600_000
const START_LOCAL = '2025-01-01T00:00'
const START_MS = Date.parse(`${START_LOCAL}Z`)
const HOURS = 8760
const MOCK_RUN_ID = 'mock'
const POINT_ROWS = 15
const POINT_COLS = 29 // 435 weather points like the real grid

const RUN: RunInfo = {
  id: MOCK_RUN_ID,
  scenario: 'mock',
  startLocal: START_LOCAL,
  hours: HOURS,
  periodHours: 1,
  houses: SITES.length,
  servers: SITES.reduce((n, s) => n + s.servers, 0),
  maxElectricKwPerServer: 1,
  heatRecoveryShare: 0.8,
  mock: true,
  note: '',
}

const at = (h: number) => START_MS + h * MS_PER_HOUR

function houseInfo(s: Site): HouseInfo {
  return {
    id: s.id,
    municipality: s.city,
    canton: s.canton,
    cellId: '',
    lat: s.lat,
    lon: s.lon,
    servers: s.servers,
    tankKwh: s.tankKwh,
    electricityPriceChfPerKwh: 0,
    summary: {
      heatDemandKwh: 0, heatUsedKwh: 0, heatDumpedKwh: 0, heatBackupKwh: 0, electricKwh: 0, coverage: 0, utilisation: 0,
      incomeChf: 0, electricityCostChf: 0, heatValueChf: 0, marginChf: 0, breakEvenChfPerServerHour: null, lateWorkKwh: 0, pauses: 0,
    },
  }
}

function period(s: Site, h: number): HousePeriod {
  const st = siteState(s, at(h))
  return {
    h,
    load: st.load,
    temperatureC: st.temperatureC,
    heatDemandKwh: st.heatDemandKw,
    heatProducedKwh: st.heatProducedKw,
    heatUsedKwh: st.heatUsedKw,
    heatDumpedKwh: st.heatDumpedKw,
    heatBackupKwh: Math.max(0, st.heatDemandKw - st.heatUsedKw),
    electricKwh: st.serversRunning,
    tankEndKwh: st.tankLevel * s.tankKwh,
    incomeChf: st.incomeChfPerHour,
    electricityCostChf: st.electricityChfPerHour,
    lateWorkKwh: st.missedPerHour,
    heatValueChf: 0,
    jobsOnTime: 0,
    deadlinesMissed: 0,
  }
}

function networkHour(h: number): NetworkHour {
  const p = networkAt(at(h), at(h))
  const misses = Math.round(p.missed)
  const arrivals = Math.round(p.jobs)
  return {
    h,
    heatDemandKwh: p.heatDemandKw,
    heatProducedKwh: p.heatProducedKw,
    heatUsedKwh: p.heatUsedKw,
    heatDumpedKwh: p.heatDumpedKw,
    heatBackupKwh: Math.max(0, p.heatDemandKw - p.heatUsedKw),
    electricKwh: p.serversRunning,
    load: p.load,
    incomeChf: p.incomeChf,
    electricityCostChf: p.electricityChf,
    arrivals,
    completions: arrivals - misses,
    rejections: 0,
    misses,
    queueT1: 0,
    queueT2: 0,
  }
}

const POINTS: WeatherPoint[] = Array.from({ length: POINT_ROWS * POINT_COLS }, (_, i) => {
  const { west, east, south, north } = WEATHER_BOUNDS
  const r = Math.floor(i / POINT_COLS)
  const c = i % POINT_COLS
  return {
    id: `m${i}`,
    lat: north - ((r + 0.5) / POINT_ROWS) * (north - south),
    lon: west + ((c + 0.5) / POINT_COLS) * (east - west),
  }
})

export class MockSource implements DataSource {
  private networkCache: NetworkHour[] | null = null
  private seriesCache = new Map<string, HousePeriod[]>()

  async runs() {
    return [RUN]
  }

  async houses() {
    return SITES.map(houseInfo)
  }

  async network() {
    this.networkCache ??= Array.from({ length: HOURS }, (_, h) => networkHour(h))
    return this.networkCache
  }

  async snapshots(_runId: string, fromH: number, toH: number) {
    const out = []
    for (let h = fromH; h < Math.min(toH, HOURS); h++) out.push({ h, periodStartH: h, periodHours: 1, houses: SITES.map((s) => period(s, h)) })
    return out
  }

  async houseSeries(_runId: string, houseId: string) {
    let series = this.seriesCache.get(houseId)
    if (!series) {
      const site = SITES.find((s) => s.id === houseId)
      if (!site) throw new Error(`unknown house ${houseId}`)
      series = Array.from({ length: HOURS }, (_, h) => period(site, h))
      this.seriesCache.set(houseId, series)
    }
    return series
  }

  async weatherPoints() {
    return POINTS
  }

  async weather(_runId: string, fromH: number, toH: number) {
    const temperatureCRows = []
    for (let h = fromH; h < Math.min(toH, HOURS); h++) temperatureCRows.push(POINTS.map((p) => temperatureC(p.lat, p.lon, at(h))))
    return { fromH, temperatureC: temperatureCRows }
  }
}
