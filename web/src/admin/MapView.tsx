import type { FeatureCollection } from 'geojson'
import { LngLatBounds, Map as MlMap, Marker, setWorkerUrl, type GeoJSONSource, type StyleSpecification } from 'maplibre-gl'
import 'maplibre-gl/dist/maplibre-gl.css'
import workerUrl from 'maplibre-gl/dist/maplibre-gl-worker.mjs?worker&url'
import { useEffect, useRef } from 'react'
import { WEATHER_BOUNDS, type Site, type SiteState, type WeatherField } from './mock'

// MapLibre loads its worker from a separate module; let Vite bundle it.
setWorkerUrl(workerUrl)

export type HeatLayer = 'none' | 'load' | 'capacity'

interface Props {
  sites: Site[]
  states: Map<string, SiteState>
  weather: WeatherField | null
  heatLayer: HeatLayer
  showWeather: boolean
  selectedId: string | null
  onSelect: (id: string | null) => void
}

const ONLINE_STYLE = 'https://basemaps.cartocdn.com/gl/dark-matter-gl-style/style.json'
const STYLE_TIMEOUT_MS = 3000
// Offline fallback: plain background, everything else is drawn by our own layers and HTML markers.
const OFFLINE_STYLE: StyleSpecification = {
  version: 8,
  sources: {},
  layers: [{ id: 'bg', type: 'background', paint: { 'background-color': '#111111' } }],
}

const CLUSTER_RADIUS_PX = 56
const DETAIL_ZOOM = 12
const SWITZERLAND_BOUNDS: [[number, number], [number, number]] = [
  [5.9, 45.8],
  [10.5, 47.85],
]

const ICONS: Record<Site['type'], string> = {
  home: '<path d="M4 11 12 4l8 7v9h-5v-6H9v6H4z"/>',
  hotel: '<path d="M3 19V6h2v8h14v-4a3 3 0 0 0-3-3h-6v6H8V9a2 2 0 1 0-1 0"/><path d="M3 19h18v-2H3z"/>',
  office: '<path d="M5 20V4h10v16H5zm2-13h2v2H7zm4 0h2v2h-2zm-4 4h2v2H7zm4 0h2v2h-2zm4-2h4v11h-4z"/>',
}

const icon = (type: Site['type']) =>
  `<svg viewBox="0 0 24 24" width="14" height="14" fill="currentColor">${ICONS[type]}</svg>`

const ring = (load: number, online: boolean) =>
  online
    ? `conic-gradient(var(--series-1) ${Math.round(load * 360)}deg, rgba(255,255,255,0.14) 0)`
    : 'rgba(255,255,255,0.14)'

async function resolveStyle(): Promise<string | StyleSpecification> {
  try {
    const ctrl = new AbortController()
    const timer = setTimeout(() => ctrl.abort(), STYLE_TIMEOUT_MS)
    const res = await fetch(ONLINE_STYLE, { signal: ctrl.signal })
    clearTimeout(timer)
    return res.ok ? ONLINE_STYLE : OFFLINE_STYLE
  } catch {
    return OFFLINE_STYLE
  }
}

function sitesGeoJson(sites: Site[], states: Map<string, SiteState>): FeatureCollection {
  return {
    type: 'FeatureCollection',
    features: sites.map((s) => {
      const st = states.get(s.id)
      const load = st?.load ?? 0
      return {
        type: 'Feature',
        geometry: { type: 'Point', coordinates: [s.lon, s.lat] },
        properties: {
          loadServers: st?.online ? load * s.servers : 0,
          freeServers: st?.online ? (1 - load) * s.servers : 0,
        },
      }
    }),
  }
}

// Relative diverging scale over the current temperature spread (5th percentile, median, 95th
// percentile): colder than the median = blue, warmer = orange, the median is a dark neutral.
const COLD = [57, 135, 229] // --series-1
const WARM = [217, 89, 38] // --series-2
const MIN_ALPHA = 0.05 // at the median temperature
const MAX_ALPHA = 0.2 //
const MID = [52, 52, 50] // dark neutral at the median, so the overlay never washes out white
const EDGE_FADE = 0.12 // share of width/height over which the overlay fades out at its edges

const smoothstep = (x: number) => {
  const c = Math.max(0, Math.min(1, x))
  return c * c * (3 - 2 * c)
}

// One canvas shared by the map's canvas source; redrawn in place whenever the time changes.
const weatherCanvas = document.createElement('canvas')

