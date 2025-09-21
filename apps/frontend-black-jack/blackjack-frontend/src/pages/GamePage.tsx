// src/pages/GamePage.tsx - ARCHIVO COMPLETO CORREGIDO: Sin auto-LeaveRoom
import React, { useEffect, useState, useCallback, useRef } from 'react'
import { useParams, useNavigate } from '@tanstack/react-router'
import { signalRService } from '../services/signalr'
import { authService } from '../services/auth'

interface GameState {
  roomCode: string
  name: string
  status: 'WaitingForPlayers' | 'InProgress' | 'Finished'
  playerCount: number
  maxPlayers: number
  players: RoomPlayer[]
  spectators: any[]
  currentPlayerTurn?: string
  canStart: boolean
  createdAt: string
}

interface RoomPlayer {
  playerId: string
  name: string
  position: number
  isReady: boolean
  isHost: boolean
  hasPlayedTurn: boolean
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
  
  // Detect if we're in viewer mode based on the current path
  const [isViewer, setIsViewer] = useState(false)
  
  const [gameState, setGameState] = useState<GameState | null>(null)
  const [chatMessages, setChatMessages] = useState<ChatMessage[]>([])
  const [isConnected, setIsConnected] = useState(false)
  const [isJoining, setIsJoining] = useState(false)
  const [chatInput, setChatInput] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [seatClickLoading, setSeatClickLoading] = useState<number | null>(null)
  
  const hasJoinedTable = useRef(false)
  const currentUser = useRef(authService.getCurrentUser())

  useEffect(() => {
    console.log('GamePage mounted - resetting flags')
    
    // Detect if we're in viewer mode based on the current path
    const isViewerMode = window.location.pathname.includes('/viewer/')
    setIsViewer(isViewerMode)
    console.log('GamePage mode:', isViewerMode ? 'VIEWER' : 'PLAYER')
  }, [])

  useEffect(() => {
    const checkConnection = () => {
      setIsConnected(signalRService.isGameConnected)
    }
    
    checkConnection()
    const interval = setInterval(checkConnection, 5000)
    
    return () => clearInterval(interval)
  }, [])

  const handleRoomInfo = useCallback((response: any) => {
    const roomData = response?.data || response
    console.log('Room info received:', roomData)
    setGameState(roomData)
    setError(null)
    setSeatClickLoading(null)
  }, [])

  const handleRoomCreated = useCallback((response: any) => {
    const roomData = response?.data || response
    console.log('Room created:', roomData)
    setGameState(roomData)
    setError(null)
    setSeatClickLoading(null)
  }, [])

  const handleRoomJoined = useCallback((response: any) => {
    const roomData = response?.data || response
    console.log('Room joined:', roomData)
    setGameState(roomData)
    setError(null)
    setSeatClickLoading(null)
  }, [])

  const handleRoomInfoUpdated = useCallback((roomData: any) => {
    console.log('Room info updated:', roomData)
    setGameState(roomData)
    setError(null)
    setSeatClickLoading(null)
  }, [])

  const handleSeatJoined = useCallback((response: any) => {
    console.log('Seat joined response:', response)
    setSeatClickLoading(null)
    
    const roomInfo = response?.roomInfo || response?.data?.roomInfo || response?.RoomInfo
    if (roomInfo) {
      setGameState(roomInfo)
    }
    setError(null)
  }, [])

  const handleSeatLeft = useCallback((response: any) => {
    console.log('Seat left response:', response)
    setSeatClickLoading(null)
    
    const roomData = response?.data || response
    if (roomData) {
      setGameState(roomData)
    }
    setError(null)
  }, [])

  const handlePlayerJoined = useCallback((eventData: any) => {
    console.log('Player joined event:', eventData)
    
    setGameState(prev => {
      if (!prev) return prev
      
      const existingPlayer = prev.players.find(p => p.playerId === eventData.playerId)
      if (existingPlayer) {
        return prev
      }

      const updatedPlayers = [...prev.players, {
        playerId: eventData.playerId,
        name: eventData.playerName,
        position: eventData.position || -1,
        isReady: false,
        isHost: false,
        hasPlayedTurn: false
      }]

      return {
        ...prev,
        players: updatedPlayers,
        playerCount: updatedPlayers.length
      }
    })
  }, [])

