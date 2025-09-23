// src/services/signalr.ts - CORREGIDO para alinearse exactamente con el backend
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr'
import { authService } from './auth'

const API_BASE = import.meta.env.VITE_API_BASE_URL || 'http://localhost:7102'

export type ConnectionState = 'Disconnected' | 'Connecting' | 'Connected' | 'Reconnecting'

// TIPOS para Auto-Betting Events
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

class SignalRService {
  // ARQUITECTURA DE 3 HUBS ESPECIALIZADOS
  private lobbyConnection?: HubConnection         // /hubs/lobby
  private gameRoomConnection?: HubConnection      // /hubs/gameroom 
  private gameControlConnection?: HubConnection   // /hubs/gamecontrol
  
  private isStarting = false
  private isDestroying = false

  // Event callbacks para el UI - BÁSICOS
  public onPlayerJoined?: (player: any) => void
  public onPlayerLeft?: (player: any) => void
  public onRoomInfo?: (roomData: any) => void
  public onRoomCreated?: (roomData: any) => void
  public onRoomJoined?: (data: any) => void
  public onRoomInfoUpdated?: (roomData: any) => void
  public onSeatJoined?: (data: any) => void
  public onSeatLeft?: (data: any) => void
  public onGameStateChanged?: (gameState: any) => void
  public onError?: (message: string) => void

  // Event callbacks para Auto-Betting - GRUPALES
  public onAutoBetProcessed?: (event: AutoBetProcessedEvent) => void
  public onAutoBetStatistics?: (event: AutoBetStatistics) => void
  public onAutoBetProcessingStarted?: (event: AutoBetProcessingStartedEvent) => void
  public onAutoBetRoundSummary?: (event: AutoBetRoundSummaryEvent) => void
  public onPlayerRemovedFromSeat?: (event: PlayerRemovedFromSeatEvent) => void
  public onPlayerBalanceUpdated?: (event: PlayerBalanceUpdatedEvent) => void
  public onInsufficientFundsWarning?: (event: InsufficientFundsWarningEvent) => void
  public onAutoBetFailed?: (event: any) => void
  public onMinBetPerRoundUpdated?: (event: any) => void
  
  // Event callbacks personales - INDIVIDUALES
  public onYouWereRemovedFromSeat?: (event: PlayerRemovedFromSeatEvent) => void
  public onYourBalanceUpdated?: (event: PlayerBalanceUpdatedEvent) => void
  public onInsufficientFundsWarningPersonal?: (event: InsufficientFundsWarningEvent) => void
  public onAutoBetFailedPersonal?: (event: any) => void

