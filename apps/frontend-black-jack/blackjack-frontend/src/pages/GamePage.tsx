// src/pages/GamePage.tsx - CORREGIDO: Estado de loading centralizado
import React, { useEffect, useState, useCallback, useRef } from 'react'
import { useParams, useNavigate } from '@tanstack/react-router'
import { signalRService } from '../services/signalr'
import { authService } from '../services/auth'
import { apiService } from '../api/apiService'

// Componentes extraídos
import GameHeader from '../components/game/GameHeader'
import GameTable from '../components/game/GameTable'
import GameSeats from '../components/game/GameSeats'
import GameChat from '../components/game/GameChat'
import GameBettings from '../components/game/GameBettings'

interface Card {
  suit: 'Hearts' | 'Diamonds' | 'Clubs' | 'Spades'
  rank: 'Ace' | '2' | '3' | '4' | '5' | '6' | '7' | '8' | '9' | '10' | 'Jack' | 'Queen' | 'King'
  value: number
}

interface Hand {
  id: string
  cards: Card[]
  value: number
  status: 'Active' | 'Stand' | 'Bust' | 'Blackjack'
}

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
  // New game state
  dealerHand?: Hand | null
  playerHand?: Hand | null
  playersWithHands?: Array<RoomPlayer & { hand?: Hand | null }>
}

interface RoomPlayer {
  playerId: string
  name: string
  position: number
  isReady: boolean
  isHost: boolean
  hasPlayedTurn: boolean
}

