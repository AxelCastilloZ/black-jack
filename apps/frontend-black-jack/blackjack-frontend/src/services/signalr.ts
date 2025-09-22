// src/services/signalr.ts - REFACTORIZADO para hubs especializados
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr'
import { authService } from './auth'

const API_BASE = import.meta.env.VITE_API_BASE_URL || 'http://localhost:7102'

export type ConnectionState = 'Disconnected' | 'Connecting' | 'Connected' | 'Reconnecting'

class SignalRService {
  // HUBS ESPECIALIZADOS
  private lobbyConnection?: HubConnection
  private connectionHub?: HubConnection
  private roomHub?: HubConnection
  private spectatorHub?: HubConnection
  private seatHub?: HubConnection
  private gameControlHub?: HubConnection
  
  private isStarting = false
  private isDestroying = false

  // Event callbacks para el UI
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
            console.error('[DEBUG] ❌ No token available!')
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
              console.log('[DEBUG] ✅ Token payload decoded:', payload)
              console.log('[DEBUG] PlayerId in token:', payload.playerId || payload.sub || payload.nameid || 'NOT_FOUND')
              console.log('[DEBUG] Name in token:', payload.name || payload.unique_name || 'NOT_FOUND')
              console.log('[DEBUG] Token expires:', new Date((payload.exp || 0) * 1000))
              
