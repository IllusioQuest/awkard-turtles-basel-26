import { useState } from 'react';
import SourceNote from '../components/SourceNote';
import { hourlyRows, run, scoredCase, variants } from '../data/dashboard-data';
import { formatChf, formatNumber, formatPercent } from '../format';

const demoHours = { january: '2025-01-15T12:00', july: '2025-07-15T12:00' };

function hourIndexAt(localTime: string): number {
  const index = hourlyRows.findIndex((row) => row.local_time.startsWith(localTime));
  return index < 0 ? 0 : index;
}

export default function AdminPage() {
  const [hourIndex, setHourIndex] = useState(hourIndexAt(demoHours.january));
  const row = hourlyRows[hourIndex];

  return (
    <div className="page">
      <p className="eyebrow">DigitalHeat operator · prototype</p>
      <h1>Run inspection</h1>
      <p className="lead">
        Provenance, variant comparison and one hour of the exported year. All values are read from
        the committed result bundle; nothing is recomputed here.
      </p>

      <section className="panel" aria-label="Run provenance">
        <h2>Run provenance</h2>
        <dl className="detail-grid">
          <div>
            <dt>Site</dt>
            <dd>{scoredCase.siteId}</dd>
          </div>
          <div>
            <dt>Time</dt>
            <dd>
              {run.timezone} · {run.time_convention}
            </dd>
          </div>
          <div>
            <dt>Code</dt>
            <dd>
              {run.code_provenance} · {run.git_commit.slice(0, 12)} · dirty:{' '}
              {String(run.git_dirty)}
            </dd>
          </div>
          <div>
            <dt>Randomness</dt>
            <dd>{run.randomness} · seed: {String(run.seed)}</dd>
          </div>
        </dl>
        <p className="command">
          <code>{run.command.join(' ')}</code>
        </p>
        <h3>Input hashes</h3>
        <ul className="hash-list">
          {run.inputs.map((input) => (
            <li key={input.path}>
              <code>{input.path}</code>
              <span>{input.sha256.slice(0, 16)}…</span>
            </li>
          ))}
        </ul>
        <p className="fine-print">
          Flows: {run.flows_status}. No tile map or external service is used; T-17 map, flows and
          forecast views remain open.
        </p>
      </section>

      <section className="panel" aria-label="Variant comparison">
        <h2>Variant comparison</h2>
        <div className="table-scroll">
          <table>
            <thead>
              <tr>
                <th>Scenario</th>
                <th>Coverage</th>
                <th>Dumped heat</th>
                <th>Utilisation</th>
                <th>Case break-even</th>
              </tr>
            </thead>
            <tbody>
              {variants.map((variant) => (
                <tr key={variant.id}>
                  <td>{variant.label}</td>
                  <td>{formatPercent(variant.result.Summary.Coverage)}</td>
                  <td>{formatNumber(variant.result.Summary.DumpedHeatKwh)} kWh</td>
                  <td>{formatPercent(variant.result.Summary.Utilisation)}</td>
                  <td>
                    {formatChf(variant.result.Summary.BreakEvenChfPerServerHour, 4)} / server-hour
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <SourceNote source="data/derived/reference-house/*.json · 4-server draft baseline" />
      </section>

      <section className="panel" aria-label="Exported hour">
        <h2>Exported hour</h2>
        <div className="hour-controls">
          <button type="button" onClick={() => setHourIndex(hourIndexAt(demoHours.january))}>
            January day
          </button>
          <button type="button" onClick={() => setHourIndex(hourIndexAt(demoHours.july))}>
            July day
          </button>
          <label className="slider-label">
            Hour {formatNumber(hourIndex + 1)} of {formatNumber(hourlyRows.length)}
            <input
              type="range"
              min={0}
              max={hourlyRows.length - 1}
              value={hourIndex}
              onChange={(event) => setHourIndex(Number(event.target.value))}
            />
          </label>
        </div>
        <dl className="detail-grid">
          <div>
            <dt>Local time</dt>
            <dd>{row.local_time}</dd>
          </div>
          <div>
            <dt>Heat demand</dt>
            <dd>{formatNumber(row.heat_demand_kwh, 2)} kWh</dd>
          </div>
          <div>
            <dt>Target load per server</dt>
            <dd>{formatNumber(row.target_load_per_server, 2)}</dd>
          </div>
          <div>
            <dt>Actual load per server</dt>
            <dd>{formatNumber(row.actual_load_per_server, 2)}</dd>
          </div>
          <div>
            <dt>Tank stored</dt>
            <dd>{formatNumber(row.tank_stored_kwh, 2)} kWh</dd>
          </div>
          <div>
            <dt>Backup heat</dt>
            <dd>{formatNumber(row.backup_heat_kwh, 2)} kWh</dd>
          </div>
          <div>
            <dt>Dumped heat</dt>
            <dd>{formatNumber(row.dumped_heat_kwh, 2)} kWh</dd>
          </div>
          <div>
            <dt>Server electricity</dt>
            <dd>{formatNumber(row.server_electric_kwh, 2)} kWh</dd>
          </div>
        </dl>
        <SourceNote source="data/derived/reference-house-v1/sites/reference-house/hourly.csv" />
      </section>
    </div>
  );
}
