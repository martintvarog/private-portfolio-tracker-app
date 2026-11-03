import { NavLink } from 'react-router'

type NavItem = { label: string; to?: string; soon?: boolean }
type NavSection = { title: string; items: NavItem[] }

const sections: NavSection[] = [
  { title: 'Overview', items: [{ label: 'Dashboard', to: '/' }] },
  {
    title: 'Wealth',
    items: [
      { label: 'Holdings', to: '/holdings' },
      { label: 'Manual assets', soon: true },
      { label: 'Wealth graph', soon: true },
    ],
  },
  {
    title: 'Plan',
    items: [
      { label: 'Goals & DCA', soon: true },
      { label: 'Insights', soon: true },
    ],
  },
  {
    title: 'Setup',
    items: [
      { label: 'Connections', to: '/connections' },
      { label: 'Settings', soon: true },
    ],
  },
]

export function Sidebar() {
  return (
    <nav className="sidebar">
      <div className="logo">Portfolio Tracker</div>
      {sections.map((section) => (
        <div key={section.title}>
          <div className="section">{section.title}</div>
          {section.items.map((item) =>
            item.to ? (
              <NavLink
                key={item.label}
                to={item.to}
                end={item.to === '/'}
                className={({ isActive }) => `item${isActive ? ' active' : ''}`}
              >
                {item.label}
              </NavLink>
            ) : (
              <button key={item.label} className="item soon" disabled>
                {item.label}
                <span className="tag">soon</span>
              </button>
            ),
          )}
        </div>
      ))}
      <div className="foot">🔓 Vault unlocked</div>
    </nav>
  )
}
