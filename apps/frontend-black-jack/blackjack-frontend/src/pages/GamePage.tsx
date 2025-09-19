// src/pages/GamePage.tsx - Versión de Producción
import React, { useEffect, useState, useCallback } from 'react'
import { useParams, useNavigate } from '@tanstack/react-router'
import { signalRService } from '../services/signalr'
import { authService } from '../services/auth'

interface GameState {
  id: string
  status: 'WaitingForPlayers' | 'InProgress' | 'Finished'
  players: Player[]
  minBet: number
  maxBet: number
  currentPlayerTurn?: string
}

interface Player {
  id: string
  displayName: string
  balance: number
  currentBet: number
  position: number
  isActive: boolean
  hand?: {
    cards: any[]
    handValue: number
    isBusted: boolean
    hasBlackjack: boolean
  }
}

interface ChatMessage {
  id: string
  playerName: string
  text: string
  timestamp: string
}

export default function GamePage() {
  const { tableId } = useParams({ strict: false }) as { tableId: string }
  const navigate = useNavigate()
  
  const [gameState, setGameState] = useState<GameState | null>(null)
  const [chatMessages, setChatMessages] = useState<ChatMessage[]>([])
  const [chatInput, setChatInput] = useState('')
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [isConnected, setIsConnected] = useState(false)
  
  const currentUser = authService.getCurrentUser()
  const currentPlayer = gameState?.players?.find(p => p.id === currentUser?.id)

  // Configurar event handlers de SignalR
  useEffect(() => {
    signalRService.onRoomInfo = (roomData) => {
      setGameState(roomData)
      setError(null)
    }

    signalRService.onPlayerJoined = (player) => {
      setGameState(prev => prev ? {
        ...prev,
        players: [...prev.players.filter(p => p.id !== player.id), player]
      } : null)
    }

    signalRService.onPlayerLeft = (player) => {
      setGameState(prev => prev ? {
        ...prev,
        players: prev.players.filter(p => p.id !== player.id)
      } : null)
    }

    signalRService.onGameStateChanged = (newState) => {
      setGameState(newState)
    }

    signalRService.onRoomJoined = (data) => {
      console.log('Successfully joined room')
      // Obtener información actualizada de la room
      if (tableId) {
        signalRService.getRoomInfo(tableId).catch(console.warn)
      }
    }

    return () => {
      // Limpiar event handlers
      signalRService.onRoomInfo = undefined
      signalRService.onPlayerJoined = undefined
      signalRService.onPlayerLeft = undefined
      signalRService.onGameStateChanged = undefined
      signalRService.onRoomJoined = undefined
    }
  }, [tableId])

  // Verificar conexión SignalR
  useEffect(() => {
    const checkConnection = () => {
      setIsConnected(signalRService.isGameConnected)
    }
    
    checkConnection()
    const interval = setInterval(checkConnection, 2000)
    
    return () => clearInterval(interval)
  }, [])

  // Inicializar game y conectar SignalR
  useEffect(() => {
    let isMounted = true
    
    const initializeGame = async () => {
      if (!tableId) return
      
      try {
        setIsLoading(true)
        setError(null)
        
        // Conectar SignalR si no está conectado
        if (!signalRService.isGameConnected) {
          const connected = await signalRService.startConnections()
          if (!connected && isMounted) {
            throw new Error('No se pudo conectar al servidor')
          }
        }

        // Esperar un momento para que se establezca la conexión
        await new Promise(resolve => setTimeout(resolve, 1000))
        
        if (signalRService.isGameConnected && isMounted) {
          // Unirse a la room
          await signalRService.joinRoom(tableId)
          
          // Obtener información inicial
          await signalRService.getRoomInfo(tableId)
        }
        
      } catch (e: any) {
        if (isMounted) {
          console.error('Error initializing game:', e)
          setError(e?.message || 'Error conectando a la mesa')
        }
      } finally {
        if (isMounted) {
          setIsLoading(false)
        }
      }
    }

    initializeGame()
    
    return () => {
      isMounted = false
      if (tableId) {
        signalRService.leaveRoom(tableId).catch(console.warn)
      }
    }
  }, [tableId])

  // Handlers para acciones del juego
  const handleJoinRoom = useCallback(async () => {
    if (!isConnected || !tableId) return
    try {
      setError(null)
      await signalRService.joinRoom(tableId)
    } catch (e: any) {
      setError(e?.message || 'Error al unirse a la mesa')
    }
  }, [isConnected, tableId])

  const handleLeaveRoom = useCallback(async () => {
    if (!isConnected || !tableId) return
    try {
      await signalRService.leaveRoom(tableId)
      navigate({ to: '/lobby' })
    } catch (e: any) {
      console.warn('Error leaving room:', e)
      // Navegar de todas formas
      navigate({ to: '/lobby' })
    }
  }, [isConnected, tableId, navigate])

  const handleRefreshRoom = useCallback(async () => {
    if (!isConnected || !tableId) return
    try {
      setError(null)
      await signalRService.getRoomInfo(tableId)
    } catch (e: any) {
      setError(e?.message || 'Error actualizando información')
    }
  }, [isConnected, tableId])

  const sendChatMessage = useCallback(() => {
    if (!chatInput.trim() || !currentUser) return
    
    // Por ahora solo agregar localmente - implementar SignalR chat después
    const newMessage: ChatMessage = {
      id: Date.now().toString(),
      playerName: currentUser.displayName,
      text: chatInput.trim(),
      timestamp: new Date().toISOString()
    }
    
    setChatMessages(prev => [...prev, newMessage])
    setChatInput('')
  }, [chatInput, currentUser])

  // Loading state
  if (isLoading) {
    return (
      <div className="fixed inset-0 bg-gradient-to-br from-emerald-900 to-emerald-800 flex items-center justify-center">
        <div className="bg-black/80 rounded-xl p-8 text-center">
          <div className="w-8 h-8 border-2 border-white border-t-transparent rounded-full animate-spin mx-auto mb-4"></div>
          <div className="text-white text-lg">Conectando a la mesa...</div>
          <div className="text-gray-300 text-sm mt-2">Mesa: {tableId?.slice(0, 8)}...</div>
        </div>
      </div>
    )
  }

  return (
    <div className="fixed inset-0 bg-gradient-to-br from-emerald-900 to-emerald-800 overflow-hidden">
      
      {/* Header */}
      <header className="absolute top-0 left-0 right-0 bg-black/60 px-6 py-3 flex justify-between items-center text-white z-10">
        <div className="flex items-center gap-4">
          <button
            onClick={handleLeaveRoom}
            className="hover:text-gray-300 transition-colors"
          >
            ← Lobby
          </button>
          <h1 className="text-xl font-bold">Mesa de BlackJack</h1>
        </div>
        
        <div className="flex items-center gap-4 text-sm">
          <span>Mesa: {tableId?.slice(0, 8)}...</span>
          <span>{gameState?.players?.length || 0}/6 jugadores</span>
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

      {/* Main Game Area */}
      <div className="absolute top-1/2 left-1/2 transform -translate-x-1/2 -translate-y-1/2 z-10">
        <GameStatusDisplay 
          gameState={gameState}
          isConnected={isConnected}
          onJoinRoom={handleJoinRoom}
          onLeaveRoom={handleLeaveRoom}
          onRefreshRoom={handleRefreshRoom}
        />
      </div>

      {/* Player Info */}
      {currentUser && (
        <div className="absolute bottom-8 left-1/2 transform -translate-x-1/2">
          <div className="bg-black/80 rounded-xl p-4 text-white text-center">
            <div className="font-bold text-lg">{currentUser.displayName}</div>
            <div className="text-green-400 font-semibold">${currentUser.balance.toLocaleString()}</div>
            {currentPlayer && (
              <div className="text-sm text-gray-300 mt-1">
                En la mesa • Posición {currentPlayer.position}
                {currentPlayer.currentBet > 0 && (
                  <span className="ml-2">• Apuesta: ${currentPlayer.currentBet}</span>
                )}
              </div>
            )}
          </div>
        </div>
      )}

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
            onKeyPress={e => e.key === 'Enter' && sendChatMessage()}
            placeholder="Escribe un mensaje..."
            className="flex-1 px-3 py-2 text-sm bg-slate-700 border border-slate-600 rounded text-white placeholder-slate-400 focus:outline-none focus:border-emerald-500"
            maxLength={100}
          />
          <button
            onClick={sendChatMessage}
            className="px-3 py-2 bg-emerald-600 hover:bg-emerald-700 rounded text-sm font-semibold transition-colors"
          >
            →
          </button>
        </div>
      </div>
    </div>
  )
}

