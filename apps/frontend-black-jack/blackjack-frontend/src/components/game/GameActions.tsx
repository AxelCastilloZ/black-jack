// src/components/game/GameActions.tsx
import React from 'react'

interface GameActionsProps {
  isPlayerTurn: boolean
  canHit: boolean
  canStand: boolean
  isGameActive: boolean
  onHit: () => void
  onStand: () => void
  className?: string
}

export default function GameActions({
  isPlayerTurn,
  canHit,
  canStand,
  isGameActive,
  onHit,
  onStand,
  className = ''
}: GameActionsProps) {
  // Don't show actions if game is not active or not player's turn
  if (!isGameActive || !isPlayerTurn) {
    return null
  }

  return (
    <div className={`flex gap-3 ${className}`}>
      <button
        onClick={onHit}
        disabled={!canHit}
        className={`px-6 py-3 rounded-lg font-bold text-white transition-all ${
          canHit
            ? 'bg-green-600 hover:bg-green-700 shadow-lg hover:shadow-xl'
            : 'bg-gray-600 cursor-not-allowed opacity-50'
        }`}
      >
        Hit
      </button>
      
      <button
        onClick={onStand}
        disabled={!canStand}
        className={`px-6 py-3 rounded-lg font-bold text-white transition-all ${
          canStand
            ? 'bg-red-600 hover:bg-red-700 shadow-lg hover:shadow-xl'
            : 'bg-gray-600 cursor-not-allowed opacity-50'
        }`}
      >
        Stand
      </button>
    </div>
  )
}

