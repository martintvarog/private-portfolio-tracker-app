type NavItem = { label: string; active?: boolean; soon?: boolean }
type NavSection = { title: string; items: NavItem[] }

const sections: NavSection[] = [
  { title: 'Overview', items: [{ label: 'Dashboard', active: true }] },
  {
    title: 'Wealth',
    items: [
      { label: 'Holdings' },
      { label: 'Manual assets' },
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
    items: [{ label: 'Connections' }, { label: 'Settings', soon: true }],
  },
]

export function Sidebar() {
  return (
    <nav className="sidebar">
      <div className="logo">Portfolio Tracker</div>
      {sections.map((section) => (
        <div key={section.title}>
          <div className="section">{section.title}</div>
          {section.items.map((item) => (
            <button
              key={item.label}
              className={`item${item.active ? ' active' : ''}${item.soon ? ' soon' : ''}`}
              disabled={item.soon}
            >
              {item.label}
              {item.soon && <span className="tag">soon</span>}
            </button>
          ))}
        </div>
      ))}
      <div className="foot">🔓 Vault unlocked</div>
    </nav>
  )
}