  private buildConnection(hubPath: string): HubConnection {
    const fullUrl = `${API_BASE}${hubPath}`
    
    console.log(`[SignalR] Building connection to: ${fullUrl}`)
    
    return new HubConnectionBuilder()
      .withUrl(fullUrl, {
        accessTokenFactory: () => {
          console.log('[DEBUG] ===== TOKEN FACTORY CALLED =====')
          
          const token = authService.getToken()
          console.log('[DEBUG] Raw token from authService:', token)
          
          if (!token) {
            console.error('[DEBUG] No token available!')
            return ''
          }
          
          const cleanToken = token.replace(/^Bearer\s+/i, '').trim()
          console.log('[DEBUG] Clean token length:', cleanToken.length)
          console.log('[DEBUG] Token preview:', cleanToken.substring(0, 100) + '...')
          
          const parts = cleanToken.split('.')
          console.log('[DEBUG] Token parts count:', parts.length)
          
          if (parts.length === 3) {
            try {
              const payload = JSON.parse(atob(parts[1]))
              console.log('[DEBUG] Token payload decoded:', payload)
              console.log('[DEBUG] PlayerId in token:', payload.playerId || payload.sub || payload.nameid || 'NOT_FOUND')
              console.log('[DEBUG] Name in token:', payload.name || payload.unique_name || 'NOT_FOUND')
              console.log('[DEBUG] Token expires:', new Date((payload.exp || 0) * 1000))
              
              const now = Math.floor(Date.now() / 1000)
              if (payload.exp && payload.exp < now) {
                console.error('[DEBUG] TOKEN EXPIRED!')
                console.error('[DEBUG] Token expired at:', new Date(payload.exp * 1000))
                console.error('[DEBUG] Current time:', new Date())
                return ''
              } else {
                console.log('[DEBUG] Token is valid and not expired')
              }
              
            } catch (e) {
              console.error('[DEBUG] Cannot decode token payload:', e)
              return ''
            }
          } else {
            console.error('[DEBUG] Invalid JWT format - should have 3 parts')
            return ''
          }
          
          console.log('[DEBUG] Returning clean token for SignalR')
          return cleanToken
        },
        
        skipNegotiation: true,
        transport: 1 // WebSockets
      })
      .withAutomaticReconnect([2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Information)
      .build()
  }

  private setupConnectionHandlers(connection: HubConnection, name: string) {
    console.log(`[SignalR] Setting up handlers for ${name}`)
    
    connection.onreconnecting((error) => {
      if (this.isDestroying) {
        console.log(`[SignalR] ${name} skip reconnecting - service is destroying`)
        return
      }
      console.log(`[SignalR] ${name} reconnecting...`, error)
    })
    
    connection.onreconnected((connectionId) => {
      if (this.isDestroying) {
        console.log(`[SignalR] ${name} skip reconnected handler - service is destroying`)
        return
      }
      console.log(`[SignalR] ${name} reconnected successfully with ID:`, connectionId)
    })
    
    connection.onclose(error => {
      if (this.isDestroying) {
        console.log(`[SignalR] ${name} connection closed normally during cleanup`)
        return
      }
      
      if (error) {
        console.error(`[SignalR] ${name} connection closed with error:`, error)
        console.error(`[SignalR] Error details:`, {
          message: error.message,
          stack: error.stack,
          name: error.name
        })
      } else {
        console.log(`[SignalR] ${name} connection closed normally`)
      }
    })

    // Handlers comunes para todos los hubs
    connection.on('Success', (successData: any) => {
      if (this.isDestroying) return
      const message = successData?.message || successData
      console.log(`[SignalR] ${name} success:`, message)
    })

    connection.on('Error', (errorData: any) => {
      if (this.isDestroying) return
      const message = errorData?.message || errorData
      console.error(`[SignalR] ${name} error:`, message)
      this.onError?.(message)
    })

    connection.on('TestResponse', (data: any) => {
      if (this.isDestroying) return
      console.log(`[SignalR] ${name} TestResponse:`, data)
    })

    // Handlers específicos por hub
    this.setupSpecificHandlers(connection, name)
  }

  private setupSpecificHandlers(connection: HubConnection, name: string) {
    switch (name) {
      case 'Lobby':
        // Eventos específicos del LobbyHub
        connection.on('ActiveRoomsUpdated', (response: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] ActiveRoomsUpdated event:', response)
        })

        connection.on('RoomListUpdated', (data: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] RoomListUpdated event:', data)
        })

        connection.on('LobbyStats', (stats: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] LobbyStats event:', stats)
        })

