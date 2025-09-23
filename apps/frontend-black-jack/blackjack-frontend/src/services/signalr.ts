// src/services/signalr.ts - TOTALMENTE ALINEADO CON EL BACKEND
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

// Request types (según el backend)
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

class SignalRService {
  // ARQUITECTURA DE 3 HUBS ESPECIALIZADOS - RUTAS EXACTAS DEL BACKEND
  private lobbyConnection?: HubConnection         // /hubs/lobby
  private gameRoomConnection?: HubConnection      // /hubs/game-room
  private gameControlConnection?: HubConnection   // /hubs/game-control
  
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

  // Lobby callbacks
  public onActiveRoomsUpdated?: (rooms: any[]) => void
  public onLobbyStats?: (stats: any) => void
  public onQuickJoinRedirect?: (data: any) => void
  public onQuickJoinTableRedirect?: (data: any) => void
  public onDetailedRoomInfo?: (data: any) => void

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
    connection.on('success', (successData: any) => {
      if (this.isDestroying) return
      const message = successData?.message || successData
      console.log(`[SignalR] ${name} success:`, message)
    })

    connection.on('error', (errorData: any) => {
      if (this.isDestroying) return
      const message = errorData?.message || errorData
      console.error(`[SignalR] ${name} error:`, message)
      this.onError?.(message)
    })

    connection.on('testResponse', (data: any) => {
      if (this.isDestroying) return
      console.log(`[SignalR] ${name} testResponse:`, data)
    })

