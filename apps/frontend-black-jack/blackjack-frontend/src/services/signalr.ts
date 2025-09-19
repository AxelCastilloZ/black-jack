// src/services/signalr.ts - CORREGIDO CON MÉTODOS CORRECTOS DEL BACKEND
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  IHttpConnectionOptions,
  HttpTransportType,
} from '@microsoft/signalr'
import { authService } from './auth'
import { apiService } from '../api/apiService'

export type ConnectionState = 'Disconnected' | 'Connecting' | 'Connected' | 'Reconnecting'

export interface LobbyUpdate {
  tableId: string
  playerCount: number
  status: string
}

export interface NewTableCreated {
  table: LobbyTable
}

export interface ChatMessage {
  id: string
  playerName: string
  text: string
  timestamp: string
  tableId: string
}

export interface GameState {
  id: string
  status: string
  players: Player[]
  minBet: number
  maxBet: number
  pot?: number
  dealer?: {
    hand?: Card[]
    handValue?: number
    isBusted?: boolean
    hasBlackjack?: boolean
  }
  [key: string]: any
}

interface Player {
  id: string
  displayName: string
  balance: number
  currentBet: number
  position: number
  isActive: boolean
  hand?: {
    cards: Card[]
    handValue: number
    isBusted: boolean
    hasBlackjack: boolean
  }
}

interface Card {
  suit: string
  rank: string
  value: number
  isHidden?: boolean
}

export type LobbyTable = {
  id: string
  name: string
  playerCount: number
  maxPlayers: number
  minBet: number
  maxBet: number
  status: string
}

function apiBase(): string {
  const raw = import.meta.env.VITE_API_BASE_URL as string | undefined
  if (!raw) return ''
  return raw.endsWith('/') ? raw.slice(0, -1) : raw
}

function hubUrl(path: '/hubs/lobby' | '/hubs/game'): string {
  const base = apiBase()
  return base ? `${base}${path}` : path
}

function mapBackendTableToLobby(x: any): LobbyTable {
  return {
    id: String(x.id ?? ''),
    name: String(x.name ?? 'Mesa'),
    playerCount: Number(x.playerCount ?? x.seats?.filter((s: any) => s.isOccupied)?.length ?? 0),
    maxPlayers: Number(x.maxPlayers ?? 6),
    minBet: Number(x.minBet ?? 0),
    maxBet: Number(x.maxBet ?? 0),
    status: String(x.status ?? 'WaitingForPlayers'),
  }
}

class SignalRService {
  private lobby?: HubConnection
  private game?: HubConnection
  private starting = false

  private lobbyHandlersAttached = false
  private gameHandlersAttached = false

  private stateListeners = new Set<(s: ConnectionState) => void>()
  private hubsListeners = new Set<(s: { lobby: boolean; game: boolean }) => void>()
  private lobbyUpdListeners = new Set<(u: LobbyUpdate) => void>()
  private newTableListeners = new Set<(t: NewTableCreated) => void>()

  private lastGameStateHash = ''

  public onGameStateUpdate?: (state: GameState) => void
  public onChatMessage?: (message: ChatMessage) => void
  public onPlayerJoined?: (player: any) => void
  public onPlayerLeft?: (playerId: string) => void

  private emitState(s: ConnectionState) {
    this.stateListeners.forEach(fn => fn(s))
  }
  
  private emitHubs() {
    this.hubsListeners.forEach(fn =>
      fn({ lobby: this.isLobbyConnected, game: this.isGameConnected }),
    )
  }

