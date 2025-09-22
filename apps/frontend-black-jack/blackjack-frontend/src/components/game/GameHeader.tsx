// src/components/game/GameHeader.tsx
import React from 'react'
import { authService } from '../../services/auth'

interface GameHeaderProps {
  roomName?: string
  playerCount: number
  maxPlayers: number
  isViewer: boolean
  isConnected: boolean
  onLeaveRoom: () => void
}

export default function GameHeader({
  roomName,
  playerCount,
  maxPlayers,
  isViewer,
  isConnected,
  onLeaveRoom
}: GameHeaderProps) {
  const handleLogout = () => {
    authService.logout()
  }

  return (
    <div className="absolute top-0 left-0 right-0 bg-black/60 px-6 py-3 flex justify-between items-center text-white">
      {/* Left Section - Navigation */}
      <div className="flex items-center gap-4">
        <button
          onClick={onLeaveRoom}
          className="bg-transparent border-none text-white text-base cursor-pointer hover:text-gray-300 transition-colors"
        >
          ← Volver al Lobby
        </button>
        <button
          onClick={handleLogout}
          className="bg-red-600/80 hover:bg-red-600 text-white text-sm px-3 py-1 rounded cursor-pointer transition-colors"
        >
          Cerrar Sesión
        </button>
      </div>
      
      {/* Center Section - Room Title */}
      <div className="flex items-center gap-2">
        <span className="text-xl">👑</span>
        <h1 className="m-0 text-xl font-bold">
          {roomName || 'Mesa VIP Diamante'}
        </h1>
      </div>
      
      {/* Right Section - Status */}
      <div className="text-sm flex items-center gap-4">
        <div>
          {isViewer ? 'Modo Viewer' : 'Modo Jugador'} • {playerCount}/{maxPlayers} jugadores
        </div>
        <div className="flex items-center gap-2">
          <div className={`w-2 h-2 rounded-full ${isConnected ? 'bg-green-400' : 'bg-red-400'}`}></div>
          <span>{isConnected ? 'Conectado' : 'Desconectado'}</span>
        </div>
      </div>
    </div>
  )
}