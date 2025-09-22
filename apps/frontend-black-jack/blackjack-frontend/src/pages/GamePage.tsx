// src/pages/GamePage.tsx - CORREGIDO: Estados sincronizados, lógica de auto-betting consistente
import React, { useEffect, useState, useCallback, useRef } from 'react'
import { useParams, useNavigate } from '@tanstack/react-router'
import { 
  signalRService, 
  AutoBetProcessedEvent, 
  AutoBetStatistics, 
  PlayerRemovedFromSeatEvent,
  PlayerBalanceUpdatedEvent,
  InsufficientFundsWarningEvent,
  AutoBetProcessingStartedEvent,
  AutoBetRoundSummaryEvent 
} from '../services/signalr'
import { authService } from '../services/auth'

// Componentes (GameBettings REMOVIDO)
import GameHeader from '../components/game/GameHeader'
import GameTable from '../components/game/GameTable'
import GameSeats from '../components/game/GameSeats'
import GameChat from '../components/game/GameChat'

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
  // Auto-betting - UNIFICADO: Solo una fuente de verdad
  minBetPerRound?: number
  autoBettingActive?: boolean
}

interface RoomPlayer {
  playerId: string
  name: string
  position: number
  isReady: boolean
  isHost: boolean
  hasPlayedTurn: boolean
  // Auto-betting stats
  currentBalance?: number
  totalBetThisSession?: number
  canAffordBet?: boolean
}

// Estados de Auto-Betting - SIMPLIFICADO: Sin duplicar isActive
interface AutoBettingState {
  isProcessing: boolean
  statistics: AutoBetStatistics | null
  lastProcessedResult: AutoBetProcessedEvent | null
  processingStartedAt: Date | null
  roundSummary: AutoBetRoundSummaryEvent | null
}

// Notificaciones
interface Notification {
  id: string
  type: 'success' | 'warning' | 'error' | 'info'
  title: string
  message: string
  timestamp: Date
  autoClose?: boolean
  duration?: number
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
  const [error, setError] = useState<string | null>(null)
  
  // Estado de loading centralizado
  const [seatClickLoading, setSeatClickLoading] = useState<number | null>(null)
  
  // Estados de Auto-Betting - SIMPLIFICADO: Sin isActive duplicado
  const [autoBettingState, setAutoBettingState] = useState<AutoBettingState>({
    isProcessing: false,
    statistics: null,
    lastProcessedResult: null,
    processingStartedAt: null,
    roundSummary: null
  })
  
  // Notificaciones
  const [notifications, setNotifications] = useState<Notification[]>([])
  
  // Refs
  const hasJoinedTable = useRef(false)
  const currentUser = useRef(authService.getCurrentUser())
  const isComponentMounted = useRef(true)

  // COMPUTED: Lógica centralizada de auto-betting
  const isAutoBettingActive = useCallback(() => {
    if (!gameState || !gameState.minBetPerRound || gameState.minBetPerRound <= 0) {
      return false
    }
    // Auto-betting está activo si hay jugadores sentados (position >= 0) y hay minBetPerRound
    const seatedPlayers = gameState.players?.filter(p => p.position >= 0) || []
    return seatedPlayers.length > 0
  }, [gameState])

  // Helper para agregar notificaciones
  const addNotification = useCallback((notification: Omit<Notification, 'id' | 'timestamp'>) => {
    if (!isComponentMounted.current) return
    
    const newNotification: Notification = {
      ...notification,
      id: Math.random().toString(36).substr(2, 9),
      timestamp: new Date()
    }
    
    setNotifications(prev => [newNotification, ...prev].slice(0, 5))
    
    if (notification.autoClose !== false) {
      const duration = notification.duration || 5000
      setTimeout(() => {
        removeNotification(newNotification.id)
      }, duration)
    }
  }, [])

  const removeNotification = useCallback((id: string) => {
    setNotifications(prev => prev.filter(n => n.id !== id))
  }, [])

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
    const interval = setInterval(checkConnections, 10000)
    
