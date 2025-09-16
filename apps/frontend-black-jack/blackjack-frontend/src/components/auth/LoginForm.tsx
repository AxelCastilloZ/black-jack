// src/components/auth/LoginForm.tsx
import React, { useState } from 'react'
import { authService } from '../../services/auth'

type Props = { onSuccess?: () => void }

const LoginForm: React.FC<Props> = ({ onSuccess }) => {
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
      await authService.login(email, password)
      onSuccess?.()
    } catch (err: any) {
      setError(err?.message ?? 'Error al iniciar sesión')
    } finally {
      setLoading(false)
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-3">
      <div className="space-y-2">
        <label className="block text-sm text-gray-300">Email</label>
        <input
          value={email}
          onChange={e => setEmail(e.target.value)}
          type="email"
          className="w-full rounded px-3 py-2 text-gray-900"
          required
        />
      </div>

      <div className="space-y-2">
        <label className="block text-sm text-gray-300">Contraseña</label>
        <input
          value={password}
          onChange={e => setPassword(e.target.value)}
          type="password"
          className="w-full rounded px-3 py-2 text-gray-900"
          required
        />
      </div>

      {error && <p className="text-red-400 text-sm">{error}</p>}

      <button
        type="submit"
        disabled={loading}
        className="w-full bg-casino-green-600 hover:bg-casino-green-700 disabled:opacity-50 rounded px-3 py-2"
      >
        {loading ? 'Ingresando…' : 'Iniciar sesión'}
      </button>
    </form>
  )
}

export default LoginForm
