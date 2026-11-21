import { useMemo, useState } from 'react'
import {
  CartesianGrid,
  Legend,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import { cumulativeCosts, hostBalance, type HostInputs } from './calc'
import {
  CANTONS,
  cantonHeatPriceChfPerKwh,
  DEFAULT_CANTON,
  DEFAULT_HOUSEHOLD,
  HEAT_PRICE,
  HOT_WATER_KWH_PER_PERSON_DAY,
  HOUSEHOLDS,
  INTERNAL,
  REFERENCE_PERSONS,
  REFERENCE_SPACE_HEATING_KWH_PER_YEAR,
  SERVERS,
  YEARS_SHOWN,
} from './params'

function buildInputs(servers: number, heatPrice: number, householdId: string): HostInputs {
  const persons = HOUSEHOLDS.find((h) => h.id === householdId)?.persons ?? REFERENCE_PERSONS
  return {
    spaceHeatingKwhPerYear: (REFERENCE_SPACE_HEATING_KWH_PER_YEAR.value * persons) / REFERENCE_PERSONS,
    hotWaterKwhPerDay: HOT_WATER_KWH_PER_PERSON_DAY.value * persons,
    heatingDaysPerYear: INTERNAL.heatingDaysPerYear.value,
    recoveredHeatKwPerServer: INTERNAL.recoveredHeatKwPerServer.value,
    heatFeeChfPerKwh: INTERNAL.heatFeeChfPerKwh.value,
    connectionCostChfPerServer: INTERNAL.connectionCostChfPerServer.value,
    serverCount: servers,
    heatPriceChfPerKwh: heatPrice,
  }
}

const chf = new Intl.NumberFormat('de-CH', { style: 'currency', currency: 'CHF', maximumFractionDigits: 0 })
const kwh = new Intl.NumberFormat('de-CH', { maximumFractionDigits: 0 })
const pct = new Intl.NumberFormat('de-CH', { style: 'percent', maximumFractionDigits: 0 })

function formatBreakEven(years: number | null) {
  if (years === null) return 'Never'
  if (years === 0) return 'Immediately'
  return `${years.toFixed(1)} years`
}

const price = new Intl.NumberFormat('de-CH', { minimumFractionDigits: 3, maximumFractionDigits: 3 })

export default function App() {
  const [canton, setCanton] = useState(DEFAULT_CANTON)
  const [servers, setServers] = useState(SERVERS.value)
  const [heatPrice, setHeatPrice] = useState(HEAT_PRICE.value)
  const [household, setHousehold] = useState(DEFAULT_HOUSEHOLD)

  const inputs = useMemo(() => buildInputs(servers, heatPrice, household), [servers, heatPrice, household])
  const result = useMemo(() => hostBalance(inputs), [inputs])
  const series = useMemo(() => cumulativeCosts(inputs, result, YEARS_SHOWN), [inputs, result])

  const changeCanton = (code: string) => {
    setCanton(code)
    const cantonPrice = cantonHeatPriceChfPerKwh(code)
    if (cantonPrice !== undefined) setHeatPrice(cantonPrice)
  }

  return (
    <main className="page">
      <h1>Heating cost calculator</h1>

      <div className="layout">
        <form className="inputs" onSubmit={(e) => e.preventDefault()}>
          <label className="field">
            <span>Canton</span>
            <select value={canton} onChange={(e) => changeCanton(e.target.value)}>
              {CANTONS.map((c) => (
                <option key={c.code} value={c.code}>
                  {c.name}
                </option>
              ))}
            </select>
          </label>

          <label className="field">
            <span className="field-head">
              Servers <span className="field-value">{servers}</span>
            </span>
            <input
              type="range"
              min={SERVERS.min}
              max={SERVERS.max}
              step={1}
              value={servers}
              onChange={(e) => setServers(Number(e.target.value))}
            />
          </label>

          <label className="field">
            <span className="field-head">
              Your heat price <span className="field-value">{price.format(heatPrice)} CHF/kWh</span>
            </span>
            <input
              type="range"
              min={HEAT_PRICE.min}
              max={HEAT_PRICE.max}
              step={HEAT_PRICE.step}
              value={heatPrice}
              onChange={(e) => setHeatPrice(Number(e.target.value))}
            />
          </label>

          <div className="field" role="radiogroup" aria-label="Household">
            <span>Household</span>
            <div className="segmented">
              {HOUSEHOLDS.map((h) => (
                <button
                  key={h.id}
                  type="button"
                  role="radio"
                  aria-checked={household === h.id}
                  className={household === h.id ? 'active' : ''}
                  onClick={() => setHousehold(h.id)}
                >
                  <span>{h.label}</span>
                  <span className="sub">{h.persons} persons</span>
                </button>
              ))}
            </div>
          </div>
        </form>

        <section className="results">
          <div className="hero">
            <span className="label">Savings per year</span>
            <span className="hero-value">{chf.format(result.savingsChfPerYear)}</span>
          </div>

          <dl className="figures">
            <div>
              <dt>Heat from servers</dt>
              <dd>{pct.format(result.coveredShare)}</dd>
              <span className="note">
                {kwh.format(result.coveredKwhPerYear)} of {kwh.format(result.demandKwhPerYear)} kWh
              </span>
            </div>
            <div>
              <dt>Cost today</dt>
              <dd>{chf.format(result.costTodayChfPerYear)}</dd>
              <span className="note">per year</span>
            </div>
            <div>
              <dt>Cost with servers</dt>
              <dd>{chf.format(result.costWithServersChfPerYear)}</dd>
              <span className="note">per year</span>
            </div>
            <div>
              <dt>Break-even</dt>
              <dd>{formatBreakEven(result.breakEvenYears)}</dd>
            </div>
          </dl>

          <div className="chart">
            <span className="label">Cumulative heating cost</span>
            <ResponsiveContainer width="100%" height={300}>
              <LineChart data={series} margin={{ top: 16, right: 8, bottom: 0, left: 8 }}>
                <CartesianGrid stroke="var(--grid)" vertical={false} />
                <XAxis
                  dataKey="year"
                  stroke="var(--muted)"
                  tickLine={false}
                  axisLine={{ stroke: 'var(--axis)' }}
                  tickFormatter={(y) => `${y}y`}
                />
                <YAxis
                  stroke="var(--muted)"
                  tickLine={false}
                  axisLine={false}
                  width={72}
                  tickFormatter={(v) => kwh.format(v)}
                />
                <Tooltip
                  contentStyle={{ background: 'var(--surface-2)', border: 'none', borderRadius: 8 }}
                  labelStyle={{ color: 'var(--ink-2)' }}
                  itemStyle={{ color: 'var(--ink)' }}
                  labelFormatter={(y) => `Year ${y}`}
                  formatter={(v) => chf.format(Number(v))}
                  cursor={{ stroke: 'var(--axis)' }}
                />
                <Legend wrapperStyle={{ color: 'var(--ink-2)', paddingTop: 8 }} iconType="plainline"
                  formatter={(name) => <span style={{ color: 'var(--ink-2)' }}>{name}</span>}
                />
                <Line
                  type="linear"
                  dataKey="today"
                  name="Today"
                  stroke="var(--series-1)"
                  strokeWidth={2}
                  dot={false}
                  activeDot={{ r: 4 }}
                  isAnimationActive={false}
                />
                <Line
                  type="linear"
                  dataKey="withServers"
                  name="With servers"
                  stroke="var(--series-2)"
                  strokeWidth={2}
                  dot={false}
                  activeDot={{ r: 4 }}
                  isAnimationActive={false}
                />
              </LineChart>
            </ResponsiveContainer>
          </div>
        </section>
      </div>
    </main>
  )
}
