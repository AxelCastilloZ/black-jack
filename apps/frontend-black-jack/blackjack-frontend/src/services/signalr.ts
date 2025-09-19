// src/services/signalr.ts - Versión de Producción
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

  // Event callbacks para el UI
  public onPlayerJoined?: (player: any) => void
  public onPlayerLeft?: (player: any) => void
  public onRoomInfo?: (roomData: any) => void
  public onGameStateChanged?: (gameState: any) => void
  public onRoomJoined?: (data: any) => void

  // Construir conexión SignalR
  private buildConnection(hubPath: string): HubConnection {
    const fullUrl = `${API_BASE}${hubPath}`
    
    return new HubConnectionBuilder()
      .withUrl(fullUrl, {
        accessTokenFactory: () => {
          const token = authService.getToken()
          if (!token) return ''
          return token.replace(/^Bearer\s+/i, '').trim()
        }
      })
      .withAutomaticReconnect([0, 2000, 10000, 30000])
      .configureLogging(LogLevel.Warning) // Solo warnings y errores en producción
      .build()
  }

  // Configurar handlers de eventos
  private setupConnectionHandlers(connection: HubConnection, name: string) {
    connection.onreconnecting(() => {
      console.log(`[SignalR] ${name} reconnecting...`)
    })
    
    connection.onreconnected(() => {
      console.log(`[SignalR] ${name} reconnected`)
    })
    
    connection.onclose(error => {
      if (error) {
        console.error(`[SignalR] ${name} connection closed:`, error.message)
      }
    })

    // Eventos del servidor
    if (name === 'Game') {
      connection.on('PlayerJoined', (data: any) => {
        console.log('[SignalR] Player joined room')
        this.onPlayerJoined?.(data)
      })
      
      connection.on('PlayerLeft', (data: any) => {
        console.log('[SignalR] Player left room')
        this.onPlayerLeft?.(data)
      })
      
      connection.on('RoomInfo', (roomData: any) => {
        console.log('[SignalR] Room info received')
        this.onRoomInfo?.(roomData)
      })

      connection.on('GameStateChanged', (gameState: any) => {
        console.log('[SignalR] Game state updated')
        this.onGameStateChanged?.(gameState)
      })

      connection.on('RoomJoined', (data: any) => {
        console.log('[SignalR] Successfully joined room')
        this.onRoomJoined?.(data)
      })

      connection.on('Error', (message: string) => {
        console.error('[SignalR] Server error:', message)
      })
    }
  }

  // Iniciar conexiones
  async startConnections(): Promise<boolean> {
    if (this.isStarting) return false
    if (!authService.isAuthenticated()) {
      console.error('[SignalR] Not authenticated')
      return false
    }

    this.isStarting = true

    try {
      // Crear conexiones
      if (!this.lobbyConnection) {
        this.lobbyConnection = this.buildConnection('/hubs/lobby')
        this.setupConnectionHandlers(this.lobbyConnection, 'Lobby')
      }

      if (!this.gameConnection) {
        this.gameConnection = this.buildConnection('/hubs/game')
        this.setupConnectionHandlers(this.gameConnection, 'Game')
      }

      // Iniciar conexiones
      const tasks: Promise<void>[] = []

      if (this.lobbyConnection.state === HubConnectionState.Disconnected) {
        tasks.push(this.lobbyConnection.start())
      }

      if (this.gameConnection.state === HubConnectionState.Disconnected) {
        tasks.push(this.gameConnection.start())
      }

      if (tasks.length > 0) {
        await Promise.all(tasks)
      }

      // Unirse al lobby
      if (this.lobbyConnection.state === HubConnectionState.Connected) {
        try {
          await this.lobbyConnection.invoke('JoinLobby')
        } catch (error) {
          console.warn('[SignalR] Could not join lobby:', error)
        }
      }

      console.log('[SignalR] Connections ready')
      return true

    } catch (error: any) {
      console.error('[SignalR] Connection failed:', error.message)
      return false
    } finally {
      this.isStarting = false
    }
  }

  // Detener conexiones
  async stopConnections() {
    const tasks: Promise<void>[] = []

    if (this.lobbyConnection?.state === HubConnectionState.Connected) {
      try {
        await this.lobbyConnection.invoke('LeaveLobby')
      } catch (error) {
        console.warn('[SignalR] Error leaving lobby')
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
  }

  // Unirse a una room
  async joinRoom(roomCode: string): Promise<void> {
    if (!this.gameConnection || this.gameConnection.state !== HubConnectionState.Connected) {
      throw new Error('No hay conexión disponible')
    }

    const user = authService.getCurrentUser()
    if (!user) {
      throw new Error('Usuario no autenticado')
    }

    await this.gameConnection.invoke('JoinRoom', {
      roomCode: roomCode,
      playerName: user.displayName,
      playerId: user.id
    })
  }

  // Salir de una room
  async leaveRoom(roomCode: string): Promise<void> {
    if (!this.gameConnection || this.gameConnection.state !== HubConnectionState.Connected) {
      return
    }

    try {
      await this.gameConnection.invoke('LeaveRoom', roomCode)
    } catch (error) {
      console.warn('[SignalR] Error leaving room')
    }
  }

  // Obtener información de una room
  async getRoomInfo(roomCode: string): Promise<void> {
    if (!this.gameConnection || this.gameConnection.state !== HubConnectionState.Connected) {
      throw new Error('No hay conexión disponible')
    }

    await this.gameConnection.invoke('GetRoomInfo', roomCode)
  }

  // Getters para estado
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