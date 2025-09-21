// src/router.tsx - Flujo completo CORREGIDO
import React from 'react'
import {
  createRootRoute,
  createRoute,
  createRouter,
  redirect,
  useNavigate,
  Outlet,
} from '@tanstack/react-router'

import AuthPage from './components/auth/AuthPage'
import HomePage from './pages/HomePage'
import LobbyPage from './pages/LobbyPage'
import ProfilePage from './pages/ProfilePage'
import GamePage from './pages/GamePage'
import { authService } from './services/auth'

// Guard para rutas protegidas
function requireAuth() {
  if (!authService.isAuthenticated()) {
    throw redirect({ to: '/auth' })
  }
}

// Wrapper para la pantalla de Auth - CORREGIDO
function AuthScreen() {
  const navigate = useNavigate()
  
  const handleAuthSuccess = () => {
    navigate({ to: '/home' })
  }

  return <AuthPage onAuthSuccess={handleAuthSuccess} />
}

// Root route limpio
const rootRoute = createRootRoute({
  component: () => (
    <div className="min-h-screen">
      <Outlet />
    </div>
  ),
})

// 1. PRIMERA PANTALLA: Autenticación
const authRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/auth',
  component: AuthScreen,
})

// 2. SEGUNDA PANTALLA: Home (después de autenticarse)
const homeRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/home',
  beforeLoad: () => requireAuth(),
  component: HomePage,
})

// 3. TERCERA PANTALLA: Lobby (cuando hace clic en "Jugar Ahora")
const lobbyRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/lobby',
  beforeLoad: () => requireAuth(),
  component: LobbyPage,
})

// 4. CUARTA PANTALLA: Game (cuando selecciona una mesa)
const gameRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/game/$tableId',
  beforeLoad: () => requireAuth(),
  component: GamePage,
})

// Viewer route - same component, different path
const viewerRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/viewer/$tableId',
  beforeLoad: () => requireAuth(),
  component: GamePage,
})

// 5. QUINTA PANTALLA: Viewer (cuando selecciona "Ver" una mesa)
// Now using same GamePage component for both player and viewer modes

// Pantalla de perfil (acceso desde cualquier lugar)
const profileRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/perfil',
  beforeLoad: () => requireAuth(),
  component: ProfilePage,
})

// Ruta raíz redirige a auth si no está autenticado, o a home si lo está
const indexRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/',
  component: () => {
    const navigate = useNavigate()
    
    React.useEffect(() => {
      if (authService.isAuthenticated()) {
        navigate({ to: '/home' })
      } else {
        navigate({ to: '/auth' })
      }
    }, [navigate])
    
    return (
      <div className="min-h-screen bg-slate-900 flex items-center justify-center">
        <div className="text-white">Redirigiendo...</div>
      </div>
    )
  },
})

// Árbol de rutas
const routeTree = rootRoute.addChildren([
  indexRoute,
  authRoute,
  homeRoute,
  lobbyRoute,
  gameRoute,
  viewerRoute,
  profileRoute,
])

// Router
export const router = createRouter({
  routeTree,
  defaultPreload: 'intent',
})

// Tipos para TypeScript
declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router
  }
}

export default router