              const now = Math.floor(Date.now() / 1000)
              if (payload.exp && payload.exp < now) {
                console.error('[DEBUG] ❌ TOKEN EXPIRED!')
                console.error('[DEBUG] Token expired at:', new Date(payload.exp * 1000))
                console.error('[DEBUG] Current time:', new Date())
                return ''
              } else {
                console.log('[DEBUG] ✅ Token is valid and not expired')
              }
              
            } catch (e) {
              console.error('[DEBUG] ❌ Cannot decode token payload:', e)
              return ''
            }
          } else {
            console.error('[DEBUG] ❌ Invalid JWT format - should have 3 parts')
            return ''
          }
          
          console.log('[DEBUG] ✅ Returning clean token for SignalR')
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
      console.log(`[SignalR] ✅ ${name} reconnected successfully with ID:`, connectionId)
    })
    
    connection.onclose(error => {
      if (this.isDestroying) {
        console.log(`[SignalR] ${name} connection closed normally during cleanup`)
        return
      }
      
      if (error) {
        console.error(`[SignalR] ❌ ${name} connection closed with error:`, error)
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
      console.log(`[SignalR] 📥 ✅ ${name} success:`, message)
    })

    connection.on('Error', (errorData: any) => {
      if (this.isDestroying) return
      const message = errorData?.message || errorData
      console.error(`[SignalR] 📥 ❌ ${name} error:`, message)
      this.onError?.(message)
    })

    connection.on('TestResponse', (data: any) => {
      if (this.isDestroying) return
      console.log(`[SignalR] 📥 ${name} TestResponse:`, data)
    })

    // Handlers específicos por hub
    this.setupSpecificHandlers(connection, name)
  }

  private setupSpecificHandlers(connection: HubConnection, name: string) {
    switch (name) {
      case 'Lobby':
        connection.on('ActiveRoomsUpdated', (response: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] 📥 ActiveRoomsUpdated event:', response)
        })
        break

      case 'Connection':
        connection.on('AutoReconnectAttempt', (data: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] 📥 AutoReconnectAttempt:', data)
        })
        break

      case 'Room':
        connection.on('RoomCreated', (response: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] 📥 RoomCreated event:', response)
          this.onRoomCreated?.(response?.data || response)
        })

        connection.on('RoomJoined', (response: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] 📥 RoomJoined event:', response)
          this.onRoomJoined?.(response?.data || response)
        })

        connection.on('RoomInfoUpdated', (roomData: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] 📥 RoomInfoUpdated event:', roomData)
          const data = roomData?.data || roomData
          this.onRoomInfoUpdated?.(data)
        })

        connection.on('RoomInfo', (roomData: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] 📥 RoomInfo event:', roomData)
          this.onRoomInfo?.(roomData)
        })

        connection.on('PlayerJoined', (data: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] 📥 PlayerJoined event:', data)
          this.onPlayerJoined?.(data)
        })
        
        connection.on('PlayerLeft', (data: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] 📥 PlayerLeft event:', data)
          this.onPlayerLeft?.(data)
        })
        break

      case 'Seat':
        connection.on('SeatJoined', (response: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] 📥 SeatJoined event:', response)
          this.onSeatJoined?.(response?.data || response)
        })

        connection.on('SeatLeft', (response: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] 📥 SeatLeft event:', response)
          this.onSeatLeft?.(response?.data || response)
        })
        break

      case 'GameControl':
        connection.on('GameStarted', (gameData: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] 📥 GameStarted event:', gameData)
          this.onGameStateChanged?.(gameData)
        })

        connection.on('GameStateChanged', (gameState: any) => {
          if (this.isDestroying) return
          console.log('[SignalR] 📥 GameStateChanged event:', gameState)
          this.onGameStateChanged?.(gameState)
        })
        break

      case 'Spectator':
        // Eventos específicos para espectadores si los hay
        break
    }
  }

  async startConnections(): Promise<boolean> {
    if (this.isStarting) {
      console.log('[SignalR] ⏳ Connection start already in progress, waiting...')
      
      let attempts = 0
      while (this.isStarting && attempts < 50) {
        await new Promise(resolve => setTimeout(resolve, 100))
        attempts++
      }
      
      return this.areAllConnected
    }
    
    if (!authService.isAuthenticated()) {
      console.error('[SignalR] ❌ Cannot start connections - user not authenticated')
      return false
    }

    console.log('[SignalR] ✅ User is authenticated, starting specialized hub connections...')

    this.isStarting = true
    this.isDestroying = false

    try {
      console.log('[SignalR] 🚀 Starting specialized SignalR connections...')

      // Crear conexiones a hubs especializados
      const hubsToCreate = [
        { name: 'Lobby', path: '/hubs/lobby', prop: 'lobbyConnection' },
        { name: 'Connection', path: '/hubs/connection', prop: 'connectionHub' },
        { name: 'Room', path: '/hubs/room', prop: 'roomHub' },
        { name: 'Spectator', path: '/hubs/spectator', prop: 'spectatorHub' },
        { name: 'Seat', path: '/hubs/seat', prop: 'seatHub' },
        { name: 'GameControl', path: '/hubs/game-control', prop: 'gameControlHub' }
      ]

      const tasks: Promise<void>[] = []

      for (const hub of hubsToCreate) {
        const connection = (this as any)[hub.prop]
        
        if (!connection || connection.state === HubConnectionState.Disconnected) {
          console.log(`[SignalR] 🏗️ Creating ${hub.name} connection...`)
          ;(this as any)[hub.prop] = this.buildConnection(hub.path)
          this.setupConnectionHandlers((this as any)[hub.prop], hub.name)
        }

        if ((this as any)[hub.prop].state === HubConnectionState.Disconnected) {
          console.log(`[SignalR] 🔌 Starting ${hub.name} connection...`)
          tasks.push(
            (this as any)[hub.prop].start().then(() => {
              console.log(`[SignalR] ✅ ${hub.name} connection started successfully`)
            }).catch((error: any) => {
              console.error(`[SignalR] ❌ ${hub.name} connection failed:`, error)
              throw error
            })
          )
        } else {
          console.log(`[SignalR] ${hub.name} connection already active:`, (this as any)[hub.prop].state)
        }
      }

      if (tasks.length > 0) {
        console.log('[SignalR] ⏳ Waiting for specialized hub connections to start...')
        await Promise.all(tasks)
        console.log('[SignalR] ✅ All specialized hub connections started successfully')
      } else {
        console.log('[SignalR] ✅ All specialized hub connections already active')
      }

      // Unirse al lobby si está conectado
      if (!this.isDestroying && this.lobbyConnection?.state === HubConnectionState.Connected) {
        try {
          console.log('[SignalR] 🏛️ Joining lobby group...')
          await this.lobbyConnection.invoke('JoinLobby')
          console.log('[SignalR] ✅ Successfully joined lobby')
        } catch (error) {
          console.warn('[SignalR] ⚠️ Could not join lobby:', error)
        }
      }

      console.log('[SignalR] 🎉 All specialized hub connections ready and configured')
      return true

    } catch (error: any) {
      console.error('[SignalR] ❌ Failed to start specialized hub connections:', error)
      
      if (error.message?.includes('401') || error.message?.includes('Unauthorized')) {
        console.error('[SignalR] 🔐 AUTHENTICATION ERROR - JWT token issue')
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
    console.log('[SignalR] 🛑 Stopping all specialized hub connections...')
    this.isDestroying = true
    
    const tasks: Promise<void>[] = []

    // Salir del lobby si está conectado
    if (this.lobbyConnection?.state === HubConnectionState.Connected) {
      try {
        await this.lobbyConnection.invoke('LeaveLobby')
      } catch (error) {
        console.warn('[SignalR] ⚠️ Error leaving lobby:', error)
      }
      tasks.push(this.lobbyConnection.stop())
    }

    // Detener todas las conexiones especializadas
    const connections = [
      this.connectionHub,
      this.roomHub,
      this.spectatorHub,
      this.seatHub,
      this.gameControlHub
    ]

    for (const connection of connections) {
      if (connection?.state === HubConnectionState.Connected) {
        tasks.push(connection.stop())
      }
    }

    if (tasks.length > 0) {
      await Promise.all(tasks)
    }

    // Limpiar referencias
    this.lobbyConnection = undefined
    this.connectionHub = undefined
    this.roomHub = undefined
    this.spectatorHub = undefined
    this.seatHub = undefined
    this.gameControlHub = undefined
    
    // Limpiar callbacks
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
    
    console.log('[SignalR] ✅ All specialized hub connections stopped and cleaned')
  }

  async verifyConnections(): Promise<boolean> {
    if (!this.areAllConnected) {
      console.log('[SignalR] Specialized hub connections not ready, attempting to start...')
      return await this.startConnections()
    }
    return true
  }

  // MÉTODOS QUE USAN ROOMHUB
  async joinOrCreateRoomForTable(tableId: string, playerName?: string): Promise<void> {
    if (!this.roomHub || this.roomHub.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('RoomHub no está disponible')
      }
    }

    const user = authService.getCurrentUser()
    const finalPlayerName = playerName || user?.displayName || 'Jugador'

    console.log(`[SignalR] 🎯 [RoomHub] Joining/creating room for table: ${tableId}, playerName: ${finalPlayerName}`)
    
    try {
      await this.roomHub!.invoke('JoinOrCreateRoomForTable', tableId, finalPlayerName)
      console.log(`[SignalR] ✅ Successfully invoked JoinOrCreateRoomForTable via RoomHub`)
    } catch (error) {
      console.error(`[SignalR] ❌ Error in JoinOrCreateRoomForTable:`, error)
      throw error
    }
  }

  async joinRoom(roomCode: string): Promise<void> {
    if (!this.roomHub || this.roomHub.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('RoomHub no está disponible')
      }
    }

    const user = authService.getCurrentUser()
    if (!user) {
      throw new Error('Usuario no autenticado')
    }

    console.log(`[SignalR] 🚪 [RoomHub] Joining existing room: ${roomCode}`)

    await this.roomHub!.invoke('JoinRoom', {
      roomCode: roomCode,
      playerName: user.displayName
    })
  }

  async createRoom(roomName: string): Promise<void> {
    if (!this.roomHub || this.roomHub.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('RoomHub no está disponible')
      }
    }

    console.log(`[SignalR] 🏗️ [RoomHub] Creating room: ${roomName}`)

    await this.roomHub!.invoke('CreateRoom', {
      roomName: roomName,
      maxPlayers: 6
    })
  }

  async leaveRoom(roomCode: string): Promise<void> {
    if (!this.roomHub || this.roomHub.state !== HubConnectionState.Connected) {
      return
    }

    try {
      console.log(`[SignalR] 🚪 [RoomHub] EXPLICIT LeaveRoom called for: ${roomCode}`)
      await this.roomHub.invoke('LeaveRoom', roomCode)
    } catch (error) {
      console.warn('[SignalR] ⚠️ Error leaving room:', error)
    }
  }

  async getRoomInfo(roomCode: string): Promise<void> {
    if (!this.roomHub || this.roomHub.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('RoomHub no está disponible')
      }
    }

    console.log(`[SignalR] 📋 [RoomHub] Getting room info: ${roomCode}`)
    await this.roomHub!.invoke('GetRoomInfo', roomCode)
  }

  // MÉTODOS QUE USAN SEATHUB
  async joinSeat(roomCode: string, position: number): Promise<void> {
    if (!this.seatHub || this.seatHub.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('SeatHub no está disponible')
      }
    }

    console.log(`[SignalR] 💺 [SeatHub] Joining seat ${position} in room: ${roomCode}`)

    try {
      await this.seatHub!.invoke('JoinSeat', {
        RoomCode: roomCode,
        Position: position
      })
      console.log(`[SignalR] ✅ Successfully invoked JoinSeat via SeatHub`)
    } catch (error) {
      console.error(`[SignalR] ❌ Error in JoinSeat:`, error)
      throw error
    }
  }

  async leaveSeat(roomCode: string): Promise<void> {
    if (!this.seatHub || this.seatHub.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('SeatHub no está disponible')
      }
    }

    console.log(`[SignalR] 🚪 [SeatHub] Leaving seat in room: ${roomCode}`)

    try {
      await this.seatHub!.invoke('LeaveSeat', {
        RoomCode: roomCode
      })
      console.log(`[SignalR] ✅ Successfully invoked LeaveSeat via SeatHub`)
    } catch (error) {
      console.error(`[SignalR] ❌ Error in LeaveSeat:`, error)
      throw error
    }
  }

  // MÉTODOS QUE USAN SPECTATORHUB
  async joinAsViewer(tableId: string, playerName: string): Promise<void> {
    if (!this.spectatorHub || this.spectatorHub.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('SpectatorHub no está disponible')
      }
    }

    const user = authService.getCurrentUser()
    if (!user) {
      throw new Error('Usuario no autenticado')
    }

    console.log(`[SignalR] 👁️ [SpectatorHub] Joining as viewer: ${playerName} to table: ${tableId}`)

    const roomCode = tableId

    await this.spectatorHub!.invoke('JoinAsViewer', {
      roomCode: roomCode,
      playerName: playerName
    })
  }

  async joinOrCreateRoomForTableAsViewer(tableId: string, playerName?: string): Promise<void> {
    if (!this.spectatorHub || this.spectatorHub.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('SpectatorHub no está disponible')
      }
    }

    const user = authService.getCurrentUser()
    const finalPlayerName = playerName || user?.displayName || 'Viewer'

    console.log(`[SignalR] 👁️ [SpectatorHub] Joining/creating room as viewer for table: ${tableId}, playerName: ${finalPlayerName}`)
    
    try {
      await this.spectatorHub!.invoke('JoinOrCreateRoomForTableAsViewer', tableId, finalPlayerName)
      console.log(`[SignalR] ✅ Successfully invoked JoinOrCreateRoomForTableAsViewer via SpectatorHub`)
    } catch (error) {
      console.error(`[SignalR] ❌ Error in JoinOrCreateRoomForTableAsViewer:`, error)
      throw error
    }
  }

  // MÉTODOS QUE USAN GAMECONTROLHUB
  async startGame(roomCode: string): Promise<void> {
    if (!this.gameControlHub || this.gameControlHub.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('GameControlHub no está disponible')
      }
    }

    console.log(`[SignalR] 🎮 [GameControlHub] Starting game for room: ${roomCode}`)
    await this.gameControlHub!.invoke('StartGame', roomCode)
  }

  // MÉTODOS DE TEST
  async testConnection(): Promise<void> {
    if (!this.connectionHub || this.connectionHub.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('ConnectionHub no está disponible')
      }
    }

    console.log('[SignalR] 🧪 [ConnectionHub] Testing connection...')
    
    try {
      await this.connectionHub!.invoke('TestConnection')
      console.log('[SignalR] ✅ Test connection successful via ConnectionHub')
    } catch (error) {
      console.error('[SignalR] ❌ Test connection failed:', error)
      throw error
    }
  }

  // MÉTODOS DE JUEGO (pueden ir a GameControlHub en el futuro)
  async placeBet(roomCode: string, amount: number): Promise<void> {
    if (!this.gameControlHub || this.gameControlHub.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('GameControlHub no está disponible')
      }
    }

    console.log(`[SignalR] 💰 [GameControlHub] Placing bet: ${amount} for room: ${roomCode}`)
    await this.gameControlHub!.invoke('PlaceBet', {
      roomCode: roomCode,
      amount: amount
    })
  }

  async playerAction(roomCode: string, action: string): Promise<void> {
    if (!this.gameControlHub || this.gameControlHub.state !== HubConnectionState.Connected) {
      if (!(await this.verifyConnections())) {
        throw new Error('GameControlHub no está disponible')
      }
    }

    console.log(`[SignalR] 🎯 [GameControlHub] Player action: ${action} for room: ${roomCode}`)
    await this.gameControlHub!.invoke('PlayerAction', {
      roomCode: roomCode,
      action: action
    })
  }

  // GETTERS DE ESTADO
  get isLobbyConnected(): boolean {
    return this.lobbyConnection?.state === HubConnectionState.Connected
  }

  get isConnectionHubConnected(): boolean {
    return this.connectionHub?.state === HubConnectionState.Connected
  }

  get isRoomHubConnected(): boolean {
    return this.roomHub?.state === HubConnectionState.Connected
  }

  get isSpectatorHubConnected(): boolean {
    return this.spectatorHub?.state === HubConnectionState.Connected
  }

  get isSeatHubConnected(): boolean {
    return this.seatHub?.state === HubConnectionState.Connected
  }

  get isGameControlHubConnected(): boolean {
    return this.gameControlHub?.state === HubConnectionState.Connected
  }

  get areAllConnected(): boolean {
    return this.isLobbyConnected && 
           this.isConnectionHubConnected && 
           this.isRoomHubConnected && 
           this.isSpectatorHubConnected && 
           this.isSeatHubConnected && 
           this.isGameControlHubConnected
  }

  get connectionState(): ConnectionState {
    const connections = [
      this.lobbyConnection,
      this.connectionHub,
      this.roomHub,
      this.spectatorHub,
      this.seatHub,
      this.gameControlHub
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