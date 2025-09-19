// src/services/auth.ts - PUERTO CORREGIDO 7102
const API_BASE = import.meta.env.VITE_API_BASE_URL || 'http://localhost:7102'

export interface AuthUser {
  id: string
  displayName: string
  email: string
  balance: number
}

interface LoginResponse {
  token: string
  user: AuthUser
}

const TOKEN_KEY = 'blackjack_token'
const USER_KEY = 'blackjack_user'

class AuthService {
  private currentUser: AuthUser | null = null

  constructor() {
    this.loadPersistedAuth()
  }

  // Cargar datos persistidos
  private loadPersistedAuth() {
    try {
      const token = localStorage.getItem(TOKEN_KEY)
      const userJson = localStorage.getItem(USER_KEY)
      if (token && userJson) {
        this.currentUser = JSON.parse(userJson)
        console.log('[AUTH] Loaded persisted auth:', this.currentUser?.displayName)
      }
    } catch (error) {
      console.error('[AUTH] Error loading persisted auth:', error)
      this.clearAuth()
    }
  }

  // LOGIN
  async login(email: string, password: string): Promise<AuthUser> {
    console.log('[AUTH] Login attempt:', email)
    
    try {
      const response = await fetch(`${API_BASE}/api/auth/login`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({ email, password })
      })

      console.log('[AUTH] Login response status:', response.status)

      if (!response.ok) {
        const errorText = await response.text()
        console.error('[AUTH] Login failed:', errorText)
        throw new Error(`Error ${response.status}: ${errorText}`)
      }

      const data = await response.text()
      console.log('[AUTH] Raw login response:', data)

      let parsedData
      try {
        parsedData = JSON.parse(data)
      } catch {
        // Si es solo un token string
        parsedData = { token: data }
      }

      if (!parsedData.token) {
        throw new Error('No token received from server')
      }

      // Decodificar token para extraer info
      const tokenPayload = this.decodeJWT(parsedData.token)
      console.log('[AUTH] Token payload:', tokenPayload)

      // Crear usuario con datos disponibles
      const user: AuthUser = {
        id: tokenPayload?.playerId || tokenPayload?.sub || tokenPayload?.nameid || Date.now().toString(),
        displayName: parsedData.user?.displayName || tokenPayload?.name || email.split('@')[0],
        email: parsedData.user?.email || tokenPayload?.email || email,
        balance: parsedData.user?.balance || 5000
      }

      // Guardar datos
      localStorage.setItem(TOKEN_KEY, parsedData.token)
      localStorage.setItem(USER_KEY, JSON.stringify(user))
      this.currentUser = user

      console.log('[AUTH] Login successful:', user)
      return user

    } catch (error: any) {
      console.error('[AUTH] Login error:', error)
      this.clearAuth()
      throw new Error(error.message || 'Error al iniciar sesión')
    }
  }

  // REGISTER
  async register(displayName: string, email: string, password: string): Promise<AuthUser> {
    console.log('[AUTH] Register attempt:', { displayName, email })
    
    try {
      const response = await fetch(`${API_BASE}/api/auth/register`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({ displayName, email, password })
      })

      console.log('[AUTH] Register response status:', response.status)

      if (!response.ok) {
        const errorText = await response.text()
        console.error('[AUTH] Register failed:', errorText)
        
        if (response.status === 400) {
          throw new Error('Datos inválidos. Verifica email y contraseña.')
        } else if (response.status === 409) {
          throw new Error('El email ya está registrado.')
        }
        
        throw new Error(`Error ${response.status}: ${errorText}`)
      }

      const data = await response.text()
      console.log('[AUTH] Register successful, now logging in...')

      // Después del registro exitoso, hacer login automático
      return await this.login(email, password)

    } catch (error: any) {
      console.error('[AUTH] Register error:', error)
      throw new Error(error.message || 'Error al registrarse')
    }
  }

  // LOGOUT
  logout() {
    console.log('[AUTH] Logging out...')
    this.clearAuth()
  }

  // Limpiar datos de auth
  private clearAuth() {
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(USER_KEY)
    this.currentUser = null
    console.log('[AUTH] Auth data cleared')
  }

  // Decodificar JWT
  private decodeJWT(token: string): any | null {
    try {
      const parts = token.split('.')
      if (parts.length !== 3) return null
      
      const payload = parts[1]
      const decoded = atob(payload.replace(/-/g, '+').replace(/_/g, '/'))
      return JSON.parse(decoded)
    } catch (error) {
      console.error('[AUTH] Error decoding JWT:', error)
      return null
    }
  }

  // Getters
  getCurrentUser(): AuthUser | null {
    return this.currentUser
  }

  getToken(): string | null {
    return localStorage.getItem(TOKEN_KEY)
  }

  isAuthenticated(): boolean {
    return !!(this.getToken() && this.currentUser)
  }

  // Debug
  debugAuthState() {
    console.log('[AUTH] === DEBUG STATE ===')
    console.log('Token:', this.getToken())
    console.log('User:', this.currentUser)
    console.log('Authenticated:', this.isAuthenticated())
    
    const token = this.getToken()
    if (token) {
      console.log('Token payload:', this.decodeJWT(token))
    }
  }
}

export const authService = new AuthService()