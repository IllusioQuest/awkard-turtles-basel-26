import { DAYS_PER_YEAR, HOURS_PER_DAY } from './params'

export interface HostInputs {
  spaceHeatingKwhPerYear: number
  heatingDaysPerYear: number
  hotWaterKwhPerDay: number
  heatPriceChfPerKwh: number
  serverCount: number
  recoveredHeatKwPerServer: number
  heatFeeChfPerKwh: number
  connectionCostChfPerServer: number
}

export interface HostResult {
  demandKwhPerYear: number
  coveredKwhPerYear: number
  coveredShare: number
  costTodayChfPerYear: number
  costWithServersChfPerYear: number
  savingsChfPerYear: number
  breakEvenYears: number | null
}

/**
 * Operator model (decision 0020): DigitalHeat pays the servers and their electricity, the host
 * receives the recovered heat and pays `heatFeeChfPerKwh` for it, plus a one-time connection cost
 * per server. The backup heater covers the rest
 * at the host's current heat price.
 *
 * Daily approximation, not the hourly simulation:
 * - server heat per day = servers × recovered kW × 24 h
 * - heating day demand = space heating ÷ heating days + hot water
 * - other days demand = hot water
 * - covered per day = min(demand, server heat); no tank, no weather profile.
 * Space heating is spread evenly over the heating days, so mild-day surpluses are ignored and
 * coverage is an upper estimate.
 */
export function hostBalance(p: HostInputs): HostResult {
  const serverHeatKwhPerDay = p.serverCount * p.recoveredHeatKwPerServer * HOURS_PER_DAY
  const heatingDays = Math.min(Math.max(p.heatingDaysPerYear, 0), DAYS_PER_YEAR)
  const otherDays = DAYS_PER_YEAR - heatingDays
  const heatingDayDemandKwh =
    heatingDays > 0 ? p.spaceHeatingKwhPerYear / heatingDays + p.hotWaterKwhPerDay : 0

  const demandKwhPerYear = p.spaceHeatingKwhPerYear + p.hotWaterKwhPerDay * DAYS_PER_YEAR
  const coveredKwhPerYear =
    heatingDays * Math.min(heatingDayDemandKwh, serverHeatKwhPerDay) +
    otherDays * Math.min(p.hotWaterKwhPerDay, serverHeatKwhPerDay)

  const costTodayChfPerYear = demandKwhPerYear * p.heatPriceChfPerKwh
  const costWithServersChfPerYear =
    (demandKwhPerYear - coveredKwhPerYear) * p.heatPriceChfPerKwh +
    coveredKwhPerYear * p.heatFeeChfPerKwh
  const savingsChfPerYear = costTodayChfPerYear - costWithServersChfPerYear

  const connectionCostChf = p.serverCount * p.connectionCostChfPerServer
  let breakEvenYears: number | null = null
  if (connectionCostChf <= 0) breakEvenYears = savingsChfPerYear >= 0 ? 0 : null
  else if (savingsChfPerYear > 0) breakEvenYears = connectionCostChf / savingsChfPerYear

  return {
    demandKwhPerYear,
    coveredKwhPerYear,
    coveredShare: demandKwhPerYear > 0 ? coveredKwhPerYear / demandKwhPerYear : 0,
    costTodayChfPerYear,
    costWithServersChfPerYear,
    savingsChfPerYear,
    breakEvenYears,
  }
}

export function cumulativeCosts(p: HostInputs, r: HostResult, years: number) {
  return Array.from({ length: years + 1 }, (_, year) => ({
    year,
    today: r.costTodayChfPerYear * year,
    withServers: p.serverCount * p.connectionCostChfPerServer + r.costWithServersChfPerYear * year,
  }))
}
