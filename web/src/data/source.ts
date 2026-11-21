// Picks the data source once at start: the API when /api/runs answers, otherwise the mock fallback.

import { useEffect, useRef, useState } from 'react'
import { ApiSource } from './api'
import { MockSource } from './mockSource'
import type { DataSource } from './types'

let sourcePromise: Promise<DataSource> | null = null

export function getSource(): Promise<DataSource> {
  sourcePromise ??= (async () => {
    const api = new ApiSource()
    try {
      await api.runs()
      return api
    } catch {
      return new MockSource()
    }
  })()
  return sourcePromise
}

export function useSource(): DataSource | null {
  const [source, setSource] = useState<DataSource | null>(null)
  useEffect(() => {
    let cancelled = false
    getSource().then((s) => !cancelled && setSource(s))
    return () => {
      cancelled = true
    }
  }, [])
  return source
}

interface AsyncState<T> {
  key: string | null
  data: T | undefined
  error: Error | undefined
}

/**
 * Runs `load` whenever `key` changes (null = idle). `data` keeps the last result while the next one
 * loads, so playing the timeline does not flicker; `loading` says whether it belongs to an older key.
 */
export function useAsync<T>(key: string | null, load: () => Promise<T>) {
  const [state, setState] = useState<AsyncState<T>>({ key: null, data: undefined, error: undefined })
  const loadRef = useRef(load)
  useEffect(() => {
    loadRef.current = load
  })
  useEffect(() => {
    if (key === null) return
    let cancelled = false
    loadRef.current().then(
      (data) => !cancelled && setState({ key, data, error: undefined }),
      (e: unknown) => !cancelled && setState((s) => ({ key, data: s.data, error: e instanceof Error ? e : new Error(String(e)) })),
    )
    return () => {
      cancelled = true
    }
  }, [key])
  return { data: state.data, error: state.key === key ? state.error : undefined, loading: key !== null && state.key !== key }
}
