// src/components/auth/RegisterForm.tsx
import React, { useState } from 'react'
import { authService } from '../../services/auth'

type Props = {
  onSuccess?: () => void
}

const RegisterForm: React.FC<Props> = ({ onSuccess }) => {
  const [displayName, setDisplayName] = useState('Alice')
  const [email, setEmail] = useState('alice@test.com')
  const [password, setPassword] = useState('secret')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (loading) return
    setError(null)
    setLoading(true)
    try {
      await authService.register(displayName, email, password)
      onSuccess?.()
    } catch (err: any) {
      setError(err?.message || 'Error al registrarse')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div>
      <div className="text-center mb-6">
        <h2 className="text-2xl font-bold text-white mb-2">¡Únete al Casino!</h2>
        <p className="text-slate-400">Crea tu cuenta y comienza a ganar</p>
      </div>

      <form onSubmit={handleSubmit} className="space-y-5">
        <div>
          <label className="block text-sm font-medium text-slate-300 mb-2">
            Nombre de Usuario
          </label>
          <input
            value={displayName}
            onChange={e => setDisplayName(e.target.value)}
            className="w-full px-4 py-3 bg-slate-700/50 border border-slate-600 rounded-xl text-white placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 transition-all"
            placeholder="Tu nombre de jugador"
            required
          />
        </div>

        <div>
          <label className="block text-sm font-medium text-slate-300 mb-2">
            Correo Electrónico
          </label>
          <input
            value={email}
            onChange={e => setEmail(e.target.value)}
            type="email"
            className="w-full px-4 py-3 bg-slate-700/50 border border-slate-600 rounded-xl text-white placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 transition-all"
            placeholder="tu@email.com"
            required
          />
        </div>

        <div>
          <label className="block text-sm font-medium text-slate-300 mb-2">
            Contraseña
          </label>
          <input
            value={password}
            onChange={e => setPassword(e.target.value)}
            type="password"
            className="w-full px-4 py-3 bg-slate-700/50 border border-slate-600 rounded-xl text-white placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 transition-all"
            placeholder="Mínimo 6 caracteres"
            required
            minLength={6}
          />
        </div>

        {error && (
          <div className="bg-red-900/50 border border-red-700 rounded-xl p-3">
            <p className="text-red-300 text-sm text-center">{error}</p>
          </div>
        )}

        <button
          type="submit"
          disabled={loading}
          className="w-full bg-emerald-600 hover:bg-emerald-700 disabled:opacity-50 disabled:cursor-not-allowed text-white font-semibold py-3 px-4 rounded-xl transition-all duration-200 transform hover:scale-[1.02] focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:ring-offset-2 focus:ring-offset-slate-800"
        >
          {loading ? (
            <div className="flex items-center justify-center gap-2">
              <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin"></div>
              Creando cuenta...
            </div>
          ) : (
            'Crear Cuenta'
          )}
        </button>
      </form>

      <div className="mt-4 p-4 bg-emerald-900/20 border border-emerald-700/50 rounded-xl">
        <p className="text-emerald-300 text-sm text-center">
          🎁 Bonus de bienvenida: $5,000 para empezar
        </p>
      </div>
    </div>
  )
}

export default RegisterForm