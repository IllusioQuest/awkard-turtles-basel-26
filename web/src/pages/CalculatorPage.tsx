import { useState } from 'react';
import MetricCard from '../components/MetricCard';
import SourceNote from '../components/SourceNote';
import { price, referenceParameters, variants } from '../data/dashboard-data';
import { formatChf, formatNumber, formatPercent } from '../format';

const priceScenarios = [
  {
    label: 'Adopted computing price (scored)',
    valueChfPerServerHour: price.adopted_value_chf_per_server_hour,
    source: price.adopted_value_source,
  },
  {
    label: 'Market-tracking cross-check (not scored)',
    valueChfPerServerHour: price.value_chf_per_server_hour,
    source: price.value_method,
  },
  {
    label: 'Explorer slider (illustrative only)',
    valueChfPerServerHour: referenceParameters.ComputePriceChfPerServerHour.value,
    source: referenceParameters.ComputePriceChfPerServerHour.source,
  },
];

export default function CalculatorPage() {
  const [variantId, setVariantId] = useState(
    variants.find((variant) => variant.id === 'explorer-default')?.id ?? variants[0].id,
  );
  const [priceIndex, setPriceIndex] = useState(0);

  const variant = variants.find((item) => item.id === variantId) ?? variants[0];
  const summary = variant.result.Summary;
  const scenario = priceScenarios[priceIndex];
  const isAboveBreakEven = scenario.valueChfPerServerHour >= summary.BreakEvenChfPerServerHour;

  return (
    <div className="page">
      <p className="eyebrow">Public · prototype</p>
      <h1>Does it pay to heat with computing?</h1>
      <p className="lead">
        Pick a sourced scenario from the shared model. No house or price is calculated in the
        browser: every figure below comes from a committed run of the C# core.
      </p>

      <section className="panel controls" aria-label="Scenario selection">
        <label>
          <span>Server scenario</span>
          <select value={variant.id} onChange={(event) => setVariantId(event.target.value)}>
            {variants.map((item) => (
              <option key={item.id} value={item.id}>
                {item.label}
              </option>
            ))}
          </select>
        </label>
        <label>
          <span>Computing price reference</span>
          <select
            value={priceIndex}
            onChange={(event) => setPriceIndex(Number(event.target.value))}
          >
            {priceScenarios.map((item, index) => (
              <option key={item.label} value={index}>
                {item.label}
              </option>
            ))}
          </select>
        </label>
        <SourceNote source={variant.source} />
      </section>

      <section className="metric-grid" aria-label="Selected scenario results">
        <MetricCard
          label="Heat demand covered"
          value={formatPercent(summary.Coverage)}
          highlight
          note={`${formatNumber(summary.ServerHeatUsedKwh)} kWh from servers of ${formatNumber(summary.TotalHeatDemandKwh)} kWh total`}
        />
        <MetricCard
          label="Heat dumped"
          value={formatNumber(summary.DumpedHeatKwh)}
          unit="kWh"
        />
        <MetricCard
          label="Server utilisation"
          value={formatPercent(summary.Utilisation)}
          note={`${formatNumber(summary.ServerHours)} full-load-equivalent server-hours`}
        />
        <MetricCard
          label="Case break-even"
          value={formatChf(summary.BreakEvenChfPerServerHour, 4)}
          unit="/ server-hour"
          note="Electricity minus used heat; excludes hardware and operations"
        />
      </section>

      <section className="panel comparison">
        <h2>Price against case break-even</h2>
        <div className="comparison-row">
          <div>
            <span className="comparison-label">{scenario.label}</span>
            <strong>{formatChf(scenario.valueChfPerServerHour, 4)} / server-hour</strong>
          </div>
          <span className={isAboveBreakEven ? 'verdict positive' : 'verdict negative'}>
            {isAboveBreakEven ? 'Above case break-even' : 'Below case break-even'}
          </span>
        </div>
        <SourceNote source={scenario.source} />
        <p className="fine-print">
          Comparison only: the case break-even omits hardware, integration and operations. A
          full-cost answer needs the sourced configurator outputs.
        </p>
      </section>

      <div className="callout">
        <strong>Prototype boundary.</strong> This is a scenario viewer, not a customer model.
        Custom house inputs, full-cost break-even and the win-win zone need T-10/T-14 outputs.
        TODO(source): sourced configurator grid.
      </div>
    </div>
  );
}
