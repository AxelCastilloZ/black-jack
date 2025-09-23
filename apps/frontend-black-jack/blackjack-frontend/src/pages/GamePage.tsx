// src/pages/GamePage.tsx - DIVIDIDO: Compatible con arquitectura de 3 hubs especializados
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

// Componentes
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
  // Auto-betting
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

// DIVIDIDO: Estado de conexión para 3 hubs especializados
interface ConnectionStatus {
  lobby: boolean
  gameRoom: boolean
  gameControl: boolean
  overall: boolean
  gameControlRequired: boolean
  gameControlConnecting: boolean
}

// Estados de Auto-Betting
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
  
  // DIVIDIDO: Estado de conexión para 3 hubs especializados
  const [connectionStatus, setConnectionStatus] = useState<ConnectionStatus>({
    lobby: false,
    gameRoom: false,
    gameControl: false,
    overall: false,
    gameControlRequired: false,
    gameControlConnecting: false
  })
  
  const [isJoining, setIsJoining] = useState(false)
  const [error, setError] = useState<string | null>(null)
  
  // Estado de loading centralizado
  const [seatClickLoading, setSeatClickLoading] = useState<number | null>(null)
  
  // Estados de Auto-Betting
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
  const gameControlSetupAttempted = useRef(false)

  // COMPUTED: Determinar si se necesita GameControl
  const needsGameControl = useCallback(() => {
    if (isViewer) return false
    
    // Se necesita GameControl si:
    // 1. Hay auto-betting activo (minBetPerRound > 0 y jugadores sentados)
    // 2. El usuario está sentado (puede iniciar juego o usar auto-betting)
    // 3. Es el host (puede controlar el juego)
    
    const hasAutoBetting = gameState?.minBetPerRound && gameState.minBetPerRound > 0
    const hasSeatedPlayers = gameState?.players?.some(p => p.position >= 0) || false
    const currentPlayer = gameState?.players?.find(p => p.playerId === currentUser.current?.id)
    const isSeated = currentPlayer && currentPlayer.position >= 0
    const isHost = currentPlayer?.isHost || false
    
    return Boolean(
      (hasAutoBetting && hasSeatedPlayers) || 
      isSeated || 
      isHost
    )
  }, [gameState, isViewer])

  // COMPUTED: Lógica centralizada de auto-betting
  const isAutoBettingActive = useCallback(() => {
    if (!gameState || !gameState.minBetPerRound || gameState.minBetPerRound <= 0) {
      return false
    }
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
    console.log('[GamePage] === COMPONENT MOUNT - DIVIDED HUBS ===')
    isComponentMounted.current = true
    
    const isViewerMode = window.location.pathname.includes('/viewer/')
    setIsViewer(isViewerMode)
    console.log('[GamePage] Mode detected:', isViewerMode ? 'VIEWER' : 'PLAYER')

    return () => {
      console.log('[GamePage] === COMPONENT UNMOUNT - DIVIDED HUBS ===')
      isComponentMounted.current = false
    }
  }, [])

  // DIVIDIDO: Verificar conexiones de 3 hubs especializados
  useEffect(() => {
    let mounted = true
    
    const checkConnections = () => {
      if (!mounted || !isComponentMounted.current) return
      
      const gameControlRequired = needsGameControl()
      
      const status: ConnectionStatus = {
        lobby: signalRService.isLobbyConnected,
        gameRoom: signalRService.isGameRoomHubConnected,
        gameControl: signalRService.isGameControlHubConnected,
        overall: signalRService.areBasicConnected,
        gameControlRequired,
        gameControlConnecting: connectionStatus.gameControlConnecting
      }
      
      setConnectionStatus(prev => {
        const hasChanged = JSON.stringify(prev) !== JSON.stringify(status)
        if (hasChanged) {
          console.log('[GamePage] DIVIDED hub connection status changed:', status)
        }
        return hasChanged ? status : prev
      })
    }
    
    checkConnections()
    const interval = setInterval(checkConnections, 2000)
    
    return () => {
      mounted = false
      clearInterval(interval)
    }
  }, [needsGameControl, connectionStatus.gameControlConnecting])

  // DIVIDIDO: Setup automático de GameControlHub cuando sea necesario
  useEffect(() => {
    let mounted = true
    
    const setupGameControlIfNeeded = async () => {
      if (
        !mounted || 
        !isComponentMounted.current ||
        !connectionStatus.gameControlRequired ||
        connectionStatus.gameControl ||
        connectionStatus.gameControlConnecting ||
        gameControlSetupAttempted.current
      ) {
        return
      }

      try {
        console.log('[GamePage] === SETTING UP GAMECONTROL HUB ===')
        console.log('[GamePage] Reason: Auto-betting or game control needed')
        
        setConnectionStatus(prev => ({ ...prev, gameControlConnecting: true }))
        gameControlSetupAttempted.current = true
        
        const success = await signalRService.startGameControlConnection()
        
        if (!mounted || !isComponentMounted.current) return
        
        if (success) {
          console.log('[GamePage] ✅ GameControlHub connected successfully')
          
          // Setup listeners de GameControl
          setupGameControlListeners()
          
          // Unirse al room para game control
          if (gameState?.roomCode) {
            try {
              await signalRService.joinRoomForGameControl(gameState.roomCode)
              console.log('[GamePage] ✅ Joined room for game control')
            } catch (error) {
              console.warn('[GamePage] Could not join room for game control:', error)
            }
          }
          
        } else {
          console.error('[GamePage] ❌ Failed to connect GameControlHub')
          addNotification({
            type: 'warning',
            title: 'GameControl No Disponible',
            message: 'Funcionalidad avanzada limitada. Reconectando...',
            duration: 8000
          })
        }
        
      } catch (error) {
        if (!mounted || !isComponentMounted.current) return
        console.error('[GamePage] Error setting up GameControlHub:', error)
        
        addNotification({
          type: 'error',
          title: 'Error GameControl',
          message: 'No se pudo conectar control de juego',
          duration: 10000
        })
      } finally {
        if (mounted && isComponentMounted.current) {
          setConnectionStatus(prev => ({ ...prev, gameControlConnecting: false }))
        }
      }
    }

    const timeoutId = setTimeout(setupGameControlIfNeeded, 500)
    
    return () => {
      mounted = false
      clearTimeout(timeoutId)
    }
  }, [connectionStatus.gameControlRequired, connectionStatus.gameControl, connectionStatus.gameControlConnecting, gameState?.roomCode])

  // DIVIDIDO: Handlers para GameRoomHub (funcionalidad básica)
  const handleRoomInfo = useCallback((response: any) => {
    if (!isComponentMounted.current) return
    const roomData = response?.data || response
    console.log('[GamePage] [GameRoomHub] Room info received:', roomData)
    setGameState(roomData)
    setError(null)
  }, [])

  const handleRoomCreated = useCallback((response: any) => {
    if (!isComponentMounted.current) return
    const roomData = response?.data || response
    console.log('[GamePage] [GameRoomHub] Room created:', roomData)
    setGameState(roomData)
    setError(null)
  }, [])

  const handleRoomJoined = useCallback((response: any) => {
    if (!isComponentMounted.current) return
    const roomData = response?.data || response
    console.log('[GamePage] [GameRoomHub] Room joined:', roomData)
    setGameState(roomData)
    setError(null)
  }, [])

  const handleRoomInfoUpdated = useCallback((roomData: any) => {
    if (!isComponentMounted.current) return
    console.log('[GamePage] [GameRoomHub] Room info updated:', roomData)
    setGameState(prev => ({
      ...roomData,
      autoBettingActive: roomData.autoBettingActive !== undefined ? roomData.autoBettingActive : prev?.autoBettingActive,
      minBetPerRound: roomData.minBetPerRound !== undefined ? roomData.minBetPerRound : prev?.minBetPerRound
    }))
    setError(null)
  }, [])

  const handleSeatJoined = useCallback((response: any) => {
    if (!isComponentMounted.current) return
    console.log('[GamePage] [GameRoomHub] Seat joined:', response)
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
    console.log('[GamePage] [GameRoomHub] Seat left:', response)
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
    console.log('[GamePage] [GameRoomHub] Player joined event:', eventData)
    
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
    console.log('[GamePage] [GameRoomHub] Player left event:', eventData)
    
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

  const handleError = useCallback((errorMessage: string) => {
    if (!isComponentMounted.current) return
    console.error('[GamePage] SignalR Error:', errorMessage)
    setError(errorMessage)
    setSeatClickLoading(null)
    console.log('[GamePage] Loading state reset after error')
  }, [])

  // DIVIDIDO: Handlers para GameControlHub (funcionalidad avanzada)
  const handleGameStarted = useCallback((gameData: any) => {
    if (!isComponentMounted.current) return
    console.log('[GamePage] [GameControlHub] Game started event:', gameData)
    setGameState(prev => prev ? { ...prev, status: 'InProgress' } : prev)
    
    addNotification({
      type: 'success',
      title: 'Juego Iniciado',
      message: 'El juego ha comenzado',
      duration: 4000
    })
  }, [addNotification])

  const handleGameEnded = useCallback((gameData: any) => {
    if (!isComponentMounted.current) return
    console.log('[GamePage] [GameControlHub] Game ended event:', gameData)
    setGameState(prev => prev ? { ...prev, status: 'Finished' } : prev)
    
    addNotification({
      type: 'info',
      title: 'Juego Terminado',
      message: 'El juego ha finalizado',
      duration: 4000
    })
  }, [addNotification])

  const handleAutoBetProcessed = useCallback((event: AutoBetProcessedEvent) => {
    if (!isComponentMounted.current) return
    console.log('[GamePage] [GameControlHub] 🎰 AutoBetProcessed event:', event)
    
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
    console.log('[GamePage] [GameControlHub] 📊 AutoBetStatistics event:', event)
    
    setAutoBettingState(prev => ({
      ...prev,
      statistics: event
    }))

    setGameState(prev => {
      if (!prev) return prev
      return {
        ...prev,
        minBetPerRound: event.minBetPerRound,
        autoBettingActive: event.seatedPlayersCount > 0 && event.minBetPerRound > 0
      }
    })
  }, [])

  const handleAutoBetProcessingStarted = useCallback((event: AutoBetProcessingStartedEvent) => {
    if (!isComponentMounted.current) return
    console.log('[GamePage] [GameControlHub] 🚀 AutoBetProcessingStarted event:', event)
    
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
    console.log('[GamePage] [GameControlHub] 📋 AutoBetRoundSummary event:', event)
    
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
    console.log('[GamePage] [GameControlHub] 🚪 PlayerRemovedFromSeat event:', event)
    
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
    console.log('[GamePage] [GameControlHub] 💰 PlayerBalanceUpdated event:', event)
    
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
    console.log('[GamePage] [GameControlHub] ⚠️ InsufficientFundsWarning event:', event)
    
    addNotification({
      type: 'warning',
      title: 'Advertencia: Fondos Insuficientes',
      message: `${event.playerName} no tiene fondos suficientes. Balance: $${event.currentBalance}, Requerido: $${event.requiredAmount}`,
      duration: 8000
    })
  }, [addNotification])

  const handleAutoBetFailed = useCallback((event: any) => {
    if (!isComponentMounted.current) return
    console.log('[GamePage] [GameControlHub] ❌ AutoBetFailed event:', event)
    
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

  // Handlers para eventos personales (GameControlHub)
  const handleYouWereRemovedFromSeat = useCallback((event: PlayerRemovedFromSeatEvent) => {
    if (!isComponentMounted.current) return
    console.log('[GamePage] [GameControlHub] 🔴 YouWereRemovedFromSeat event:', event)
    
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
    console.log('[GamePage] [GameControlHub] 💳 YourBalanceUpdated event:', event)
    
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
    console.log('[GamePage] [GameControlHub] 🟠 InsufficientFundsWarningPersonal event:', event)
    
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

  // DIVIDIDO: Setup de listeners GameRoomHub (funcionalidad básica)
  const setupGameRoomListeners = useCallback(() => {
    console.log('[GamePage] Setting up GameRoomHub listeners (basic functionality)...')
    
    signalRService.onRoomInfo = handleRoomInfo
    signalRService.onRoomCreated = handleRoomCreated
    signalRService.onRoomJoined = handleRoomJoined
    signalRService.onRoomInfoUpdated = handleRoomInfoUpdated
    signalRService.onSeatJoined = handleSeatJoined
    signalRService.onSeatLeft = handleSeatLeft
    signalRService.onPlayerJoined = handlePlayerJoined
    signalRService.onPlayerLeft = handlePlayerLeft
    signalRService.onError = handleError
  }, [
    handleRoomInfo, handleRoomCreated, handleRoomJoined, handleRoomInfoUpdated,
    handleSeatJoined, handleSeatLeft, handlePlayerJoined, handlePlayerLeft, handleError
  ])

  // DIVIDIDO: Setup de listeners GameControlHub (funcionalidad avanzada)
  const setupGameControlListeners = useCallback(() => {
    console.log('[GamePage] Setting up GameControlHub listeners (advanced functionality)...')
    
    signalRService.onGameStarted = handleGameStarted
    signalRService.onGameEnded = handleGameEnded
    signalRService.onAutoBetProcessed = handleAutoBetProcessed
    signalRService.onAutoBetStatistics = handleAutoBetStatistics
    signalRService.onAutoBetProcessingStarted = handleAutoBetProcessingStarted
    signalRService.onAutoBetRoundSummary = handleAutoBetRoundSummary
    signalRService.onPlayerRemovedFromSeat = handlePlayerRemovedFromSeat
    signalRService.onPlayerBalanceUpdated = handlePlayerBalanceUpdated
    signalRService.onInsufficientFundsWarning = handleInsufficientFundsWarning
    signalRService.onAutoBetFailed = handleAutoBetFailed
    signalRService.onYouWereRemovedFromSeat = handleYouWereRemovedFromSeat
    signalRService.onYourBalanceUpdated = handleYourBalanceUpdated
    signalRService.onInsufficientFundsWarningPersonal = handleInsufficientFundsWarningPersonal
  }, [
    handleGameStarted, handleGameEnded, handleAutoBetProcessed, handleAutoBetStatistics,
    handleAutoBetProcessingStarted, handleAutoBetRoundSummary, handlePlayerRemovedFromSeat,
    handlePlayerBalanceUpdated, handleInsufficientFundsWarning, handleAutoBetFailed,
    handleYouWereRemovedFromSeat, handleYourBalanceUpdated, handleInsufficientFundsWarningPersonal
  ])

  // DIVIDIDO: Setup inicial de listeners
  useEffect(() => {
    if (!isComponentMounted.current) return
    
    setupGameRoomListeners()

    return () => {
      console.log('[GamePage] Cleaning up ALL hub listeners...')
      
      // Limpiar GameRoomHub listeners
      signalRService.onRoomInfo = undefined
      signalRService.onRoomCreated = undefined
      signalRService.onRoomJoined = undefined
      signalRService.onRoomInfoUpdated = undefined
      signalRService.onSeatJoined = undefined
      signalRService.onSeatLeft = undefined
      signalRService.onPlayerJoined = undefined
      signalRService.onPlayerLeft = undefined
      signalRService.onError = undefined
      
      // Limpiar GameControlHub listeners
      signalRService.onGameStarted = undefined
      signalRService.onGameEnded = undefined
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
  }, [setupGameRoomListeners])

  // DIVIDIDO: Auto-join logic actualizado para nueva arquitectura
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

      if (!connectionStatus.gameRoom) {
        console.log('[GamePage] Waiting for GameRoomHub to connect...')
        return
      }

      try {
        if (!mounted || !isComponentMounted.current) return
        
        setIsJoining(true)
        hasJoinedTable.current = true
        setError(null)
        
        console.log(`[GamePage] === AUTO-JOINING TABLE - DIVIDED HUBS ===`)
        console.log(`[GamePage] TableId: ${tableId}`)
        console.log(`[GamePage] Mode: ${isViewer ? 'VIEWER' : 'PLAYER'}`)
        
        const playerName = currentUser.current?.displayName || (isViewer ? 'Viewer' : 'Jugador')
        
        if (isViewer) {
          await signalRService.joinOrCreateRoomForTableAsViewer(tableId, playerName)
        } else {
          await signalRService.joinOrCreateRoomForTable(tableId, playerName)
          console.log('[GamePage] Player joined table, GameControl will connect if needed')
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
  }, [connectionStatus.overall, connectionStatus.gameRoom, tableId, isViewer])

  // DIVIDIDO: Obtener estadísticas de auto-betting solo cuando GameControl esté disponible
  useEffect(() => {
    const autoBettingActive = isAutoBettingActive()
    
    if (
      gameState?.roomCode && 
      connectionStatus.gameControl && 
      autoBettingActive &&
      !autoBettingState.statistics &&
      !isViewer
    ) {
      console.log('[GamePage] [GameControlHub] Getting auto-bet statistics for room:', gameState.roomCode)
      
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

  // DIVIDIDO: Start round requiere GameControlHub
  const handleStartRound = useCallback(async () => {
    if (!gameState?.roomCode || !isComponentMounted.current) {
      console.log('[GamePage] Cannot start game - No room code')
      return
    }

    if (!connectionStatus.gameControl) {
      console.log('[GamePage] GameControlHub not connected, attempting to connect...')
      
      try {
        const success = await signalRService.startGameControlConnection()
        if (!success) {
          setError('No se pudo conectar al control de juego')
          return
        }
        
        // Setup listeners si no están configurados
        setupGameControlListeners()
        
        // Unirse al room para game control
        await signalRService.joinRoomForGameControl(gameState.roomCode)
        
      } catch (error) {
        console.error('[GamePage] Error connecting GameControlHub:', error)
        setError('Error conectando control de juego')
        return
      }
    }
    
    try {
      setError(null)
      console.log('[GamePage] [GameControlHub] Starting game')
      await signalRService.startGame(gameState.roomCode)
    } catch (error) {
      if (!isComponentMounted.current) return
      console.error('[GamePage] Error starting game:', error)
      setError(error instanceof Error ? error.message : 'Error iniciando juego')
    }
  }, [gameState?.roomCode, connectionStatus.gameControl, setupGameControlListeners])

  // DIVIDIDO: Process auto-bets requiere GameControlHub
  const handleProcessAutoBets = useCallback(async () => {
    if (!gameState?.roomCode || !connectionStatus.gameControl) {
      addNotification({
        type: 'error',
        title: 'GameControl No Disponible',
        message: 'Conectando control de juego...',
        duration: 5000
      })
      return
    }
    
    try {
      console.log('[GamePage] [GameControlHub] Processing auto-bets')
      await signalRService.processRoundAutoBets(gameState.roomCode, true)
    } catch (error) {
      console.error('[GamePage] Error processing auto-bets:', error)
      addNotification({
        type: 'error',
        title: 'Error Auto-Betting',
        message: error instanceof Error ? error.message : 'Error desconocido',
        duration: 8000
      })
    }
  }, [gameState?.roomCode, connectionStatus.gameControl, addNotification])

  // Computed values
  const currentPlayer = gameState?.players?.find(p => p.playerId === currentUser.current?.id)
  const isPlayerSeated = !!currentPlayer && currentPlayer.position >= 0
  const shouldShowGameControl = connectionStatus.gameControlRequired && !isViewer

  // DIVIDIDO: Loading screen para 3 hubs especializados
  if (!connectionStatus.overall || isJoining) {
    return (
      <div className="fixed top-0 left-0 w-screen h-screen bg-gradient-to-br from-emerald-900 to-emerald-800 z-[9999] overflow-hidden m-0 p-0 flex items-center justify-center">
        <div className="bg-black/80 rounded-xl p-8 text-center max-w-md">
          <div className="w-8 h-8 border-2 border-white border-t-transparent rounded-full animate-spin mx-auto mb-4"></div>
          <div className="text-white text-lg mb-4">
            {!connectionStatus.overall ? 'Conectando hubs divididos...' : 'Uniéndose a la mesa...'}
          </div>
          <div className="text-gray-300 text-sm mb-4">Mesa: {tableId?.slice(0, 8)}...</div>
          
          {!connectionStatus.overall && (
            <div className="text-xs text-gray-400 space-y-1 text-left">
              <div className="flex justify-between">
                <span>LobbyHub:</span>
                <span className={connectionStatus.lobby ? 'text-green-400' : 'text-yellow-400'}>
                  {connectionStatus.lobby ? '✓' : '⏳'}
                </span>
              </div>
              <div className="flex justify-between">
                <span>GameRoomHub:</span>
                <span className={connectionStatus.gameRoom ? 'text-green-400' : 'text-yellow-400'}>
                  {connectionStatus.gameRoom ? '✓' : '⏳'}
                </span>
              </div>
              {shouldShowGameControl && (
                <div className="flex justify-between">
                  <span>GameControlHub:</span>
                  <span className={
                    connectionStatus.gameControl ? 'text-green-400' : 
                    connectionStatus.gameControlConnecting ? 'text-yellow-400' : 
                    connectionStatus.gameControlRequired ? 'text-orange-400' : 'text-gray-500'
                  }>
                    {connectionStatus.gameControl ? '✓' : 
                     connectionStatus.gameControlConnecting ? '⏳' : 
                     connectionStatus.gameControlRequired ? '!' : '-'}
                  </span>
                </div>
              )}
            </div>
          )}
          
          {connectionStatus.gameControlRequired && !connectionStatus.gameControl && (
            <div className="text-xs text-orange-300 mt-2">
              Configurando control avanzado...
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
        onProcessAutoBets={shouldShowGameControl ? handleProcessAutoBets : undefined}
        autoBettingActive={isAutoBettingActive()}
        autoBettingProcessing={autoBettingState.isProcessing}
      />

      {/* Game Seats */}
      <GameSeats
        players={gameState?.players || []}
        roomCode={gameState?.roomCode}
        gameStatus={gameState?.status}
        currentPlayerTurn={gameState?.currentPlayerTurn}
        currentUser={currentUser.current}
        isViewer={isViewer}
        seatHubConnected={connectionStatus.gameRoom}
        isComponentMounted={isComponentMounted.current}
        onError={setError}
        seatClickLoading={seatClickLoading}
        setSeatClickLoading={setSeatClickLoading}
        autoBettingActive={isAutoBettingActive()}
        minBetPerRound={gameState?.minBetPerRound || 0}
      />

      {/* Game Chat */}
      <GameChat
        currentUser={currentUser.current}
        isComponentMounted={isComponentMounted.current}
      />

      {/* Debug Info - Solo en desarrollo */}
      {process.env.NODE_ENV === 'development' && (
        <div className="fixed bottom-4 left-4 bg-black/80 text-white text-xs p-2 rounded">
          <div>GameRoom: {connectionStatus.gameRoom ? '✓' : '✗'}</div>
          <div>GameControl: {connectionStatus.gameControl ? '✓' : connectionStatus.gameControlRequired ? '!' : '-'}</div>
          <div>AutoBetting: {isAutoBettingActive() ? '✓' : '✗'}</div>
        </div>
      )}

    </div>
  )
}