  private buildConnection(url: string): HubConnection {
    const options: IHttpConnectionOptions = {
      accessTokenFactory: () => {
        const token = authService.getToken()
        console.log('🔑 [SignalR] AccessTokenFactory called')
        console.log('  - Token exists:', !!token)
        console.log('  - Token length:', token?.length || 0)
        
        if (!token) {
          console.warn('  - No token available!')
          return ''
        }
        
        // CRÍTICO: Limpiar el token si tiene "Bearer "
        let cleanToken = token
        if (cleanToken.startsWith('Bearer ')) {
          cleanToken = cleanToken.substring(7) // Remover "Bearer "
          console.log('  - Removed Bearer prefix, new length:', cleanToken.length)
        }
        
        console.log('  - Final token preview:', cleanToken.substring(0, 50) + '...')
        return cleanToken
      },
      transport: HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents | HttpTransportType.LongPolling,
      skipNegotiation: false
    }
    
    return new HubConnectionBuilder()
      .withUrl(url, options)
      .withAutomaticReconnect([0, 2000, 10000, 30000])
      .configureLogging(LogLevel.Information)
      .build()
  }

  private attachCommonHandlers(conn: HubConnection, which: 'lobby' | 'game') {
    conn.onreconnecting(() => {
      console.warn(`[SignalR:${which}] reconnecting...`)
      this.emitState('Reconnecting')
    })
    conn.onreconnected(() => {
      console.info(`[SignalR:${which}] reconnected`)
      this.emitState('Connected')
      this.emitHubs()
    })
    conn.onclose(error => {
      console.warn(`[SignalR:${which}] closed:`, error?.message || 'Sin error')
      this.emitState('Disconnected')
      this.emitHubs()
    })
  }

  private attachLobbyDomainHandlers() {
    if (!this.lobby || this.lobbyHandlersAttached) return

    // Limpiar handlers existentes
    this.lobby.off('ActiveRoomsUpdated')
    this.lobby.off('RoomCreated')
    this.lobby.off('JoinedLobby')

    this.lobby.on('JoinedLobby', () => {
      console.info('✅ [Lobby] JoinedLobby recibido')
    })
    
    // CORREGIDO: Usar nombres correctos del backend
    this.lobby.on('ActiveRoomsUpdated', (response: any) => {
      console.info('📊 [Lobby] ActiveRoomsUpdated:', response)
      if (response?.data && Array.isArray(response.data)) {
        const tables = response.data.map((room: any) => ({
          id: room.roomCode,
          name: room.name,
          playerCount: room.playerCount,
          maxPlayers: room.maxPlayers,
          minBet: 10, // valores por defecto
          maxBet: 1000,
          status: room.status
        }))
        tables.forEach((table: LobbyTable) => {
          this.lobbyUpdListeners.forEach(fn => fn({
            tableId: table.id,
            playerCount: table.playerCount,
            status: table.status
          }))
        })
      }
    })
    
    this.lobby.on('RoomCreated', (response: any) => {
      console.info('🆕 [Lobby] RoomCreated:', response)
      if (response?.data) {
        const table = {
          id: response.data.roomCode,
          name: response.data.name,
          playerCount: response.data.playerCount,
          maxPlayers: response.data.maxPlayers,
          minBet: 10,
          maxBet: 1000,
          status: response.data.status
        }
        this.newTableListeners.forEach(fn => fn({ table }))
      }
    })

    this.lobbyHandlersAttached = true
  }

  private attachGameDomainHandlers() {
    if (!this.game || this.gameHandlersAttached) return

    // Limpiar handlers existentes
    const gameEvents = [
      'RoomJoined', 'RoomLeft', 'RoomInfo', 'GameStateUpdated', 
      'PlayerJoined', 'PlayerLeft', 'RoundStarted', 'BetPlaced', 
      'PlayerActionPerformed', 'ReceiveMessage', 'Error', 'Success'
    ]
    
    gameEvents.forEach(event => this.game?.off(event))

    // Eventos principales del backend GameHub
    this.game.on('RoomJoined', (response: any) => {
      console.info('✅ [Game] RoomJoined:', response)
      if (response?.data) {
        // Convertir RoomInfo a GameState
        const gameState: GameState = {
          id: response.data.roomCode,
          status: response.data.status,
          players: response.data.players?.map((p: any) => ({
            id: p.playerId,
            displayName: p.name,
            position: p.position,
            isActive: p.hasPlayedTurn,
            balance: 5000, // valor por defecto
            currentBet: 0,
          })) || [],
          minBet: 10,
          maxBet: 1000
        }
        this.onGameStateUpdate?.(gameState)
      }
    })

    this.game.on('RoomInfo', (response: any) => {
      console.info('📊 [Game] RoomInfo:', response)
      if (response?.data) {
        const gameState: GameState = {
          id: response.data.roomCode,
          status: response.data.status,
          players: response.data.players?.map((p: any) => ({
            id: p.playerId,
            displayName: p.name,
            position: p.position,
            isActive: p.hasPlayedTurn,
            balance: 5000,
            currentBet: 0,
          })) || [],
          minBet: 10,
          maxBet: 1000
        }
        this.onGameStateUpdate?.(gameState)
      }
    })

    this.game.on('Error', (response: any) => {
      console.error('❌ [Game] Error:', response)
    })

    this.game.on('Success', (response: any) => {
      console.info('✅ [Game] Success:', response)
    })

    this.gameHandlersAttached = true
    console.info('✅ [SignalR] Game handlers attached successfully')
  }

