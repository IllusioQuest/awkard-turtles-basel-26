import { Navigate } from 'react-router'
import { useAsync, useSource } from '../data/source'
import MockBanner from './MockBanner'

const PREFERRED_RUN = 'ch-100'

/** /admin: redirects to the last chosen run, else ch-100 if present, else the first run. */
export default function AdminHome() {
  const source = useSource()
  const runs = useAsync(source ? 'runs' : null, () => source!.runs())
  if (runs.error) return <main className="page admin"><p className="banner">{runs.error.message}</p></main>
  if (!runs.data) return <main className="page admin"><p className="note">Loading…</p></main>
  const storedRunId = localStorage.getItem('digitalheat.admin.runId')
  const run = runs.data.find((r) => r.id === storedRunId) ?? runs.data.find((r) => r.id === PREFERRED_RUN) ?? runs.data[0]
  if (!run)
    return (
      <main className="page admin">
        <p className="banner">No runs found. Run the fleet simulation first (data/derived/fleet/).</p>
      </main>
    )
  return (
    <>
      {run.mock && <MockBanner />}
      <Navigate to={`/admin/${encodeURIComponent(run.id)}`} replace />
    </>
  )
}
