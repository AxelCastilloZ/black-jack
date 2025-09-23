// src/services/signalr.ts - CON DEBUG COMPLETO PARA DIAGNOSTICAR EL PROBLEMA
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr'
import { authService } from './auth'

const API_BASE = import.meta.env.VITE_API_BASE_URL || 'http://localhost:7102'

export type ConnectionState = 'Disconnected' | 'Connecting' | 'Connected' | 'Reconnecting'

// ===== TIPOS E INTERFACES =====

// Auto-Betting Events
export interface AutoBetProcessedEvent {
  roomCode: string
  totalPlayersProcessed: number
  successfulBets: number
  failedBets: number
  playersRemovedFromSeats: number
  totalAmountProcessed: number
  playerResults: AutoBetPlayerResult[]
  processedAt: string
  hasErrors: boolean
  successRate: number
}

export interface AutoBetPlayerResult {
  playerId: string
  playerName: string
  seatPosition: number
  status: 'BetDeducted' | 'InsufficientFunds' | 'RemovedFromSeat' | 'Failed'
  originalBalance: number
  newBalance: number
  betAmount: number
  errorMessage?: string
}

export interface AutoBetStatistics {
  roomCode: string
  minBetPerRound: number
  seatedPlayersCount: number
  totalBetPerRound: number
  playersWithSufficientFunds: number
  playersWithInsufficientFunds: number
  totalAvailableFunds: number
  expectedSuccessfulBets: number
  expectedTotalDeduction: number
  playerDetails: PlayerAutoBetDetail[]
  calculatedAt: string
}

export interface PlayerAutoBetDetail {
  playerId: string
  playerName: string
  seatPosition: number
  currentBalance: number
  canAffordBet: boolean
  balanceAfterBet: number
  roundsAffordable: number
}

export interface PlayerRemovedFromSeatEvent {
  roomCode: string
  playerId: string
  playerName: string
  seatPosition: number
  requiredAmount: number
  availableBalance: number
  reason: string
  removedAt: string
}

export interface PlayerBalanceUpdatedEvent {
  roomCode: string
  playerId: string
  playerName: string
  previousBalance: number
  newBalance: number
  amountChanged: number
  changeReason: string
  updatedAt: string
}

export interface InsufficientFundsWarningEvent {
  roomCode: string
  playerId: string
  playerName: string
  currentBalance: number
  requiredAmount: number
  deficitAmount: number
  roundsRemaining: number
  willBeRemovedNextRound: boolean
  warningTime: string
}

export interface AutoBetProcessingStartedEvent {
  roomCode: string
  seatedPlayersCount: number
  minBetPerRound: number
  totalBetAmount: number
  startedAt: string
}

export interface AutoBetRoundSummaryEvent {
  roomCode: string
  roundNumber: number
  roundStartedAt: string
  roundCompletedAt: string
  processingDuration: number
  results: AutoBetProcessedEvent
  notifications: string[]
}

// Game Events
export interface GameStartedEventModel {
  roomCode: string
  gameTableId: string
  playerNames: string[]
  firstPlayerTurn: string
  timestamp: Date
}

export interface GameEndedEventModel {
  roomCode: string
  results: PlayerResultModel[]
  dealerHandValue: number
  winnerId?: string | null
  timestamp: Date
}

export interface PlayerResultModel {
  playerId: string
  playerName: string
  handValue: number
  result: 'Win' | 'Lose' | 'Push' | 'Bust'
  payout: number
}

// Room Events
export interface PlayerJoinedEventModel {
  roomCode: string
  playerId: string
  playerName: string
  position: number
  totalPlayers: number
  timestamp: Date
}

export interface PlayerLeftEventModel {
  roomCode: string
  playerId: string
  playerName: string
  remainingPlayers: number
  timestamp: Date
}

export interface RoomInfoModel {
  roomCode: string
  name: string
  status: string
  playerCount: number
  maxPlayers: number
  minBetPerRound: number
  autoBettingActive: boolean
  players: RoomPlayerModel[]
  spectators: SpectatorModel[]
  currentPlayerTurn?: string
  canStart: boolean
  createdAt: Date
}

export interface RoomPlayerModel {
  playerId: string
  name: string
  position: number
  isReady: boolean
  isHost: boolean
  hasPlayedTurn: boolean
  currentBalance: number
  totalBetThisSession: number
  canAffordBet: boolean
}

export interface SpectatorModel {
  playerId: string
  name: string
  joinedAt: Date
}

export interface ActiveRoomModel {
  roomCode: string
  name: string
  playerCount: number
  maxPlayers: number
  status: string
  createdAt: Date
}

// Request Types
export interface CreateRoomRequest {
  roomName: string
  maxPlayers?: number
}

export interface JoinRoomRequest {
  roomCode: string
  playerName: string
}

export interface JoinSeatRequest {
  roomCode: string
  position: number
}

export interface LeaveSeatRequest {
  roomCode: string
}

// ===== SERVICIO PRINCIPAL =====

class SignalRService {
  // 3 HUBS ESPECIALIZADOS
  private lobbyConnection?: HubConnection         // /hubs/lobby
  private gameRoomConnection?: HubConnection      // /hubs/game-room
  private gameControlConnection?: HubConnection   // /hubs/game-control
  
  private isStarting = false
  private isDestroying = false

  // ===== CALLBACKS DE EVENTOS =====
  
