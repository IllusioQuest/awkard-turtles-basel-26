import { NavLink, Outlet } from 'react-router-dom';

const views = [
  { to: '/', label: 'Price calculator', audience: 'Public' },
  { to: '/admin', label: 'DigitalHeat admin', audience: 'Operator' },
  { to: '/owner', label: 'Homeowner', audience: 'Server owner' },
];

export default function Layout() {
  return (
    <div className="app-shell">
      <header className="topbar">
        <div className="brand">
          <span className="brand-mark">DH</span>
          <span className="brand-text">
            <strong>DigitalHeat</strong>
            <small>Demo dashboards</small>
          </span>
        </div>
        <nav aria-label="Demo views">
          {views.map(({ to, label, audience }) => (
            <NavLink
              key={to}
              to={to}
              end={to === '/'}
              className={({ isActive }) => (isActive ? 'nav-link active' : 'nav-link')}
            >
              <span>{label}</span>
              <small>{audience}</small>
            </NavLink>
          ))}
        </nav>
        <span className="demo-badge">Demo mode · no login</span>
      </header>

      <main>
        <Outlet />
      </main>

      <footer>
        <p>
          Figures come from committed outputs of the shared C# model. The case break-even excludes
          hardware, integration and operations.
        </p>
        <p>Decision 0022 · one local Vite/React/TypeScript demo app · no network calls</p>
      </footer>
    </div>
  );
}