    // Handlers específicos por hub
    this.setupSpecificHandlers(connection, name)
  }

  private setupSpecificHandlers(connection: HubConnection, name: string) {
    switch (name) {
      case 'Lobby':
        // Eventos específicos del LobbyHub
        connection.on('activeRoomsUpdated', (response: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] activeRoomsUpdated event:', response)
          this.onActiveRoomsUpdated?.(response)
        })

        connection.on('lobbyStats', (stats: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] lobbyStats event:', stats)
          this.onLobbyStats?.(stats)
        })

        connection.on('quickJoinRedirect', (data: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] quickJoinRedirect event:', data)
          this.onQuickJoinRedirect?.(data)
        })

        connection.on('quickJoinTableRedirect', (data: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] quickJoinTableRedirect event:', data)
          this.onQuickJoinTableRedirect?.(data)
        })

        connection.on('detailedRoomInfo', (data: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] detailedRoomInfo event:', data)
          this.onDetailedRoomInfo?.(data)
        })
        break

      case 'GameRoom':
        // Eventos de sala y jugadores
        connection.on('roomCreated', (response: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] roomCreated event:', response)
          this.onRoomCreated?.(response?.data || response)
        })

        connection.on('roomJoined', (response: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] roomJoined event:', response)
          this.onRoomJoined?.(response?.data || response)
        })

        connection.on('roomInfoUpdated', (roomData: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] roomInfoUpdated event:', roomData)
          const data = roomData?.data || roomData
          this.onRoomInfoUpdated?.(data)
        })

        connection.on('roomInfo', (roomData: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] roomInfo event:', roomData)
          this.onRoomInfo?.(roomData)
        })

        connection.on('roomLeft', (data: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] roomLeft event:', data)
        })

        // Eventos de jugadores
        connection.on('playerJoined', (data: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] playerJoined event:', data)
          this.onPlayerJoined?.(data)
        })

        connection.on('playerLeft', (data: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] playerLeft event:', data)
          this.onPlayerLeft?.(data)
        })

        // Eventos de asientos
        connection.on('seatJoined', (response: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] seatJoined event:', response)
          this.onSeatJoined?.(response?.data || response)
        })

        connection.on('seatLeft', (response: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] seatLeft event:', response)
          this.onSeatLeft?.(response?.data || response)
        })

        // Eventos de espectadores
        connection.on('spectatorJoined', (data: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] spectatorJoined event:', data)
        })

        connection.on('spectatorLeft', (data: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] spectatorLeft event:', data)
        })
        break

      case 'GameControl':
        // Eventos de juego básicos
        connection.on('gameStarted', (gameData: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] gameStarted event:', gameData)
          this.onGameStateChanged?.(gameData)
        })

        connection.on('gameEnded', (gameData: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] gameEnded event:', gameData)
          this.onGameStateChanged?.(gameData)
        })

        connection.on('gameStateUpdated', (gameState: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] gameStateUpdated event:', gameState)
          this.onGameStateChanged?.(gameState)
        })

        connection.on('turnChanged', (data: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] turnChanged event:', data)
          this.onGameStateChanged?.(data)
        })

        // Eventos de cartas y acciones
        connection.on('cardDealt', (data: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] cardDealt event:', data)
          this.onGameStateChanged?.(data)
        })

        connection.on('playerActionPerformed', (data: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] playerActionPerformed event:', data)
          this.onGameStateChanged?.(data)
        })

        connection.on('betPlaced', (data: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] betPlaced event:', data)
          this.onGameStateChanged?.(data)
        })

        // EVENTOS DE AUTO-BETTING
        connection.on('autoBetProcessed', (event: AutoBetProcessedEvent) => {
          if (this.isDestroying) return
          console.log('[SignalR] autoBetProcessed event:', event)
          this.onAutoBetProcessed?.(event)
        })

        connection.on('autoBetStatistics', (event: AutoBetStatistics) => {
          if (this.isDestroying) return
          console.log('[SignalR] autoBetStatistics event:', event)
          this.onAutoBetStatistics?.(event)
        })

        connection.on('autoBetProcessingStarted', (event: AutoBetProcessingStartedEvent) => {
          if (this.isDestroying) return
          console.log('[SignalR] autoBetProcessingStarted event:', event)
          this.onAutoBetProcessingStarted?.(event)
        })

        connection.on('autoBetRoundSummary', (event: AutoBetRoundSummaryEvent) => {
          if (this.isDestroying) return
          console.log('[SignalR] autoBetRoundSummary event:', event)
          this.onAutoBetRoundSummary?.(event)
        })

        connection.on('playerRemovedFromSeat', (event: PlayerRemovedFromSeatEvent) => {
          if (this.isDestroying) return
          console.log('[SignalR] playerRemovedFromSeat event:', event)
          this.onPlayerRemovedFromSeat?.(event)
        })

        connection.on('playerBalanceUpdated', (event: PlayerBalanceUpdatedEvent) => {
          if (this.isDestroying) return
          console.log('[SignalR] playerBalanceUpdated event:', event)
          this.onPlayerBalanceUpdated?.(event)
        })

        connection.on('insufficientFundsWarning', (event: InsufficientFundsWarningEvent) => {
          if (this.isDestroying) return
          console.log('[SignalR] insufficientFundsWarning event:', event)
          this.onInsufficientFundsWarning?.(event)
        })

        connection.on('autoBetFailed', (event: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] autoBetFailed event:', event)
          this.onAutoBetFailed?.(event)
        })

        connection.on('minBetPerRoundUpdated', (event: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] minBetPerRoundUpdated event:', event)
          this.onMinBetPerRoundUpdated?.(event)
        })

        // EVENTOS PERSONALES
        connection.on('youWereRemovedFromSeat', (event: PlayerRemovedFromSeatEvent) => {
          if (this.isDestroying) return
          console.log('[SignalR] youWereRemovedFromSeat event:', event)
          this.onYouWereRemovedFromSeat?.(event)
        })

        connection.on('yourBalanceUpdated', (event: PlayerBalanceUpdatedEvent) => {
          if (this.isDestroying) return
          console.log('[SignalR] yourBalanceUpdated event:', event)
          this.onYourBalanceUpdated?.(event)
        })

        connection.on('insufficientFundsWarningPersonal', (event: InsufficientFundsWarningEvent) => {
          if (this.isDestroying) return
          console.log('[SignalR] insufficientFundsWarningPersonal event:', event)
          this.onInsufficientFundsWarningPersonal?.(event)
        })

        connection.on('autoBetFailedPersonal', (event: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] autoBetFailedPersonal event:', event)
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

      // RUTAS EXACTAS DEL BACKEND
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

    if (this.lobbyConnection?.state === HubConnectionState.Connected) {
      try {
        await this.lobbyConnection.invoke('LeaveLobby')
      } catch (error) {
        console.warn('[SignalR] Error leaving lobby:', error)
      }
      tasks.push(this.lobbyConnection.stop())
    }

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

    this.lobbyConnection = undefined
    this.gameRoomConnection = undefined
    this.gameControlConnection = undefined
    
    // Limpiar todos los callbacks
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
    
    this.onAutoBetProcessed = undefined
    this.onAutoBetStatistics = undefined
    this.onAutoBetProcessingStarted = undefined
    this.onAutoBetRoundSummary = undefined
    this.onPlayerRemovedFromSeat = undefined
    this.onPlayerBalanceUpdated = undefined
    this.onInsufficientFundsWarning = undefined
    this.onAutoBetFailed = undefined
    this.onMinBetPerRoundUpdated = undefined
    
    this.onYouWereRemovedFromSeat = undefined
    this.onYourBalanceUpdated = undefined
    this.onInsufficientFundsWarningPersonal = undefined
    this.onAutoBetFailedPersonal = undefined
    
    this.onActiveRoomsUpdated = undefined
    this.onLobbyStats = undefined
    this.onQuickJoinRedirect = undefined
    this.onQuickJoinTableRedirect = undefined
    this.onDetailedRoomInfo = undefined
    
    console.log('[SignalR] All 3 specialized hub connections stopped and cleaned')
  }

  async verifyConnections(): Promise<boolean> {
    if (!this.areAllConnected) {
      console.log('[SignalR] 3 specialized hub connections not ready, attempting to start...')
      return await this.startConnections()
    }
    return true
  }

  // ==============================================
  // MÉTODOS DEL LOBBYHUB - EXACTOS DEL BACKEND
  // ==============================================

  async joinLobby(): Promise<void> {
    if (!this.lobbyConnection || this.lobbyConnection.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('LobbyHub no está disponible')
      }
    }

    console.log(`[SignalR] [LobbyHub] Joining lobby`)
    await this.lobbyConnection!.invoke('JoinLobby')
  }

  async leaveLobby(): Promise<void> {
    if (!this.lobbyConnection || this.lobbyConnection.state !== HubConnectionState.Connected) {
      return
    }

    console.log(`[SignalR] [LobbyHub] Leaving lobby`)
    await this.lobbyConnection.invoke('LeaveLobby')
  }

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

  // ==============================================
  // MÉTODOS DEL GAMEROOMHUB - EXACTOS DEL BACKEND
  // ==============================================

  async createRoom(request: CreateRoomRequest): Promise<void> {
    if (!this.gameRoomConnection || this.gameRoomConnection.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('GameRoomHub no está disponible')
      }
    }

    console.log(`[SignalR] [GameRoomHub] Creating room:`, request)
    await this.gameRoomConnection!.invoke('CreateRoom', request)
  }

  async joinRoom(request: JoinRoomRequest): Promise<void> {
    if (!this.gameRoomConnection || this.gameRoomConnection.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('GameRoomHub no está disponible')
      }
    }

    console.log(`[SignalR] [GameRoomHub] Joining room:`, request)
    await this.gameRoomConnection!.invoke('JoinRoom', request)
  }

  async joinOrCreateRoomForTable(tableId: string, playerName: string): Promise<void> {
    if (!this.gameRoomConnection || this.gameRoomConnection.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('GameRoomHub no está disponible')
      }
    }

    console.log(`[SignalR] [GameRoomHub] Joining/creating room for table: ${tableId}, playerName: ${playerName}`)
    await this.gameRoomConnection!.invoke('JoinOrCreateRoomForTable', tableId, playerName)
  }

  async leaveRoom(roomCode: string): Promise<void> {
    if (!this.gameRoomConnection || this.gameRoomConnection.state !== HubConnectionState.Connected) {
      return
    }

    try {
      console.log(`[SignalR] [GameRoomHub] Leaving room: ${roomCode}`)
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

  async joinSeat(request: JoinSeatRequest): Promise<void> {
    if (!this.gameRoomConnection || this.gameRoomConnection.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('GameRoomHub no está disponible')
      }
    }

    console.log(`[SignalR] [GameRoomHub] Joining seat:`, request)
    await this.gameRoomConnection!.invoke('JoinSeat', request)
  }

  async leaveSeat(request: LeaveSeatRequest): Promise<void> {
    if (!this.gameRoomConnection || this.gameRoomConnection.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('GameRoomHub no está disponible')
      }
    }

    console.log(`[SignalR] [GameRoomHub] Leaving seat:`, request)
    await this.gameRoomConnection!.invoke('LeaveSeat', request)
  }

  async joinAsViewer(request: JoinRoomRequest): Promise<void> {
    if (!this.gameRoomConnection || this.gameRoomConnection.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('GameRoomHub no está disponible')
      }
    }

    console.log(`[SignalR] [GameRoomHub] Joining as viewer:`, request)
    await this.gameRoomConnection!.invoke('JoinAsViewer', request)
  }

  // ==============================================
  // MÉTODOS DEL GAMECONTROLHUB - EXACTOS DEL BACKEND
  // ==============================================

  async joinRoomForGameControl(roomCode: string): Promise<void> {
    if (!this.gameControlConnection || this.gameControlConnection.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('GameControlHub no está disponible')
      }
    }

    console.log(`[SignalR] [GameControlHub] Joining room for game control: ${roomCode}`)
    await this.gameControlConnection!.invoke('JoinRoomForGameControl', roomCode)
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

  async getAutoBetStatistics(roomCode: string): Promise<void> {
    if (!this.gameControlConnection || this.gameControlConnection.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('GameControlHub no está disponible')
      }
    }

    console.log(`[SignalR] [GameControlHub] Getting auto-bet statistics for room: ${roomCode}`)
    await this.gameControlConnection!.invoke('GetAutoBetStatistics', roomCode)
  }

  // ==============================================
  // MÉTODOS DE PRUEBA - EXACTOS DEL BACKEND
  // ==============================================

  async testConnection(): Promise<void> {
    // Probar LobbyHub
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

    // Probar GameRoomHub
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

    // Probar GameControlHub
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

  // ==============================================
  // MÉTODOS DE UTILIDAD Y COMPATIBILIDAD
  // ==============================================

  // Métodos de compatibilidad para mantener la API existente
  async createRoomCompat(roomName: string): Promise<void> {
    const request: CreateRoomRequest = {
      roomName: roomName,
      maxPlayers: 6
    }
    await this.createRoom(request)
  }

  async joinRoomCompat(roomCode: string): Promise<void> {
    const user = authService.getCurrentUser()
    if (!user) {
      throw new Error('Usuario no autenticado')
    }

    const request: JoinRoomRequest = {
      roomCode: roomCode,
      playerName: user.displayName
    }
    await this.joinRoom(request)
  }

  async joinSeatCompat(roomCode: string, position: number): Promise<void> {
    const request: JoinSeatRequest = {
      roomCode: roomCode,
      position: position
    }
    await this.joinSeat(request)
  }

  async leaveSeatCompat(roomCode: string): Promise<void> {
    const request: LeaveSeatRequest = {
      roomCode: roomCode
    }
    await this.leaveSeat(request)
  }

  async joinAsViewerCompat(roomCode: string, playerName: string): Promise<void> {
    const request: JoinRoomRequest = {
      roomCode: roomCode,
      playerName: playerName
    }
    await this.joinAsViewer(request)
  }

  // ==============================================
  // PROPIEDADES DE ESTADO
  // ==============================================

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

  // ==============================================
  // INFORMACIÓN DE DEBUGGING
  // ==============================================

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