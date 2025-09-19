// src/pages/GamePage.tsx - CORREGIDO TODOS LOS ERRORES DE TYPESCRIPT
import React, { useEffect, useState, useCallback } from 'react'
import { useParams, useNavigate } from '@tanstack/react-router'
import { signalRService, type GameState, type ChatMessage } from '../services/signalr'
import { authService } from '../services/auth'
import { useSignalR } from '../hooks/useSignalR'

export default function GamePage() {
  const { tableId } = useParams({ strict: false }) as { tableId: string }
  const navigate = useNavigate()
  const { isConnected } = useSignalR()
  
  const [gameState, setGameState] = useState<GameState | null>(null)
  const [chatMessages, setChatMessages] = useState<ChatMessage[]>([])
  const [chatInput, setChatInput] = useState('')
  const [isJoining, setIsJoining] = useState(false)
  const [error, setError] = useState<string | null>(null)
  
  const currentUser = authService.getCurrentUser()
  const currentPlayer = gameState?.players?.find(p => p.id === currentUser?.id)

  // Configurar callbacks de SignalR
  useEffect(() => {
    signalRService.onGameStateUpdate = (state: GameState) => {
      console.log('🎮 Game state actualizado:', state)
      setGameState(state)
    }

    signalRService.onChatMessage = (message: ChatMessage) => {
      setChatMessages(prev => [...prev.slice(-9), message])
    }

    return () => {
      signalRService.onGameStateUpdate = undefined
      signalRService.onChatMessage = undefined
    }
  }, [])

  // Conectar y unirse a la mesa
  useEffect(() => {
    let isMounted = true
    
    const connectToTable = async () => {
      if (!tableId || !isConnected) return
      
      setIsJoining(true)
      setError(null)
      
      try {
        console.log('🎯 Uniéndose a mesa:', tableId)
        await signalRService.joinTable(tableId)
        console.log('✅ Conectado a mesa exitosamente')
      } catch (e: any) {
        if (isMounted) {
          console.error('❌ Error conectando:', e)
          setError(e?.message || 'Error conectando a la mesa')
        }
      } finally {
        if (isMounted) {
          setIsJoining(false)
        }
      }
    }

    connectToTable()
    
    return () => {
      isMounted = false
    }
  }, [tableId, isConnected])

  // Cleanup al salir
  useEffect(() => {
    return () => {
      if (tableId) {
        signalRService.leaveTable(tableId).catch(console.warn)
      }
    }
  }, [tableId])

  // Handlers para acciones de juego
  const handleJoinSeat = useCallback(async (position: number) => {
    if (!isConnected) return
    try {
      setError(null)
      await signalRService.joinSeat(tableId, position)
    } catch (e: any) {
      setError(e?.message || 'Error al unirse al asiento')
    }
  }, [isConnected, tableId])

  const handleLeaveSeat = useCallback(async () => {
    if (!isConnected) return
    try {
      setError(null)
      await signalRService.leaveSeat(tableId)
    } catch (e: any) {
      setError(e?.message || 'Error al dejar el asiento')
    }
  }, [isConnected, tableId])

  const handlePlaceBet = useCallback(async (amount: number) => {
    if (!isConnected) return
    try {
      setError(null)
      await signalRService.placeBet(tableId, amount)
    } catch (e: any) {
      setError(e?.message || 'Error al apostar')
    }
  }, [isConnected, tableId])

  const handleStartRound = useCallback(async () => {
    if (!isConnected) return
    try {
      setError(null)
      await signalRService.startRound(tableId)
    } catch (e: any) {
      setError(e?.message || 'Error al iniciar ronda')
    }
  }, [isConnected, tableId])

  const handleSendMessage = useCallback(async () => {
    if (!chatInput.trim() || !isConnected) return
    try {
      await signalRService.sendChatMessage(tableId, chatInput.trim())
      setChatInput('')
    } catch (e: any) {
      console.warn('Error sending message:', e)
    }
  }, [chatInput, isConnected, tableId])

  const handleGameAction = useCallback(async (action: 'hit' | 'stand' | 'double') => {
    if (!isConnected) return
    try {
      setError(null)
      switch (action) {
        case 'hit':
          await signalRService.hit(tableId)
          break
        case 'stand':
          await signalRService.stand(tableId)
          break
        case 'double':
          await signalRService.doubleDown(tableId)
          break
      }
    } catch (e: any) {
      setError(e?.message || `Error al ${action}`)
    }
  }, [isConnected, tableId])

  // Loading state
  if (isJoining || !gameState) {
    return (
      <div className="fixed inset-0 bg-gradient-to-br from-emerald-900 to-emerald-800 flex items-center justify-center">
        <div className="bg-black/80 rounded-xl p-8 text-center">
          <div className="w-8 h-8 border-2 border-white border-t-transparent rounded-full animate-spin mx-auto mb-4"></div>
          <div className="text-white text-lg">
            {isJoining ? 'Conectando a la mesa...' : 'Cargando juego...'}
          </div>
        </div>
      </div>
    )
  }

  // Variables calculadas con tipos seguros
  const isMyTurn = Boolean(currentPlayer?.isActive && gameState.status === 'InProgress')
  const canStartGame = gameState.status === 'WaitingForPlayers' && gameState.players.length >= 2

  return (
    <div className="fixed inset-0 bg-gradient-to-br from-emerald-900 to-emerald-800 overflow-hidden">
      
      {/* Header */}
      <header className="absolute top-0 left-0 right-0 bg-black/60 px-6 py-3 flex justify-between items-center text-white z-10">
        <div className="flex items-center gap-4">
          <button
            onClick={() => navigate({ to: '/lobby' })}
            className="hover:text-gray-300 transition-colors"
          >
            ← Lobby
          </button>
          <h1 className="text-xl font-bold">Mesa de BlackJack</h1>
        </div>
        
        <div className="flex items-center gap-4 text-sm">
          <span>{gameState.players.length}/6 jugadores</span>
          <div className="flex items-center gap-2">
            <div className={`w-2 h-2 rounded-full ${isConnected ? 'bg-green-400' : 'bg-red-400'}`}></div>
            <span>{isConnected ? 'Conectado' : 'Desconectado'}</span>
          </div>
        </div>
      </header>

      {/* Error Banner */}
      {error && (
        <div className="absolute top-16 left-1/2 transform -translate-x-1/2 bg-red-600/90 text-white px-6 py-3 rounded-lg shadow-lg z-20">
          <div className="flex items-center gap-2">
            <span>⚠️</span>
            <span>{error}</span>
            <button 
              onClick={() => setError(null)}
              className="ml-2 text-red-200 hover:text-white"
            >
              ✕
            </button>
          </div>
        </div>
      )}

      {/* Dealer Area */}
      <div className="absolute top-20 left-1/2 transform -translate-x-1/2 text-center z-10">
        <div className="w-16 h-16 rounded-full bg-amber-400 flex items-center justify-center text-black text-2xl font-bold mx-auto mb-2">
          D
        </div>
        <div className="text-amber-400 font-bold">Dealer</div>
        
        {gameState.dealer?.hand && gameState.dealer.hand.length > 0 && (
          <div className="mt-2 bg-black/50 rounded px-3 py-2 text-white text-sm">
            <div>Cartas: {gameState.dealer.hand.length}</div>
            {gameState.dealer.handValue !== undefined && (
              <div>Valor: {gameState.dealer.handValue}</div>
            )}
          </div>
        )}
      </div>

      {/* Game Status Banner */}
      <div className="absolute top-1/2 left-1/2 transform -translate-x-1/2 -translate-y-1/2 z-10">
        <GameStatusBanner 
          gameState={gameState}
          isMyTurn={isMyTurn}
          canStartGame={canStartGame}
          onStartRound={handleStartRound}
          onGameAction={handleGameAction}
        />
      </div>

      {/* Player Positions */}
      <div className="absolute bottom-8 left-1/2 transform -translate-x-1/2">
        <div className="flex justify-center items-end gap-8" style={{ width: '800px' }}>
          {[1, 2, 3, 4, 5].map(position => {
            const playerAtPosition = gameState.players.find(p => p.position === position)
            return (
              <PlayerSeat
                key={position}
                position={position}
                player={playerAtPosition}
                isCurrentUser={Boolean(playerAtPosition?.id === currentUser?.id)}
                isCurrentUserSeated={Boolean(currentPlayer)}
                gameStatus={gameState.status}
                onJoinSeat={() => handleJoinSeat(position)}
                onLeaveSeat={handleLeaveSeat}
                onPlaceBet={handlePlaceBet}
              />
            )
          })}
        </div>
      </div>

      {/* Chat */}
      <div className="absolute bottom-4 right-4 bg-black/80 rounded-lg p-4 w-80 text-white z-10">
        <div className="text-amber-400 font-bold text-sm mb-2">Chat de Mesa</div>
        
        <div className="h-32 overflow-y-auto mb-3 text-sm">
          {chatMessages.length === 0 ? (
            <div className="text-gray-400">Sin mensajes...</div>
          ) : (
            chatMessages.slice(-10).map((msg, idx) => (
              <div key={idx} className="mb-1">
                <span className="text-amber-300 font-semibold">{msg.playerName}:</span>
                <span className="ml-2">{msg.text}</span>
              </div>
            ))
          )}
        </div>
        
        <div className="flex gap-2">
          <input
            type="text"
            value={chatInput}
            onChange={e => setChatInput(e.target.value)}
            onKeyPress={e => e.key === 'Enter' && handleSendMessage()}
            placeholder="Escribe un mensaje..."
            className="flex-1 px-3 py-2 text-sm bg-slate-700 border border-slate-600 rounded text-white placeholder-slate-400 focus:outline-none focus:border-emerald-500"
            maxLength={100}
          />
          <button
            onClick={handleSendMessage}
            className="px-3 py-2 bg-emerald-600 hover:bg-emerald-700 rounded text-sm font-semibold transition-colors"
          >
            →
          </button>
        </div>
      </div>
    </div>
  )
}