  async startConnections(): Promise<boolean> {
    if (this.starting) return false
    if (!authService.getToken()) {
      this.emitState('Disconnected')
      console.warn('❌ [SignalR] No token; no se inicia')
      return false
    }

    this.starting = true
    try {
      // Crear conexiones si no existen
      if (!this.lobby) {
        this.lobby = this.buildConnection(hubUrl('/hubs/lobby'))
        this.attachCommonHandlers(this.lobby, 'lobby')
      }
      
      if (!this.game) {
        this.game = this.buildConnection(hubUrl('/hubs/game'))
        this.attachCommonHandlers(this.game, 'game')
      }

      // Iniciar conexiones si no están conectadas
      const tasks: Promise<void>[] = []
      
      if (this.lobby.state !== HubConnectionState.Connected) {
        tasks.push(this.lobby.start())
      }
      
      if (this.game.state !== HubConnectionState.Connected) {
        tasks.push(this.game.start())
      }

      if (tasks.length === 0) {
        this.emitState('Connected')
        this.emitHubs()
        return true
      }

      this.emitState('Connecting')
      await Promise.all(tasks)

      // Configurar handlers después de conectar
      this.attachLobbyDomainHandlers()
      this.attachGameDomainHandlers()

      await this.joinLobby()

      console.info('✅ [SignalR] conexiones iniciadas exitosamente')
      this.emitState('Connected')
      this.emitHubs()
      return true
    } catch (err) {
      console.error('❌ [SignalR] Error al iniciar conexiones:', err)
      this.emitState('Disconnected')
      return false
    } finally {
      this.starting = false
    }
  }

  async stopConnections() {
    console.log('🛑 Deteniendo conexiones SignalR...')

    const tasks: Promise<void>[] = []
    
    if (this.lobby?.state === HubConnectionState.Connected) {
      try {
        await this.lobby.invoke('LeaveLobby')
      } catch (error) {
        console.warn('[Lobby] Error al salir del lobby:', error)
      }
      tasks.push(this.lobby.stop())
    }
    
    if (this.game?.state === HubConnectionState.Connected) {
      tasks.push(this.game.stop())
    }
    
    if (tasks.length) {
      await Promise.all(tasks).catch(err => 
        console.warn('Error deteniendo conexiones:', err)
      )
    }

    // Reset completo del estado
    this.lobbyHandlersAttached = false
    this.gameHandlersAttached = false
    this.lobby = undefined
    this.game = undefined

    // Limpiar callbacks
    this.onGameStateUpdate = undefined
    this.onChatMessage = undefined
    this.onPlayerJoined = undefined
    this.onPlayerLeft = undefined

    this.emitState('Disconnected')
    this.emitHubs()
    
    console.log('✅ Conexiones SignalR detenidas completamente')
  }

  async joinLobby() {
    if (this.lobby?.state !== HubConnectionState.Connected) return
    try {
      await this.lobby.invoke('JoinLobby')
    } catch (error) {
      console.warn('Error joining lobby:', error)
    }
  }

