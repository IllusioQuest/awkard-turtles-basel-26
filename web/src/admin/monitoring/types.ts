// Snake_case transport only. Heat/placement calculations remain in the existing C# monitor.
export interface MonitorInputs {
  schema_version: number
  mode: 'replay' | 'live'
  selected_hour_local: string | null
  initial_reservations: number
  observe_docker: boolean
  bypass_capabilities: boolean
  show_details: boolean
}
export interface Metric {
  value: number | null
  unit: string
  kind: string
  meaning: string
  source: string
}
export interface MonitorNode {
  node_id: string
  site_id: string
  observation_status: string
  container_running: boolean | null
  application_running: boolean | null
  observation_detail: string
  observed_at_utc: string | null
  cpu_percent: string | null
  memory_used_and_limit: string | null
  ready_for_new_claims: boolean
  readiness_reason: string
  planned_sessions: Metric
  session_slots: Metric
  planned_heat_kwh_per_hour: Metric
  resources_observed_at_utc?: string | null
  cpu_sample_interval_seconds?: number | null
  observation_source?: string
}
export interface MonitorSite {
  site_id: string
  name: string
  heat_needed_kwh: Metric
  planned_heat_kwh: Metric
  tank_stored_kwh: Metric
  tank_capacity_kwh: Metric
  heat_headroom_kwh: Metric
  wasted_heat_kwh: Metric
  backup_heat_kwh: Metric
  temperature_c: Metric
  electricity_price_chf_per_kwh: Metric
  heat_value_chf_per_kwh: Metric
  potential_heat_value_chf_per_hour: Metric
}
export interface MonitorSnapshot {
  schema_version: number
  inputs: MonitorInputs
  mode: string
  local_hour: string
  time_basis: string
  application_id: string
  generated_at_utc: string
  scenario_source: string
  session_heat_kwh_per_hour: Metric
  placed_reservations: number
  warnings: string[]
  nodes: MonitorNode[]
  sites: MonitorSite[]
}
export interface MonitorConfig {
  schema_version: number
  default_inputs: MonitorInputs
  available_hours: string[]
  winter_hour_local: string
  summer_hour_local: string
  max_reservations: number
  observation_cache_seconds: number
}