  // Eventos de Room Management
  public onPlayerJoined?: (event: PlayerJoinedEventModel) => void
  public onPlayerLeft?: (event: PlayerLeftEventModel) => void
  public onRoomInfo?: (roomData: RoomInfoModel) => void
  public onRoomCreated?: (roomData: RoomInfoModel) => void
  public onRoomJoined?: (data: RoomInfoModel) => void
  public onRoomInfoUpdated?: (roomData: RoomInfoModel) => void
  public onRoomLeft?: (data: any) => void
  public onSeatJoined?: (data: any) => void
  public onSeatLeft?: (data: any) => void
  
  // Eventos de Game State
  public onGameStateChanged?: (gameState: any) => void
  public onGameStateUpdated?: (gameState: any) => void
  public onGameStarted?: (event: GameStartedEventModel) => void
  public onGameEnded?: (event: GameEndedEventModel) => void
  public onTurnChanged?: (data: any) => void
  public onCardDealt?: (data: any) => void
  public onPlayerActionPerformed?: (data: any) => void
  public onBetPlaced?: (data: any) => void
  
  // Eventos de Error/Success
  public onError?: (message: string) => void
  public onSuccess?: (data: any) => void

  // Eventos de Auto-Betting
  public onAutoBetProcessed?: (event: AutoBetProcessedEvent) => void
  public onAutoBetStatistics?: (event: AutoBetStatistics) => void
  public onAutoBetProcessingStarted?: (event: AutoBetProcessingStartedEvent) => void
  public onAutoBetRoundSummary?: (event: AutoBetRoundSummaryEvent) => void
  public onPlayerRemovedFromSeat?: (event: PlayerRemovedFromSeatEvent) => void
  public onPlayerBalanceUpdated?: (event: PlayerBalanceUpdatedEvent) => void
  public onInsufficientFundsWarning?: (event: InsufficientFundsWarningEvent) => void
  public onAutoBetFailed?: (event: any) => void
  public onMinBetPerRoundUpdated?: (event: any) => void
  
  // Eventos personales de Auto-Betting
  public onYouWereRemovedFromSeat?: (event: PlayerRemovedFromSeatEvent) => void
  public onYourBalanceUpdated?: (event: PlayerBalanceUpdatedEvent) => void
  public onInsufficientFundsWarningPersonal?: (event: InsufficientFundsWarningEvent) => void
  public onAutoBetFailedPersonal?: (event: any) => void

  // Eventos del Lobby
  public onActiveRoomsUpdated?: (rooms: ActiveRoomModel[]) => void
  public onLobbyStats?: (stats: any) => void
  public onQuickJoinRedirect?: (data: any) => void
  public onQuickJoinTableRedirect?: (data: any) => void
  public onDetailedRoomInfo?: (data: any) => void

  // ===== FUNCIÓN HELPER PARA NORMALIZAR currentPlayerTurn =====
  private normalizeCurrentPlayerTurn(gameState: any, eventName: string): any {
    if (!gameState || !gameState.currentPlayerTurn) {
      console.log(`[SignalR-${eventName}] No currentPlayerTurn to normalize`)
      return gameState
    }

    // Detectar si currentPlayerTurn es un GUID (formato: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx)
    const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i
    
    if (guidPattern.test(gameState.currentPlayerTurn)) {
      // Ya es un GUID, no necesita normalización
      console.log(`[SignalR-${eventName}] currentPlayerTurn is already GUID:`, gameState.currentPlayerTurn)
      return gameState
    }

    // Es un nombre, necesitamos encontrar el GUID correspondiente
    const playerName = gameState.currentPlayerTurn
    console.log(`[SignalR-${eventName}] currentPlayerTurn is name, need to find GUID for:`, playerName)

    if (gameState.players && Array.isArray(gameState.players)) {
      const player = gameState.players.find((p: any) => p.name === playerName || p.playerName === playerName)
      
      if (player && player.playerId) {
        console.log(`[SignalR-${eventName}] Found player GUID:`, player.playerId, 'for name:', playerName)
        
        // Crear copia del gameState con el GUID normalizado
        const normalizedGameState = {
          ...gameState,
          currentPlayerTurn: player.playerId
        }
        
        console.log(`[SignalR-${eventName}] Normalized currentPlayerTurn from`, playerName, 'to', player.playerId)
        return normalizedGameState
      } else {
        console.warn(`[SignalR-${eventName}] Could not find player with name:`, playerName, 'in players:', gameState.players)
      }
    } else {
      console.warn(`[SignalR-${eventName}] No players array found in gameState:`, gameState)
    }

    // Si no pudimos normalizar, devolver el estado original
    console.warn(`[SignalR-${eventName}] Could not normalize currentPlayerTurn, returning original:`, gameState.currentPlayerTurn)
    return gameState
  }

