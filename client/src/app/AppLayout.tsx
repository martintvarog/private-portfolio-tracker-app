import { Outlet } from 'react-router'
import { Sidebar } from './Sidebar'

// The frame every page shares. Rendered once by the parent (layout) route;
// child routes render into <Outlet />, so navigating swaps only that part.
export function AppLayout() {
  return (
    <>
      <Sidebar />
      <main>
        <Outlet />
      </main>
    </>
  )
}
