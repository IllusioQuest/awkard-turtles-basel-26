import SourceNote from './SourceNote';

interface MetricCardProps {
  label: string;
  value: string;
  unit?: string;
  note?: string;
  source?: string;
  highlight?: boolean;
}

export default function MetricCard({
  label,
  value,
  unit,
  note,
  source,
  highlight = false,
}: MetricCardProps) {
  return (
    <article className={highlight ? 'metric-card highlight' : 'metric-card'}>
      <h3>{label}</h3>
      <p className="metric-value">
        {value}
        {unit ? <span className="metric-unit"> {unit}</span> : null}
      </p>
      {note ? <p className="metric-note">{note}</p> : null}
      {source ? <SourceNote source={source} /> : null}
    </article>
  );
}
