import MetricCard from '../components/MetricCard';
import SourceNote from '../components/SourceNote';
import { referenceParameters, run, scoredCase } from '../data/dashboard-data';
import { formatChf, formatNumber, formatPercent } from '../format';

export default function OwnerPage() {
  const summary = scoredCase.summary;

  return (
    <div className="page">
      <p className="eyebrow">Server owner · private demo</p>
      <h1>Your home in 2025</h1>
      <p className="lead">
        This home needed {formatNumber(summary.total_heat_demand_kwh)} kWh of heat. Servers in the
        building supplied {formatNumber(summary.server_heat_used_kwh)} kWh of that, leaving the
        backup heater to cover the rest.
      </p>

      <section className="metric-grid" aria-label="Homeowner results">
        <MetricCard
          label="Heat covered by servers"
          value={formatPercent(summary.coverage)}
          note="Share of space heating and hot water, not an estimate"
          highlight
        />
        <MetricCard
          label="Backup heat still needed"
          value={formatNumber(summary.backup_heat_kwh)}
          unit="kWh"
        />
        <MetricCard
          label="Heat thrown away"
          value={formatNumber(summary.dumped_heat_kwh)}
          unit="kWh"
        />
        <MetricCard
          label="Electricity for servers"
          value={formatChf(summary.electricity_cost_chf)}
          note={`${formatNumber(summary.server_hours)} server-hours sold (assumption)`}
        />
        <MetricCard
          label="Value of heat used"
          value={formatChf(summary.heat_value_chf)}
          note="Heat valued as electricity avoided at heat-pump efficiency 2.5"
        />
        <MetricCard
          label="Case break-even price"
          value={formatChf(summary.break_even_chf_per_server_hour, 4)}
          unit="/ server-hour"
          note="Below this price the case balance fails before fixed costs"
        />
      </section>

      <section className="panel">
        <h2>How to read these numbers</h2>
        <p>{summary.profitability_note}</p>
        <SourceNote source={summary.job_demand_assumption} label="Job assumption" />
        <p className="fine-print">
          The backup heater is still required; savings concern its running cost, not its purchase.
          Missed deadlines: {summary.missed_deadlines}.
        </p>
      </section>

      <section className="panel">
        <h2>Assumptions behind this view</h2>
        <div className="table-scroll">
          <table>
            <thead>
              <tr>
                <th>Parameter</th>
                <th>Value</th>
                <th>Unit</th>
                <th>Source</th>
              </tr>
            </thead>
            <tbody>
              {Object.entries(referenceParameters).map(([name, parameter]) => (
                <tr key={name}>
                  <td>{name}</td>
                  <td>{parameter.value}</td>
                  <td>{parameter.unit}</td>
                  <td className="source-cell">{parameter.source}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <SourceNote
          source={`${run.git_commit.slice(0, 12)} · ${run.timezone} · ${run.time_convention}`}
          label="Run"
        />
      </section>

      <div className="callout">
        <strong>Demo view.</strong> This is the fixed case reference house, not your own building.
        A personal calculation needs the sourced configurator outputs (T-10/T-14). TODO(source):
        per-house results grid.
      </div>
    </div>
  );
}