function drawWeather(field: WeatherField | null) {
  if (!field) return
  const canvas = weatherCanvas
  if (canvas.width !== field.cols) canvas.width = field.cols
  if (canvas.height !== field.rows) canvas.height = field.rows
  const ctx = canvas.getContext('2d')!
  const img = ctx.createImageData(field.cols, field.rows)
  const mid = field.medianC
  const coldSpan = Math.max(0.5, mid - field.lowC)
  const warmSpan = Math.max(0.5, field.highC - mid)
  field.temperatureC.forEach((temp, i) => {
    const x = (i % field.cols) / (field.cols - 1)
    const y = Math.floor(i / field.cols) / (field.rows - 1)
    const edge = smoothstep(Math.min(x, 1 - x) / EDGE_FADE) * smoothstep(Math.min(y, 1 - y) / EDGE_FADE)
    const d = Math.max(-1, Math.min(1, (temp - mid) / (temp < mid ? coldSpan : warmSpan)))
    const end = d < 0 ? COLD : WARM
    const k = Math.abs(d) ** 0.7
    img.data[i * 4] = MID[0] + (end[0] - MID[0]) * k
    img.data[i * 4 + 1] = MID[1] + (end[1] - MID[1]) * k
    img.data[i * 4 + 2] = MID[2] + (end[2] - MID[2]) * k
    img.data[i * 4 + 3] = Math.round(255 * (MIN_ALPHA + (MAX_ALPHA - MIN_ALPHA) * Math.sqrt(Math.abs(d))) * edge)
  })
  ctx.putImageData(img, 0, 0)
}

const { west, east, south, north } = WEATHER_BOUNDS
const WEATHER_COORDS: [[number, number], [number, number], [number, number], [number, number]] = [
  [west, north],
  [east, north],
  [east, south],
  [west, south],
]

function addLayers(map: MlMap) {
  // Our layers go below the first label layer of the base map, so place names stay on top.
  const firstLabel = map.getStyle().layers.find((l) => l.type === 'symbol')?.id
  map.addSource('sites', { type: 'geojson', data: { type: 'FeatureCollection', features: [] } })
  // animate: MapLibre re-reads the canvas every frame, so redrawing it is enough to update the map.
  map.addSource('weather', { type: 'canvas', canvas: weatherCanvas, coordinates: WEATHER_COORDS, animate: true })
  map.addLayer(
    {
      id: 'weather',
      type: 'raster',
      source: 'weather',
      layout: { visibility: 'none' },
      paint: { 'raster-resampling': 'linear', 'raster-fade-duration': 0, 'raster-opacity': 1 },
    },
    firstLabel,
  )
  const heatmap = (id: string, weight: string, color: string) =>
    map.addLayer(
      {
      id,
      type: 'heatmap',
      source: 'sites',
      layout: { visibility: 'none' },
      paint: {
        'heatmap-weight': ['/', ['get', weight], 6],
        'heatmap-intensity': ['interpolate', ['linear'], ['zoom'], 6, 0.8, 13, 2],
        'heatmap-radius': ['interpolate', ['linear'], ['zoom'], 6, 18, 10, 40, 14, 80],
        'heatmap-opacity': 0.75,
        'heatmap-color': [
          'interpolate',
          ['linear'],
          ['heatmap-density'],
          0,
          'rgba(0,0,0,0)',
          0.2,
          `${color}33`,
          0.6,
          `${color}aa`,
          1,
          color,
        ],
      },
    },
      firstLabel,
    )
  heatmap('heat-load', 'loadServers', '#3987e5')
  heatmap('heat-capacity', 'freeServers', '#199e70')
}

interface Cluster {
  sites: Site[]
  x: number
  y: number
}

function clusterSites(map: MlMap, sites: Site[]): Cluster[] {
  if (map.getZoom() >= DETAIL_ZOOM) {
    return sites.map((s) => {
      const p = map.project([s.lon, s.lat])
      return { sites: [s], x: p.x, y: p.y }
    })
  }
  const clusters: Cluster[] = []
  for (const s of sites) {
    const p = map.project([s.lon, s.lat])
    const hit = clusters.find((c) => Math.hypot(c.x - p.x, c.y - p.y) < CLUSTER_RADIUS_PX)
    if (hit) {
      hit.sites.push(s)
      const n = hit.sites.length
      hit.x += (p.x - hit.x) / n
      hit.y += (p.y - hit.y) / n
    } else clusters.push({ sites: [s], x: p.x, y: p.y })
  }
  return clusters
}