  const handlePlayerLeft = useCallback((eventData: any) => {
    console.log('Player left event:', eventData)
    
    setGameState(prev => {
      if (!prev) return prev
      
      const updatedPlayers = prev.players.filter(p => p.playerId !== eventData.playerId)
      return {
        ...prev,
        players: updatedPlayers,
        playerCount: updatedPlayers.length
      }
    })
  }, [])

  const handleGameStateChanged = useCallback((newState: any) => {
    console.log('Game state changed:', newState)
    setGameState(newState)
    setSeatClickLoading(null)
  }, [])

  const handleError = useCallback((errorMessage: string) => {
    console.error('SignalR Error:', errorMessage)
    setError(errorMessage)
    setSeatClickLoading(null)
  }, [])

  useEffect(() => {
    console.log('Configurando listeners de GamePage...')
    
    signalRService.onRoomInfo = handleRoomInfo
    signalRService.onRoomCreated = handleRoomCreated
    signalRService.onRoomJoined = handleRoomJoined
    signalRService.onRoomInfoUpdated = handleRoomInfoUpdated
    signalRService.onSeatJoined = handleSeatJoined
    signalRService.onSeatLeft = handleSeatLeft
    signalRService.onPlayerJoined = handlePlayerJoined
    signalRService.onPlayerLeft = handlePlayerLeft
    signalRService.onGameStateChanged = handleGameStateChanged
    signalRService.onError = handleError

    return () => {
      console.log('Limpiando listeners de GamePage...')
      signalRService.onRoomInfo = undefined
      signalRService.onRoomCreated = undefined
      signalRService.onRoomJoined = undefined
      signalRService.onRoomInfoUpdated = undefined
      signalRService.onSeatJoined = undefined
      signalRService.onSeatLeft = undefined
      signalRService.onPlayerJoined = undefined
      signalRService.onPlayerLeft = undefined
      signalRService.onGameStateChanged = undefined
      signalRService.onError = undefined
    }
  }, [
    handleRoomInfo,
    handleRoomCreated,
    handleRoomJoined,
    handleRoomInfoUpdated,
    handleSeatJoined,
    handleSeatLeft,
    handlePlayerJoined,
    handlePlayerLeft,
    handleGameStateChanged,
    handleError
  ])

  useEffect(() => {
    const autoJoinTable = async () => {
      if (
        isConnected && 
        tableId && 
        !isJoining && 
        !hasJoinedTable.current
      ) {
        try {
          setIsJoining(true)
          hasJoinedTable.current = true
          setError(null)
          
          console.log('Auto-joining table:', tableId, 'Mode:', isViewer ? 'VIEWER' : 'PLAYER')
          
          const playerName = currentUser.current?.displayName || (isViewer ? 'Viewer' : 'Jugador')
          
          if (isViewer) {
            await signalRService.joinOrCreateRoomForTableAsViewer(tableId, playerName)
          } else {
            await signalRService.joinOrCreateRoomForTable(tableId, playerName)
          }
          
        } catch (error) {
          console.error('Error joining table:', error)
          setError(error instanceof Error ? error.message : 'Error conectando a la mesa')
          hasJoinedTable.current = false
        } finally {
          setIsJoining(false)
        }
      }
    }

    autoJoinTable()
  }, [isConnected, tableId, isViewer])

  useEffect(() => {
    console.log('GamePage mounted - component instance created')
    
    return () => {
      console.log('GamePage cleanup triggered')
    }
  }, [])

  // CORREGIDO: Cleanup simplificado - SOLO en beforeunload real
  useEffect(() => {
    const handleBeforeUnload = () => {
      if (gameState?.roomCode) {
        // Solo enviar beacon para cleanup en servidor
        navigator.sendBeacon && navigator.sendBeacon(
          '/api/cleanup', 
          JSON.stringify({ roomCode: gameState.roomCode })
        )
        // ELIMINADO: signalRService.leaveRoom automático
      }
    }

    // ELIMINADO: handleVisibilityChange que causaba auto-LeaveRoom

    window.addEventListener('beforeunload', handleBeforeUnload)

    return () => {
      window.removeEventListener('beforeunload', handleBeforeUnload)
    }
  }, [gameState?.roomCode])

