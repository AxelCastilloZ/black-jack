import { Link, Outlet } from '@tanstack/react-router'

export default function App() {
  return (
    <div className="min-h-screen bg-slate-900 text-white">
      <nav className="p-4 flex gap-4">
        <Link to="/">Inicio</Link>
        <Link to="/lobby">Lobby</Link>
        <Link to="/perfil">Perfil</Link> {/* <-- aquí estaba /profile */}
      </nav>

      <main className="p-4">
        <Outlet />
      </main>
    </div>
  )
}
