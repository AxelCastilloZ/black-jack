// src/services/signalr.ts - ARCHIVO COMPLETO CORREGIDO
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
  private lobbyConnection?: HubConnection
  private gameConnection?: HubConnection
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

    // Handlers específicos para LobbyHub
    if (name === 'Lobby') {
      connection.on('ActiveRoomsUpdated', (response: any) => {
        if (this.isDestroying) return
        console.log('[SignalR] 📥 ActiveRoomsUpdated event:', response)
      })

      connection.on('Success', (successData: any) => {
        if (this.isDestroying) return
        const message = successData?.message || successData
        console.log('[SignalR] 📥 ✅ Lobby success:', message)
      })

      connection.on('Error', (errorData: any) => {
        if (this.isDestroying) return
        const message = errorData?.message || errorData
        console.error('[SignalR] 📥 ❌ Lobby error:', message)
        this.onError?.(message)
      })

      connection.on('TestResponse', (data: any) => {
        if (this.isDestroying) return
        console.log('[SignalR] 📥 Lobby TestResponse:', data)
      })
    }

    // Eventos del servidor para GameHub
    if (name === 'Game') {
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
      
      connection.on('RoomInfo', (roomData: any) => {
        if (this.isDestroying) return
        console.log('[SignalR] 📥 RoomInfo event:', roomData)
        this.onRoomInfo?.(roomData)
      })

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

      connection.on('GameStateChanged', (gameState: any) => {
        if (this.isDestroying) return
        console.log('[SignalR] 📥 GameStateChanged event:', gameState)
        this.onGameStateChanged?.(gameState)
      })

      connection.on('GameStarted', (gameData: any) => {
        if (this.isDestroying) return
        console.log('[SignalR] 📥 GameStarted event:', gameData)
        this.onGameStateChanged?.(gameData)
      })

      connection.on('Error', (errorData: any) => {
        if (this.isDestroying) return
        const message = errorData?.message || errorData
        console.error('[SignalR] 📥 ❌ Server error:', message)
        this.onError?.(message)
      })

      connection.on('Success', (successData: any) => {
        if (this.isDestroying) return
        const message = successData?.message || successData
        console.log('[SignalR] 📥 ✅ Server success:', message)
      })

      connection.on('TestResponse', (data: any) => {
        if (this.isDestroying) return
        console.log('[SignalR] 📥 TestResponse:', data)
      })
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
      
      return this.isLobbyConnected && this.isGameConnected
    }
    
    if (!authService.isAuthenticated()) {
      console.error('[SignalR] ❌ Cannot start connections - user not authenticated')
      return false
    }

    console.log('[SignalR] ✅ User is authenticated, starting connections...')

    this.isStarting = true
    this.isDestroying = false

    try {
      console.log('[SignalR] 🚀 Starting SignalR connections...')

      if (!this.lobbyConnection || this.lobbyConnection.state === HubConnectionState.Disconnected) {
        console.log('[SignalR] 🏗️ Creating lobby connection...')
        this.lobbyConnection = this.buildConnection('/hubs/lobby')
        this.setupConnectionHandlers(this.lobbyConnection, 'Lobby')
      }

      if (!this.gameConnection || this.gameConnection.state === HubConnectionState.Disconnected) {
        console.log('[SignalR] 🏗️ Creating game connection...')
        this.gameConnection = this.buildConnection('/hubs/game')
        this.setupConnectionHandlers(this.gameConnection, 'Game')
      }

      const tasks: Promise<void>[] = []

      if (this.lobbyConnection.state === HubConnectionState.Disconnected) {
        console.log('[SignalR] 🔌 Starting lobby connection...')
        tasks.push(
          this.lobbyConnection.start().then(() => {
            console.log('[SignalR] ✅ Lobby connection started successfully')
          }).catch(error => {
            console.error('[SignalR] ❌ Lobby connection failed:', error)
            throw error
          })
        )
      } else {
        console.log('[SignalR] Lobby connection already active:', this.lobbyConnection.state)
      }

      if (this.gameConnection.state === HubConnectionState.Disconnected) {
        console.log('[SignalR] 🔌 Starting game connection...')
        tasks.push(
          this.gameConnection.start().then(() => {
            console.log('[SignalR] ✅ Game connection started successfully')
          }).catch(error => {
            console.error('[SignalR] ❌ Game connection failed:', error)
            throw error
          })
        )
      } else {
        console.log('[SignalR] Game connection already active:', this.gameConnection.state)
      }

      if (tasks.length > 0) {
        console.log('[SignalR] ⏳ Waiting for connections to start...')
        await Promise.all(tasks)
        console.log('[SignalR] ✅ All connections started successfully')
      } else {
        console.log('[SignalR] ✅ All connections already active')
      }

      if (!this.isDestroying && this.lobbyConnection.state === HubConnectionState.Connected) {
        try {
          console.log('[SignalR] 🏛️ Joining lobby group...')
          await this.lobbyConnection.invoke('JoinLobby')
          console.log('[SignalR] ✅ Successfully joined lobby')
        } catch (error) {
          console.warn('[SignalR] ⚠️ Could not join lobby:', error)
        }
      }

      console.log('[SignalR] 🎉 All connections ready and configured')
      return true

    } catch (error: any) {
      console.error('[SignalR] ❌ Failed to start connections:', error)
      
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
    console.log('[SignalR] 🛑 Stopping all connections...')
    this.isDestroying = true
    
    const tasks: Promise<void>[] = []

    if (this.lobbyConnection?.state === HubConnectionState.Connected) {
      try {
        await this.lobbyConnection.invoke('LeaveLobby')
      } catch (error) {
        console.warn('[SignalR] ⚠️ Error leaving lobby:', error)
      }
      tasks.push(this.lobbyConnection.stop())
    }

    if (this.gameConnection?.state === HubConnectionState.Connected) {
      tasks.push(this.gameConnection.stop())
    }

    if (tasks.length > 0) {
      await Promise.all(tasks)
    }

    this.lobbyConnection = undefined
    this.gameConnection = undefined
    
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
    
    console.log('[SignalR] ✅ All connections stopped and cleaned')
  }

  async verifyConnections(): Promise<boolean> {
    if (!this.isLobbyConnected || !this.isGameConnected) {
      console.log('[SignalR] Connections not ready, attempting to start...')
      return await this.startConnections()
    }
    return true
  }

  async joinOrCreateRoomForTable(tableId: string, playerName?: string): Promise<void> {
    if (!(await this.verifyConnections())) {
      throw new Error('No hay conexión de juego disponible')
    }

    const user = authService.getCurrentUser()
    const finalPlayerName = playerName || user?.displayName || 'Jugador'

    console.log(`[SignalR] 🎯 Joining/creating room for table: ${tableId}, playerName: ${finalPlayerName}`)
    
    try {
      await this.gameConnection!.invoke('JoinOrCreateRoomForTable', tableId, finalPlayerName)
      console.log(`[SignalR] ✅ Successfully invoked JoinOrCreateRoomForTable`)
    } catch (error) {
      console.error(`[SignalR] ❌ Error in JoinOrCreateRoomForTable:`, error)
      throw error
    }
  }

  async joinSeat(roomCode: string, position: number): Promise<void> {
    if (!(await this.verifyConnections())) {
      throw new Error('No hay conexión de juego disponible')
    }

    console.log(`[SignalR] 💺 Joining seat ${position} in room: ${roomCode}`)

    try {
      await this.gameConnection!.invoke('JoinSeat', {
        RoomCode: roomCode,
        Position: position
      })
      console.log(`[SignalR] ✅ Successfully invoked JoinSeat`)
    } catch (error) {
      console.error(`[SignalR] ❌ Error in JoinSeat:`, error)
      throw error
    }
  }

  async leaveSeat(roomCode: string): Promise<void> {
    if (!(await this.verifyConnections())) {
      throw new Error('No hay conexión de juego disponible')
    }

    console.log(`[SignalR] 🚪 Leaving seat in room: ${roomCode}`)

    try {
      await this.gameConnection!.invoke('LeaveSeat', {
        RoomCode: roomCode
      })
      console.log(`[SignalR] ✅ Successfully invoked LeaveSeat`)
    } catch (error) {
      console.error(`[SignalR] ❌ Error in LeaveSeat:`, error)
      throw error
    }
  }

  async testConnection(): Promise<void> {
    if (!(await this.verifyConnections())) {
      throw new Error('No hay conexión de juego disponible')
    }

    console.log('[SignalR] 🧪 Testing connection...')
    
    try {
      await this.gameConnection!.invoke('TestConnection')
      console.log('[SignalR] ✅ Test connection successful')
    } catch (error) {
      console.error('[SignalR] ❌ Test connection failed:', error)
      throw error
    }
  }

  async joinRoom(roomCode: string): Promise<void> {
    if (!(await this.verifyConnections())) {
      throw new Error('No hay conexión de juego disponible')
    }

    const user = authService.getCurrentUser()
    if (!user) {
      throw new Error('Usuario no autenticado')
    }

    console.log(`[SignalR] 🚪 Joining existing room: ${roomCode}`)

    await this.gameConnection!.invoke('JoinRoom', {
      roomCode: roomCode,
      playerName: user.displayName
    })
  }

  async joinAsViewer(tableId: string, playerName: string): Promise<void> {
    if (!(await this.verifyConnections())) {
      throw new Error('No hay conexión de juego disponible')
    }

    const user = authService.getCurrentUser()
    if (!user) {
      throw new Error('Usuario no autenticado')
    }

    console.log(`[SignalR] 👁️ Joining as viewer: ${playerName} to table: ${tableId}`)

    // Convert tableId to roomCode (assuming tableId is the roomCode for now)
    const roomCode = tableId

    await this.gameConnection!.invoke('JoinAsViewer', {
      roomCode: roomCode,
      playerName: playerName
    })
  }

  async joinOrCreateRoomForTableAsViewer(tableId: string, playerName?: string): Promise<void> {
    if (!(await this.verifyConnections())) {
      throw new Error('No hay conexión de juego disponible')
    }

    const user = authService.getCurrentUser()
    const finalPlayerName = playerName || user?.displayName || 'Viewer'

    console.log(`[SignalR] 👁️ Joining/creating room as viewer for table: ${tableId}, playerName: ${finalPlayerName}`)
    
    try {
      await this.gameConnection!.invoke('JoinOrCreateRoomForTableAsViewer', tableId, finalPlayerName)
      console.log(`[SignalR] ✅ Successfully invoked JoinOrCreateRoomForTableAsViewer`)
    } catch (error) {
      console.error(`[SignalR] ❌ Error in JoinOrCreateRoomForTableAsViewer:`, error)
      throw error
    }
  }

  async createRoom(roomName: string): Promise<void> {
    if (!(await this.verifyConnections())) {
      throw new Error('No hay conexión de juego disponible')
    }

    console.log(`[SignalR] 🏗️ Creating room: ${roomName}`)

    await this.gameConnection!.invoke('CreateRoom', {
      roomName: roomName,
      maxPlayers: 6
    })
  }

  // CORREGIDO: leaveRoom solo cuando es EXPLÍCITO
  async leaveRoom(roomCode: string): Promise<void> {
    if (!this.gameConnection || this.gameConnection.state !== HubConnectionState.Connected) {
      return
    }

    try {
      console.log(`[SignalR] 🚪 EXPLICIT LeaveRoom called for: ${roomCode}`)
      await this.gameConnection.invoke('LeaveRoom', roomCode)
    } catch (error) {
      console.warn('[SignalR] ⚠️ Error leaving room:', error)
    }
  }

  async getRoomInfo(roomCode: string): Promise<void> {
    if (!(await this.verifyConnections())) {
      throw new Error('No hay conexión de juego disponible')
    }

    console.log(`[SignalR] 📋 Getting room info: ${roomCode}`)
    await this.gameConnection!.invoke('GetRoomInfo', roomCode)
  }

  async startGame(roomCode: string): Promise<void> {
    if (!(await this.verifyConnections())) {
      throw new Error('No hay conexión de juego disponible')
    }

    await this.gameConnection!.invoke('StartGame', roomCode)
  }

  async placeBet(roomCode: string, amount: number): Promise<void> {
    if (!(await this.verifyConnections())) {
      throw new Error('No hay conexión de juego disponible')
    }

    await this.gameConnection!.invoke('PlaceBet', {
      roomCode: roomCode,
      amount: amount
    })
  }

  async playerAction(roomCode: string, action: string): Promise<void> {
    if (!(await this.verifyConnections())) {
      throw new Error('No hay conexión de juego disponible')
    }

    await this.gameConnection!.invoke('PlayerAction', {
      roomCode: roomCode,
      action: action
    })
  }

  get isLobbyConnected(): boolean {
    return this.lobbyConnection?.state === HubConnectionState.Connected
  }

  get isGameConnected(): boolean {
    return this.gameConnection?.state === HubConnectionState.Connected
  }

  get connectionState(): ConnectionState {
    const lobbyState = this.lobbyConnection?.state
    const gameState = this.gameConnection?.state

    if (lobbyState === HubConnectionState.Connected || gameState === HubConnectionState.Connected) {
      return 'Connected'
    }
    if (lobbyState === HubConnectionState.Connecting || gameState === HubConnectionState.Connecting) {
      return 'Connecting'
    }
    if (lobbyState === HubConnectionState.Reconnecting || gameState === HubConnectionState.Reconnecting) {
      return 'Reconnecting'
    }
    return 'Disconnected'
  }
}

export const signalRService = new SignalRService()