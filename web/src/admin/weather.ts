// Temperature field for the map overlay, interpolated from the temperatures at the weather points.

import type { WeatherPoint } from '../data/types'

export const WEATHER_BOUNDS = { west: 5.8, east: 10.6, south: 45.75, north: 47.9 }
const WEATHER_COLS = 160
const WEATHER_ROWS = 96
const NEIGHBOURS = 8
const IDW_POWER = 2
const MIN_DIST_DEG = 1e-4 // avoids a division by zero when a cell centre sits on a point
const LON_SCALE = Math.cos((46.8 * Math.PI) / 180) // degrees of longitude are shorter than degrees of latitude

export interface WeatherField {
  cols: number
  rows: number
  temperatureC: Float32Array // row-major, north row first
  lowC: number // 5th percentile
  medianC: number
  highC: number // 95th percentile
}

export interface WeatherInterpolator {
  fill(temperatureC: number[]): WeatherField
}

/** Inverse-distance weighting (power 2) over the 8 nearest points; indices and weights are precomputed per cell. */
export function makeInterpolator(points: WeatherPoint[]): WeatherInterpolator {
  const { west, east, south, north } = WEATHER_BOUNDS
  const cells = WEATHER_COLS * WEATHER_ROWS
  const k = Math.min(NEIGHBOURS, points.length)
  const index = new Int32Array(cells * k)
  const weight = new Float32Array(cells * k)
  const bestD = new Float64Array(k)
  const bestP = new Int32Array(k)
  for (let r = 0; r < WEATHER_ROWS; r++) {
    const lat = north - ((r + 0.5) / WEATHER_ROWS) * (north - south)
    for (let c = 0; c < WEATHER_COLS; c++) {
      const lon = west + ((c + 0.5) / WEATHER_COLS) * (east - west)
      // Keep the k nearest points by insertion into a short sorted list.
      bestD.fill(Infinity)
      for (let p = 0; p < points.length; p++) {
        const d = Math.hypot(points[p].lat - lat, (points[p].lon - lon) * LON_SCALE)
        if (d >= bestD[k - 1]) continue
        let i = k - 1
        while (i > 0 && bestD[i - 1] > d) {
          bestD[i] = bestD[i - 1]
          bestP[i] = bestP[i - 1]
          i--
        }
        bestD[i] = d
        bestP[i] = p
      }
      const cell = r * WEATHER_COLS + c
      let sum = 0
      for (let i = 0; i < k; i++) {
        const w = 1 / Math.max(MIN_DIST_DEG, bestD[i]) ** IDW_POWER
        index[cell * k + i] = bestP[i]
        weight[cell * k + i] = w
        sum += w
      }
      for (let i = 0; i < k; i++) weight[cell * k + i] /= sum
    }
  }
  return {
    fill(temperatureC) {
      const field = new Float32Array(cells)
      for (let cell = 0; cell < cells; cell++) {
        let t = 0
        for (let i = 0; i < k; i++) t += weight[cell * k + i] * temperatureC[index[cell * k + i]]
        field[cell] = t
      }
      const sorted = Float32Array.from(field).sort()
      return {
        cols: WEATHER_COLS,
        rows: WEATHER_ROWS,
        temperatureC: field,
        lowC: sorted[Math.floor(sorted.length * 0.05)],
        medianC: sorted[Math.floor(sorted.length * 0.5)],
        highC: sorted[Math.floor(sorted.length * 0.95)],
      }
    },
  }
}
