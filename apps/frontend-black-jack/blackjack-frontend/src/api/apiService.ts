// src/api/apiService.ts
import axios, {
  AxiosError,
  type AxiosInstance,
  type AxiosResponse,
  type AxiosRequestConfig,
} from 'axios'

/** Une base + path sin barras duplicadas */
function joinUrl(base: string, path: string) {
  const b = base.replace(/\/+$/, '')
  const p = path.replace(/^\/+/, '')
  return `${b}/${p}`
}

const TOKEN_KEY = 'auth_token'

export class ApiService {
  private api: AxiosInstance

  constructor() {
    // Lee la URL base desde .env. Ej:
    // VITE_API_BASE_URL=https://localhost:7102
    const env = import.meta.env as any
    const apiRoot = env.VITE_API_BASE_URL ?? env.VITE_API_URL ?? ''

    // Si hay baseURL => {base}/api ; si no, usa proxy /api
    const baseURL = apiRoot ? joinUrl(apiRoot, 'api') : '/api'

    this.api = axios.create({
      baseURL,
      timeout: 15000,
      // Usamos JWT por Authorization header, no cookies:
      withCredentials: false,
      headers: {
        'Content-Type': 'application/json',
        Accept: 'application/json',
      },
    })

    // Carga token persistido si existe
    const existing = localStorage.getItem(TOKEN_KEY)
    if (existing) this.setToken(existing)

    this.setupInterceptors()

    // Log útil en dev
    if (env.DEV) {
      // eslint-disable-next-line no-console
      console.log('[ApiService] baseURL:', baseURL)
    }
  }

  // ---- Interceptores ----
  private setupInterceptors() {
    // Authorization: Bearer <token>
    this.api.interceptors.request.use(
      (config) => {
        const token = localStorage.getItem(TOKEN_KEY)
        if (token) {
          config.headers = config.headers ?? {}
          ;(config.headers as Record<string, string>)['Authorization'] = `Bearer ${token}`
        }
        return config
      },
      (error) => Promise.reject(error),
    )

    // Manejo de errores y 401
    this.api.interceptors.response.use(
      (response: AxiosResponse) => response,
      (error: AxiosError) => {
        const status = error.response?.status
        const cfg = error.config
        const fullUrl = joinUrl(String(cfg?.baseURL ?? ''), String(cfg?.url ?? ''))

        // eslint-disable-next-line no-console
        console.error('API Error:', status, fullUrl, error.message)

        if (status === 401) {
          this.clearToken()
          // Notifica a la app para redirigir a login, etc.
          window.dispatchEvent(new CustomEvent('auth:unauthorized'))
        }
        return Promise.reject(error)
      },
    )
  }

  // ---- HTTP helpers ----
  async get<T>(url: string, params?: unknown, config?: AxiosRequestConfig): Promise<T> {
    const res = await this.api.get<T>(url, { params, ...(config ?? {}) })
    return res.data
  }

  async post<T>(url: string, data?: unknown, config?: AxiosRequestConfig): Promise<T> {
    const res = await this.api.post<T>(url, data, config)
    return res.data
  }

  async put<T>(url: string, data?: unknown, config?: AxiosRequestConfig): Promise<T> {
    const res = await this.api.put<T>(url, data, config)
    return res.data
  }

  async patch<T>(url: string, data?: unknown, config?: AxiosRequestConfig): Promise<T> {
    const res = await this.api.patch<T>(url, data, config)
    return res.data
  }

  async delete<T>(url: string, config?: AxiosRequestConfig): Promise<T> {
    const res = await this.api.delete<T>(url, config)
    return res.data
  }

  // ---- Token helpers ----
  setToken(token: string) {
    localStorage.setItem(TOKEN_KEY, token)
    this.api.defaults.headers.common['Authorization'] = `Bearer ${token}`
  }

  clearToken() {
    localStorage.removeItem(TOKEN_KEY)
    delete this.api.defaults.headers.common['Authorization']
  }

  getInstance() {
    return this.api
  }
}

export const apiService = new ApiService()