  const handleJoinSeat = useCallback(async (position: number) => {
    if (!isConnected || !gameState?.roomCode || seatClickLoading !== null) {
      console.log('Cannot join seat - conditions not met:', {
        isConnected,
        roomCode: gameState?.roomCode,
        seatClickLoading
      })
      return
    }
    
    try {
      setSeatClickLoading(position)
      setError(null)
      console.log(`Attempting to join seat ${position} in room ${gameState.roomCode}`)
      
      await signalRService.joinSeat(gameState.roomCode, position)
      
    } catch (error) {
      console.error('Error joining seat:', error)
      setError(error instanceof Error ? error.message : 'Error uniéndose al asiento')
      setSeatClickLoading(null)
    }
  }, [isConnected, gameState?.roomCode, seatClickLoading])

  const handleLeaveSeat = useCallback(async () => {
    if (!isConnected || !gameState?.roomCode || seatClickLoading !== null) {
      console.log('Cannot leave seat - conditions not met')
      return
    }
    
    try {
      setSeatClickLoading(-1)
      setError(null)
      console.log('Attempting to leave seat')
      
      await signalRService.leaveSeat(gameState.roomCode)
      
    } catch (error) {
      console.error('Error leaving seat:', error)
      setError(error instanceof Error ? error.message : 'Error saliendo del asiento')
      setSeatClickLoading(null)
    }
  }, [isConnected, gameState?.roomCode, seatClickLoading])

  const handleStartRound = useCallback(async () => {
    if (!isConnected || !gameState?.roomCode) return
    try {
      setError(null)
      await signalRService.startGame(gameState.roomCode)
    } catch (error) {
      console.error('Error starting game:', error)
      setError(error instanceof Error ? error.message : 'Error iniciando juego')
    }
  }, [isConnected, gameState?.roomCode])

  const handleSendMessage = useCallback(() => {
    if (!chatInput.trim() || !currentUser.current) return
    
    const messageId = `${Date.now()}-${Math.random().toString(36).substr(2, 9)}`
    const newMessage: ChatMessage = {
      id: messageId,
      playerName: currentUser.current.displayName,
      text: chatInput.trim(),
      timestamp: new Date().toISOString()
    }
    
    setChatMessages(prev => {
      if (prev.some(m => m.id === newMessage.id)) {
        return prev
      }
      return [...prev.slice(-9), newMessage]
    })
    setChatInput('')
  }, [chatInput])

  // NUEVA: Función para salir EXPLÍCITAMENTE de la sala
  const handleExplicitLeaveRoom = useCallback(async () => {
    if (!gameState?.roomCode) return
    
    try {
      console.log('EXPLICIT USER ACTION: Leaving room', gameState.roomCode)
      await signalRService.leaveRoom(gameState.roomCode)
      navigate({ to: '/lobby' })
    } catch (error) {
      console.error('Error leaving room explicitly:', error)
      navigate({ to: '/lobby' }) // Navegar de todas formas
    }
  }, [gameState?.roomCode, navigate])

  const getPlayerAtPosition = useCallback((position: number) => {
    return gameState?.players?.find(p => p.position === position)
  }, [gameState?.players])

  const currentPlayer = gameState?.players?.find(p => p.playerId === currentUser.current?.id)
  const isPlayerSeated = !!currentPlayer

  if (!isConnected || isJoining) {
    return (
      <div className="fixed top-0 left-0 w-screen h-screen bg-gradient-to-br from-emerald-900 to-emerald-800 z-[9999] overflow-hidden m-0 p-0 flex items-center justify-center">
        <div className="bg-black/80 rounded-xl p-8 text-center">
          <div className="w-8 h-8 border-2 border-white border-t-transparent rounded-full animate-spin mx-auto mb-4"></div>
          <div className="text-white text-lg">
            {!isConnected ? 'Conectando al servidor...' : 'Uniéndose a la mesa...'}
          </div>
          <div className="text-gray-300 text-sm mt-2">Mesa: {tableId?.slice(0, 8)}...</div>
          {error && (
            <div className="text-red-400 text-sm mt-2 max-w-md">
              Error: {error}
            </div>
          )}
        </div>
      </div>
    )
  }