        connection.on('QuickJoinRedirect', (data: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] QuickJoinRedirect event:', data)
        })

        connection.on('QuickJoinTableRedirect', (data: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] QuickJoinTableRedirect event:', data)
        })

        connection.on('DetailedRoomInfo', (data: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] DetailedRoomInfo event:', data)
        })
        break

      case 'GameRoom':
        // Eventos de sala y jugadores - GameRoomHub maneja TODAS las funciones básicas
        connection.on('RoomCreated', (response: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] RoomCreated event:', response)
          this.onRoomCreated?.(response?.data || response)
        })

        connection.on('RoomJoined', (response: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] RoomJoined event:', response)
          this.onRoomJoined?.(response?.data || response)
        })

        connection.on('RoomInfoUpdated', (roomData: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] RoomInfoUpdated event:', roomData)
          const data = roomData?.data || roomData
          this.onRoomInfoUpdated?.(data)
        })

        connection.on('RoomInfo', (roomData: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] RoomInfo event:', roomData)
          this.onRoomInfo?.(roomData)
        })

        connection.on('RoomLeft', (data: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] RoomLeft event:', data)
        })

        // Eventos de jugadores
        connection.on('PlayerJoined', (data: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] PlayerJoined event:', data)
          this.onPlayerJoined?.(data)
        })

        connection.on('PlayerLeft', (data: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] PlayerLeft event:', data)
          this.onPlayerLeft?.(data)
        })

        // Eventos de asientos
        connection.on('SeatJoined', (response: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] SeatJoined event:', response)
          this.onSeatJoined?.(response?.data || response)
        })

        connection.on('SeatLeft', (response: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] SeatLeft event:', response)
          this.onSeatLeft?.(response?.data || response)
        })

        // Eventos de espectadores
        connection.on('SpectatorJoined', (data: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] SpectatorJoined event:', data)
        })

        connection.on('SpectatorLeft', (data: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] SpectatorLeft event:', data)
        })
        break

      case 'GameControl':
        // Eventos de juego básicos
        connection.on('GameStarted', (gameData: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] GameStarted event:', gameData)
          this.onGameStateChanged?.(gameData)
        })

        connection.on('GameEnded', (gameData: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] GameEnded event:', gameData)
          this.onGameStateChanged?.(gameData)
        })

        connection.on('GameStateUpdated', (gameState: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] GameStateUpdated event:', gameState)
          this.onGameStateChanged?.(gameState)
        })

        connection.on('TurnChanged', (data: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] TurnChanged event:', data)
          this.onGameStateChanged?.(data)
        })

        // Eventos de cartas y acciones
        connection.on('CardDealt', (data: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] CardDealt event:', data)
          this.onGameStateChanged?.(data)
        })

        connection.on('PlayerActionPerformed', (data: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] PlayerActionPerformed event:', data)
          this.onGameStateChanged?.(data)
        })

        connection.on('BetPlaced', (data: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] BetPlaced event:', data)
          this.onGameStateChanged?.(data)
        })

        // EVENTOS DE AUTO-BETTING - GRUPALES
        connection.on('AutoBetProcessed', (event: AutoBetProcessedEvent) => {
          if (this.isDestroying) return
          console.log('[SignalR] AutoBetProcessed event:', event)
          this.onAutoBetProcessed?.(event)
        })

        connection.on('AutoBetStatistics', (event: AutoBetStatistics) => {
          if (this.isDestroying) return
          console.log('[SignalR] AutoBetStatistics event:', event)
          this.onAutoBetStatistics?.(event)
        })

        connection.on('AutoBetProcessingStarted', (event: AutoBetProcessingStartedEvent) => {
          if (this.isDestroying) return
          console.log('[SignalR] AutoBetProcessingStarted event:', event)
          this.onAutoBetProcessingStarted?.(event)
        })

        connection.on('AutoBetRoundSummary', (event: AutoBetRoundSummaryEvent) => {
          if (this.isDestroying) return
          console.log('[SignalR] AutoBetRoundSummary event:', event)
          this.onAutoBetRoundSummary?.(event)
        })

        connection.on('PlayerRemovedFromSeat', (event: PlayerRemovedFromSeatEvent) => {
          if (this.isDestroying) return
          console.log('[SignalR] PlayerRemovedFromSeat event:', event)
          this.onPlayerRemovedFromSeat?.(event)
        })

        connection.on('PlayerBalanceUpdated', (event: PlayerBalanceUpdatedEvent) => {
          if (this.isDestroying) return
          console.log('[SignalR] PlayerBalanceUpdated event:', event)
          this.onPlayerBalanceUpdated?.(event)
        })

        connection.on('InsufficientFundsWarning', (event: InsufficientFundsWarningEvent) => {
          if (this.isDestroying) return
          console.log('[SignalR] InsufficientFundsWarning event:', event)
          this.onInsufficientFundsWarning?.(event)
        })

        connection.on('AutoBetFailed', (event: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] AutoBetFailed event:', event)
          this.onAutoBetFailed?.(event)
        })

        connection.on('MinBetPerRoundUpdated', (event: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] MinBetPerRoundUpdated event:', event)
          this.onMinBetPerRoundUpdated?.(event)
        })

        // EVENTOS DE AUTO-BETTING - PERSONALES
        connection.on('YouWereRemovedFromSeat', (event: PlayerRemovedFromSeatEvent) => {
          if (this.isDestroying) return
          console.log('[SignalR] YouWereRemovedFromSeat event:', event)
          this.onYouWereRemovedFromSeat?.(event)
        })

        connection.on('YourBalanceUpdated', (event: PlayerBalanceUpdatedEvent) => {
          if (this.isDestroying) return
          console.log('[SignalR] YourBalanceUpdated event:', event)
          this.onYourBalanceUpdated?.(event)
        })

        connection.on('InsufficientFundsWarningPersonal', (event: InsufficientFundsWarningEvent) => {
          if (this.isDestroying) return
          console.log('[SignalR] InsufficientFundsWarningPersonal event:', event)
          this.onInsufficientFundsWarningPersonal?.(event)
        })

        connection.on('AutoBetFailedPersonal', (event: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] AutoBetFailedPersonal event:', event)
          this.onAutoBetFailedPersonal?.(event)
        })

        break
    }
  }

  async startConnections(): Promise<boolean> {
    if (this.isStarting) {
      console.log('[SignalR] Connection start already in progress, waiting...')
      
      let attempts = 0
      while (this.isStarting && attempts < 50) {
        await new Promise(resolve => setTimeout(resolve, 100))
        attempts++
      }
      
      return this.areAllConnected
    }
    
    if (!authService.isAuthenticated()) {
      console.error('[SignalR] Cannot start connections - user not authenticated')
      return false
    }

    console.log('[SignalR] User is authenticated, starting 3 specialized hub connections...')

    this.isStarting = true
    this.isDestroying = false

    try {
      console.log('[SignalR] Starting 3 specialized SignalR connections...')

      // Crear conexiones a los 3 hubs especializados según el backend
      const hubsToCreate = [
        { name: 'Lobby', path: '/hubs/lobby', prop: 'lobbyConnection' },
        { name: 'GameRoom', path: '/hubs/gameroom', prop: 'gameRoomConnection' },
        { name: 'GameControl', path: '/hubs/gamecontrol', prop: 'gameControlConnection' }
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
              console.log(`[SignalR] ${hub.name} connection started successfully`)
            }).catch((error: any) => {
              console.error(`[SignalR] ${hub.name} connection failed:`, error)
              throw error
            })
          )
        } else {
          console.log(`[SignalR] ${hub.name} connection already active:`, (this as any)[hub.prop].state)
        }
      }

      if (tasks.length > 0) {
        console.log('[SignalR] Waiting for 3 specialized hub connections to start...')
        await Promise.all(tasks)
        console.log('[SignalR] All 3 specialized hub connections started successfully')
      } else {
        console.log('[SignalR] All 3 specialized hub connections already active')
      }

      // Unirse al lobby si está conectado
      if (!this.isDestroying && this.lobbyConnection?.state === HubConnectionState.Connected) {
        try {
          console.log('[SignalR] Joining lobby group...')
          await this.lobbyConnection.invoke('JoinLobby')
          console.log('[SignalR] Successfully joined lobby')
        } catch (error) {
          console.warn('[SignalR] Could not join lobby:', error)
        }
      }

      console.log('[SignalR] All 3 specialized hub connections ready and configured')
      return true

    } catch (error: any) {
      console.error('[SignalR] Failed to start 3 specialized hub connections:', error)
      
      if (error.message?.includes('401') || error.message?.includes('Unauthorized')) {
        console.error('[SignalR] AUTHENTICATION ERROR - JWT token issue')
        console.log('[SignalR] Current auth state:')
        authService.debugAuthState()
        
        const token = authService.getToken()
        if (token) {
          try {
            const parts = token.split('.')
            if (parts.length === 3) {
              const payload = JSON.parse(atob(parts[1]))
              const now = Math.floor(Date.now() / 1000)
              console.log('[SignalR] Token analysis:')
              console.log('  - Expires:', new Date((payload.exp || 0) * 1000))
              console.log('  - Current time:', new Date())
              console.log('  - Is expired:', payload.exp < now)
              console.log('  - PlayerId:', payload.playerId || payload.sub || 'MISSING')
            }
          } catch (e) {
            console.error('[SignalR] Cannot analyze token:', e)
          }
        }
      }
      
      console.error('[SignalR] Error details:', {
        message: error.message,
        stack: error.stack,
        name: error.name
      })
      
      return false
    } finally {
      this.isStarting = false
    }
  }

  async stopConnections() {
    console.log('[SignalR] Stopping all 3 specialized hub connections...')
    this.isDestroying = true
    
    const tasks: Promise<void>[] = []

    // Salir del lobby si está conectado
    if (this.lobbyConnection?.state === HubConnectionState.Connected) {
      try {
        await this.lobbyConnection.invoke('LeaveLobby')
      } catch (error) {
        console.warn('[SignalR] Error leaving lobby:', error)
      }
      tasks.push(this.lobbyConnection.stop())
    }

    // Detener las otras 2 conexiones especializadas
    const connections = [
      this.gameRoomConnection,
      this.gameControlConnection
    ]

    for (const connection of connections) {
      if (connection?.state === HubConnectionState.Connected) {
        tasks.push(connection.stop())
      }
    }

    if (tasks.length > 0) {
      await Promise.all(tasks)
    }

    // Limpiar referencias de los 3 hubs
    this.lobbyConnection = undefined
    this.gameRoomConnection = undefined
    this.gameControlConnection = undefined
    
    // Limpiar callbacks básicos
    this.onPlayerJoined = undefined
    this.onPlayerLeft = undefined
    this.onRoomInfo = undefined
    this.onRoomCreated = undefined
    this.onRoomJoined = undefined
    this.onRoomInfoUpdated = undefined
    this.onSeatJoined = undefined
    this.onSeatLeft = undefined
    this.onGameStateChanged = undefined
    this.onError = undefined
    
    // Limpiar callbacks de auto-betting - GRUPALES
    this.onAutoBetProcessed = undefined
    this.onAutoBetStatistics = undefined
    this.onAutoBetProcessingStarted = undefined
    this.onAutoBetRoundSummary = undefined
    this.onPlayerRemovedFromSeat = undefined
    this.onPlayerBalanceUpdated = undefined
    this.onInsufficientFundsWarning = undefined
    this.onAutoBetFailed = undefined
    this.onMinBetPerRoundUpdated = undefined
    
    // Limpiar callbacks de auto-betting - PERSONALES
    this.onYouWereRemovedFromSeat = undefined
    this.onYourBalanceUpdated = undefined
    this.onInsufficientFundsWarningPersonal = undefined
    this.onAutoBetFailedPersonal = undefined
    
    console.log('[SignalR] All 3 specialized hub connections stopped and cleaned')
  }

  async verifyConnections(): Promise<boolean> {
    if (!this.areAllConnected) {
      console.log('[SignalR] 3 specialized hub connections not ready, attempting to start...')
      return await this.startConnections()
    }
    return true
  }

  // MÉTODOS QUE USAN GAMEROOMHUB - SALAS Y FUNCIONALIDAD BÁSICA
  async joinOrCreateRoomForTable(tableId: string, playerName?: string): Promise<void> {
    if (!this.gameRoomConnection || this.gameRoomConnection.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('GameRoomHub no está disponible')
      }
    }

    const user = authService.getCurrentUser()
    const finalPlayerName = playerName || user?.displayName || 'Jugador'

    console.log(`[SignalR] [GameRoomHub] Joining/creating room for table: ${tableId}, playerName: ${finalPlayerName}`)
    
    try {
      await this.gameRoomConnection!.invoke('JoinOrCreateRoomForTable', tableId, finalPlayerName)
      console.log(`[SignalR] Successfully invoked JoinOrCreateRoomForTable via GameRoomHub`)
    } catch (error) {
      console.error(`[SignalR] Error in JoinOrCreateRoomForTable:`, error)
      throw error
    }
  }

  async joinRoom(roomCode: string): Promise<void> {
    if (!this.gameRoomConnection || this.gameRoomConnection.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('GameRoomHub no está disponible')
      }
    }

    const user = authService.getCurrentUser()
    if (!user) {
      throw new Error('Usuario no autenticado')
    }

    console.log(`[SignalR] [GameRoomHub] Joining existing room: ${roomCode}`)

    await this.gameRoomConnection!.invoke('JoinRoom', {
      roomCode: roomCode,
      playerName: user.displayName
    })
  }

  async createRoom(roomName: string): Promise<void> {
    if (!this.gameRoomConnection || this.gameRoomConnection.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('GameRoomHub no está disponible')
      }
    }

    console.log(`[SignalR] [GameRoomHub] Creating room: ${roomName}`)

    await this.gameRoomConnection!.invoke('CreateRoom', {
      roomName: roomName,
      maxPlayers: 6
    })
  }

  async leaveRoom(roomCode: string): Promise<void> {
    if (!this.gameRoomConnection || this.gameRoomConnection.state !== HubConnectionState.Connected) {
      return
    }

    try {
      console.log(`[SignalR] [GameRoomHub] EXPLICIT LeaveRoom called for: ${roomCode}`)
      await this.gameRoomConnection.invoke('LeaveRoom', roomCode)
    } catch (error) {
      console.warn('[SignalR] Error leaving room:', error)
    }
  }

  async getRoomInfo(roomCode: string): Promise<void> {
    if (!this.gameRoomConnection || this.gameRoomConnection.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('GameRoomHub no está disponible')
      }
    }

    console.log(`[SignalR] [GameRoomHub] Getting room info: ${roomCode}`)
    await this.gameRoomConnection!.invoke('GetRoomInfo', roomCode)
  }

  // MÉTODOS DE ASIENTOS - CONSOLIDADOS EN GAMEROOMHUB
  async joinSeat(roomCode: string, position: number): Promise<void> {
    if (!this.gameRoomConnection || this.gameRoomConnection.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('GameRoomHub no está disponible')
      }
    }

    console.log(`[SignalR] [GameRoomHub] Joining seat ${position} in room: ${roomCode}`)

    try {
      await this.gameRoomConnection!.invoke('JoinSeat', {
        RoomCode: roomCode,
        Position: position
      })
      console.log(`[SignalR] Successfully invoked JoinSeat via GameRoomHub`)
    } catch (error) {
      console.error(`[SignalR] Error in JoinSeat:`, error)
      throw error
    }
  }

  async leaveSeat(roomCode: string): Promise<void> {
    if (!this.gameRoomConnection || this.gameRoomConnection.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('GameRoomHub no está disponible')
      }
    }

    console.log(`[SignalR] [GameRoomHub] Leaving seat in room: ${roomCode}`)

    try {
      await this.gameRoomConnection!.invoke('LeaveSeat', {
        RoomCode: roomCode
      })
      console.log(`[SignalR] Successfully invoked LeaveSeat via GameRoomHub`)
    } catch (error) {
      console.error(`[SignalR] Error in LeaveSeat:`, error)
      throw error
    }
  }

  // MÉTODOS DE ESPECTADORES - CONSOLIDADOS EN GAMEROOMHUB
  async joinAsViewer(roomCode: string, playerName: string): Promise<void> {
    if (!this.gameRoomConnection || this.gameRoomConnection.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('GameRoomHub no está disponible')
      }
    }

    const user = authService.getCurrentUser()
    if (!user) {
      throw new Error('Usuario no autenticado')
    }

    console.log(`[SignalR] [GameRoomHub] Joining as viewer: ${playerName} to room: ${roomCode}`)

    await this.gameRoomConnection!.invoke('JoinAsViewer', {
      roomCode: roomCode,
      playerName: playerName
    })
  }

  // MÉTODOS QUE USAN GAMECONTROLHUB - CONTROL DE JUEGO Y AUTO-BETTING
  async joinRoomForGameControl(roomCode: string): Promise<void> {
    if (!this.gameControlConnection || this.gameControlConnection.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('GameControlHub no está disponible')
      }
    }

    console.log(`[SignalR] [GameControlHub] Joining room for game control: ${roomCode}`)
    await this.gameControlConnection!.invoke('JoinRoomForGameControl', roomCode)
  }

  async leaveRoomGameControl(roomCode: string): Promise<void> {
    if (!this.gameControlConnection || this.gameControlConnection.state !== HubConnectionState.Connected) {
      return
    }

    try {
      console.log(`[SignalR] [GameControlHub] Leaving room game control: ${roomCode}`)
      await this.gameControlConnection.invoke('LeaveRoomGameControl', roomCode)
    } catch (error) {
      console.warn('[SignalR] Error leaving room game control:', error)
    }
  }

  async startGame(roomCode: string): Promise<void> {
    if (!this.gameControlConnection || this.gameControlConnection.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('GameControlHub no está disponible')
      }
    }

    console.log(`[SignalR] [GameControlHub] Starting game for room: ${roomCode}`)
    await this.gameControlConnection!.invoke('StartGame', roomCode)
  }

  async endGame(roomCode: string): Promise<void> {
    if (!this.gameControlConnection || this.gameControlConnection.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('GameControlHub no está disponible')
      }
    }

    console.log(`[SignalR] [GameControlHub] Ending game for room: ${roomCode}`)
    await this.gameControlConnection!.invoke('EndGame', roomCode)
  }

  // AUTO-BETTING METHODS - GAMECONTROLHUB
  async processRoundAutoBets(roomCode: string, removePlayersWithoutFunds: boolean = true): Promise<void> {
    if (!this.gameControlConnection || this.gameControlConnection.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('GameControlHub no está disponible')
      }
    }

    console.log(`[SignalR] [GameControlHub] Processing auto-bets for room: ${roomCode}, removeWithoutFunds: ${removePlayersWithoutFunds}`)
    
    try {
      await this.gameControlConnection!.invoke('ProcessRoundAutoBets', roomCode, removePlayersWithoutFunds)
      console.log(`[SignalR] Successfully invoked ProcessRoundAutoBets via GameControlHub`)
    } catch (error) {
      console.error(`[SignalR] Error in ProcessRoundAutoBets:`, error)
      throw error
    }
  }

  async getAutoBetStatistics(roomCode: string): Promise<void> {
    if (!this.gameControlConnection || this.gameControlConnection.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('GameControlHub no está disponible')
      }
    }

    console.log(`[SignalR] [GameControlHub] Getting auto-bet statistics for room: ${roomCode}`)
    
    try {
      await this.gameControlConnection!.invoke('GetAutoBetStatistics', roomCode)
      console.log(`[SignalR] Successfully invoked GetAutoBetStatistics via GameControlHub`)
    } catch (error) {
      console.error(`[SignalR] Error in GetAutoBetStatistics:`, error)
      throw error
    }
  }

  // MÉTODOS DE ACCIONES DE JUGADOR - GAMECONTROLHUB (métodos específicos según backend)
  async hit(roomCode: string): Promise<void> {
    if (!this.gameControlConnection || this.gameControlConnection.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('GameControlHub no está disponible')
      }
    }

    console.log(`[SignalR] [GameControlHub] Hit action for room: ${roomCode}`)
    await this.gameControlConnection!.invoke('Hit', roomCode)
  }

  async stand(roomCode: string): Promise<void> {
    if (!this.gameControlConnection || this.gameControlConnection.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('GameControlHub no está disponible')
      }
    }

    console.log(`[SignalR] [GameControlHub] Stand action for room: ${roomCode}`)
    await this.gameControlConnection!.invoke('Stand', roomCode)
  }

  async doubleDown(roomCode: string): Promise<void> {
    if (!this.gameControlConnection || this.gameControlConnection.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('GameControlHub no está disponible')
      }
    }

    console.log(`[SignalR] [GameControlHub] DoubleDown action for room: ${roomCode}`)
    await this.gameControlConnection!.invoke('DoubleDown', roomCode)
  }

  async split(roomCode: string): Promise<void> {
    if (!this.gameControlConnection || this.gameControlConnection.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('GameControlHub no está disponible')
      }
    }

    console.log(`[SignalR] [GameControlHub] Split action for room: ${roomCode}`)
    await this.gameControlConnection!.invoke('Split', roomCode)
  }

  // MÉTODOS DE LOBBY - LOBBYHUB
  async getActiveRooms(): Promise<void> {
    if (!this.lobbyConnection || this.lobbyConnection.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('LobbyHub no está disponible')
      }
    }

    console.log(`[SignalR] [LobbyHub] Getting active rooms`)
    await this.lobbyConnection!.invoke('GetActiveRooms')
  }

  async refreshRooms(): Promise<void> {
    if (!this.lobbyConnection || this.lobbyConnection.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('LobbyHub no está disponible')
      }
    }

    console.log(`[SignalR] [LobbyHub] Refreshing rooms`)
    await this.lobbyConnection!.invoke('RefreshRooms')
  }

  async quickJoin(preferredRoomCode?: string): Promise<void> {
    if (!this.lobbyConnection || this.lobbyConnection.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('LobbyHub no está disponible')
      }
    }

    console.log(`[SignalR] [LobbyHub] Quick join, preferred room: ${preferredRoomCode || 'none'}`)
    await this.lobbyConnection!.invoke('QuickJoin', preferredRoomCode)
  }

  async quickJoinTable(tableId: string): Promise<void> {
    if (!this.lobbyConnection || this.lobbyConnection.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('LobbyHub no está disponible')
      }
    }

    console.log(`[SignalR] [LobbyHub] Quick join table: ${tableId}`)
    await this.lobbyConnection!.invoke('QuickJoinTable', tableId)
  }

  async getLobbyStats(): Promise<void> {
    if (!this.lobbyConnection || this.lobbyConnection.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('LobbyHub no está disponible')
      }
    }

    console.log(`[SignalR] [LobbyHub] Getting lobby stats`)
    await this.lobbyConnection!.invoke('GetLobbyStats')
  }

  async getRoomDetails(roomCode: string): Promise<void> {
    if (!this.lobbyConnection || this.lobbyConnection.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('LobbyHub no está disponible')
      }
    }

    console.log(`[SignalR] [LobbyHub] Getting room details: ${roomCode}`)
    await this.lobbyConnection!.invoke('GetRoomDetails', roomCode)
  }

  // MÉTODOS DE TEST - USA CUALQUIER HUB DISPONIBLE
  async testConnection(): Promise<void> {
    // Probar el hub más básico primero
    if (this.lobbyConnection?.state === HubConnectionState.Connected) {
      console.log('[SignalR] [LobbyHub] Testing connection...')
      try {
        await this.lobbyConnection.invoke('TestConnection')
        console.log('[SignalR] Test connection successful via LobbyHub')
        return
      } catch (error) {
        console.error('[SignalR] Test connection failed via LobbyHub:', error)
      }
    }

    // Probar GameRoomHub como fallback
    if (this.gameRoomConnection?.state === HubConnectionState.Connected) {
      console.log('[SignalR] [GameRoomHub] Testing connection...')
      try {
        await this.gameRoomConnection.invoke('TestConnection')
        console.log('[SignalR] Test connection successful via GameRoomHub')
        return
      } catch (error) {
        console.error('[SignalR] Test connection failed via GameRoomHub:', error)
      }
    }

    // Probar GameControlHub como último recurso
    if (this.gameControlConnection?.state === HubConnectionState.Connected) {
      console.log('[SignalR] [GameControlHub] Testing connection...')
      try {
        await this.gameControlConnection.invoke('TestConnection')
        console.log('[SignalR] Test connection successful via GameControlHub')
        return
      } catch (error) {
        console.error('[SignalR] Test connection failed via GameControlHub:', error)
      }
    }

    throw new Error('No hub connections available for testing')
  }

  // GETTERS DE ESTADO - SOLO 3 HUBS
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
}

export const signalRService = new SignalRService()