  private buildConnection(hubPath: string): HubConnection {
    const fullUrl = `${API_BASE}${hubPath}`
    
    console.log(`[SignalR] Building connection to: ${fullUrl}`)
    
    return new HubConnectionBuilder()
      .withUrl(fullUrl, {
        accessTokenFactory: () => {
          const token = authService.getToken()
          if (!token) {
            console.error('[SignalR] No token available!')
            return ''
          }
          
          const cleanToken = token.replace(/^Bearer\s+/i, '').trim()
          
          // Validar token
          const parts = cleanToken.split('.')
          if (parts.length === 3) {
            try {
              const payload = JSON.parse(atob(parts[1]))
              const now = Math.floor(Date.now() / 1000)
              
              if (payload.exp && payload.exp < now) {
                console.error('[SignalR] Token expired!')
                return ''
              }
            } catch (e) {
              console.error('[SignalR] Cannot decode token:', e)
              return ''
            }
          }
          
          return cleanToken
        },
        skipNegotiation: true,
        transport: 1 // WebSockets only
      })
      .withAutomaticReconnect([2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Information)
      .build()
  }

  private setupConnectionHandlers(connection: HubConnection, name: string) {
    console.log(`[SignalR] Setting up handlers for ${name}`)
    
    // Handlers de conexión
    connection.onreconnecting((error) => {
      if (this.isDestroying) return
      console.log(`[SignalR] ${name} reconnecting...`, error)
    })
    
    connection.onreconnected((connectionId) => {
      if (this.isDestroying) return
      console.log(`[SignalR] ${name} reconnected with ID:`, connectionId)
    })
    
    connection.onclose(error => {
      if (this.isDestroying) {
        console.log(`[SignalR] ${name} closed normally during cleanup`)
        return
      }
      
      if (error) {
        console.error(`[SignalR] ${name} closed with error:`, error)
      } else {
        console.log(`[SignalR] ${name} closed normally`)
      }
    })

    // Handlers comunes CON DEBUG
    connection.on('Success', (successData: any) => {
      if (this.isDestroying) return
      const message = successData?.message || successData
      console.log(`[SignalR-${name}] Success:`, message)
      
      // DEBUG: Buscar currentPlayerTurn en Success
      if (successData && typeof successData === 'object') {
        this.debugCurrentPlayerTurn(successData, `Success-${name}`)
      }
      
      this.onSuccess?.(successData)
    })

    connection.on('Error', (errorData: any) => {
      if (this.isDestroying) return
      const message = errorData?.message || errorData
      console.error(`[SignalR-${name}] Error:`, message)
      this.onError?.(message)
    })

    connection.on('TestResponse', (data: any) => {
      if (this.isDestroying) return
      console.log(`[SignalR-${name}] TestResponse:`, data)
    })

    // Configurar handlers específicos de cada hub
    this.setupSpecificHandlers(connection, name)
  }

  // ===== FUNCIÓN DEBUG PARA DETECTAR currentPlayerTurn =====
  private debugCurrentPlayerTurn(data: any, eventName: string) {
    if (!data) return
    
    // Buscar currentPlayerTurn en cualquier nivel del objeto
    const searchForCurrentPlayerTurn = (obj: any, path: string = ''): void => {
      if (!obj || typeof obj !== 'object') return
      
      for (const key in obj) {
        const fullPath = path ? `${path}.${key}` : key
        const value = obj[key]
        
        if (key === 'currentPlayerTurn' && value) {
          console.log(`[DEBUG-${eventName}] Found currentPlayerTurn at ${fullPath}:`, value, typeof value)
        } else if (key === 'currentTurn' && value) {
          console.log(`[DEBUG-${eventName}] Found currentTurn at ${fullPath}:`, value, typeof value)
        } else if (typeof value === 'object' && value !== null) {
          searchForCurrentPlayerTurn(value, fullPath)
        }
      }
    }
    
    searchForCurrentPlayerTurn(data, eventName)
  }

  private setupSpecificHandlers(connection: HubConnection, name: string) {
    switch (name) {
      case 'Lobby':
        this.setupLobbyHandlers(connection)
        break
      case 'GameRoom':
        this.setupGameRoomHandlers(connection)
        break
      case 'GameControl':
        this.setupGameControlHandlers(connection)
        break
    }
  }

  private setupLobbyHandlers(connection: HubConnection) {
    // Eventos del LobbyHub CON DEBUG
    connection.on('ActiveRoomsUpdated', (response: any) => {
      if (this.isDestroying) return
      console.log('[SignalR-Lobby] ActiveRoomsUpdated:', response)
      this.debugCurrentPlayerTurn(response, 'ActiveRoomsUpdated')
      this.onActiveRoomsUpdated?.(response)
    })

    connection.on('LobbyStats', (stats: any) => {
      if (this.isDestroying) return
      console.log('[SignalR-Lobby] LobbyStats:', stats)
      this.debugCurrentPlayerTurn(stats, 'LobbyStats')
      this.onLobbyStats?.(stats)
    })

    connection.on('QuickJoinRedirect', (data: any) => {
      if (this.isDestroying) return
      console.log('[SignalR-Lobby] QuickJoinRedirect:', data)
      this.debugCurrentPlayerTurn(data, 'QuickJoinRedirect')
      this.onQuickJoinRedirect?.(data)
    })

    connection.on('QuickJoinTableRedirect', (data: any) => {
      if (this.isDestroying) return
      console.log('[SignalR-Lobby] QuickJoinTableRedirect:', data)
      this.debugCurrentPlayerTurn(data, 'QuickJoinTableRedirect')
      this.onQuickJoinTableRedirect?.(data)
    })

    connection.on('DetailedRoomInfo', (data: any) => {
      if (this.isDestroying) return
      console.log('[SignalR-Lobby] DetailedRoomInfo:', data)
      this.debugCurrentPlayerTurn(data, 'DetailedRoomInfo')
      this.onDetailedRoomInfo?.(data)
    })
  }

  private setupGameRoomHandlers(connection: HubConnection) {
    // Eventos de salas CON DEBUG Y NORMALIZACIÓN
    connection.on('RoomCreated', (response: any) => {
      if (this.isDestroying) return
      console.log('[SignalR-GameRoom] RoomCreated:', response)
      this.debugCurrentPlayerTurn(response, 'RoomCreated')
      
      const processedData = response?.data || response
      const normalizedData = this.normalizeCurrentPlayerTurn(processedData, 'RoomCreated')
      this.onRoomCreated?.(normalizedData)
    })

    connection.on('RoomJoined', (response: any) => {
      if (this.isDestroying) return
      console.log('[SignalR-GameRoom] RoomJoined:', response)
      this.debugCurrentPlayerTurn(response, 'RoomJoined')
      
      const processedData = response?.data || response
      const normalizedData = this.normalizeCurrentPlayerTurn(processedData, 'RoomJoined')
      this.onRoomJoined?.(normalizedData)
    })

    connection.on('RoomInfoUpdated', (roomData: any) => {
      if (this.isDestroying) return
      console.log('[SignalR-GameRoom] RoomInfoUpdated:', roomData)
      this.debugCurrentPlayerTurn(roomData, 'RoomInfoUpdated')
      
      const processedData = roomData?.data || roomData
      const normalizedData = this.normalizeCurrentPlayerTurn(processedData, 'RoomInfoUpdated')
      this.onRoomInfoUpdated?.(normalizedData)
    })

    connection.on('RoomInfo', (roomData: any) => {
      if (this.isDestroying) return
      console.log('[SignalR-GameRoom] RoomInfo:', roomData)
      this.debugCurrentPlayerTurn(roomData, 'RoomInfo')
      
      const normalizedData = this.normalizeCurrentPlayerTurn(roomData, 'RoomInfo')
      this.onRoomInfo?.(normalizedData)
    })

    connection.on('RoomLeft', (data: any) => {
      if (this.isDestroying) return
      console.log('[SignalR-GameRoom] RoomLeft:', data)
      this.debugCurrentPlayerTurn(data, 'RoomLeft')
      this.onRoomLeft?.(data)
    })

    // Eventos de jugadores CON DEBUG Y NORMALIZACIÓN
    connection.on('PlayerJoined', (data: any) => {
      if (this.isDestroying) return
      console.log('[SignalR-GameRoom] PlayerJoined:', data)
      this.debugCurrentPlayerTurn(data, 'PlayerJoined')
      
      const normalizedData = this.normalizeCurrentPlayerTurn(data, 'PlayerJoined')
      this.onPlayerJoined?.(normalizedData)
    })

    connection.on('PlayerLeft', (data: any) => {
      if (this.isDestroying) return
      console.log('[SignalR-GameRoom] PlayerLeft:', data)
      this.debugCurrentPlayerTurn(data, 'PlayerLeft')
      
      const normalizedData = this.normalizeCurrentPlayerTurn(data, 'PlayerLeft')
      this.onPlayerLeft?.(normalizedData)
    })

    // Eventos de asientos CON DEBUG Y NORMALIZACIÓN
    connection.on('SeatJoined', (response: any) => {
      if (this.isDestroying) return
      console.log('[SignalR-GameRoom] SeatJoined:', response)
      this.debugCurrentPlayerTurn(response, 'SeatJoined')
      
      const processedData = response?.data || response
      const normalizedData = this.normalizeCurrentPlayerTurn(processedData, 'SeatJoined')
      this.onSeatJoined?.(normalizedData)
    })

    connection.on('SeatLeft', (response: any) => {
      if (this.isDestroying) return
      console.log('[SignalR-GameRoom] SeatLeft:', response)
      this.debugCurrentPlayerTurn(response, 'SeatLeft')
      
      const processedData = response?.data || response
      const normalizedData = this.normalizeCurrentPlayerTurn(processedData, 'SeatLeft')
      this.onSeatLeft?.(normalizedData)
    })
  }

  private setupGameControlHandlers(connection: HubConnection) {
    // Eventos principales del juego CON DEBUG Y NORMALIZACIÓN
    connection.on('GameStarted', (gameData: any) => {
      if (this.isDestroying) return
      console.log('[SignalR-GameControl] GameStarted:', gameData)
      this.debugCurrentPlayerTurn(gameData, 'GameStarted')
      
      const processedData = gameData?.data || gameData
      const normalizedData = this.normalizeCurrentPlayerTurn(processedData, 'GameStarted')
      this.onGameStarted?.(normalizedData)
      this.onGameStateChanged?.(normalizedData)
    })

    connection.on('GameEnded', (gameData: any) => {
      if (this.isDestroying) return
      console.log('[SignalR-GameControl] GameEnded:', gameData)
      this.debugCurrentPlayerTurn(gameData, 'GameEnded')
      
      const processedData = gameData?.data || gameData
      const normalizedData = this.normalizeCurrentPlayerTurn(processedData, 'GameEnded')
      this.onGameEnded?.(normalizedData)
      this.onGameStateChanged?.(normalizedData)
    })

    // HANDLER PRINCIPAL CON NORMALIZACIÓN Y DEBUG
    connection.on('GameStateUpdated', (gameState: any) => {
      if (this.isDestroying) return
      console.log('[SignalR-GameControl] GameStateUpdated (original):', gameState)
      this.debugCurrentPlayerTurn(gameState, 'GameStateUpdated')
      
      // NORMALIZAR currentPlayerTurn antes de procesar
      const normalizedGameState = this.normalizeCurrentPlayerTurn(gameState, 'GameStateUpdated')
      
      console.log('[SignalR-GameControl] GameStateUpdated (normalized):', normalizedGameState)
      this.onGameStateUpdated?.(normalizedGameState)
      this.onGameStateChanged?.(normalizedGameState)
    })

    // HANDLER ADICIONAL PARA MINÚSCULAS CON NORMALIZACIÓN Y DEBUG
    connection.on('gamestateupdated', (gameState: any) => {
      if (this.isDestroying) return
      console.log('[SignalR-GameControl] gamestateupdated (lowercase, original):', gameState)
      this.debugCurrentPlayerTurn(gameState, 'gamestateupdated')
      
      // NORMALIZAR currentPlayerTurn antes de procesar
      const normalizedGameState = this.normalizeCurrentPlayerTurn(gameState, 'gamestateupdated')
      
      console.log('[SignalR-GameControl] gamestateupdated (lowercase, normalized):', normalizedGameState)
      this.onGameStateUpdated?.(normalizedGameState)
      this.onGameStateChanged?.(normalizedGameState)
    })

    connection.on('TurnChanged', (data: any) => {
      if (this.isDestroying) return
      console.log('[SignalR-GameControl] TurnChanged:', data)
      this.debugCurrentPlayerTurn(data, 'TurnChanged')
      
      const normalizedData = this.normalizeCurrentPlayerTurn(data, 'TurnChanged')
      this.onTurnChanged?.(normalizedData)
      this.onGameStateChanged?.(normalizedData)
    })

    // Eventos de acciones del juego CON DEBUG Y NORMALIZACIÓN
    connection.on('CardDealt', (data: any) => {
      if (this.isDestroying) return
      console.log('[SignalR-GameControl] CardDealt:', data)
      this.debugCurrentPlayerTurn(data, 'CardDealt')
      
      const normalizedData = this.normalizeCurrentPlayerTurn(data, 'CardDealt')
      this.onCardDealt?.(normalizedData)
      this.onGameStateChanged?.(normalizedData)
    })

    connection.on('PlayerActionPerformed', (data: any) => {
      if (this.isDestroying) return
      console.log('[SignalR-GameControl] PlayerActionPerformed:', data)
      this.debugCurrentPlayerTurn(data, 'PlayerActionPerformed')
      
      const normalizedData = this.normalizeCurrentPlayerTurn(data, 'PlayerActionPerformed')
      this.onPlayerActionPerformed?.(normalizedData)
      this.onGameStateChanged?.(normalizedData)
    })

    connection.on('BetPlaced', (data: any) => {
      if (this.isDestroying) return
      console.log('[SignalR-GameControl] BetPlaced:', data)
      this.debugCurrentPlayerTurn(data, 'BetPlaced')
      
      const normalizedData = this.normalizeCurrentPlayerTurn(data, 'BetPlaced')
      this.onBetPlaced?.(normalizedData)
      this.onGameStateChanged?.(normalizedData)
    })

    // Eventos de Auto-Betting CON DEBUG
    connection.on('AutoBetProcessed', (event: AutoBetProcessedEvent) => {
      if (this.isDestroying) return
      console.log('[SignalR-GameControl] AutoBetProcessed:', event)
      this.debugCurrentPlayerTurn(event, 'AutoBetProcessed')
      this.onAutoBetProcessed?.(event)
    })

    connection.on('AutoBetStatistics', (event: AutoBetStatistics) => {
      if (this.isDestroying) return
      console.log('[SignalR-GameControl] AutoBetStatistics:', event)
      this.debugCurrentPlayerTurn(event, 'AutoBetStatistics')
      this.onAutoBetStatistics?.(event)
    })

    connection.on('AutoBetProcessingStarted', (event: AutoBetProcessingStartedEvent) => {
      if (this.isDestroying) return
      console.log('[SignalR-GameControl] AutoBetProcessingStarted:', event)
      this.debugCurrentPlayerTurn(event, 'AutoBetProcessingStarted')
      this.onAutoBetProcessingStarted?.(event)
    })

    connection.on('AutoBetRoundSummary', (event: AutoBetRoundSummaryEvent) => {
      if (this.isDestroying) return
      console.log('[SignalR-GameControl] AutoBetRoundSummary:', event)
      this.debugCurrentPlayerTurn(event, 'AutoBetRoundSummary')
      this.onAutoBetRoundSummary?.(event)
    })

    connection.on('PlayerRemovedFromSeat', (event: PlayerRemovedFromSeatEvent) => {
      if (this.isDestroying) return
      console.log('[SignalR-GameControl] PlayerRemovedFromSeat:', event)
      this.debugCurrentPlayerTurn(event, 'PlayerRemovedFromSeat')
      this.onPlayerRemovedFromSeat?.(event)
    })

    connection.on('PlayerBalanceUpdated', (event: PlayerBalanceUpdatedEvent) => {
      if (this.isDestroying) return
      console.log('[SignalR-GameControl] PlayerBalanceUpdated:', event)
      this.debugCurrentPlayerTurn(event, 'PlayerBalanceUpdated')
      this.onPlayerBalanceUpdated?.(event)
    })

    connection.on('InsufficientFundsWarning', (event: InsufficientFundsWarningEvent) => {
      if (this.isDestroying) return
      console.log('[SignalR-GameControl] InsufficientFundsWarning:', event)
      this.debugCurrentPlayerTurn(event, 'InsufficientFundsWarning')
      this.onInsufficientFundsWarning?.(event)
    })

    connection.on('AutoBetFailed', (event: any) => {
      if (this.isDestroying) return
      console.log('[SignalR-GameControl] AutoBetFailed:', event)
      this.debugCurrentPlayerTurn(event, 'AutoBetFailed')
      this.onAutoBetFailed?.(event)
    })

    connection.on('MinBetPerRoundUpdated', (event: any) => {
      if (this.isDestroying) return
      console.log('[SignalR-GameControl] MinBetPerRoundUpdated:', event)
      this.debugCurrentPlayerTurn(event, 'MinBetPerRoundUpdated')
      this.onMinBetPerRoundUpdated?.(event)
    })

    // Eventos personales CON DEBUG
    connection.on('YouWereRemovedFromSeat', (event: PlayerRemovedFromSeatEvent) => {
      if (this.isDestroying) return
      console.log('[SignalR-GameControl] YouWereRemovedFromSeat:', event)
      this.debugCurrentPlayerTurn(event, 'YouWereRemovedFromSeat')
      this.onYouWereRemovedFromSeat?.(event)
    })

    connection.on('YourBalanceUpdated', (event: PlayerBalanceUpdatedEvent) => {
      if (this.isDestroying) return
      console.log('[SignalR-GameControl] YourBalanceUpdated:', event)
      this.debugCurrentPlayerTurn(event, 'YourBalanceUpdated')
      this.onYourBalanceUpdated?.(event)
    })

    connection.on('InsufficientFundsWarningPersonal', (event: InsufficientFundsWarningEvent) => {
      if (this.isDestroying) return
      console.log('[SignalR-GameControl] InsufficientFundsWarningPersonal:', event)
      this.debugCurrentPlayerTurn(event, 'InsufficientFundsWarningPersonal')
      this.onInsufficientFundsWarningPersonal?.(event)
    })

    connection.on('AutoBetFailedPersonal', (event: any) => {
      if (this.isDestroying) return
      console.log('[SignalR-GameControl] AutoBetFailedPersonal:', event)
      this.debugCurrentPlayerTurn(event, 'AutoBetFailedPersonal')
      this.onAutoBetFailedPersonal?.(event)
    })

    // ===== HANDLERS WILDCARDS PARA DETECTAR EVENTOS DESCONOCIDOS =====
    
    // Capturar CUALQUIER evento que pueda tener currentPlayerTurn
    const originalOn = connection.on.bind(connection)
    connection.on = (methodName: string, newMethod: (...args: any[]) => void) => {
      // Envolver el método original para agregar debug
      const wrappedMethod = (...args: any[]) => {
        console.log(`[SignalR-GameControl] CAPTURED EVENT: ${methodName}`, args)
        
        // Buscar currentPlayerTurn en todos los argumentos
        args.forEach((arg, index) => {
          if (arg && typeof arg === 'object') {
            this.debugCurrentPlayerTurn(arg, `${methodName}[${index}]`)
          }
        })
        
        // Llamar al método original
        newMethod(...args)
      }
      
      // Llamar al on original con el método envuelto
      return originalOn(methodName, wrappedMethod)
    }
  }

  async startConnections(): Promise<boolean> {
    if (this.isStarting) {
      console.log('[SignalR] Start already in progress...')
      let attempts = 0
      while (this.isStarting && attempts < 50) {
        await new Promise(resolve => setTimeout(resolve, 100))
        attempts++
      }
      return this.areAllConnected
    }
    
    if (!authService.isAuthenticated()) {
      console.error('[SignalR] Cannot start - not authenticated')
      return false
    }

    this.isStarting = true
    this.isDestroying = false

    try {
      console.log('[SignalR] Starting 3 specialized connections...')

      const hubsToCreate = [
        { name: 'Lobby', path: '/hubs/lobby', prop: 'lobbyConnection' },
        { name: 'GameRoom', path: '/hubs/game-room', prop: 'gameRoomConnection' },
        { name: 'GameControl', path: '/hubs/game-control', prop: 'gameControlConnection' }
      ]

      const tasks: Promise<void>[] = []

      for (const hub of hubsToCreate) {
        const connection = (this as any)[hub.prop]
        
        if (!connection || connection.state === HubConnectionState.Disconnected) {
          console.log(`[SignalR] Creating ${hub.name} connection...`)
          ;(this as any)[hub.prop] = this.buildConnection(hub.path)
          this.setupConnectionHandlers((this as any)[hub.prop], hub.name)
        }

        if ((this as any)[hub.prop].state === HubConnectionState.Disconnected) {
          console.log(`[SignalR] Starting ${hub.name} connection...`)
          tasks.push(
            (this as any)[hub.prop].start().then(() => {
              console.log(`[SignalR] ${hub.name} started successfully`)
            }).catch((error: any) => {
              console.error(`[SignalR] ${hub.name} failed:`, error)
              throw error
            })
          )
        } else {
          console.log(`[SignalR] ${hub.name} already active`)
        }
      }

      if (tasks.length > 0) {
        await Promise.all(tasks)
        console.log('[SignalR] All connections started')
      }

      // Auto-join lobby
      if (!this.isDestroying && this.lobbyConnection?.state === HubConnectionState.Connected) {
        try {
          await this.lobbyConnection.invoke('JoinLobby')
          console.log('[SignalR] Joined lobby')
        } catch (error) {
          console.warn('[SignalR] Could not join lobby:', error)
        }
      }

      console.log('[SignalR] All connections ready')
      return true

    } catch (error: any) {
      console.error('[SignalR] Failed to start:', error)
      return false
    } finally {
      this.isStarting = false
    }
  }

  async stopConnections() {
    console.log('[SignalR] Stopping all connections...')
    this.isDestroying = true
    
    const tasks: Promise<void>[] = []

    // Leave lobby first
    if (this.lobbyConnection?.state === HubConnectionState.Connected) {
      try {
        await this.lobbyConnection.invoke('LeaveLobby')
      } catch (error) {
        console.warn('[SignalR] Error leaving lobby:', error)
      }
      tasks.push(this.lobbyConnection.stop())
    }

    // Stop other connections
    if (this.gameRoomConnection?.state === HubConnectionState.Connected) {
      tasks.push(this.gameRoomConnection.stop())
    }

    if (this.gameControlConnection?.state === HubConnectionState.Connected) {
      tasks.push(this.gameControlConnection.stop())
    }

    if (tasks.length > 0) {
      await Promise.all(tasks)
    }

    // Clear connections
    this.lobbyConnection = undefined
    this.gameRoomConnection = undefined
    this.gameControlConnection = undefined
    
    // Clear all callbacks
    this.clearAllCallbacks()
    
    console.log('[SignalR] All connections stopped')
  }

  private clearAllCallbacks() {
    // Room events
    this.onPlayerJoined = undefined
    this.onPlayerLeft = undefined
    this.onRoomInfo = undefined
    this.onRoomCreated = undefined
    this.onRoomJoined = undefined
    this.onRoomInfoUpdated = undefined
    this.onRoomLeft = undefined
    this.onSeatJoined = undefined
    this.onSeatLeft = undefined
    
    // Game events
    this.onGameStateChanged = undefined
    this.onGameStateUpdated = undefined
    this.onGameStarted = undefined
    this.onGameEnded = undefined
    this.onTurnChanged = undefined
    this.onCardDealt = undefined
    this.onPlayerActionPerformed = undefined
    this.onBetPlaced = undefined
    
    // General events
    this.onError = undefined
    this.onSuccess = undefined
    
    // Auto-betting events
    this.onAutoBetProcessed = undefined
    this.onAutoBetStatistics = undefined
    this.onAutoBetProcessingStarted = undefined
    this.onAutoBetRoundSummary = undefined
    this.onPlayerRemovedFromSeat = undefined
    this.onPlayerBalanceUpdated = undefined
    this.onInsufficientFundsWarning = undefined
    this.onAutoBetFailed = undefined
    this.onMinBetPerRoundUpdated = undefined
    
    // Personal events
    this.onYouWereRemovedFromSeat = undefined
    this.onYourBalanceUpdated = undefined
    this.onInsufficientFundsWarningPersonal = undefined
    this.onAutoBetFailedPersonal = undefined
    
    // Lobby events
    this.onActiveRoomsUpdated = undefined
    this.onLobbyStats = undefined
    this.onQuickJoinRedirect = undefined
    this.onQuickJoinTableRedirect = undefined
    this.onDetailedRoomInfo = undefined
  }

  async verifyConnections(): Promise<boolean> {
    if (!this.areAllConnected) {
      console.log('[SignalR] Connections not ready, starting...')
      return await this.startConnections()
    }
    return true
  }

  // ===== MÉTODOS DEL LOBBYHUB =====

  async joinLobby(): Promise<void> {
    if (!await this.verifyLobbyConnection()) return
    await this.lobbyConnection!.invoke('JoinLobby')
  }

  async leaveLobby(): Promise<void> {
    if (!this.isLobbyConnected) return
    await this.lobbyConnection!.invoke('LeaveLobby')
  }

  async getActiveRooms(): Promise<void> {
    if (!await this.verifyLobbyConnection()) return
    await this.lobbyConnection!.invoke('GetActiveRooms')
  }

  async refreshRooms(): Promise<void> {
    if (!await this.verifyLobbyConnection()) return
    await this.lobbyConnection!.invoke('RefreshRooms')
  }

  async quickJoin(preferredRoomCode?: string): Promise<void> {
    if (!await this.verifyLobbyConnection()) return
    await this.lobbyConnection!.invoke('QuickJoin', preferredRoomCode)
  }

  async quickJoinTable(tableId: string): Promise<void> {
    if (!await this.verifyLobbyConnection()) return
    await this.lobbyConnection!.invoke('QuickJoinTable', tableId)
  }

  async getLobbyStats(): Promise<void> {
    if (!await this.verifyLobbyConnection()) return
    await this.lobbyConnection!.invoke('GetLobbyStats')
  }

  async getRoomDetails(roomCode: string): Promise<void> {
    if (!await this.verifyLobbyConnection()) return
    await this.lobbyConnection!.invoke('GetRoomDetails', roomCode)
  }

  // ===== MÉTODOS DEL GAMEROOMHUB =====

  async createRoom(request: CreateRoomRequest): Promise<void> {
    if (!await this.verifyGameRoomConnection()) return
    await this.gameRoomConnection!.invoke('CreateRoom', request)
  }

  async joinRoom(request: JoinRoomRequest): Promise<void> {
    if (!await this.verifyGameRoomConnection()) return
    await this.gameRoomConnection!.invoke('JoinRoom', request)
  }

  async joinOrCreateRoomForTable(tableId: string, playerName: string): Promise<void> {
    if (!await this.verifyGameRoomConnection()) return
    await this.gameRoomConnection!.invoke('JoinOrCreateRoomForTable', tableId, playerName)
  }

  async leaveRoom(roomCode: string): Promise<void> {
    if (!this.isGameRoomConnected) return
    try {
      await this.gameRoomConnection!.invoke('LeaveRoom', roomCode)
    } catch (error) {
      console.warn('[SignalR] Error leaving room:', error)
    }
  }

  async getRoomInfo(roomCode: string): Promise<void> {
    if (!await this.verifyGameRoomConnection()) return
    await this.gameRoomConnection!.invoke('GetRoomInfo', roomCode)
  }

  async joinSeat(request: JoinSeatRequest): Promise<void> {
    if (!await this.verifyGameRoomConnection()) return
    await this.gameRoomConnection!.invoke('JoinSeat', request)
  }

  async leaveSeat(request: LeaveSeatRequest): Promise<void> {
    if (!await this.verifyGameRoomConnection()) return
    await this.gameRoomConnection!.invoke('LeaveSeat', request)
  }

  async joinAsViewer(request: JoinRoomRequest): Promise<void> {
    if (!await this.verifyGameRoomConnection()) return
    await this.gameRoomConnection!.invoke('JoinAsViewer', request)
  }

  // ===== MÉTODOS DEL GAMECONTROLHUB =====

  async joinRoomForGameControl(roomCode: string): Promise<void> {
    if (!await this.verifyGameControlConnection()) return
    await this.gameControlConnection!.invoke('JoinRoomForGameControl', roomCode)
  }

  async startGame(roomCode: string): Promise<void> {
    if (!await this.verifyGameControlConnection()) return
    await this.gameControlConnection!.invoke('StartGame', roomCode)
  }

  async endGame(roomCode: string): Promise<void> {
    if (!await this.verifyGameControlConnection()) return
    await this.gameControlConnection!.invoke('EndGame', roomCode)
  }

  async hit(roomCode: string): Promise<void> {
    if (!await this.verifyGameControlConnection()) return
    await this.gameControlConnection!.invoke('Hit', roomCode)
  }

  async stand(roomCode: string): Promise<void> {
    if (!await this.verifyGameControlConnection()) return
    await this.gameControlConnection!.invoke('Stand', roomCode)
  }

  async getAutoBetStatistics(roomCode: string): Promise<void> {
    if (!await this.verifyGameControlConnection()) return
    await this.gameControlConnection!.invoke('GetAutoBetStatistics', roomCode)
  }

  // ===== MÉTODOS DE PRUEBA =====

  async testConnection(): Promise<void> {
    let tested = false

    if (this.isLobbyConnected) {
      try {
        await this.lobbyConnection!.invoke('TestConnection')
        console.log('[SignalR] Test successful via LobbyHub')
        tested = true
      } catch (error) {
        console.error('[SignalR] Test failed via LobbyHub:', error)
      }
    }

    if (!tested && this.isGameRoomConnected) {
      try {
        await this.gameRoomConnection!.invoke('TestConnection')
        console.log('[SignalR] Test successful via GameRoomHub')
        tested = true
      } catch (error) {
        console.error('[SignalR] Test failed via GameRoomHub:', error)
      }
    }

    if (!tested && this.isGameControlConnected) {
      try {
        await this.gameControlConnection!.invoke('TestConnection')
        console.log('[SignalR] Test successful via GameControlHub')
        tested = true
      } catch (error) {
        console.error('[SignalR] Test failed via GameControlHub:', error)
      }
    }

    if (!tested) {
      throw new Error('No connections available for testing')
    }
  }

  // ===== VERIFICACIÓN DE CONEXIONES =====

  private async verifyLobbyConnection(): Promise<boolean> {
    if (!this.isLobbyConnected) {
      if (!await this.verifyConnections()) {
        throw new Error('LobbyHub not available')
      }
    }
    return this.isLobbyConnected
  }

  private async verifyGameRoomConnection(): Promise<boolean> {
    if (!this.isGameRoomConnected) {
      if (!await this.verifyConnections()) {
        throw new Error('GameRoomHub not available')
      }
    }
    return this.isGameRoomConnected
  }

  private async verifyGameControlConnection(): Promise<boolean> {
    if (!this.isGameControlConnected) {
      if (!await this.verifyConnections()) {
        throw new Error('GameControlHub not available')
      }
    }
    return this.isGameControlConnected
  }

  // ===== PROPIEDADES DE ESTADO =====

  get isLobbyConnected(): boolean {
    return this.lobbyConnection?.state === HubConnectionState.Connected
  }

  get isGameRoomConnected(): boolean {
    return this.gameRoomConnection?.state === HubConnectionState.Connected
  }

  get isGameControlConnected(): boolean {
    return this.gameControlConnection?.state === HubConnectionState.Connected
  }

  get areAllConnected(): boolean {
    return this.isLobbyConnected && 
           this.isGameRoomConnected && 
           this.isGameControlConnected
  }

  get connectionState(): ConnectionState {
    const connections = [
      this.lobbyConnection,
      this.gameRoomConnection,
      this.gameControlConnection
    ]

    const states = connections.map(conn => conn?.state).filter(Boolean)

    if (states.some(state => state === HubConnectionState.Connected)) {
      return 'Connected'
    }
    if (states.some(state => state === HubConnectionState.Connecting)) {
      return 'Connecting'
    }
    if (states.some(state => state === HubConnectionState.Reconnecting)) {
      return 'Reconnecting'
    }
    return 'Disconnected'
  }

  // ===== DEBUG INFO =====

  getConnectionInfo() {
    return {
      lobby: {
        connected: this.isLobbyConnected,
        state: this.lobbyConnection?.state || 'Not Created',
        connectionId: this.lobbyConnection?.connectionId || null
      },
      gameRoom: {
        connected: this.isGameRoomConnected,
        state: this.gameRoomConnection?.state || 'Not Created',
        connectionId: this.gameRoomConnection?.connectionId || null
      },
      gameControl: {
        connected: this.isGameControlConnected,
        state: this.gameControlConnection?.state || 'Not Created',
        connectionId: this.gameControlConnection?.connectionId || null
      },
      overall: {
        allConnected: this.areAllConnected,
        connectionState: this.connectionState,
        isStarting: this.isStarting,
        isDestroying: this.isDestroying
      }
    }
  }

  logConnectionStatus() {
    const info = this.getConnectionInfo()
    console.log('[SignalR] Connection Status:', JSON.stringify(info, null, 2))
  }
}

export const signalRService = new SignalRService()