  async leaveLobby() {
    if (this.lobby?.state !== HubConnectionState.Connected) return
    try {
      await this.lobby.invoke('LeaveLobby')
    } catch (error) {
      console.warn('Error leaving lobby:', error)
    }
  }

  // CORREGIDO: Usar /api/table en lugar de /api/game/tables
  private async fetchLobbySnapshotRest(): Promise<LobbyTable[]> {
    try {
      const data = await apiService.get<any[]>('/table')
      return (Array.isArray(data) ? data : []).map(mapBackendTableToLobby)
    } catch (error) {
      console.error('Error fetching tables:', error)
      return []
    }
  }

  async getLobbySnapshot(): Promise<LobbyTable[]> {
    if (!this.isLobbyConnected) {
      const ok = await this.startConnections()
      if (ok) await this.joinLobby()
    }
    return this.fetchLobbySnapshotRest()
  }

  // CORREGIDO: Usar /api/table y apiService
  async httpCreateTable(name: string): Promise<LobbyTable> {
    try {
      // CAMBIO: Crear GameRoom en lugar de Table
      const backendRoom = await apiService.post<any>('/gameroom', { name })
      return {
        id: backendRoom.roomCode,
        name: backendRoom.name,
        playerCount: backendRoom.playerCount || 0,
        maxPlayers: backendRoom.maxPlayers || 6,
        minBet: 10,
        maxBet: 1000,
        status: backendRoom.status || 'WaitingForPlayers'
      }
    } catch (error: any) {
      const errorMsg = error?.response?.data?.error || error?.message || 'Error creando mesa'
      throw new Error(errorMsg)
    }
  }

  async createTable(tableData: { name: string }) {
    const created = await this.httpCreateTable(tableData.name)
    this.newTableListeners.forEach(fn => fn({ table: created }))
    return created
  }

  // CORREGIDO: Usar métodos correctos del GameHub
  async joinTable(roomCode: string) {
    if (!this.isGameConnected) {
      throw new Error('Not connected to game hub')
    }
    try {
      const user = authService.getCurrentUser()
      console.log(`🎮 [SignalR] Invocando JoinRoom para roomCode: ${roomCode}`)
      
      // Usar JoinRoom con los parámetros correctos
      await this.game!.invoke('JoinRoom', {
        roomCode: roomCode,
        playerName: user?.displayName || 'Player'
      })
      console.log(`✅ [SignalR] JoinRoom exitoso`)
    } catch (error) {
      console.error('❌ Error joining room via SignalR:', error)
      throw error
    }
  }

  async leaveTable(roomCode: string) {
    if (!this.isGameConnected) return
    try {
      console.log(`🚪 [SignalR] Invocando LeaveRoom para roomCode: ${roomCode}`)
      await this.game!.invoke('LeaveRoom', roomCode)
      console.log(`✅ [SignalR] LeaveRoom exitoso`)
    } catch (error) {
      console.warn('❌ Error leaving room:', error)
    }
  }

  async getRoomInfo(roomCode: string) {
    if (!this.isGameConnected) {
      throw new Error('Not connected to game hub')
    }
    try {
      console.log(`📊 [SignalR] Obteniendo info de room: ${roomCode}`)
      await this.game!.invoke('GetRoomInfo', roomCode)
      console.log(`✅ [SignalR] GetRoomInfo exitoso`)
    } catch (error) {
      console.error('❌ Error getting room info:', error)
      throw error
    }
  }

  async startRound(roomCode: string) {
    if (!this.isGameConnected) {
      throw new Error('Not connected to game hub')
    }
    try {
      console.info(`🚀 [Game] StartGame invocado para roomCode: ${roomCode}`)
      await this.game!.invoke('StartGame', roomCode)
      console.info('✅ [Game] StartGame exitoso')
    } catch (error) {
      console.error('❌ Error starting game:', error)
      throw error
    }
  }