    return () => {
      mounted = false
      clearInterval(interval)
    }
  }, [])

  // Handlers para eventos SignalR existentes
  const handleRoomInfo = useCallback((response: any) => {
    if (!isComponentMounted.current) return
    const roomData = response?.data || response
    console.log('[GamePage] Room info received via RoomHub:', roomData)
    setGameState(roomData)
    setError(null)
  }, [])

  const handleRoomCreated = useCallback((response: any) => {
    if (!isComponentMounted.current) return
    const roomData = response?.data || response
    console.log('[GamePage] Room created via RoomHub:', roomData)
    setGameState(roomData)
    setError(null)
  }, [])

  const handleRoomJoined = useCallback((response: any) => {
    if (!isComponentMounted.current) return
    const roomData = response?.data || response
    console.log('[GamePage] Room joined via RoomHub:', roomData)
    setGameState(roomData)
    setError(null)
  }, [])

  const handleRoomInfoUpdated = useCallback((roomData: any) => {
    if (!isComponentMounted.current) return
    console.log('[GamePage] Room info updated via RoomHub:', roomData)
    setGameState(prev => ({
      ...roomData,
      // CORREGIDO: Mantener continuidad con datos existentes si no vienen en el update
      autoBettingActive: roomData.autoBettingActive !== undefined ? roomData.autoBettingActive : prev?.autoBettingActive,
      minBetPerRound: roomData.minBetPerRound !== undefined ? roomData.minBetPerRound : prev?.minBetPerRound
    }))
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
        hasPlayedTurn: false,
        currentBalance: eventData.currentBalance || 0,
        totalBetThisSession: eventData.totalBetThisSession || 0,
        canAffordBet: eventData.canAffordBet || false
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
    setGameState(newState)
  }, [])

  const handleError = useCallback((errorMessage: string) => {
    if (!isComponentMounted.current) return
    console.error('[GamePage] SignalR Error from any hub:', errorMessage)
    setError(errorMessage)
    setSeatClickLoading(null)
    console.log('[GamePage] Loading state reset after error')
  }, [])

  // Handlers para Auto-Betting Events
  const handleAutoBetProcessed = useCallback((event: AutoBetProcessedEvent) => {
    if (!isComponentMounted.current) return
    console.log('[GamePage] 🎰 AutoBetProcessed event:', event)
    
    setAutoBettingState(prev => ({
      ...prev,
      isProcessing: false,
      lastProcessedResult: event,
      processingStartedAt: null
    }))

    const { successfulBets, failedBets, totalAmountProcessed, playersRemovedFromSeats } = event
    
    addNotification({
      type: failedBets > 0 ? 'warning' : 'success',
      title: 'Apuestas Automáticas Procesadas',
      message: `Exitosas: ${successfulBets}, Fallidas: ${failedBets}, Total: $${totalAmountProcessed}${
        playersRemovedFromSeats > 0 ? `, ${playersRemovedFromSeats} removidos por fondos` : ''
      }`,
      duration: 6000
    })
  }, [addNotification])

  const handleAutoBetStatistics = useCallback((event: AutoBetStatistics) => {
    if (!isComponentMounted.current) return
    console.log('[GamePage] 📊 AutoBetStatistics event:', event)
    
    setAutoBettingState(prev => ({
      ...prev,
      statistics: event
    }))

    // CORREGIDO: Actualizar gameState con datos del evento
    setGameState(prev => {
      if (!prev) return prev
      return {
        ...prev,
        minBetPerRound: event.minBetPerRound,
        // LÓGICA CORREGIDA: autoBettingActive se determina por jugadores sentados, no por el evento
        autoBettingActive: event.seatedPlayersCount > 0 && event.minBetPerRound > 0
      }
    })
  }, [])

  const handleAutoBetProcessingStarted = useCallback((event: AutoBetProcessingStartedEvent) => {
    if (!isComponentMounted.current) return
    console.log('[GamePage] 🚀 AutoBetProcessingStarted event:', event)
    
    setAutoBettingState(prev => ({
      ...prev,
      isProcessing: true,
      processingStartedAt: new Date(event.startedAt)
    }))

    addNotification({
      type: 'info',
      title: 'Procesando Apuestas Automáticas',
      message: `${event.seatedPlayersCount} jugadores sentados, apuesta total: $${event.totalBetAmount}`,
      duration: 3000
    })
  }, [addNotification])

  const handleAutoBetRoundSummary = useCallback((event: AutoBetRoundSummaryEvent) => {
    if (!isComponentMounted.current) return
    console.log('[GamePage] 📋 AutoBetRoundSummary event:', event)
    
    setAutoBettingState(prev => ({
      ...prev,
      roundSummary: event
    }))

    event.notifications.forEach(notification => {
      addNotification({
        type: 'info',
        title: `Ronda ${event.roundNumber} Completada`,
        message: notification,
        duration: 4000
      })
    })
  }, [addNotification])

  const handlePlayerRemovedFromSeat = useCallback((event: PlayerRemovedFromSeatEvent) => {
    if (!isComponentMounted.current) return
    console.log('[GamePage] 🚪 PlayerRemovedFromSeat event:', event)
    
    addNotification({
      type: 'warning',
      title: 'Jugador Removido',
      message: `${event.playerName} fue removido del asiento ${event.seatPosition} - ${event.reason}`,
      duration: 7000
    })

    setGameState(prev => {
      if (!prev) return prev
      
      const updatedPlayers = prev.players.map(player => {
        if (player.playerId === event.playerId) {
          return { ...player, position: -1 }
        }
        return player
      })
      
      return {
        ...prev,
        players: updatedPlayers
      }
    })
  }, [addNotification])

  const handlePlayerBalanceUpdated = useCallback((event: PlayerBalanceUpdatedEvent) => {
    if (!isComponentMounted.current) return
    console.log('[GamePage] 💰 PlayerBalanceUpdated event:', event)
    
    setGameState(prev => {
      if (!prev) return prev
      
      const updatedPlayers = prev.players.map(player => {
        if (player.playerId === event.playerId) {
          return { 
            ...player, 
            currentBalance: event.newBalance,
            canAffordBet: prev.minBetPerRound ? event.newBalance >= prev.minBetPerRound : true
          }
        }
        return player
      })
      
      return {
        ...prev,
        players: updatedPlayers
      }
    })

    if (event.playerId === currentUser.current?.id) {
      const amountText = event.amountChanged > 0 
        ? `+$${event.amountChanged}` 
        : `-$${Math.abs(event.amountChanged)}`
      
      addNotification({
        type: event.amountChanged > 0 ? 'success' : 'warning',
        title: 'Balance Actualizado',
        message: `${amountText} (${event.changeReason}). Nuevo balance: $${event.newBalance}`,
        duration: 5000
      })
    }
  }, [addNotification])

  const handleInsufficientFundsWarning = useCallback((event: InsufficientFundsWarningEvent) => {
    if (!isComponentMounted.current) return
    console.log('[GamePage] ⚠️ InsufficientFundsWarning event:', event)
    
    addNotification({
      type: 'warning',
      title: 'Advertencia: Fondos Insuficientes',
      message: `${event.playerName} no tiene fondos suficientes. Balance: $${event.currentBalance}, Requerido: $${event.requiredAmount}`,
      duration: 8000
    })
  }, [addNotification])

  const handleAutoBetFailed = useCallback((event: any) => {
    if (!isComponentMounted.current) return
    console.log('[GamePage] ❌ AutoBetFailed event:', event)
    
    setAutoBettingState(prev => ({
      ...prev,
      isProcessing: false,
      processingStartedAt: null
    }))

    addNotification({
      type: 'error',
      title: 'Error en Apuestas Automáticas',
      message: event.errorMessage || 'Error desconocido en el procesamiento',
      duration: 10000,
      autoClose: false
    })
  }, [addNotification])

  // Handlers para eventos personales
  const handleYouWereRemovedFromSeat = useCallback((event: PlayerRemovedFromSeatEvent) => {
    if (!isComponentMounted.current) return
    console.log('[GamePage] 🔴 YouWereRemovedFromSeat event:', event)
    
    addNotification({
      type: 'error',
      title: 'Fuiste Removido del Asiento',
      message: `Removido del asiento ${event.seatPosition}: ${event.reason}. Balance: $${event.availableBalance}, Requerido: $${event.requiredAmount}`,
      duration: 12000,
      autoClose: false
    })
  }, [addNotification])

  const handleYourBalanceUpdated = useCallback((event: PlayerBalanceUpdatedEvent) => {
    if (!isComponentMounted.current) return
    console.log('[GamePage] 💳 YourBalanceUpdated event:', event)
    
    const amountText = event.amountChanged > 0 
      ? `+$${event.amountChanged}` 
      : `-$${Math.abs(event.amountChanged)}`
    
    addNotification({
      type: event.amountChanged > 0 ? 'success' : 'info',
      title: 'Tu Balance Actualizado',
      message: `${amountText} (${event.changeReason}). Nuevo balance: $${event.newBalance}`,
      duration: 6000
    })
  }, [addNotification])

  const handleInsufficientFundsWarningPersonal = useCallback((event: InsufficientFundsWarningEvent) => {
    if (!isComponentMounted.current) return
    console.log('[GamePage] 🟠 InsufficientFundsWarningPersonal event:', event)
    
    addNotification({
      type: 'warning',
      title: 'Fondos Insuficientes',
      message: `Tu balance ($${event.currentBalance}) es menor al requerido ($${event.requiredAmount}). ${
        event.willBeRemovedNextRound ? 'Serás removido en la próxima ronda.' : `Te quedan ${event.roundsRemaining} rondas.`
      }`,
      duration: 10000,
      autoClose: false
    })
  }, [addNotification])

  // Setup de listeners SignalR
  useEffect(() => {
    if (!isComponentMounted.current) return
    
    console.log('[GamePage] Setting up hub listeners including auto-betting...')
    
    // Listeners existentes
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

    // Listeners de auto-betting
    signalRService.onAutoBetProcessed = handleAutoBetProcessed
    signalRService.onAutoBetStatistics = handleAutoBetStatistics
    signalRService.onAutoBetProcessingStarted = handleAutoBetProcessingStarted
    signalRService.onAutoBetRoundSummary = handleAutoBetRoundSummary
    signalRService.onPlayerRemovedFromSeat = handlePlayerRemovedFromSeat
    signalRService.onPlayerBalanceUpdated = handlePlayerBalanceUpdated
    signalRService.onInsufficientFundsWarning = handleInsufficientFundsWarning
    signalRService.onAutoBetFailed = handleAutoBetFailed
    
    // Listeners personales
    signalRService.onYouWereRemovedFromSeat = handleYouWereRemovedFromSeat
    signalRService.onYourBalanceUpdated = handleYourBalanceUpdated
    signalRService.onInsufficientFundsWarningPersonal = handleInsufficientFundsWarningPersonal

    return () => {
      console.log('[GamePage] Cleaning up hub listeners including auto-betting...')
      
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
      
      signalRService.onAutoBetProcessed = undefined
      signalRService.onAutoBetStatistics = undefined
      signalRService.onAutoBetProcessingStarted = undefined
      signalRService.onAutoBetRoundSummary = undefined
      signalRService.onPlayerRemovedFromSeat = undefined
      signalRService.onPlayerBalanceUpdated = undefined
      signalRService.onInsufficientFundsWarning = undefined
      signalRService.onAutoBetFailed = undefined
      signalRService.onYouWereRemovedFromSeat = undefined
      signalRService.onYourBalanceUpdated = undefined
      signalRService.onInsufficientFundsWarningPersonal = undefined
    }
  }, [])

  // Auto-join logic - CORREGIDO: Sin auto-activar auto-betting
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
          // CORREGIDO: NO auto-activar auto-betting aquí
          console.log('[GamePage] Player joined table, auto-betting status will be determined by game state')
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

  // Obtener estadísticas de auto-betting - CORREGIDO: Usar lógica centralizada
  useEffect(() => {
    const autoBettingActive = isAutoBettingActive()
    
    if (
      gameState?.roomCode && 
      connectionStatus.gameControl && 
      autoBettingActive &&
      !autoBettingState.statistics &&
      !isViewer
    ) {
      console.log('[GamePage] Getting auto-bet statistics for room:', gameState.roomCode)
      
      signalRService.getAutoBetStatistics(gameState.roomCode)
        .catch(error => {
          console.warn('[GamePage] Could not get auto-bet statistics:', error)
        })
    }
  }, [gameState?.roomCode, connectionStatus.gameControl, isAutoBettingActive, autoBettingState.statistics, isViewer])

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
    if (!connectionStatus.gameControl || !gameState?.roomCode || !isComponentMounted.current) {
      console.log('[GamePage] Cannot start game - GameControlHub not connected')
      return
    }
    
    try {
      setError(null)
      console.log('[GamePage] Starting game via GameControlHub')
      await signalRService.startGame(gameState.roomCode)
    } catch (error) {
      if (!isComponentMounted.current) return
      console.error('[GamePage] Error starting game:', error)
      setError(error instanceof Error ? error.message : 'Error iniciando juego')
    }
  }, [connectionStatus.gameControl, gameState?.roomCode])

  // Computed values
  const currentPlayer = gameState?.players?.find(p => p.playerId === currentUser.current?.id)
  const isPlayerSeated = !!currentPlayer && currentPlayer.position >= 0

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

      {/* Notificaciones */}
      <div className="fixed top-20 right-4 space-y-2 z-30 max-w-md">
        {notifications.map((notification) => (
          <div
            key={notification.id}
            className={`
              rounded-lg shadow-lg p-4 border-l-4 transform transition-all duration-300 ease-in-out
              ${notification.type === 'success' ? 'bg-green-800/90 border-green-400 text-green-100' :
                notification.type === 'warning' ? 'bg-yellow-800/90 border-yellow-400 text-yellow-100' :
                notification.type === 'error' ? 'bg-red-800/90 border-red-400 text-red-100' :
                'bg-blue-800/90 border-blue-400 text-blue-100'}
            `}
          >
            <div className="flex items-start justify-between">
              <div className="flex-1">
                <div className="font-semibold text-sm">{notification.title}</div>
                <div className="text-xs mt-1 opacity-90">{notification.message}</div>
                <div className="text-xs mt-1 opacity-70">
                  {notification.timestamp.toLocaleTimeString()}
                </div>
              </div>
              <button
                onClick={() => removeNotification(notification.id)}
                className="ml-2 text-white/70 hover:text-white text-lg leading-none"
              >
                ×
              </button>
            </div>
          </div>
        ))}
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

      {/* Game Table - Dealer and Central Banner */}
      <GameTable
        gameStatus={gameState?.status}
        canStart={gameState?.canStart || false}
        isPlayerSeated={isPlayerSeated}
        isViewer={isViewer}
        isCurrentPlayerHost={currentPlayer?.isHost || false}
        gameControlConnected={connectionStatus.gameControl}
        onStartRound={handleStartRound}
      />

      {/* Game Seats - CORREGIDO: Props sincronizados */}
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
        // CORREGIDO: Usar función centralizada para determinar estado
        autoBettingActive={isAutoBettingActive()}
        minBetPerRound={gameState?.minBetPerRound || 0}
      />

      {/* Game Chat */}
      <GameChat
        currentUser={currentUser.current}
        isComponentMounted={isComponentMounted.current}
      />

    </div>
  )
}