// Componente para el banner central del estado del juego
function GameStatusBanner({ 
  gameState, 
  isMyTurn, 
  canStartGame, 
  onStartRound, 
  onGameAction 
}: {
  gameState: GameState
  isMyTurn: boolean
  canStartGame: boolean
  onStartRound: () => void
  onGameAction: (action: 'hit' | 'stand' | 'double') => void
}) {
  // Estado de espera
  if (gameState.status === 'WaitingForPlayers') {
    return (
      <div className="bg-blue-600/90 backdrop-blur-sm border-2 border-blue-400 rounded-xl p-6 text-center shadow-xl text-white">
        <div className="text-2xl font-bold mb-2">Esperando Jugadores</div>
        <div className="mb-4">
          {gameState.players.length}/6 jugadores en la mesa
        </div>
        {canStartGame ? (
          <button
            onClick={onStartRound}
            className="px-6 py-3 rounded-lg bg-green-600 hover:bg-green-700 font-bold shadow-lg transition-all"
          >
            🎲 Iniciar Partida
          </button>
        ) : (
          <div className="text-blue-200">Se necesitan mínimo 2 jugadores</div>
        )}
      </div>
    )
  }

  // Turno del jugador
  if (isMyTurn) {
    return (
      <div className="bg-yellow-600/90 backdrop-blur-sm border-2 border-yellow-400 rounded-xl p-6 text-center shadow-xl text-black">
        <div className="text-2xl font-bold mb-4">¡ES TU TURNO!</div>
        <div className="flex justify-center gap-3">
          <button
            onClick={() => onGameAction('hit')}
            className="px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-700 text-white font-bold transition-all"
          >
            Pedir Carta
          </button>
          <button
            onClick={() => onGameAction('stand')}
            className="px-4 py-2 rounded-lg bg-red-600 hover:bg-red-700 text-white font-bold transition-all"
          >
            Plantarse
          </button>
          <button
            onClick={() => onGameAction('double')}
            className="px-4 py-2 rounded-lg bg-purple-600 hover:bg-purple-700 text-white font-bold transition-all"
          >
            Doblar
          </button>
        </div>
      </div>
    )
  }

  // Partida en curso
  if (gameState.status === 'InProgress') {
    return (
      <div className="bg-green-600/90 backdrop-blur-sm border-2 border-green-400 rounded-xl p-4 text-center shadow-xl text-white">
        <div className="text-xl font-bold">Partida en Curso</div>
        <div className="text-green-200">Esperando jugadores...</div>
      </div>
    )
  }

  return null
}

