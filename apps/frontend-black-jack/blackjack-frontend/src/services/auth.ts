// src/services/auth.ts
import { apiService } from '../api/apiService'

export interface AuthUser {
  id: string
  displayName: string
  email?: string
  balance: number
}

interface LoginResponse {
  token: string
  user: AuthUser
}

const TOKEN_KEY = 'auth_token'
const USER_KEY = 'user'

/* ===== Utils: decodificar JWT y extraer playerId ===== */
function base64UrlDecode(input: string) {
  const pad = (s: string) => s + '='.repeat((4 - (s.length % 4)) % 4)
  const b64 = pad(input.replace(/-/g, '+').replace(/_/g, '/'))
  const bin = atob(b64)
  try {
    return decodeURIComponent(
      bin
        .split('')
        .map(c => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
        .join(''),
    )
  } catch {
    return bin
  }
}

function decodeJwt(token: string): Record<string, any> | null {
  try {
    const [, payload] = token.split('.')
    if (!payload) return null
    return JSON.parse(base64UrlDecode(payload))
  } catch {
    return null
  }
}

function pickPlayerIdFromClaims(claims: Record<string, any> | null): string | null {
  if (!claims) return null
  const keys = [
    'playerId',
    'PlayerId',
    'sub',
    'nameid',
    'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier',
  ]
  for (const k of keys) {
    const v = claims[k]
    if (typeof v === 'string' && v.length >= 16) return v
  }
  return null
}
/* ===================================================== */

class AuthService {
  private currentUser: AuthUser | null = null

  constructor() {
    this.loadPersistedAuth()
  }

  // ------- Persistencia -------
  private loadPersistedAuth() {
    try {
      const token = localStorage.getItem(TOKEN_KEY)
      const userJson = localStorage.getItem(USER_KEY)
      if (token && userJson) {
        this.currentUser = JSON.parse(userJson) as AuthUser
        apiService.setToken(token)
      }
    } catch {
      this.clearAuth()
    }
  }

  // --------- PERFIL (intenta varios endpoints comunes) ----------
  private async fetchProfileWithFallback(): Promise<Partial<AuthUser> | null> {
    const candidates = ['/auth/me', '/users/me', '/players/me', '/me']
    for (const url of candidates) {
      try {
        const data = await apiService.get<any>(url)
        if (!data) continue
        // intenta mapear campos habituales
        const id =
          data.id ??
          data.playerId ??
          data.userId ??
          data.user?.id ??
          data.user?.playerId ??
          null
        const displayName =
          data.displayName ?? data.name ?? data.username ?? data.user?.displayName
        const email = data.email ?? data.user?.email
        const balance = Number(data.balance ?? data.user?.balance ?? 5000)

        if (id || displayName || email) {
          return { id: String(id ?? ''), displayName, email, balance }
        }
      } catch {
        // probar siguiente
      }
    }
    return null
  }
  // ---------------------------------------------------------------

  // ------- LOGIN -------
  async login(email: string, password: string): Promise<AuthUser> {
    console.log('🔑 Intentando login...', { email })
    const data = await apiService.post<any>('/auth/login', { email, password })

    if (typeof data === 'string') {
      // token plano
      return this.finishAuthWithToken(data, email)
    } else {
      // JSON: { token, user? , displayName?, playerId? }
      const token: string = data.token

      // 1) intenta claims del JWT
      const claims = decodeJwt(token)
      const playerIdFromToken = pickPlayerIdFromClaims(claims)

      // 2) si no hay user, crea uno provisional
      let user: AuthUser =
        data.user ??
        ({
          id: playerIdFromToken ?? data.playerId ?? `temp-${Date.now()}`,
          displayName: data.displayName ?? email.split('@')[0],
          email,
          balance: 5000,
        } as AuthUser)

      // guarda provisional para poder pegarle al /me
      this.finishAuth({ token, user })

      // 3) intenta completar/normalizar con /me
      const profile = await this.fetchProfileWithFallback()
      if (profile) {
        user = {
          id: (profile.id && profile.id.length ? profile.id : user.id)!,
          displayName: profile.displayName ?? user.displayName,
          email: profile.email ?? user.email,
          balance: profile.balance ?? user.balance,
        }
        localStorage.setItem(USER_KEY, JSON.stringify(user))
        this.currentUser = user
      }

      console.log('✅ Auth OK:', {
        tokenLength: token.length,
        user: user.displayName,
        playerId: user.id,
      })
      return user
    }
  }

  // ------- REGISTER - CORREGIDO -------
  async register(displayName: string, email: string, password: string): Promise<AuthUser> {
    console.log('📝 Intentando registro...', { displayName, email })
    
    try {
      // 1. Hacer el registro (el backend solo devuelve UserProfile, no token)
      const registerResponse = await apiService.post<any>('/auth/register', { 
        displayName, 
        email, 
        password 
      })
      console.log('✅ Registro exitoso:', registerResponse)
      
      // 2. Hacer login inmediatamente después del registro exitoso
      console.log('🔑 Haciendo login automático después del registro...')
      const user = await this.login(email, password)
      
      // 3. Asegurar que el displayName se mantenga correcto
      if (!user.displayName || user.displayName !== displayName) {
        user.displayName = displayName
        localStorage.setItem(USER_KEY, JSON.stringify(user))
        this.currentUser = user
      }
      
      console.log('✅ Registro y login completados para:', user.displayName)
      return user
      
    } catch (error: any) {
      console.error('❌ Error en registro:', error)
      
      // Manejar errores específicos del backend
      if (error.response?.status === 400) {
        const errorData = error.response?.data
        let errorMessage = 'Error en los datos proporcionados'
        
        // Intentar extraer el mensaje de error del backend
        if (typeof errorData === 'string') {
          errorMessage = errorData
        } else if (errorData?.message) {
          errorMessage = errorData.message
        } else if (errorData?.error) {
          errorMessage = errorData.error
        } else if (errorData?.errors && Array.isArray(errorData.errors)) {
          errorMessage = errorData.errors.join(', ')
        }
        
        throw new Error(errorMessage)
      } else if (error.response?.status === 409) {
        throw new Error('El email ya está registrado')
      } else if (error.response?.status === 500) {
        throw new Error('Error interno del servidor. Intenta de nuevo.')
      } else {
        throw new Error(error?.message || 'Error al crear cuenta')
      }
    }
  }

  // ------- Helpers de auth -------
  private finishAuth(res: LoginResponse) {
    localStorage.setItem(TOKEN_KEY, res.token)
    localStorage.setItem(USER_KEY, JSON.stringify(res.user))
    apiService.setToken(res.token)
    this.currentUser = res.user
  }

  private finishAuthWithToken(token: string, email: string, displayName?: string): AuthUser {
    const claims = decodeJwt(token)
    const playerIdFromToken = pickPlayerIdFromClaims(claims)

    const tempUser: AuthUser = {
      id: playerIdFromToken ?? `temp-${Date.now()}`,
      displayName: displayName ?? email.split('@')[0] ?? 'Usuario',
      email,
      balance: 5000,
    }
    localStorage.setItem(TOKEN_KEY, token)
    localStorage.setItem(USER_KEY, JSON.stringify(tempUser))
    apiService.setToken(token)
    this.currentUser = tempUser
    console.log('✅ Auth (token plano):', {
      tokenLength: token.length,
      user: tempUser.displayName,
      playerId: tempUser.id,
      claims,
    })
    return tempUser
  }

  logout() {
    this.clearAuth()
    // Redirigir a la página de autenticación
    window.location.href = '/auth'
  }

  private clearAuth() {
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(USER_KEY)
    apiService.clearToken()
    this.currentUser = null
  }

  getCurrentUser(): AuthUser | null {
    return this.currentUser
  }

  getToken(): string | null {
    return localStorage.getItem(TOKEN_KEY)
  }

  isAuthenticated(): boolean {
    return !!this.getToken() && !!this.currentUser
  }

  // ------- utilidades de juego -------
  updateUserBalance(newBalance: number) {
    if (this.currentUser) {
      this.currentUser.balance = newBalance
      localStorage.setItem(USER_KEY, JSON.stringify(this.currentUser))
      console.log('💰 Balance actualizado:', newBalance)
    }
  }

  spendMoney(amount: number): boolean {
    if (!this.currentUser) return false
    if (this.currentUser.balance >= amount) {
      this.currentUser.balance -= amount
      localStorage.setItem(USER_KEY, JSON.stringify(this.currentUser))
      console.log('💸 Gastado:', amount, 'Restante:', this.currentUser.balance)
      return true
    }
    console.warn('💸 Fondos insuficientes:', amount)
    return false
  }

  addMoney(amount: number) {
    if (this.currentUser) {
      this.currentUser.balance += amount
      localStorage.setItem(USER_KEY, JSON.stringify(this.currentUser))
      console.log('💰 Ganado:', amount, 'Actual:', this.currentUser.balance)
    }
  }
}

export const authService = new AuthService()