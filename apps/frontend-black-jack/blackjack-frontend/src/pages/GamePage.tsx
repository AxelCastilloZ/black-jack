// src/pages/GamePage.tsx
import React, { useState, useEffect, useRef } from 'react'
import { useParams, useNavigate } from '@tanstack/react-router'
import { signalRService, type GameState, type ChatMessage } from '../services/signalr'
import { authService } from '../services/auth'
import BlackjackTable from '../components/game/BlackJackTable' // asegúrate del nombre del archivo

export default function GamePage() {
  const { tableId } = useParams({ strict: false }) as { tableId: string }
  const navigate = useNavigate()

  const [gameState, setGameState] = useState<GameState | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [chatMessages, setChatMessages] = useState<ChatMessage[]>([])
  const [chatInput, setChatInput] = useState('')

  // Refs para evitar re-inicializaciones
  const isInitializedRef = useRef(false)
  const cleanupRef = useRef<(() => void) | null>(null)

  const currentUser = authService.getCurrentUser()

  useEffect(() => {
    if (isInitializedRef.current) return

    let timeoutId: NodeJS.Timeout | null = null

    const initializeGame = async () => {
      if (!currentUser) {
        setError('No estás autenticado')
        setLoading(false)
        return
      }

      if (!tableId) {
        setError('ID de mesa inválido')
        setLoading(false)
        return
      }

      try {
        setLoading(true)
        setError(null)

        // Timeout de seguridad
        timeoutId = setTimeout(() => {
          setGameState({
            id: tableId,
            status: 'WaitingForPlayers',
            players: [],
            minBet: 10,
            maxBet: 500,
          })
          setLoading(false)
        }, 3000)

        // Handlers
        const handleGameStateUpdate = (state: GameState) => {
          setGameState(state)
          setLoading(false)
          if (timeoutId) {
            clearTimeout(timeoutId)
            timeoutId = null
          }
        }

        const handleChatMessage = (message: ChatMessage) => {
          setChatMessages(prev => {
            const exists = prev.some(m => m.id === message.id)
            if (exists) return prev
            return [...prev.slice(-49), message]
          })
        }

        const handlePlayerJoined = (data: any) => {
          console.log('Player joined event:', data)
        }

        const handlePlayerLeft = (data: any) => {
          console.log('Player left event:', data)
        }

        // Asignar handlers antes de conectar/entrar
        signalRService.onGameStateUpdate = handleGameStateUpdate
        signalRService.onChatMessage = handleChatMessage
        signalRService.onPlayerJoined = handlePlayerJoined
        signalRService.onPlayerLeft = handlePlayerLeft

        // Conectar si hace falta
        if (!signalRService.isGameConnected) {
          const connected = await signalRService.startConnections()
          if (!connected) throw new Error('No se pudo conectar al servidor de juego')
        }

        // Unirse a la mesa
        await signalRService.joinTable(tableId)
        isInitializedRef.current = true
      } catch (err) {
        console.error('Error initializing game:', err)
        setError(err instanceof Error ? err.message : 'Error desconocido al conectar')
        setLoading(false)
        if (timeoutId) {
          clearTimeout(timeoutId)
          timeoutId = null
        }
      }
    }

    // Cleanup
    cleanupRef.current = () => {
      if (timeoutId) {
        clearTimeout(timeoutId)
        timeoutId = null
      }

      signalRService.onGameStateUpdate = undefined
      signalRService.onChatMessage = undefined
      signalRService.onPlayerJoined = undefined
      signalRService.onPlayerLeft = undefined

      if (isInitializedRef.current && tableId) {
        signalRService.leaveTable(tableId).catch(err =>
          console.warn('Error leaving table during cleanup:', err),
        )
      }
      isInitializedRef.current = false
    }

    void initializeGame()

    return () => {
      cleanupRef.current?.()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const handleGameAction = async (action: string, data?: any) => {
    console.log('Game action:', action, data)
    // Implementar acciones cuando tu GameHub las exponga
  }

  const handleSendChat = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!chatInput.trim() || !tableId) return
    try {
      await signalRService.sendMessage(tableId, chatInput.trim())
      setChatInput('')
    } catch (error) {
      console.error('Error sending chat:', error)
    }
  }

  const handleCancelConnection = () => {
    navigate({ to: '/lobby' })
  }

  if (loading) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-gray-900 via-blue-900 to-green-900 flex items-center justify-center">
        <div className="bg-gray-800/90 backdrop-blur-sm p-8 rounded-xl text-center max-w-md">
          <div className="animate-spin w-12 h-12 border-4 border-white border-t-transparent rounded-full mx-auto mb-4"></div>
          <h3 className="text-xl font-bold text-white mb-2">Conectando a la mesa...</h3>
          <p className="text-gray-300 text-sm mb-4">Mesa #{tableId?.slice(0, 8)}...</p>
          <p className="text-gray-400 text-xs mb-6">Conectado, cargando mesa...</p>
          <button
            onClick={handleCancelConnection}
            className="bg-red-600 hover:bg-red-700 px-6 py-2 rounded-lg text-white font-medium transition-colors"
          >
            Cancelar
          </button>
        </div>
      </div>
    )
  }

  if (error) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-gray-900 via-red-900 to-gray-900 flex items-center justify-center">
        <div className="bg-gray-800/90 backdrop-blur-sm p-8 rounded-xl text-center max-w-md">
          <div className="text-red-500 text-6xl mb-4">⚠️</div>
          <h3 className="text-xl font-bold text-white mb-2">Error de conexión</h3>
          <p className="text-gray-300 text-sm mb-6">{error}</p>
          <div className="space-y-3">
            <button
              onClick={() => window.location.reload()}
              className="w-full bg-blue-600 hover:bg-blue-700 px-6 py-2 rounded-lg text-white font-medium transition-colors"
            >
              Reintentar
            </button>
            <button
              onClick={handleCancelConnection}
              className="w-full bg-gray-600 hover:bg-gray-700 px-6 py-2 rounded-lg text-white font-medium transition-colors"
            >
              Volver al Lobby
            </button>
          </div>
        </div>
      </div>
    )
  }

  if (!gameState) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-gray-900 via-blue-900 to-green-900 flex items-center justify-center">
        <div className="bg-gray-800/90 backdrop-blur-sm p-8 rounded-xl text-center">
          <h3 className="text-xl font-bold text-white mb-4">Cargando mesa...</h3>
          <div className="animate-pulse w-16 h-16 bg-gray-600 rounded-full mx-auto"></div>
        </div>
      </div>
    )
  }

  return (
    <div className="min-h-screen bg-gradient-to-br from-gray-900 via-green-900 to-gray-800">
      {/* Header */}
      <div className="bg-gray-800/80 backdrop-blur-sm border-b border-gray-700">
        <div className="max-w-7xl mx-auto px-4 py-4 flex items-center justify-between">
          <button
            onClick={() => navigate({ to: '/lobby' })}
            className="flex items-center gap-2 text-gray-300 hover:text-white transition-colors"
          >
            <span className="text-lg">←</span>
            Volver al Lobby
          </button>

          <h1 className="text-xl font-bold text-white">
            Mesa de BlackJack #{gameState.id.slice(0, 8)}
          </h1>

          <div className="text-right">
            <div className="text-sm text-gray-300">{currentUser?.displayName}</div>
            <div className="text-lg font-bold text-green-400">
              ${currentUser?.balance ?? 5000}
            </div>
          </div>
        </div>
      </div>

      {/* Main Game Area */}
      <div className="flex-1 p-6">
        <div className="max-w-7xl mx-auto flex gap-6">
          {/* Game Table */}
          <div className="flex-1">
            <BlackjackTable gameState={gameState} onGameAction={handleGameAction} />
          </div>

          {/* Chat Sidebar */}
          <div className="w-80 bg-gray-800/80 backdrop-blur-sm rounded-lg border border-gray-700 flex flex-col">
            <div className="p-4 border-b border-gray-700">
              <h3 className="font-bold text-white">Chat de Mesa</h3>
            </div>

            <div className="flex-1 p-4 overflow-y-auto max-h-96">
              {chatMessages.length === 0 ? (
                <p className="text-gray-400 text-sm text-center py-8">
                  No hay mensajes aún...
                </p>
              ) : (
                <div className="space-y-2">
                  {chatMessages.map(msg => (
                    <div key={msg.id} className="text-sm">
                      <span className="text-blue-400 font-medium">{msg.playerName}:</span>
                      <span className="text-gray-300 ml-2">{msg.text}</span>
                    </div>
                  ))}
                </div>
              )}
            </div>

            <form onSubmit={handleSendChat} className="p-4 border-t border-gray-700">
              <div className="flex gap-2">
                <input
                  type="text"
                  value={chatInput}
                  onChange={e => setChatInput(e.target.value)}
                  placeholder="Escribe un mensaje..."
                  className="flex-1 bg-gray-700 text-white px-3 py-2 rounded border border-gray-600 focus:border-blue-500 focus:outline-none text-sm"
                  maxLength={200}
                />
                <button
                  type="submit"
                  disabled={!chatInput.trim()}
                  className="bg-blue-600 hover:bg-blue-700 disabled:bg-gray-600 px-4 py-2 rounded text-white text-sm font-medium transition-colors"
                >
                  Enviar
                </button>
              </div>
            </form>
          </div>
        </div>
      </div>
    </div>
  )
}
