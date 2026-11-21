import caseExport from '../../../data/derived/reference-house-v1/summary.json';
import runExport from '../../../data/derived/reference-house-v1/run.json';
import priceExport from '../../../data/derived/computing-price.json';
import referenceHouse from '../../../config/reference-house.json';
import explorerDefault from '../../../data/derived/reference-house/explorer-default.json';
import threeServers from '../../../data/derived/reference-house/three-server-comparison.json';
import fiveServers from '../../../data/derived/reference-house/five-server-comparison.json';
import summerMinimum from '../../../data/derived/reference-house/summer-hot-water-minimum.json';
import coldestHour from '../../../data/derived/reference-house/coldest-hour-capacity.json';
import hourlyCsv from '../../../data/derived/reference-house-v1/sites/reference-house/hourly.csv?raw';

export interface Parameter {
  value: number;
  unit: string;
  source: string;
}

export interface CaseSummary {
  server_count: number;
  heating_days: number;
  hours: number;
  total_heat_demand_kwh: number;
  server_heat_used_kwh: number;
  backup_heat_kwh: number;
  dumped_heat_kwh: number;
  server_hours: number;
  utilisation: number;
  coverage: number;
  electricity_cost_chf: number;
  heat_value_chf: number;
  computing_income_chf: number;
  break_even_chf_per_server_hour: number;
  missed_deadlines: string;
  job_demand_assumption: string;
  profitability_note: string;
}

export interface RunMetadata {
  schema_version: number;
  timezone: string;
  time_convention: string;
  step_hours: number;
  git_commit: string;
  git_dirty: boolean;
  code_provenance: string;
  seed: string | null;
  randomness: string;
  command: string[];
  command_working_directory: string;
  inputs: { path: string; sha256: string }[];
  flows_status: string;
}

export interface VariantSummary {
  ServerCount: number;
  HeatingDays: number;
  Hours: number;
  TotalHeatDemandKwh: number;
  ServerHeatUsedKwh: number;
  BackupHeatKwh: number;
  DumpedHeatKwh: number;
  ServerHours: number;
  Utilisation: number;
  Coverage: number;
  ElectricityCostChf: number;
  HeatValueChf: number;
  ComputingIncomeChf: number;
  BreakEvenChfPerServerHour: number;
  ProfitabilityNote: string;
}

export interface VariantResult {
  Summary: VariantSummary;
  Parameters: Record<string, { Value: number; Unit: string; Source: string }>;
  Variant?: { Id?: string; Role?: string; Status?: string; SelectionSource?: string };
}

export interface VariantOption {
  id: string;
  label: string;
  source: string;
  result: VariantResult;
}

export interface HourlyRow {
  local_time: string;
  heat_demand_kwh: number;
  space_heat_kwh: number;
  hot_water_kwh: number;
  target_load_per_server: number;
  actual_load_per_server: number;
  produced_heat_kwh: number;
  direct_used_kwh: number;
  tank_charge_kwh: number;
  tank_discharge_kwh: number;
  tank_stored_kwh: number;
  dumped_heat_kwh: number;
  backup_heat_kwh: number;
  server_electric_kwh: number;
}

const variantFiles = [
  { file: 'three-server-comparison', label: '3 servers · comparison variant', result: threeServers },
  { file: 'explorer-default', label: '4 servers · draft baseline', result: explorerDefault },
  { file: 'five-server-comparison', label: '5 servers · comparison variant', result: fiveServers },
  { file: 'summer-hot-water-minimum', label: '1 server · summer sizing bound', result: summerMinimum },
  { file: 'coldest-hour-capacity', label: '13 servers · coldest-hour sizing bound', result: coldestHour },
];

const csvLines = hourlyCsv.trim().split('\n');
const csvHeaders = csvLines[0].split(',');

export const referenceParameters = referenceHouse as Record<string, Parameter>;
export const run = runExport as RunMetadata;
export const price = priceExport;
export const scoredCase = {
  siteId: caseExport.sites[0].site_id,
  hourlyPath: caseExport.sites[0].hourly_path,
  summary: caseExport.sites[0].summary as CaseSummary,
};
export const variants: VariantOption[] = variantFiles.map(({ file, label, result }) => ({
  id: result.Variant?.Id ?? file,
  label,
  source:
    result.Variant?.SelectionSource ??
    `${result.Variant?.Role ?? 'variant'} · data/derived/reference-house/${file}.json`,
  result: result as VariantResult,
}));
export const hourlyRows: HourlyRow[] = csvLines.slice(1).map((line) => {
  const fields = line.split(',');
  return Object.fromEntries(
    csvHeaders.map((header, index) => [
      header,
      header === 'local_time' ? fields[index] : Number(fields[index]),
    ]),
  ) as unknown as HourlyRow;
});