export default function GamePage() {
  const { tableId } = useParams({ strict: false }) as { tableId: string }
  const navigate = useNavigate()
  
  // Estados principales
  const [isViewer, setIsViewer] = useState(false)
  const [gameState, setGameState] = useState<GameState | null>(null)
  const [connectionStatus, setConnectionStatus] = useState({
    room: false,
    seat: false,
    spectator: false,
    gameControl: false,
    overall: false
  })
  const [isJoining, setIsJoining] = useState(false)
  const [isStartingRound, setIsStartingRound] = useState(false)
  const [error, setError] = useState<string | null>(null)
  
  // CORREGIDO: Estado de loading centralizado aquí
  const [seatClickLoading, setSeatClickLoading] = useState<number | null>(null)
  
  // Refs
  const hasJoinedTable = useRef(false)
  const currentUser = useRef(authService.getCurrentUser())
  const isComponentMounted = useRef(true)

  // Detectar modo viewer y inicializar componente
  useEffect(() => {
    console.log('[GamePage] === COMPONENT MOUNT ===')
    isComponentMounted.current = true
    
    const isViewerMode = window.location.pathname.includes('/viewer/')
    setIsViewer(isViewerMode)
    console.log('[GamePage] Mode detected:', isViewerMode ? 'VIEWER' : 'PLAYER')

    return () => {
      console.log('[GamePage] === COMPONENT UNMOUNT ===')
      isComponentMounted.current = false
    }
  }, [])

  // Verificar conexiones de hubs
  useEffect(() => {
    let mounted = true
    
    const checkConnections = () => {
      if (!mounted || !isComponentMounted.current) return
      
      const status = {
        room: signalRService.isRoomHubConnected,
        seat: signalRService.isSeatHubConnected,
        spectator: signalRService.isSpectatorHubConnected,
        gameControl: signalRService.isGameControlHubConnected,
        overall: signalRService.areAllConnected
      }
      
      setConnectionStatus(prev => {
        const hasChanged = JSON.stringify(prev) !== JSON.stringify(status)
        if (hasChanged) {
          console.log('[GamePage] Hub connection status changed:', status)
        }
        return hasChanged ? status : prev
      })
    }
    
    checkConnections()
    const interval = setInterval(checkConnections, 10000) // 10 segundos
    
    return () => {
      mounted = false
      clearInterval(interval)
    }
  }, [])

  // Handlers para eventos SignalR
  const handleRoomInfo = useCallback((response: any) => {
    if (!isComponentMounted.current) return
    const roomData = response?.data || response
    console.log('[GamePage] Room info received via RoomHub:', roomData)
    setGameState(prev => {
      // Preserve hand information when updating room info
      return {
        ...roomData,
        playerHand: prev?.playerHand || null,
        playersWithHands: prev?.playersWithHands || [],
        dealerHand: prev?.dealerHand || null
      }
    })
    setError(null)
  }, [])

  const handleRoomCreated = useCallback((response: any) => {
    if (!isComponentMounted.current) return
    const roomData = response?.data || response
    console.log('[GamePage] Room created via RoomHub:', roomData)
    setGameState(prev => {
      // Preserve hand information when updating room info
      return {
        ...roomData,
        playerHand: prev?.playerHand || null,
        playersWithHands: prev?.playersWithHands || [],
        dealerHand: prev?.dealerHand || null
      }
    })
    setError(null)
  }, [])

  const handleRoomJoined = useCallback((response: any) => {
    if (!isComponentMounted.current) return
    const roomData = response?.data || response
    console.log('[GamePage] Room joined via RoomHub:', roomData)
    setGameState(prev => {
      // Preserve hand information when updating room info
      return {
        ...roomData,
        playerHand: prev?.playerHand || null,
        playersWithHands: prev?.playersWithHands || [],
        dealerHand: prev?.dealerHand || null
      }
    })
    setError(null)
  }, [])

  const handleRoomInfoUpdated = useCallback((roomData: any) => {
    if (!isComponentMounted.current) return
    console.log('[GamePage] Room info updated via RoomHub:', roomData)
    setGameState(prev => {
      // Preserve hand information when updating room info
      return {
        ...roomData,
        playerHand: prev?.playerHand || null,
        playersWithHands: prev?.playersWithHands || [],
        dealerHand: prev?.dealerHand || null
      }
    })
    setError(null)
  }, [])

  const handleSeatJoined = useCallback((response: any) => {
    if (!isComponentMounted.current) return
    console.log('[GamePage] Seat joined via SeatHub:', response)
    const roomInfo = response?.roomInfo || response?.data?.roomInfo || response?.RoomInfo
    if (roomInfo) {
      setGameState(roomInfo)
    }
    setError(null)
    
    // CORREGIDO: Resetear loading state cuando operación es exitosa
    setSeatClickLoading(null)
    console.log('[GamePage] Loading state reset after successful seat join')
  }, [])

  const handleSeatLeft = useCallback((response: any) => {
    if (!isComponentMounted.current) return
    console.log('[GamePage] Seat left via SeatHub:', response)
    const roomData = response?.data || response
    if (roomData) {
      setGameState(roomData)
    }
    setError(null)
    
    // CORREGIDO: Resetear loading state cuando operación es exitosa
    setSeatClickLoading(null)
    console.log('[GamePage] Loading state reset after successful seat leave')
  }, [])

  const handlePlayerJoined = useCallback((eventData: any) => {
    if (!isComponentMounted.current) return
    console.log('[GamePage] Player joined event via RoomHub:', eventData)
    
    setGameState(prev => {
      if (!prev) return prev
      
      const existingPlayer = prev.players.find(p => p.playerId === eventData.playerId)
      if (existingPlayer) {
        console.log('[GamePage] Player already exists, ignoring duplicate join event')
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

      console.log('[GamePage] Adding new player to local state:', eventData.playerName)
      return {
        ...prev,
        players: updatedPlayers,
        playerCount: updatedPlayers.length
      }
    })
  }, [])

  const handlePlayerLeft = useCallback((eventData: any) => {
    if (!isComponentMounted.current) return
    console.log('[GamePage] Player left event via RoomHub:', eventData)
    
    setGameState(prev => {
      if (!prev) return prev
      
      const updatedPlayers = prev.players.filter(p => p.playerId !== eventData.playerId)
      console.log('[GamePage] Removing player from local state:', eventData.playerName)
      
      return {
        ...prev,
        players: updatedPlayers,
        playerCount: updatedPlayers.length
      }
    })
  }, [])

  const handleGameStateChanged = useCallback((newState: any) => {
    if (!isComponentMounted.current) return
    console.log('[GamePage] Game state changed via GameControlHub:', newState)
    if (newState?.players) {
      console.log('[GamePage] Players with hands:', newState.players.map((p: any) => ({
        playerId: p.playerId,
        name: p.name,
        seat: p.seat,
        hasHand: !!p.hand,
        handValue: p.hand?.value
      })))
    }
    setGameState(prev => {
      const prevState: any = prev || {}

      // Normalize payloads from GameStarted/GameStateChanged/GameStateUpdated
      const status = (newState?.status as string) || 'InProgress'
      const dealerHand = newState?.dealerHand || prevState.dealerHand || null

      // If server sends players array with hand per player
      let playerHand = prevState.playerHand || null
      let playersWithHands = prevState.playersWithHands || []
      const currentUserId = (authService.getCurrentUser()?.id) as string | undefined
      if (Array.isArray(newState?.players)) {
        // Set current player's hand if available
        // console.log('[GamePage] currentUserId:', currentUserId)
        if (currentUserId) {
          const me = newState.players.find((p: any) => p.playerId === currentUserId)
          // console.log('[GamePage] Found current user in players:', me)
          if (me?.hand) {
            playerHand = {
              id: me.hand.handId || me.hand.id,
              cards: me.hand.cards || [],
              value: me.hand.value || 0,
              status: me.hand.status || 'Active'
            }
            // console.log('[GamePage] Set playerHand:', playerHand)
          }
        } else {
          // console.log('[GamePage] No currentUserId available, will try to set playerHand later')
        }

        // Map all players with their hands
        playersWithHands = newState.players.map((p: any) => ({
          ...p,
          position: p.seat, // Map seat to position for consistency
          hand: p.hand ? {
            id: p.hand.handId || p.hand.id,
            cards: p.hand.cards || [],
            value: p.hand.value || 0,
            status: p.hand.status || 'Active'
          } : null
        }))
        // console.log('[GamePage] Mapped playersWithHands:', playersWithHands)
      }

      // Coerce canStart=false when game is in progress
      const canStart = status === 'InProgress' ? false : (prevState.canStart ?? false)

        const newGameState = {
          ...prevState,
          status: 'InProgress',
          canStart,
          dealerHand: dealerHand ? {
            id: dealerHand.handId || dealerHand.id,
            cards: dealerHand.cards || [],
            value: dealerHand.value || 0,
            status: dealerHand.status || 'Active'
          } : null,
          playerHand,
          playersWithHands
        } as any

        // Fallback: if playerHand is still null but we have currentUserId, try to find it
        if (!newGameState.playerHand && currentUserId && Array.isArray(newState?.players)) {
          const me = newState.players.find((p: any) => p.playerId === currentUserId)
          if (me?.hand) {
            newGameState.playerHand = {
              id: me.hand.handId || me.hand.id,
              cards: me.hand.cards || [],
              value: me.hand.value || 0,
              status: me.hand.status || 'Active'
            }
            // console.log('[GamePage] Fallback: Set playerHand:', newGameState.playerHand)
          }
        }

        return newGameState
    })
  }, [])

  const handleError = useCallback((errorMessage: string) => {
    if (!isComponentMounted.current) return
    console.error('[GamePage] SignalR Error from any hub:', errorMessage)
    setError(errorMessage)
    
    // CORREGIDO: Resetear loading state en caso de error
    setSeatClickLoading(null)
    console.log('[GamePage] Loading state reset after error')
  }, [])

  // Setup de listeners SignalR
  useEffect(() => {
    if (!isComponentMounted.current) return
    
    console.log('[GamePage] Setting up hub listeners...')
    
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
      console.log('[GamePage] Cleaning up hub listeners...')
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
  }, [])

  // Auto-join logic
  useEffect(() => {
    let mounted = true
    
    const autoJoinTable = async () => {
      if (
        !mounted ||
        !isComponentMounted.current ||
        !connectionStatus.overall || 
        !tableId || 
        isJoining || 
        hasJoinedTable.current
      ) {
        return
      }

      const requiredHub = isViewer ? connectionStatus.spectator : connectionStatus.room
      
      if (!requiredHub) {
        console.log(`[GamePage] Waiting for ${isViewer ? 'SpectatorHub' : 'RoomHub'} to connect...`)
        return
      }

      try {
        if (!mounted || !isComponentMounted.current) return
        
        setIsJoining(true)
        hasJoinedTable.current = true
        setError(null)
        
        console.log(`[GamePage] === AUTO-JOINING TABLE ===`)
        console.log(`[GamePage] TableId: ${tableId}`)
        console.log(`[GamePage] Mode: ${isViewer ? 'VIEWER' : 'PLAYER'}`)
        
        const playerName = currentUser.current?.displayName || (isViewer ? 'Viewer' : 'Jugador')
        
        if (isViewer) {
          await signalRService.joinOrCreateRoomForTableAsViewer(tableId, playerName)
        } else {
          await signalRService.joinOrCreateRoomForTable(tableId, playerName)
        }
        
        console.log('[GamePage] Successfully joined table')
        
      } catch (error) {
        if (!mounted || !isComponentMounted.current) return
        
        console.error('[GamePage] Error joining table:', error)
        setError(error instanceof Error ? error.message : 'Error conectando a la mesa')
        hasJoinedTable.current = false
      } finally {
        if (mounted && isComponentMounted.current) {
          setIsJoining(false)
        }
      }
    }

    const timeoutId = setTimeout(autoJoinTable, 100)
    
    return () => {
      mounted = false
      clearTimeout(timeoutId)
    }
  }, [connectionStatus.overall, connectionStatus.room, connectionStatus.spectator, tableId, isViewer])

  // Cleanup en beforeunload
  useEffect(() => {
    const handleBeforeUnload = () => {
      if (gameState?.roomCode) {
        navigator.sendBeacon && navigator.sendBeacon(
          '/api/cleanup', 
          JSON.stringify({ roomCode: gameState.roomCode })
        )
      }
    }

    window.addEventListener('beforeunload', handleBeforeUnload)
    return () => window.removeEventListener('beforeunload', handleBeforeUnload)
  }, [gameState?.roomCode])

  // Handlers para acciones del usuario
  const handleLeaveRoom = useCallback(async () => {
    if (!gameState?.roomCode || !isComponentMounted.current) return
    
    try {
      console.log('[GamePage] === EXPLICIT LEAVE ROOM ===')
      console.log('[GamePage] RoomCode:', gameState.roomCode)
      
      await signalRService.leaveRoom(gameState.roomCode)
      navigate({ to: '/lobby' })
    } catch (error) {
      console.error('[GamePage] Error leaving room:', error)
      navigate({ to: '/lobby' })
    }
  }, [gameState?.roomCode, navigate])

  const handleStartRound = useCallback(async () => {
    if (isStartingRound) return
    if (!connectionStatus.gameControl || !gameState?.roomCode || !isComponentMounted.current) {
      console.log('[GamePage] Cannot start game - GameControlHub not connected')
      return
    }
    
    try {
      setIsStartingRound(true)
      setError(null)
      console.log('[GamePage] Starting game via GameControlHub')
      await signalRService.startGame(gameState.roomCode)
      // Optimistically update local status and request fresh room info
      setGameState(prev => (prev ? { ...prev, status: 'InProgress' } as any : prev))
      signalRService.getRoomInfo(gameState.roomCode).catch(() => {})
    } catch (error) {
      if (!isComponentMounted.current) return
      console.error('[GamePage] Error starting game:', error)
      setError(error instanceof Error ? error.message : 'Error iniciando juego')
    } finally {
      // Keep disabled until status flips to InProgress; re-enable otherwise
      setIsStartingRound(false)
    }
  }, [connectionStatus.gameControl, gameState?.roomCode, isStartingRound])

  // Game action handlers
  const handleHit = useCallback(async () => {
    if (!gameState?.roomCode || !isComponentMounted.current) {
      console.log('[GamePage] Cannot hit - no room code')
      return
    }
    
    try {
      setError(null)
      console.log('[GamePage] Player hits')
      await apiService.playerAction(gameState.roomCode, 'Hit')
    } catch (error) {
      if (!isComponentMounted.current) return
      console.error('[GamePage] Error hitting:', error)
      setError(error instanceof Error ? error.message : 'Error al pedir carta')
    }
  }, [gameState?.roomCode])

  const handleStand = useCallback(async () => {
    if (!gameState?.roomCode || !isComponentMounted.current) {
      console.log('[GamePage] Cannot stand - no room code')
      return
    }
    
    try {
      setError(null)
      console.log('[GamePage] Player stands')
      await apiService.playerAction(gameState.roomCode, 'Stand')
    } catch (error) {
      if (!isComponentMounted.current) return
      console.error('[GamePage] Error standing:', error)
      setError(error instanceof Error ? error.message : 'Error al plantarse')
    }
  }, [gameState?.roomCode])

  // Computed values
  const currentPlayer = gameState?.players?.find(p => p.playerId === currentUser.current?.id)
  const isPlayerSeated = !!currentPlayer
  // Debug logging for troubleshooting
  // console.log('[GamePage] currentUser.current?.id:', currentUser.current?.id)
  // console.log('[GamePage] currentPlayer:', currentPlayer)
  // console.log('[GamePage] isPlayerSeated:', isPlayerSeated)
  // console.log('[GamePage] playerHand:', gameState?.playerHand)
  // console.log('[GamePage] currentPlayerTurn:', gameState?.currentPlayerTurn)
  // console.log('[GamePage] isPlayerTurn:', gameState?.currentPlayerTurn === currentPlayer?.name)

  // Loading screen
  if (!connectionStatus.overall || isJoining) {
    return (
      <div className="fixed top-0 left-0 w-screen h-screen bg-gradient-to-br from-emerald-900 to-emerald-800 z-[9999] overflow-hidden m-0 p-0 flex items-center justify-center">
        <div className="bg-black/80 rounded-xl p-8 text-center max-w-md">
          <div className="w-8 h-8 border-2 border-white border-t-transparent rounded-full animate-spin mx-auto mb-4"></div>
          <div className="text-white text-lg mb-4">
            {!connectionStatus.overall ? 'Conectando hubs especializados...' : 'Uniéndose a la mesa...'}
          </div>
          <div className="text-gray-300 text-sm mb-4">Mesa: {tableId?.slice(0, 8)}...</div>
          
          {!connectionStatus.overall && (
            <div className="text-xs text-gray-400 space-y-1 text-left">
              <div className="flex justify-between">
                <span>RoomHub:</span>
                <span className={connectionStatus.room ? 'text-green-400' : 'text-yellow-400'}>
                  {connectionStatus.room ? '✓' : '⏳'}
                </span>
              </div>
              <div className="flex justify-between">
                <span>SeatHub:</span>
                <span className={connectionStatus.seat ? 'text-green-400' : 'text-yellow-400'}>
                  {connectionStatus.seat ? '✓' : '⏳'}
                </span>
              </div>
              <div className="flex justify-between">
                <span>SpectatorHub:</span>
                <span className={connectionStatus.spectator ? 'text-green-400' : 'text-yellow-400'}>
                  {connectionStatus.spectator ? '✓' : '⏳'}
                </span>
              </div>
              <div className="flex justify-between">
                <span>GameControlHub:</span>
                <span className={connectionStatus.gameControl ? 'text-green-400' : 'text-yellow-400'}>
                  {connectionStatus.gameControl ? '✓' : '⏳'}
                </span>
              </div>
            </div>
          )}
          
          {error && (
            <div className="text-red-400 text-sm mt-4 max-w-md">
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
      <GameHeader
        roomName={gameState?.name}
        playerCount={gameState?.playerCount || 0}
        maxPlayers={gameState?.maxPlayers || 6}
        isViewer={isViewer}
        isConnected={connectionStatus.overall}
        onLeaveRoom={handleLeaveRoom}
      />

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

      {/* Game Table - Dealer and Central Banner */}
      <GameTable
        gameStatus={gameState?.status}
        canStart={gameState?.canStart || false}
        isPlayerSeated={isPlayerSeated}
        isViewer={isViewer}
        isCurrentPlayerHost={currentPlayer?.isHost || false}
        gameControlConnected={connectionStatus.gameControl}
        isStarting={isStartingRound}
        onStartRound={handleStartRound}
        dealerHand={gameState?.dealerHand}
        playerHand={gameState?.playerHand}
        isPlayerTurn={gameState?.currentPlayerTurn === currentPlayer?.name}
        onHit={handleHit}
        onStand={handleStand}
      />

      {/* Game Seats - All 6 player positions */}
      <GameSeats
        players={gameState?.players || []}
        roomCode={gameState?.roomCode}
        gameStatus={gameState?.status}
        currentPlayerTurn={gameState?.currentPlayerTurn}
        currentUser={currentUser.current}
        isViewer={isViewer}
        seatHubConnected={connectionStatus.seat}
        isComponentMounted={isComponentMounted.current}
        onError={setError}
        seatClickLoading={seatClickLoading}
        setSeatClickLoading={setSeatClickLoading}
        // Pass hand information for each player
        playersWithHands={gameState?.playersWithHands || []}
      />

      {/* Game Bettings - hidden for gameplay testing (handled by coworker) */}
      {false && (
        <GameBettings
          isPlayerSeated={isPlayerSeated}
          gameStatus={gameState?.status}
          isViewer={isViewer}
          currentPlayerBalance={1000}
          isPlayerTurn={gameState?.currentPlayerTurn === currentPlayer?.name}
          roomCode={gameState?.roomCode}
        />
      )}

      {/* Game Chat */}
      <GameChat
        currentUser={currentUser.current}
        isComponentMounted={isComponentMounted.current}
      />

    </div>
  )
}