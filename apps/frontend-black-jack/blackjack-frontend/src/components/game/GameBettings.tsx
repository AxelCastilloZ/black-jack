// src/components/game/GameBettings.tsx
import React, { useState, useCallback } from 'react'

interface GameBettingsProps {
  isPlayerSeated: boolean
  gameStatus?: 'WaitingForPlayers' | 'InProgress' | 'Finished'
  isViewer: boolean
  currentPlayerBalance?: number
  isPlayerTurn?: boolean
  roomCode?: string
}

export default function GameBettings({
  isPlayerSeated,
  gameStatus,
  isViewer,
  currentPlayerBalance = 1000,
  isPlayerTurn = false,
  roomCode
}: GameBettingsProps) {
  const [selectedBet, setSelectedBet] = useState<number>(0)
  const [customBetInput, setCustomBetInput] = useState<string>('')

  const predefinedBets = [10, 25, 50, 100, 250, 500]
  const isGameActive = gameStatus === 'InProgress'
  const canBet = isPlayerSeated && !isViewer && isGameActive && isPlayerTurn

  const handleQuickBet = useCallback((amount: number) => {
    if (!canBet) return
    setSelectedBet(amount)
    console.log(`[GameBettings] Quick bet selected: $${amount}`)
  }, [canBet])

  const handleCustomBet = useCallback(() => {
    const amount = parseInt(customBetInput)
    if (isNaN(amount) || amount <= 0 || amount > currentPlayerBalance || !canBet) return
    
    setSelectedBet(amount)
    setCustomBetInput('')
    console.log(`[GameBettings] Custom bet selected: $${amount}`)
  }, [customBetInput, currentPlayerBalance, canBet])

  const handlePlaceBet = useCallback(() => {
    if (!selectedBet || !canBet || !roomCode) return
    
    console.log(`[GameBettings] Placing bet: $${selectedBet} in room ${roomCode}`)
    // TODO: Implementar signalRService.placeBet(roomCode, selectedBet)
    setSelectedBet(0)
  }, [selectedBet, canBet, roomCode])

  const handleClearBet = useCallback(() => {
    setSelectedBet(0)
    setCustomBetInput('')
  }, [])

  // No mostrar el panel si no es un juego activo o si es espectador
  if (!isPlayerSeated || isViewer || gameStatus !== 'InProgress') {
    return null
  }

  return (
    <div className="absolute bottom-5 left-5 bg-black/80 rounded-lg p-4 min-w-[280px] text-white">
      <div className="text-amber-400 font-bold text-sm mb-3 flex justify-between items-center">
        <span>Apuestas</span>
        <span className="text-emerald-400 text-xs">Balance: ${currentPlayerBalance}</span>
      </div>

      {/* Quick Bet Buttons */}
      <div className="grid grid-cols-3 gap-2 mb-3">
        {predefinedBets.map(amount => (
          <button
            key={amount}
            onClick={() => handleQuickBet(amount)}
            disabled={!canBet || amount > currentPlayerBalance}
            className={`px-2 py-1 text-xs rounded transition-colors ${
              selectedBet === amount
                ? 'bg-emerald-600 text-white'
                : canBet && amount <= currentPlayerBalance
                ? 'bg-gray-600 hover:bg-gray-500 text-gray-200'
                : 'bg-gray-700 text-gray-500 cursor-not-allowed'
            }`}
          >
            ${amount}
          </button>
        ))}
      </div>

      {/* Custom Bet Input */}
      <div className="flex gap-2 mb-3">
        <input
          type="number"
          value={customBetInput}
          onChange={e => setCustomBetInput(e.target.value)}
          placeholder="Cantidad..."
          disabled={!canBet}
          min="1"
          max={currentPlayerBalance}
          className="flex-1 px-2 py-1 text-xs bg-slate-700 border border-slate-600 rounded text-white placeholder-slate-400 disabled:opacity-50"
        />
        <button
          onClick={handleCustomBet}
          disabled={!canBet || !customBetInput}
          className="px-2 py-1 bg-blue-600 hover:bg-blue-700 disabled:bg-gray-600 disabled:cursor-not-allowed rounded text-xs transition-colors"
        >
          Set
        </button>
      </div>

      {/* Selected Bet Display */}
      {selectedBet > 0 && (
        <div className="bg-emerald-600/20 border border-emerald-600 rounded p-2 mb-3">
          <div className="text-emerald-400 text-xs font-bold">
            Apuesta seleccionada: ${selectedBet}
          </div>
        </div>
      )}

      {/* Action Buttons */}
      <div className="flex gap-2">
        <button
          onClick={handlePlaceBet}
          disabled={!selectedBet || !canBet}
          className="flex-1 bg-emerald-600 hover:bg-emerald-700 disabled:bg-gray-600 disabled:cursor-not-allowed text-white px-3 py-2 rounded text-sm font-semibold transition-colors"
        >
          {isPlayerTurn ? 'Apostar' : 'Esperando turno...'}
        </button>
        
        <button
          onClick={handleClearBet}
          disabled={!selectedBet}
          className="bg-red-600 hover:bg-red-700 disabled:bg-gray-600 disabled:cursor-not-allowed text-white px-3 py-2 rounded text-sm transition-colors"
        >
          Limpiar
        </button>
      </div>

      {/* Status Messages */}
      {!canBet && isPlayerSeated && (
        <div className="text-gray-400 text-xs mt-2 text-center">
          {!isPlayerTurn ? 'Esperando tu turno para apostar' : 'No puedes apostar en este momento'}
        </div>
      )}
    </div>
  )
}