function renderMarkers(map: MlMap | null, props: Props, markers: { current: Marker[] }) {
    if (!map) return
    const { sites, states, selectedId, onSelect } = props
    markers.current.forEach((m) => m.remove())
    markers.current = clusterSites(map, sites).map((c) => {
      const el = document.createElement('div')
      const servers = c.sites.reduce((n, s) => n + s.servers, 0)
      const running = c.sites.reduce((n, s) => n + (states.get(s.id)?.serversRunning ?? 0), 0)
      const online = c.sites.some((s) => states.get(s.id)?.online)
      const load = servers > 0 ? running / servers : 0
      const pct = `${Math.round(load * 100)}%`
      if (c.sites.length > 1) {
        const size = Math.round(Math.min(64, 30 + Math.sqrt(servers) * 3))
        el.className = 'mk mk-cluster'
        el.style.width = el.style.height = `${size}px`
        el.style.background = ring(load, online)
        el.innerHTML = `<div><strong>${servers}</strong><span>${pct}</span></div>`
        el.title = `${c.sites.length} sites · ${servers} servers · ${pct} load`
        el.onclick = () => {
          const b = new LngLatBounds()
          c.sites.forEach((s) => b.extend([s.lon, s.lat]))
          map.fitBounds(b, { padding: 80, maxZoom: DETAIL_ZOOM + 1 })
        }
      } else {
        const s = c.sites[0]
        const detail = map.getZoom() >= DETAIL_ZOOM
        el.className = `mk mk-site${detail ? ' mk-detail' : ''}${s.id === selectedId ? ' selected' : ''}${online ? '' : ' offline'}`
        el.innerHTML = `<span class="mk-ring" style="background:${ring(load, online)}"><span class="mk-icon">${icon(s.type)}</span></span>${
          detail ? `<span class="mk-label">${s.servers} servers · ${online ? pct : 'offline'}</span>` : ''
        }`
        el.title = `${s.name} · ${s.servers} servers · ${online ? pct : 'offline'}`
        el.onclick = () => onSelect(s.id)
      }
      return new Marker({ element: el }).setLngLat(map.unproject([c.x, c.y])).addTo(map)
    })
  }

function syncLayers(map: MlMap | null, props: Props) {
    if (!map || !map.getSource('sites')) return
    const { sites, states, weather, heatLayer, showWeather } = props
    ;(map.getSource('sites') as GeoJSONSource).setData(sitesGeoJson(sites, states))
    if (showWeather) drawWeather(weather)
    map.setLayoutProperty('weather', 'visibility', showWeather ? 'visible' : 'none')
    map.setLayoutProperty('heat-load', 'visibility', heatLayer === 'load' ? 'visible' : 'none')
    map.setLayoutProperty('heat-capacity', 'visibility', heatLayer === 'capacity' ? 'visible' : 'none')
  }

export default function MapView(props: Props) {
  const container = useRef<HTMLDivElement>(null)
  const mapRef = useRef<MlMap | null>(null)
  const markers = useRef<Marker[]>([])
  const latest = useRef(props)

  useEffect(() => {
    latest.current = props
  })

  useEffect(() => {
    const markerList = markers
    let cancelled = false
    resolveStyle().then((style) => {
      if (cancelled || !container.current) return
      const map = new MlMap({
        container: container.current,
        style,
        bounds: SWITZERLAND_BOUNDS,
        fitBoundsOptions: { padding: 24 },
        attributionControl: { compact: true },
      })
      mapRef.current = map
      renderMarkers(map, latest.current, markers)
      map.on('load', () => {
        addLayers(map)
        syncLayers(map, latest.current)
      })
      map.on('moveend', () => renderMarkers(map, latest.current, markers))
      map.on('click', (e: { originalEvent: MouseEvent }) => {
        if ((e.originalEvent.target as HTMLElement).closest('.mk')) return
        latest.current.onSelect(null)
      })
    })
    return () => {
      cancelled = true
      markerList.current.forEach((m) => m.remove())
      mapRef.current?.remove()
      mapRef.current = null
    }
  }, [])

  useEffect(() => {
    syncLayers(mapRef.current, props)
    renderMarkers(mapRef.current, props, markers)
  })

  return <div ref={container} className="map" />
}
