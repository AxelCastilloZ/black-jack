// src/pages/ProfilePage.tsx
import React from 'react'
import { authService } from '../services/auth'

export default function ProfilePage() {
  const user = authService.getCurrentUser()

  return (
    <div className="max-w-4xl mx-auto">
      <div className="text-center mb-8">
        <h1 className="text-4xl font-bold text-white mb-2">Mi Perfil</h1>
        <p className="text-casino-gold-400 text-lg">Gestiona tu cuenta y preferencias</p>
      </div>

      <div className="bg-white rounded-lg shadow-xl p-6">
        <h2 className="text-2xl font-semibold text-gray-800 mb-4">Información Personal</h2>

        <div className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700">Nombre de Usuario</label>
            <p className="mt-1 text-gray-900">{user?.displayName || 'No disponible'}</p>
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700">Email</label>
            <p className="mt-1 text-gray-900">{user?.email || 'No disponible'}</p>
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700">Saldo</label>
            <p className="mt-1 text-casino-green-600 font-bold">
              ${(user?.balance ?? 5000).toLocaleString()}
            </p>
          </div>
        </div>
      </div>
    </div>
  )
}