  async placeBet(roomCode: string, amount: number) {
    if (!this.isGameConnected) {
      throw new Error('Not connected to game hub')
    }
    try {
      console.info(`💰 [Game] PlaceBet ${amount} on room: ${roomCode}`)
      await this.game!.invoke('PlaceBet', {
        roomCode: roomCode,
        amount: amount
      })
      console.info('✅ [Game] PlaceBet enviado')
    } catch (error) {
      console.error('❌ Error placing bet via SignalR:', error)
      throw error
    }
  }

  async hit(roomCode: string) {
    if (!this.isGameConnected) {
      throw new Error('Not connected to game hub')
    }
    try {
      await this.game!.invoke('PlayerAction', {
        roomCode: roomCode,
        action: 'Hit'
      })
    } catch (error) {
      console.error('❌ Error invoking Hit:', error)
      throw error
    }
  }

  async stand(roomCode: string) {
    if (!this.isGameConnected) {
      throw new Error('Not connected to game hub')
    }
    try {
      await this.game!.invoke('PlayerAction', {
        roomCode: roomCode,
        action: 'Stand'
      })
    } catch (error) {
      console.error('❌ Error invoking Stand:', error)
      throw error
    }
  }

  async doubleDown(roomCode: string) {
    if (!this.isGameConnected) {
      throw new Error('Not connected to game hub')
    }
    try {
      await this.game!.invoke('PlayerAction', {
        roomCode: roomCode,
        action: 'Double'
      })
    } catch (error) {
      console.error('❌ Error invoking DoubleDown:', error)
      throw error
    }
  }

  async split(roomCode: string) {
    if (!this.isGameConnected) {
      throw new Error('Not connected to game hub')
    }
    try {
      await this.game!.invoke('PlayerAction', {
        roomCode: roomCode,
        action: 'Split'
      })
    } catch (error) {
      console.error('❌ Error invoking Split:', error)
      throw error
    }
  }

  // Métodos auxiliares mantenidos por compatibilidad
  async joinSeat(tableId: string, position: number) {
    console.log(`🎯 [SignalR] JoinSeat no implementado en GameRoom model. TableId: ${tableId}, Position: ${position}`)
    // En el modelo GameRoom, los jugadores se unen automáticamente sin posiciones específicas
  }

  async leaveSeat(tableId: string) {
    return this.leaveTable(tableId)
  }

  async resetTable(tableId: string) {
    console.log(`🔄 [SignalR] Reset no implementado. TableId: ${tableId}`)
  }

  async sendMessage(tableId: string, message: string) {
    console.log(`💬 [SignalR] SendMessage no implementado. TableId: ${tableId}, Message: ${message}`)
  }

  async sendChatMessage(tableId: string, message: string) {
    return this.sendMessage(tableId, message)
  }

  async forceRefreshGameState(tableId: string) {
    return this.getRoomInfo(tableId)
  }

  onConnectionStateChange(cb: (s: ConnectionState) => void) {
    this.stateListeners.add(cb)
    return () => this.stateListeners.delete(cb)
  }
  
  onHubsStatus(cb: (s: { lobby: boolean; game: boolean }) => void) {
    this.hubsListeners.add(cb)
    return () => this.hubsListeners.delete(cb)
  }
  
  onLobbyUpdate(cb: (u: LobbyUpdate) => void) {
    this.lobbyUpdListeners.add(cb)
    return () => this.lobbyUpdListeners.delete(cb)
  }
  
  onNewTable(cb: (t: NewTableCreated) => void) {
    this.newTableListeners.add(cb)
    return () => this.newTableListeners.delete(cb)
  }

  get isLobbyConnected() {
    return this.lobby?.state === HubConnectionState.Connected
  }
  
  get isGameConnected() {
    return this.game?.state === HubConnectionState.Connected
  }
  
  get connectionState(): ConnectionState {
    const st = this.lobby?.state || this.game?.state
    if (!st) return 'Disconnected'
    switch (st) {
      case HubConnectionState.Connected:
        return 'Connected'
      case HubConnectionState.Connecting:
        return 'Connecting'
      case HubConnectionState.Reconnecting:
        return 'Reconnecting'
      default:
        return 'Disconnected'
    }
  }
}

export const signalRService = new SignalRService()
export type { SignalRService }