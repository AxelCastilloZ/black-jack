// src/components/game/GameBettings.tsx - REDISEÑADO: Compacto y no invasivo
import React, { useState, useCallback, useEffect } from 'react'
import { AutoBetStatistics } from '../../services/signalr'

interface GameBettingsProps {
  isPlayerSeated: boolean
  gameStatus?: 'WaitingForPlayers' | 'InProgress' | 'Finished'
  isViewer: boolean
  currentPlayerBalance: number
  isPlayerTurn: boolean
  roomCode?: string
  
  autoBettingState: {
    isActive: boolean
    isProcessing: boolean
    statistics: AutoBetStatistics | null
    lastProcessedResult: any
    processingStartedAt: Date | null
    roundSummary: any
  }
  minBetPerRound?: number
  onProcessAutoBets: () => Promise<void>
  onRefreshStatistics: () => Promise<void>
  gameControlConnected: boolean
}

function formatMoney(amount: number): string {
  return `$${amount.toLocaleString()}`
}

export default function GameBettings({
  isPlayerSeated,
  gameStatus,
  isViewer,
  currentPlayerBalance,
  isPlayerTurn,
  roomCode,
  autoBettingState,
  minBetPerRound = 0,
  onProcessAutoBets,
  onRefreshStatistics,
  gameControlConnected
}: GameBettingsProps) {
  const [isExpanded, setIsExpanded] = useState(false)
  const [manualBetAmount, setManualBetAmount] = useState(minBetPerRound || 50)

  const handleProcessAutoBets = useCallback(async () => {
    if (!gameControlConnected || !roomCode || autoBettingState.isProcessing) return
    
    try {
      await onProcessAutoBets()
    } catch (error) {
      console.error('[GameBettings] Error processing auto-bets:', error)
    }
  }, [gameControlConnected, roomCode, autoBettingState.isProcessing, onProcessAutoBets])

  const canAffordAutoBet = currentPlayerBalance >= minBetPerRound
  const estimatedRounds = minBetPerRound > 0 ? Math.floor(currentPlayerBalance / minBetPerRound) : 0
  const fundingGap = Math.max(0, minBetPerRound - currentPlayerBalance)
  
  const stats = autoBettingState.statistics
  const lastResult = autoBettingState.lastProcessedResult

  if (isViewer) {
    return (
      <div className="fixed top-20 left-4 z-10">
        {/* Mini viewer widget */}
        <div className="bg-slate-800/90 backdrop-blur border border-slate-600 rounded-lg p-3 shadow-lg">
          <div className="flex items-center gap-2 text-blue-400 text-sm mb-2">
            <span>👁️</span>
            <span className="font-medium">Espectador</span>
          </div>
          
          {autoBettingState.isActive && minBetPerRound > 0 && (
            <div className="space-y-1 text-xs">
              <div className="flex justify-between">
                <span className="text-slate-300">Por ronda:</span>
                <span className="text-white font-semibold">{formatMoney(minBetPerRound)}</span>
              </div>
              
              {stats && (
                <div className="flex justify-between">
                  <span className="text-slate-300">Sentados:</span>
                  <span className="text-emerald-400">{stats.seatedPlayersCount}</span>
                </div>
              )}

              {autoBettingState.isProcessing && (
                <div className="flex items-center gap-1 text-yellow-400 text-xs">
                  <div className="w-3 h-3 border border-yellow-400 border-t-transparent rounded-full animate-spin"></div>
                  <span>Procesando...</span>
                </div>
              )}
            </div>
          )}
        </div>
      </div>
    )
  }

  return (
    <div className="fixed top-20 left-4 z-10">
      {/* Mini widget compacto */}
      <div 
        className={`bg-slate-800/95 backdrop-blur border border-slate-600 rounded-lg shadow-lg transition-all duration-300 ${
          isExpanded ? 'w-80' : 'w-64'
        }`}
        onMouseEnter={() => setIsExpanded(true)}
        onMouseLeave={() => setIsExpanded(false)}
      >
        {/* Header compacto */}
        <div className="p-3 border-b border-slate-600">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <span className="text-lg">
                {autoBettingState.isActive ? '🎰' : '💰'}
              </span>
              <div>
                <div className="text-white font-medium text-sm">
                  {autoBettingState.isActive ? 'Auto-Betting' : 'Apuestas'}
                </div>
                <div className="text-emerald-400 font-bold text-xs">
                  {formatMoney(currentPlayerBalance)}
                </div>
              </div>
            </div>
            
            <div className="flex items-center gap-1">
              {isPlayerSeated ? (
                <span className="w-2 h-2 bg-green-400 rounded-full"></span>
              ) : (
                <span className="w-2 h-2 bg-orange-400 rounded-full"></span>
              )}
              {!gameControlConnected && (
                <span className="w-2 h-2 bg-red-400 rounded-full"></span>
              )}
            </div>
          </div>
        </div>

        {/* Auto-betting status compacto */}
        {autoBettingState.isActive && (
          <div className="p-3 bg-purple-900/20">
            <div className="grid grid-cols-2 gap-2 mb-2">
              <div className="text-center">
                <div className="text-purple-300 font-bold">
                  {formatMoney(minBetPerRound)}
                </div>
                <div className="text-xs text-slate-400">Por Ronda</div>
              </div>
              <div className="text-center">
                <div className={`font-bold ${canAffordAutoBet ? 'text-green-400' : 'text-red-400'}`}>
                  {estimatedRounds}
                </div>
                <div className="text-xs text-slate-400">Rondas</div>
              </div>
            </div>

            {/* Estado en una línea */}
            <div className={`text-center text-xs px-2 py-1 rounded ${
              canAffordAutoBet 
                ? 'bg-green-900/30 text-green-300'
                : 'bg-red-900/30 text-red-300'
            }`}>
              {canAffordAutoBet ? '✓ Listo' : `❌ Faltan ${formatMoney(fundingGap)}`}
            </div>

            {/* Estado de procesamiento */}
            {autoBettingState.isProcessing && (
              <div className="mt-2 flex items-center gap-2 text-yellow-300 text-xs">
                <div className="w-3 h-3 border border-yellow-400 border-t-transparent rounded-full animate-spin"></div>
                <span>Procesando...</span>
              </div>
            )}
          </div>
        )}

        {/* Panel expandido (solo en hover) */}
        {isExpanded && (
          <div className="p-3 border-t border-slate-600">
            {autoBettingState.isActive ? (
              <div className="space-y-3">
                {/* Controles */}
                <div className="flex gap-2">
                  <button
                    onClick={handleProcessAutoBets}
                    disabled={!gameControlConnected || autoBettingState.isProcessing || !isPlayerSeated}
                    className="flex-1 px-3 py-2 bg-purple-600 hover:bg-purple-700 disabled:bg-slate-600 disabled:opacity-50 text-white font-medium rounded text-sm transition-colors"
                  >
                    {autoBettingState.isProcessing ? 'Procesando...' : '🎰 Procesar'}
                  </button>

                  <button
                    onClick={onRefreshStatistics}
                    disabled={!gameControlConnected}
                    className="px-3 py-2 bg-slate-700 hover:bg-slate-600 disabled:opacity-50 text-white rounded text-sm transition-colors"
                  >
                    ↻
                  </button>
                </div>

                {/* Estadísticas rápidas */}
                {stats && (
                  <div className="grid grid-cols-2 gap-2 text-xs">
                    <div className="text-center p-2 bg-slate-700/30 rounded">
                      <div className="text-green-400 font-semibold">{stats.playersWithSufficientFunds}</div>
                      <div className="text-slate-400">Con fondos</div>
                    </div>
                    <div className="text-center p-2 bg-slate-700/30 rounded">
                      <div className="text-red-400 font-semibold">{stats.playersWithInsufficientFunds}</div>
                      <div className="text-slate-400">Sin fondos</div>
                    </div>
                  </div>
                )}

                {/* Último resultado */}
                {lastResult && !autoBettingState.isProcessing && (
                  <div className="p-2 bg-slate-700/30 rounded">
                    <div className="text-xs text-slate-300 mb-1">Último resultado:</div>
                    <div className="flex justify-between text-xs">
                      <span className="text-green-400">{lastResult.successfulBets} ✓</span>
                      <span className="text-red-400">{lastResult.failedBets} ❌</span>
                      <span className="text-white">{formatMoney(lastResult.totalAmountProcessed)}</span>
                    </div>
                  </div>
                )}

                {!isPlayerSeated && (
                  <div className="text-center text-xs text-orange-400">
                    Únete a un asiento para participar
                  </div>
                )}
              </div>
            ) : (
              /* Controles manuales */
              <div className="space-y-2">
                <div className="flex items-center gap-2">
                  <input
                    type="number"
                    value={manualBetAmount}
                    onChange={(e) => setManualBetAmount(Number(e.target.value))}
                    min={10}
                    max={currentPlayerBalance}
                    className="flex-1 px-2 py-1 bg-slate-700 border border-slate-600 rounded text-white text-sm focus:outline-none focus:border-emerald-500"
                  />
                  <span className="text-slate-400 text-xs">$</span>
                </div>

                <button
                  disabled={!isPlayerSeated || !isPlayerTurn || manualBetAmount > currentPlayerBalance}
                  className="w-full px-3 py-2 bg-emerald-600 hover:bg-emerald-700 disabled:bg-slate-600 disabled:opacity-50 text-white font-medium rounded text-sm transition-colors"
                >
                  {!isPlayerSeated ? 'Únete a Asiento' : 
                   !isPlayerTurn ? 'Espera tu Turno' :
                   `Apostar ${formatMoney(manualBetAmount)}`}
                </button>
              </div>
            )}
          </div>
        )}

        {/* Footer minimalista */}
        <div className="px-3 py-2 border-t border-slate-600 flex items-center justify-between text-xs text-slate-400">
          <div className="flex items-center gap-1">
            <div className={`w-1.5 h-1.5 rounded-full ${gameControlConnected ? 'bg-green-400' : 'bg-red-400'}`}></div>
            <span>{gameControlConnected ? 'Online' : 'Offline'}</span>
          </div>
          {!isExpanded && (
            <span className="text-slate-500">Hover para más</span>
          )}
        </div>
      </div>
    </div>
  )
}