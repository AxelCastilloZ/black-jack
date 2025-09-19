// src/pages/ProfilePage.tsx - CORREGIDO
import React, { useState } from 'react'
import { useNavigate } from '@tanstack/react-router'
import { authService } from '../services/auth'

export default function ProfilePage() {
  const navigate = useNavigate()
  const currentUser = authService.getCurrentUser()
  const [isEditing, setIsEditing] = useState(false)
  
  if (!currentUser) {
    return (
      <div className="min-h-screen bg-slate-900 flex items-center justify-center">
        <div className="text-white">Usuario no encontrado</div>
      </div>
    )
  }

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900">
      {/* Header */}
      <header className="flex items-center justify-between p-6 border-b border-slate-700">
        <div className="flex items-center gap-4">
          <button
            onClick={() => navigate({ to: '/home' })}
            className="text-slate-300 hover:text-white flex items-center gap-2 transition-colors"
          >
            ← Volver
          </button>
          <div className="flex items-center gap-3">
            <div className="flex gap-1">
              <span className="text-xl">♠</span>
              <span className="text-xl text-red-500">♥</span>
              <span className="text-xl text-red-500">♦</span>
              <span className="text-xl">♣</span>
            </div>
            <h1 className="text-xl font-bold text-white">Mi Perfil</h1>
          </div>
        </div>

        <button
          onClick={() => authService.logout()}
          className="text-slate-400 hover:text-red-400 transition-colors"
        >
          Cerrar Sesión
        </button>
      </header>

      {/* Main Content */}
      <main className="max-w-4xl mx-auto px-6 py-12">
        <div className="text-center mb-8">
          <h2 className="text-4xl font-bold text-white mb-2">Mi Perfil</h2>
          <p className="text-slate-400 text-lg">Gestiona tu cuenta y preferencias</p>
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
          {/* Profile Card */}
          <div className="lg:col-span-2">
            <div className="bg-slate-800/50 rounded-2xl p-8 border border-slate-700">
              <div className="flex items-center gap-6 mb-8">
                <div className="w-20 h-20 bg-emerald-500 rounded-full flex items-center justify-center text-white text-2xl font-bold">
                  {currentUser.displayName.substring(0, 2).toUpperCase()}
                </div>
                <div>
                  <h3 className="text-2xl font-bold text-white mb-1">
                    {currentUser.displayName}
                  </h3>
                  <p className="text-slate-400">{currentUser.email}</p>
                  <div className="flex items-center gap-2 mt-2">
                    <span className="inline-block w-2 h-2 rounded-full bg-green-400"></span>
                    <span className="text-green-400 text-sm">En línea</span>
                  </div>
                </div>
              </div>

              {/* Account Info */}
              <div className="space-y-6">
                <div>
                  <label className="block text-sm font-medium text-slate-300 mb-2">
                    Nombre de Usuario
                  </label>
                  <div className="px-4 py-3 bg-slate-700/50 rounded-xl text-white">
                    {currentUser.displayName}
                  </div>
                </div>

                <div>
                  <label className="block text-sm font-medium text-slate-300 mb-2">
                    Correo Electrónico
                  </label>
                  <div className="px-4 py-3 bg-slate-700/50 rounded-xl text-white">
                    {currentUser.email || 'No disponible'}
                  </div>
                </div>

                <div>
                  <label className="block text-sm font-medium text-slate-300 mb-2">
                    ID de Jugador
                  </label>
                  <div className="px-4 py-3 bg-slate-700/50 rounded-xl text-slate-400 font-mono text-sm">
                    {currentUser.id}
                  </div>
                </div>
              </div>
            </div>
          </div>

          {/* Stats Sidebar */}
          <div className="space-y-6">
            {/* Balance Card */}
            <div className="bg-gradient-to-br from-emerald-900/50 to-emerald-800/50 rounded-2xl p-6 border border-emerald-700/50">
              <div className="text-center">
                <div className="text-3xl font-bold text-white mb-2">
                  ${currentUser.balance.toLocaleString()}
                </div>
                <div className="text-emerald-300 font-medium">Balance Actual</div>
              </div>
            </div>

            {/* Quick Stats */}
            <div className="bg-slate-800/50 rounded-2xl p-6 border border-slate-700">
              <h4 className="text-lg font-semibold text-white mb-4">Estadísticas</h4>
              <div className="space-y-4">
                <div className="flex justify-between items-center">
                  <span className="text-slate-400">Partidas Jugadas:</span>
                  <span className="text-white font-semibold">12</span>
                </div>
                <div className="flex justify-between items-center">
                  <span className="text-slate-400">Partidas Ganadas:</span>
                  <span className="text-emerald-400 font-semibold">8</span>
                </div>
                <div className="flex justify-between items-center">
                  <span className="text-slate-400">Ratio de Victoria:</span>
                  <span className="text-emerald-400 font-semibold">66.7%</span>
                </div>
                <div className="flex justify-between items-center">
                  <span className="text-slate-400">Mayor Ganancia:</span>
                  <span className="text-yellow-400 font-semibold">$850</span>
                </div>
              </div>
            </div>

            {/* Quick Actions */}
            <div className="bg-slate-800/50 rounded-2xl p-6 border border-slate-700">
              <h4 className="text-lg font-semibold text-white mb-4">Acciones Rápidas</h4>
              <div className="space-y-3">
                <button 
                  onClick={() => navigate({ to: '/lobby' })}
                  className="w-full bg-emerald-600 hover:bg-emerald-700 text-white font-semibold py-3 rounded-xl transition-colors"
                >
                  Ir al Lobby
                </button>
                <button 
                  onClick={() => navigate({ to: '/home' })}
                  className="w-full bg-slate-700 hover:bg-slate-600 text-white font-semibold py-3 rounded-xl transition-colors"
                >
                  Página Principal
                </button>
              </div>
            </div>
          </div>
        </div>

        {/* Game History */}
        <div className="mt-12">
          <div className="bg-slate-800/50 rounded-2xl p-8 border border-slate-700">
            <h3 className="text-2xl font-bold text-white mb-6">Historial de Partidas</h3>
            
            <div className="overflow-hidden">
              <table className="w-full">
                <thead>
                  <tr className="border-b border-slate-700">
                    <th className="text-left py-3 text-slate-300 font-medium">Fecha</th>
                    <th className="text-left py-3 text-slate-300 font-medium">Mesa</th>
                    <th className="text-left py-3 text-slate-300 font-medium">Apuesta</th>
                    <th className="text-left py-3 text-slate-300 font-medium">Resultado</th>
                    <th className="text-right py-3 text-slate-300 font-medium">Ganancia</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-700">
                  <tr>
                    <td className="py-4 text-slate-400">Hace 2 horas</td>
                    <td className="py-4 text-white">Mesa VIP Diamante</td>
                    <td className="py-4 text-white">$150</td>
                    <td className="py-4">
                      <span className="px-3 py-1 bg-emerald-900 text-emerald-300 rounded-full text-sm font-semibold">
                        Victoria
                      </span>
                    </td>
                    <td className="py-4 text-right text-emerald-400 font-semibold">+$225</td>
                  </tr>
                  <tr>
                    <td className="py-4 text-slate-400">Hace 5 horas</td>
                    <td className="py-4 text-white">Mesa Gold</td>
                    <td className="py-4 text-white">$100</td>
                    <td className="py-4">
                      <span className="px-3 py-1 bg-red-900 text-red-300 rounded-full text-sm font-semibold">
                        Derrota
                      </span>
                    </td>
                    <td className="py-4 text-right text-red-400 font-semibold">-$100</td>
                  </tr>
                  <tr>
                    <td className="py-4 text-slate-400">Ayer</td>
                    <td className="py-4 text-white">Mesa Silver</td>
                    <td className="py-4 text-white">$50</td>
                    <td className="py-4">
                      <span className="px-3 py-1 bg-emerald-900 text-emerald-300 rounded-full text-sm font-semibold">
                        Victoria
                      </span>
                    </td>
                    <td className="py-4 text-right text-emerald-400 font-semibold">+$75</td>
                  </tr>
                </tbody>
              </table>
            </div>

            <div className="text-center mt-6">
              <button className="text-slate-400 hover:text-white transition-colors">
                Ver historial completo →
              </button>
            </div>
          </div>
        </div>
      </main>
    </div>
  )
}