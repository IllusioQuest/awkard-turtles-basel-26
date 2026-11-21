// DataSource backed by the fleet-results API (docs/model/fleet-api.md), with in-memory caching.
// Snapshots and weather are fetched in day-aligned blocks so that playing the timeline is smooth.

import type { DataSource, HouseInfo, HousePeriod, NetworkHour, RunInfo, Snapshot, WeatherHours, WeatherPoint } from './types'

const HOURLY_BLOCK_H = 48
const DAILY_BLOCK_H = 336 // 14 days, below the API maximum of 744 hours per request

async function getJson<T>(url: string): Promise<T> {
  const res = await fetch(url)
  if (!res.ok) throw new Error(`${url}: ${res.status} ${(await res.text().catch(() => '')).slice(0, 200)}`)
  return (await res.json()) as T
}

export class ApiSource implements DataSource {
  private cache = new Map<string, Promise<unknown>>()

  private memo<T>(key: string, load: () => Promise<T>): Promise<T> {
    let p = this.cache.get(key) as Promise<T> | undefined
    if (!p) {
      p = load()
      this.cache.set(key, p)
      p.catch(() => this.cache.delete(key))
    }
    return p
  }

  runs() {
    return this.memo('runs', () => getJson<RunInfo[]>('/api/runs'))
  }

  private async run(runId: string) {
    const run = (await this.runs()).find((r) => r.id === runId)
    if (!run) throw new Error(`unknown run ${runId}`)
    return run
  }

  houses(runId: string) {
    return this.memo(`houses:${runId}`, () => getJson<HouseInfo[]>(`/api/runs/${encodeURIComponent(runId)}/houses`))
  }

  network(runId: string) {
    return this.memo(`network:${runId}`, () => getJson<NetworkHour[]>(`/api/runs/${encodeURIComponent(runId)}/network`))
  }

  houseSeries(runId: string, houseId: string) {
    return this.memo(`series:${runId}:${houseId}`, () =>
      getJson<HousePeriod[]>(`/api/runs/${encodeURIComponent(runId)}/houses/${encodeURIComponent(houseId)}/series`),
    )
  }

  weatherPoints() {
    return this.memo('weather-points', () => getJson<WeatherPoint[]>('/api/weather/points'))
  }

  /** Rows for the hours [fromH, toH), taken from cached blocks; the block after the last one is prefetched. */
  private async rows<T>(key: string, run: RunInfo, fromH: number, toH: number, fetchBlock: (f: number, t: number) => Promise<T[]>) {
    const size = run.periodHours === 24 ? DAILY_BLOCK_H : HOURLY_BLOCK_H
    const to = Math.min(toH, run.hours)
    const block = (b: number) => this.memo(`${key}:${b}`, () => fetchBlock(b * size, Math.min(run.hours, (b + 1) * size)))
    const first = Math.floor(fromH / size)
    const last = Math.floor((to - 1) / size)
    const out: T[] = []
    for (let b = first; b <= last; b++) {
      const rows = await block(b)
      for (let h = Math.max(fromH, b * size); h < Math.min(to, (b + 1) * size); h++) out.push(rows[h - b * size])
    }
    if ((last + 1) * size < run.hours) block(last + 1).catch(() => undefined)
    return out
  }

  async snapshots(runId: string, fromH: number, toH: number) {
    const run = await this.run(runId)
    return this.rows<Snapshot>(`snap:${runId}`, run, fromH, toH, (f, t) =>
      getJson<Snapshot[]>(`/api/runs/${encodeURIComponent(runId)}/snapshots?from=${f}&to=${t}`),
    )
  }

  async weather(runId: string, fromH: number, toH: number): Promise<WeatherHours> {
    const run = await this.run(runId)
    const temperatureC = await this.rows<number[]>(`weather:${runId}`, run, fromH, toH, async (f, t) =>
      (await getJson<WeatherHours>(`/api/runs/${encodeURIComponent(runId)}/weather?from=${f}&to=${t}`)).temperatureC,
    )
    return { fromH, temperatureC }
  }
}
