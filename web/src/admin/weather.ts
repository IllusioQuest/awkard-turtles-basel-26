// Temperature field for the map overlay, interpolated from the temperatures at the weather points.

import type { WeatherPoint } from '../data/types'

export const WEATHER_BOUNDS = { west: 5.8, east: 10.6, south: 45.75, north: 47.9 }
const WEATHER_COLS = 160
const WEATHER_ROWS = 96
const NEIGHBOURS = 8
const IDW_POWER = 2
const MIN_DIST_DEG = 1e-4 // avoids a division by zero when a cell centre sits on a point
const LON_SCALE = Math.cos((46.8 * Math.PI) / 180) // degrees of longitude are shorter than degrees of latitude

/**
 * Fixed temperature colour scale for the map overlay and its legend. The ends do not move with the
 * data: 2025 grid temperatures span −32 to +37.5 °C (data/raw/swiss-grid/weather-2025-hourly.csv.gz),
 * only 0.7 % of point-hours lie below −15 °C, so values outside [−20, 35] °C are clamped to the ends.
 */
export const TEMP_SCALE_MIN_C = -20
export const TEMP_SCALE_MAX_C = 35
export const TEMP_STOPS: { c: number; rgb: [number, number, number] }[] = [
  { c: -20, rgb: [59, 47, 158] }, // indigo
  { c: -10, rgb: [44, 123, 214] }, // blue
  { c: 0, rgb: [43, 183, 201] }, // cyan
  { c: 8, rgb: [76, 191, 107] }, // green
  { c: 16, rgb: [227, 211, 74] }, // yellow
  { c: 25, rgb: [240, 138, 44] }, // orange
  { c: 35, rgb: [214, 51, 42] }, // red
]
export const TEMP_LEGEND_TICKS_C = [-20, -10, 0, 10, 20, 35]

/** Colour of a temperature on the fixed scale (linear between stops, clamped at the ends). */
export function temperatureRgb(tempC: number): [number, number, number] {
  const t = Math.max(TEMP_SCALE_MIN_C, Math.min(TEMP_SCALE_MAX_C, tempC))
  for (let i = 1; i < TEMP_STOPS.length; i++) {
    const a = TEMP_STOPS[i - 1]
    const b = TEMP_STOPS[i]
    if (t <= b.c) {
      const f = (t - a.c) / (b.c - a.c)
      return [a.rgb[0] + (b.rgb[0] - a.rgb[0]) * f, a.rgb[1] + (b.rgb[1] - a.rgb[1]) * f, a.rgb[2] + (b.rgb[2] - a.rgb[2]) * f]
    }
  }
  return TEMP_STOPS[TEMP_STOPS.length - 1].rgb
}

/** CSS gradient of the same scale, for the legend bar. */
export function temperatureGradientCss(): string {
  const span = TEMP_SCALE_MAX_C - TEMP_SCALE_MIN_C
  const stops = TEMP_STOPS.map((s) => `rgb(${s.rgb.join(',')}) ${((s.c - TEMP_SCALE_MIN_C) / span) * 100}%`)
  return `linear-gradient(90deg, ${stops.join(', ')})`
}

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