// Componente para cada posición de jugador
function PlayerSeat({ 
  position, 
  player, 
  isCurrentUser,
  isCurrentUserSeated,
  gameStatus,
  onJoinSeat, 
  onLeaveSeat,
  onPlaceBet
}: {
  position: number
  player?: any
  isCurrentUser: boolean
  isCurrentUserSeated: boolean
  gameStatus: string
  onJoinSeat: () => void
  onLeaveSeat: () => void
  onPlaceBet: (amount: number) => void
}) {
  const [showBetButtons, setShowBetButtons] = useState(false)

  // Asiento vacío
  if (!player) {
    return (
      <div className="flex flex-col items-center">
        <button
          onClick={onJoinSeat}
          disabled={isCurrentUserSeated}
          className={`w-16 h-16 rounded-full border-2 border-dashed flex items-center justify-center mb-2 transition-all ${
            isCurrentUserSeated 
              ? 'border-gray-500 text-gray-500 cursor-not-allowed' 
              : 'border-green-400 text-green-400 hover:bg-green-400/20'
          }`}
        >
          {isCurrentUserSeated ? position : '+'}
        </button>
        <div className="text-sm text-gray-400">
          {isCurrentUserSeated ? 'Ocupado' : 'Unirse'}
        </div>
      </div>
    )
  }

  // Jugador sentado
  return (
    <div className="flex flex-col items-center">
      {/* Avatar del jugador */}
      <div className={`w-16 h-16 rounded-full flex items-center justify-center mb-2 border-2 relative ${
        player.isActive ? 'border-yellow-400 bg-yellow-900/40 shadow-lg' : 
        isCurrentUser ? 'border-blue-400 bg-blue-900/40' : 
        'border-green-400 bg-green-800/40'
      }`}>
        <div className="w-12 h-12 rounded-full bg-white flex items-center justify-center">
          <span className="text-sm font-bold text-gray-800">
            {isCurrentUser ? 'TÚ' : player.displayName.slice(0, 2).toUpperCase()}
          </span>
        </div>
        
        {/* Indicador de turno */}
        {player.isActive && (
          <div className="absolute -top-1 -right-1 w-4 h-4 bg-yellow-400 rounded-full animate-pulse"></div>
        )}
      </div>

      {/* Info del jugador */}
      <div className="text-center text-white">
        <div className={`font-bold text-sm ${isCurrentUser ? 'text-blue-300' : 'text-white'}`}>
          {isCurrentUser ? 'Tú' : player.displayName}
        </div>
        <div className="text-xs text-green-300">
          ${player.balance?.toLocaleString()}
        </div>
        
        {/* Apuesta actual */}
        {player.currentBet > 0 && (
          <div className="mt-1 px-2 py-1 bg-yellow-600 rounded text-xs font-bold text-black">
            ${player.currentBet}
          </div>
        )}
      </div>

      {/* Cartas */}
      {player.hand?.cards && player.hand.cards.length > 0 && (
        <div className="mt-2 text-center">
          <div className="text-xs text-white mb-1">
            Valor: {player.hand.handValue}
          </div>
          {player.hand.isBusted && (
            <div className="text-xs text-red-400 font-bold">BUST!</div>
          )}
          {player.hand.hasBlackjack && (
            <div className="text-xs text-yellow-400 font-bold">BLACKJACK!</div>
          )}
        </div>
      )}

      {/* Acciones del usuario actual */}
      {isCurrentUser && (
        <div className="mt-2 space-y-2">
          {/* Botones de apuesta */}
          {gameStatus === 'WaitingForPlayers' && !player.currentBet && (
            <div>
              {!showBetButtons ? (
                <button
                  onClick={() => setShowBetButtons(true)}
                  className="px-3 py-1 rounded bg-blue-600 hover:bg-blue-700 text-white text-xs font-semibold"
                >
                  Apostar
                </button>
              ) : (
                <div className="flex gap-1">
                  <button onClick={() => onPlaceBet(25)} className="px-2 py-1 rounded bg-green-600 hover:bg-green-700 text-white text-xs">$25</button>
                  <button onClick={() => onPlaceBet(50)} className="px-2 py-1 rounded bg-green-600 hover:bg-green-700 text-white text-xs">$50</button>
                  <button onClick={() => onPlaceBet(100)} className="px-2 py-1 rounded bg-green-600 hover:bg-green-700 text-white text-xs">$100</button>
                  <button onClick={() => setShowBetButtons(false)} className="px-2 py-1 rounded bg-gray-600 hover:bg-gray-700 text-white text-xs">×</button>
                </div>
              )}
            </div>
          )}
          
          {/* Botón de salir */}
          {gameStatus !== 'InProgress' && (
            <button
              onClick={onLeaveSeat}
              className="px-3 py-1 rounded bg-red-600 hover:bg-red-700 text-white text-xs font-semibold"
            >
              Salir
            </button>
          )}
        </div>
      )}
    </div>
  )
}