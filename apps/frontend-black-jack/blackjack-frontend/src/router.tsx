// src/router.tsx
import React from 'react'
import {
  createRootRoute,
  createRoute,
  createRouter,
  redirect,
  useNavigate,
} from '@tanstack/react-router'

import App from './App'
import AuthPage from './components/auth/AuthPage'
import LobbyPage from './pages/LobbyPage'
import ProfilePage from './pages/ProfilePage'
import GamePage from './pages/GamePage'
import { authService } from './services/auth'

// ----- Guard simple -----
function requireAuth() {
  if (!authService.isAuthenticated()) {
    // si no hay sesión, redirige al root (/)
    throw redirect({ to: '/' })
  }
}

// Wrapper para la pantalla de Auth que navega al lobby al terminar
function IndexScreen() {
  const navigate = useNavigate()
  return <AuthPage onAuthSuccess={() => navigate({ to: '/lobby' })} />
}

// ----- Rutas -----
const rootRoute = createRootRoute({
  component: App,
})

const indexRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/',
  component: IndexScreen,
})

const lobbyRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/lobby',
  beforeLoad: () => requireAuth(),
  component: LobbyPage,
})

const profileRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/perfil',
  beforeLoad: () => requireAuth(),
  component: ProfilePage,
})

// Nueva ruta para el juego con parámetro tableId
const gameRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/game/$tableId',
  beforeLoad: () => requireAuth(),
  component: GamePage,
})

// Árbol de rutas y router
const routeTree = rootRoute.addChildren([
  indexRoute, 
  lobbyRoute, 
  profileRoute,
  gameRoute
])

export const router = createRouter({ routeTree })

// Augment de tipos para TanStack Router
declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router
  }
}

export default router