// Componente para mostrar el estado del juego
function GameStatusDisplay({ 
  gameState, 
  isConnected,
  onJoinRoom,
  onLeaveRoom,
  onRefreshRoom
}: {
  gameState: GameState | null
  isConnected: boolean
  onJoinRoom: () => void
  onLeaveRoom: () => void
  onRefreshRoom: () => void
}) {
  // No conectado a SignalR
  if (!isConnected) {
    return (
      <div className="bg-red-600/90 backdrop-blur-sm border-2 border-red-400 rounded-xl p-6 text-center shadow-xl text-white">
        <div className="text-2xl font-bold mb-2">Conexión Perdida</div>
        <div className="mb-4">No hay conexión con el servidor</div>
        <button
          onClick={() => window.location.reload()}
          className="px-6 py-3 rounded-lg bg-white text-red-600 font-bold shadow-lg transition-all hover:bg-gray-100"
        >
          Reconectar
        </button>
      </div>
    )
  }

  // No hay información de la sala
  if (!gameState) {
    return (
      <div className="bg-blue-600/90 backdrop-blur-sm border-2 border-blue-400 rounded-xl p-6 text-center shadow-xl text-white">
        <div className="text-2xl font-bold mb-2">Cargando Mesa</div>
        <div className="mb-4">Obteniendo información de la sala...</div>
        <button
          onClick={onRefreshRoom}
          className="px-6 py-3 rounded-lg bg-white text-blue-600 font-bold shadow-lg transition-all hover:bg-gray-100"
        >
          Actualizar
        </button>
      </div>
    )
  }

  // Estado de espera
  if (gameState.status === 'WaitingForPlayers') {
    return (
      <div className="bg-blue-600/90 backdrop-blur-sm border-2 border-blue-400 rounded-xl p-6 text-center shadow-xl text-white">
        <div className="text-2xl font-bold mb-2">Sala de Juego</div>
        <div className="mb-4">
          {gameState.players.length}/6 jugadores en la mesa
        </div>
        <div className="space-y-3">
          <button
            onClick={onJoinRoom}
            className="block mx-auto px-6 py-3 rounded-lg bg-green-600 hover:bg-green-700 font-bold shadow-lg transition-all"
          >
            Unirse a la Mesa
          </button>
          <button
            onClick={onRefreshRoom}
            className="block mx-auto px-4 py-2 rounded-lg bg-blue-700 hover:bg-blue-800 font-semibold text-sm transition-all"
          >
            Actualizar Estado
          </button>
        </div>
      </div>
    )
  }

  // Partida en curso
  if (gameState.status === 'InProgress') {
    return (
      <div className="bg-green-600/90 backdrop-blur-sm border-2 border-green-400 rounded-xl p-6 text-center shadow-xl text-white">
        <div className="text-2xl font-bold mb-2">Partida en Curso</div>
        <div className="mb-2">El juego ha comenzado</div>
        {gameState.currentPlayerTurn && (
          <div className="mb-4 text-sm">
            Turno del jugador: {gameState.currentPlayerTurn}
          </div>
        )}
        <button
          onClick={onLeaveRoom}
          className="px-6 py-3 rounded-lg bg-red-600 hover:bg-red-700 font-bold shadow-lg transition-all"
        >
          Salir de la Mesa
        </button>
      </div>
    )
  }

  // Partida terminada
  if (gameState.status === 'Finished') {
    return (
      <div className="bg-yellow-600/90 backdrop-blur-sm border-2 border-yellow-400 rounded-xl p-6 text-center shadow-xl text-white">
        <div className="text-2xl font-bold mb-2">Partida Terminada</div>
        <div className="mb-4">La partida ha finalizado</div>
        <div className="space-y-3">
          <button
            onClick={onRefreshRoom}
            className="block mx-auto px-6 py-3 rounded-lg bg-blue-600 hover:bg-blue-700 font-bold shadow-lg transition-all"
          >
            Nueva Partida
          </button>
          <button
            onClick={onLeaveRoom}
            className="block mx-auto px-4 py-2 rounded-lg bg-gray-600 hover:bg-gray-700 font-semibold text-sm transition-all"
          >
            Volver al Lobby
          </button>
        </div>
      </div>
    )
  }

  // Estado por defecto
  return (
    <div className="bg-gray-600/90 backdrop-blur-sm border-2 border-gray-400 rounded-xl p-6 text-center shadow-xl text-white">
      <div className="text-2xl font-bold mb-2">Mesa de BlackJack</div>
      <div className="mb-4">Estado: {gameState.status}</div>
      <div className="space-y-2">
        <button
          onClick={onRefreshRoom}
          className="block mx-auto px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-700 font-semibold transition-all"
        >
          Actualizar
        </button>
        <button
          onClick={onLeaveRoom}
          className="block mx-auto px-4 py-2 rounded-lg bg-red-600 hover:bg-red-700 font-semibold transition-all"
        >
          Salir
        </button>
      </div>
    </div>
  )
}