  return (
    <div data-game-page className="fixed top-0 left-0 w-screen h-screen bg-gradient-to-br from-emerald-900 to-emerald-800 z-[9999] overflow-hidden m-0 p-0">
      
      {/* Header */}
      <div className="absolute top-0 left-0 right-0 bg-black/60 px-6 py-3 flex justify-between items-center text-white">
        <div className="flex items-center gap-4">
          <button
            onClick={handleExplicitLeaveRoom}
            className="bg-transparent border-none text-white text-base cursor-pointer hover:text-gray-300 transition-colors"
          >
            ← Volver al Lobby
          </button>
          <button
            onClick={() => authService.logout()}
            className="bg-red-600/80 hover:bg-red-600 text-white text-sm px-3 py-1 rounded cursor-pointer transition-colors"
          >
            Cerrar Sesión
          </button>
        </div>
        
        <div className="flex items-center gap-2">
          <span className="text-xl">👑</span>
          <h1 className="m-0 text-xl font-bold">
            {gameState?.name || 'Mesa VIP Diamante'}
          </h1>
        </div>
        
        <div className="text-sm flex items-center gap-4">
          <div>
            {isViewer ? 'Modo Viewer' : 'Modo Jugador'} • {gameState?.playerCount || 0}/{gameState?.maxPlayers || 6} jugadores
          </div>
          <div className="flex items-center gap-2">
            <div className={`w-2 h-2 rounded-full ${isConnected ? 'bg-green-400' : 'bg-red-400'}`}></div>
            <span>{isConnected ? 'Conectado' : 'Desconectado'}</span>
          </div>
        </div>
      </div>

      {/* Error Banner */}
      {error && (
        <div className="absolute top-16 left-1/2 transform -translate-x-1/2 bg-red-600/90 text-white px-6 py-3 rounded-lg shadow-lg z-20 max-w-md">
          <div className="flex items-center gap-2">
            <span>⚠️</span>
            <span className="flex-1 text-sm">{error}</span>
            <button 
              onClick={() => setError(null)}
              className="ml-2 text-red-200 hover:text-white text-lg leading-none"
            >
              ✕
            </button>
          </div>
        </div>
      )}

      {/* Dealer */}
      <div className="absolute top-20 left-1/2 transform -translate-x-1/2 text-center">
        <div className="w-[60px] h-[60px] rounded-full bg-amber-400 flex items-center justify-center text-2xl font-bold text-black mb-2 mx-auto">
          D
        </div>
        <div className="text-amber-400 font-bold">Dealer</div>
      </div>

      {/* Banner Central */}
      <div className="absolute top-1/2 left-1/2 transform -translate-x-1/2 -translate-y-1/2 bg-white/95 border-l-4 border-amber-500 rounded-lg p-6 shadow-[0_10px_25px_rgba(0,0,0,0.3)] min-w-[400px] flex items-center">
        <div className="w-8 h-8 rounded-full bg-amber-500 flex items-center justify-center mr-4 text-white font-bold">
          {gameState?.status === 'InProgress' ? '🎯' : '!'}
        </div>
        <div className="flex-1">
          <h3 className="m-0 mb-2 text-lg font-bold text-amber-800">
            {gameState?.status === 'InProgress' ? 'Partida en Curso' : 'Esperando Jugadores'}
          </h3>
          <p className="m-0 text-gray-700">
            {gameState?.status === 'InProgress' 
              ? 'La partida está en progreso. ¡Buena suerte!'
              : 'Se necesitan mínimo 2 jugadores para comenzar'
            }
          </p>
          {!isPlayerSeated && !isViewer && (
            <p className="m-0 text-gray-600 text-sm mt-2">
              Haz clic en un asiento libre para unirte a la mesa
            </p>
          )}
          {isViewer && (
            <p className="m-0 text-gray-600 text-sm mt-2">
              Modo espectador - Observando la partida
            </p>
          )}
        </div>
        {!isViewer && gameState?.canStart && gameState.status !== 'InProgress' && currentPlayer?.isHost && (
          <button
            onClick={handleStartRound}
            className="ml-4 bg-emerald-600 hover:bg-emerald-700 text-white px-4 py-2 rounded-lg font-semibold transition-colors"
          >
            Iniciar Ronda
          </button>
        )}
      </div>

      {/* Posiciones de jugadores */}
      <PlayerPosition 
        position={0}
        player={getPlayerAtPosition(0)}
        currentUser={currentUser.current}
        isCurrentUserSeated={isPlayerSeated}
        gameStatus={gameState?.status}
        currentPlayerTurn={gameState?.currentPlayerTurn}
        onJoinSeat={handleJoinSeat}
        onLeaveSeat={handleLeaveSeat}
        seatClickLoading={seatClickLoading}
        isViewer={isViewer}
        className="absolute top-[120px] left-10"
      />

      <PlayerPosition 
        position={1}
        player={getPlayerAtPosition(1)}
        currentUser={currentUser.current}
        isCurrentUserSeated={isPlayerSeated}
        gameStatus={gameState?.status}
        currentPlayerTurn={gameState?.currentPlayerTurn}
        onJoinSeat={handleJoinSeat}
        onLeaveSeat={handleLeaveSeat}
        seatClickLoading={seatClickLoading}
        isViewer={isViewer}
        className="absolute bottom-[120px] left-10"
      />

      <PlayerPosition 
        position={2}
        player={getPlayerAtPosition(2)}
        currentUser={currentUser.current}
        isCurrentUserSeated={isPlayerSeated}
        gameStatus={gameState?.status}
        currentPlayerTurn={gameState?.currentPlayerTurn}
        onJoinSeat={handleJoinSeat}
        onLeaveSeat={handleLeaveSeat}
        seatClickLoading={seatClickLoading}
        isViewer={isViewer}
        className="absolute bottom-10 left-1/2 transform -translate-x-1/2"
        isMainPosition={true}
      />

      <PlayerPosition 
        position={3}
        player={getPlayerAtPosition(3)}
        currentUser={currentUser.current}
        isCurrentUserSeated={isPlayerSeated}
        gameStatus={gameState?.status}
        currentPlayerTurn={gameState?.currentPlayerTurn}
        onJoinSeat={handleJoinSeat}
        onLeaveSeat={handleLeaveSeat}
        seatClickLoading={seatClickLoading}
        isViewer={isViewer}
        className="absolute bottom-[120px] right-10"
      />

      <PlayerPosition 
        position={4}
        player={getPlayerAtPosition(4)}
        currentUser={currentUser.current}
        isCurrentUserSeated={isPlayerSeated}
        gameStatus={gameState?.status}
        currentPlayerTurn={gameState?.currentPlayerTurn}
        onJoinSeat={handleJoinSeat}
        onLeaveSeat={handleLeaveSeat}
        seatClickLoading={seatClickLoading}
        isViewer={isViewer}
        className="absolute top-[120px] right-10"
      />

      <PlayerPosition 
        position={5}
        player={getPlayerAtPosition(5)}
        currentUser={currentUser.current}
        isCurrentUserSeated={isPlayerSeated}
        gameStatus={gameState?.status}
        currentPlayerTurn={gameState?.currentPlayerTurn}
        onJoinSeat={handleJoinSeat}
        onLeaveSeat={handleLeaveSeat}
        seatClickLoading={seatClickLoading}
        isViewer={isViewer}
        className="absolute top-[120px] left-1/2 transform -translate-x-1/2"
      />

      {/* Chat */}
      <div className="absolute bottom-5 right-5 bg-black/80 rounded-lg p-3 min-w-[250px] max-w-[300px] text-white">
        <div className="text-amber-400 font-bold text-xs mb-2">
          Chat
        </div>
        <div className="max-h-[120px] overflow-y-auto mb-2">
          {chatMessages.length === 0 ? (
            <div className="text-gray-400 text-xs">Sin mensajes</div>
          ) : (
            chatMessages.slice(-5).map((msg) => (
              <div key={msg.id} className="text-xs mb-1">
                <span className="text-amber-300">{msg.playerName}:</span>
                <span className="ml-1">{msg.text}</span>
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
            placeholder="Escribe mensaje..."
            className="flex-1 px-2 py-1 text-xs bg-slate-700 border border-slate-600 rounded text-white placeholder-slate-400"
            maxLength={100}
          />
          <button
            onClick={handleSendMessage}
            className="px-2 py-1 bg-emerald-600 hover:bg-emerald-700 rounded text-xs transition-colors"
          >
            →
          </button>
        </div>
      </div>

    </div>
  )
}

// Componente PlayerPosition optimizado
function PlayerPosition({ 
  position, 
  player, 
  currentUser, 
  isCurrentUserSeated,
  gameStatus,
  currentPlayerTurn,
  onJoinSeat,
  onLeaveSeat,
  seatClickLoading,
  isViewer,
  className, 
  isMainPosition = false 
}: {
  position: number
  player?: RoomPlayer
  currentUser: any
  isCurrentUserSeated: boolean
  gameStatus?: string
  currentPlayerTurn?: string
  onJoinSeat: (position: number) => Promise<void>
  onLeaveSeat: () => Promise<void>
  seatClickLoading: number | null
  isViewer: boolean
  className: string
  isMainPosition?: boolean
}) {
  const isCurrentUser = player?.playerId === currentUser?.id
  const isEmpty = !player
  const isLoading = seatClickLoading === position || (isCurrentUser && seatClickLoading === -1)
  
  const canJoinSeat = isEmpty && !isLoading && !isViewer

  const handleSeatClick = useCallback(async () => {
    if (canJoinSeat && !isViewer) {
      console.log(`Clicking to join seat ${position}`)
      await onJoinSeat(position)
    }
  }, [canJoinSeat, position, onJoinSeat, isViewer])

  const handleLeaveSeat = useCallback(async () => {
    if (isCurrentUser && !isLoading && gameStatus !== 'InProgress' && !isViewer) {
      console.log('Clicking to leave seat')
      await onLeaveSeat()
    }
  }, [isCurrentUser, isLoading, gameStatus, onLeaveSeat, isViewer])

  // Asiento vacío
  if (isEmpty) {
    return (
      <div className={className}>
        <div className="flex items-center mb-2">
          <div 
            className={`w-10 h-10 rounded-full border-2 border-dashed flex items-center justify-center mr-3 font-bold transition-all ${
              canJoinSeat 
                ? 'bg-gray-600 border-gray-400 text-gray-300 hover:bg-gray-500 hover:border-gray-300 cursor-pointer transform hover:scale-105' 
                : 'bg-gray-700 border-gray-500 text-gray-500 cursor-not-allowed'
            }`}
            onClick={handleSeatClick}
          >
            {isLoading ? (
              <div className="w-4 h-4 border border-gray-400 border-t-transparent rounded-full animate-spin"></div>
            ) : (
              position + 1
            )}
          </div>
          <div className="bg-gray-700/70 px-3 py-1 rounded text-gray-300">
            <div className="font-bold text-sm">
              {isLoading ? 'Uniéndose...' : 'Asiento libre'}
            </div>
            <div className="text-gray-400 text-xs">
              {isViewer ? 'Asiento vacío' :
               canJoinSeat ? 'Clic para unirse' : 
               isLoading ? 'Procesando...' : 'No disponible'}
            </div>
          </div>
        </div>
      </div>
    )
  }

  // Jugador sentado
  return (
    <div className={className}>
      <div className="flex items-center mb-2">
        <div className={`w-10 h-10 rounded-full bg-white flex items-center justify-center mr-3 font-bold text-black border-2 relative transition-all ${
          isCurrentUser ? 'border-red-500 shadow-lg' : 'border-gray-300'
        }`}>
          {isLoading ? (
            <div className="w-4 h-4 border border-gray-600 border-t-transparent rounded-full animate-spin"></div>
          ) : (
            player.name.substring(0, 2).toUpperCase()
          )}
          
          {isCurrentUser && !isLoading && (
            <div className="absolute -top-1 -right-1 w-3 h-3 bg-red-500 rounded-full flex items-center justify-center">
              <div className="w-1.5 h-1.5 bg-red-600 rounded-full"></div>
            </div>
          )}
          {player.isHost && (
            <div className="absolute -top-2 -left-2 text-yellow-400 text-lg">👑</div>
          )}
        </div>
        
        <div className="bg-black/70 px-3 py-1 rounded text-white">
          <div className="font-bold text-sm">
            {isCurrentUser ? `${player.name} (TÚ)` : player.name}
          </div>
          <div className="text-emerald-400 text-xs">$1,000</div>
          {gameStatus === 'InProgress' && currentPlayerTurn === player.name && (
            <div className="text-yellow-400 text-xs animate-pulse">Su turno</div>
          )}
          {isLoading && (
            <div className="text-orange-400 text-xs">
              {seatClickLoading === -1 ? 'Saliendo...' : 'Procesando...'}
            </div>
          )}
        </div>
      </div>

      {/* Información adicional para el jugador */}
      <div className="ml-[52px] space-y-1">
        {player.isReady && (
          <div className="px-2 py-1 bg-green-500 rounded text-xs font-bold text-white inline-block">
            ✓ Listo
          </div>
        )}
        
        {player.hasPlayedTurn && gameStatus === 'InProgress' && (
          <div className="text-xs text-white bg-blue-500 rounded px-2 py-1 inline-block">
            Turno jugado
          </div>
        )}

        {isCurrentUser && !isLoading && gameStatus !== 'InProgress' && !isViewer && (
          <div>
            <button
              onClick={handleLeaveSeat}
              className="text-xs bg-red-500/80 hover:bg-red-500 text-white px-2 py-1 rounded transition-colors"
            >
              Salir del asiento
            </button>
          </div>
        )}
      </div>
    </div>
  )
}