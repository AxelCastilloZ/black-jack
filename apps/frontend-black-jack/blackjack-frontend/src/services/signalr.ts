// src/services/signalr.ts
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  IHttpConnectionOptions,
  HttpTransportType,
} from '@microsoft/signalr'
import { authService } from './auth'

export type ConnectionState = 'Disconnected' | 'Connecting' | 'Connected' | 'Reconnecting'

export interface LobbyUpdate {
  tableId: string
  playerCount: number
  status: string
}

export interface NewTableCreated {
  table: {
    id: string
    name: string
    playerCount: number
    maxPlayers: number
    minBet: number
    maxBet: number
    status: string
  }
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
}

interface Card {
  suit: string
  rank: string
  value: number
  isHidden?: boolean
}

/** Base URL absoluta para el backend (sin slash final) */
function apiBase(): string {
  const raw = import.meta.env.VITE_API_BASE_URL as string | undefined
  if (!raw) return ''
  return raw.endsWith('/') ? raw.slice(0, -1) : raw
}

/** Construye URL absoluta del hub */
function hubUrl(path: '/hubs/lobby' | '/hubs/game'): string {
  const base = apiBase()
  return base ? `${base}${path}` : path
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
      accessTokenFactory: () => authService.getToken() || '',
      transport: HttpTransportType.WebSockets,
    }

    return new HubConnectionBuilder()
      .withUrl(url, options)
      .withAutomaticReconnect()
      .configureLogging(import.meta.env.DEV ? LogLevel.Information : LogLevel.Warning)
      .build()
  }

  private attachCommonHandlers(conn: HubConnection, which: 'lobby' | 'game') {
    conn.onreconnecting(() => {
      if (import.meta.env.DEV) console.warn(`[SignalR:${which}] reconnecting...`)
      this.emitState('Reconnecting')
    })
    conn.onreconnected(() => {
      if (import.meta.env.DEV) console.info(`[SignalR:${which}] reconnected`)
      this.emitState('Connected')
      this.emitHubs()
    })
    conn.onclose(() => {
      if (import.meta.env.DEV) console.warn(`[SignalR:${which}] closed`)
      this.emitState('Disconnected')
      this.emitHubs()
    })
  }

  private attachLobbyDomainHandlers() {
    if (!this.lobby || this.lobbyHandlersAttached) return

    this.lobby.off('TableUpdated')
    this.lobby.off('TableCreated')
    this.lobby.off('JoinedLobby')
    this.lobby.off('joinedlobby')

    this.lobby.on('JoinedLobby', () => {
      if (import.meta.env.DEV) console.info('[Lobby] JoinedLobby recibido')
    })
    this.lobby.on('joinedlobby', () => {
      if (import.meta.env.DEV) console.info('[Lobby] joinedlobby recibido')
    })
    this.lobby.on('TableUpdated', (u: LobbyUpdate) => {
      this.lobbyUpdListeners.forEach(fn => fn(u))
    })
    this.lobby.on('TableCreated', (t: NewTableCreated) => {
      this.newTableListeners.forEach(fn => fn(t))
    })

    this.lobbyHandlersAttached = true
  }

  private attachGameDomainHandlers() {
    if (!this.game || this.gameHandlersAttached) return

    this.game.off('GameStateUpdated')
    this.game.off('PlayerJoined')
    this.game.off('PlayerLeft')
    this.game.off('playerJoined')
    this.game.off('playerLeft')
    this.game.off('PlayerJoinedSeat')
    this.game.off('ReceiveMessage')

    this.game.on('PlayerJoined', (connectionId: string) => {
      if (import.meta.env.DEV) console.info(`[Game] PlayerJoined: ${connectionId}`)
      this.onPlayerJoined?.({ connectionId })
    })
    this.game.on('PlayerLeft', (connectionId: string) => {
      if (import.meta.env.DEV) console.info(`[Game] PlayerLeft: ${connectionId}`)
      this.onPlayerLeft?.(connectionId)
    })
    this.game.on('playerJoined', (connectionId: string) => {
      if (import.meta.env.DEV) console.info(`[Game] playerJoined: ${connectionId}`)
      this.onPlayerJoined?.({ connectionId })
    })
    this.game.on('playerLeft', (connectionId: string) => {
      if (import.meta.env.DEV) console.info(`[Game] playerLeft: ${connectionId}`)
      this.onPlayerLeft?.(connectionId)
    })
    this.game.on(
      'PlayerJoinedSeat',
      (connectionId: string, seatPosition: number, playerId: string) => {
        if (import.meta.env.DEV)
          console.info(`[Game] PlayerJoinedSeat:`, { connectionId, seatPosition, playerId })
        this.onPlayerJoined?.({ connectionId, seatPosition, playerId })
      },
    )
    this.game.on('GameStateUpdated', (state: GameState) => {
      if (import.meta.env.DEV) console.info('[Game] GameStateUpdated:', state)
      this.onGameStateUpdate?.(state)
    })
    this.game.on('ReceiveMessage', (connectionId: string, message: string) => {
      if (import.meta.env.DEV) console.info('[Game] ReceiveMessage:', { connectionId, message })
      const chatMessage: ChatMessage = {
        id: Date.now().toString(),
        playerName: 'Jugador',
        text: message,
        timestamp: new Date().toISOString(),
        tableId: '',
      }
      this.onChatMessage?.(chatMessage)
    })

    this.gameHandlersAttached = true
  }

  // ===== API pública =====
  async startConnections(): Promise<boolean> {
    if (this.starting) return false
    if (!authService.getToken()) {
      this.emitState('Disconnected')
      if (import.meta.env.DEV) console.warn('[SignalR] No token; no se inicia')
      return false
    }

    this.starting = true
    try {
      if (!this.lobby) {
        this.lobby = this.buildConnection(hubUrl('/hubs/lobby'))
        this.attachCommonHandlers(this.lobby, 'lobby')
        this.attachLobbyDomainHandlers()
      }
      if (!this.game) {
        this.game = this.buildConnection(hubUrl('/hubs/game'))
        this.attachCommonHandlers(this.game, 'game')
        this.attachGameDomainHandlers()
      }

      const tasks: Promise<void>[] = []
      if (this.lobby.state !== HubConnectionState.Connected) tasks.push(this.lobby.start())
      if (this.game.state !== HubConnectionState.Connected) tasks.push(this.game.start())

      if (tasks.length === 0) {
        this.emitState('Connected')
        this.emitHubs()
        return true
      }

      this.emitState('Connecting')
      await Promise.all(tasks)

      await this.joinLobby()

      if (import.meta.env.DEV) console.info('[SignalR] conexiones iniciadas')
      this.emitState('Connected')
      this.emitHubs()
      return true
    } catch (err) {
      console.error('[SignalR] Error al iniciar conexiones:', err)
      this.emitState('Disconnected')
      return false
    } finally {
      this.starting = false
    }
  }

  async stopConnections() {
    const tasks: Promise<void>[] = []
    if (this.lobby?.state === HubConnectionState.Connected) tasks.push(this.lobby.stop())
    if (this.game?.state === HubConnectionState.Connected) tasks.push(this.game.stop())
    if (tasks.length) await Promise.all(tasks)

    this.lobbyHandlersAttached = false
    this.gameHandlersAttached = false
    this.lobby = undefined
    this.game = undefined

    this.emitState('Disconnected')
    this.emitHubs()
  }

  // ===== Dominio Lobby/Game =====
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

  async createTable(tableData: any) {
    if (this.lobby?.state !== HubConnectionState.Connected) {
      throw new Error('Not connected to lobby hub')
    }
    await this.lobby.invoke('CreateTable', tableData)
  }

  async joinTable(tableId: string) {
    if (this.game?.state !== HubConnectionState.Connected) {
      throw new Error('Not connected to game hub')
    }
    try {
      await this.game.invoke('JoinTable', tableId)
    } catch (error) {
      console.error('Error joining table via SignalR:', error)
      throw error
    }
  }

  async leaveTable(tableId: string) {
    if (this.game?.state !== HubConnectionState.Connected) return
    try {
      await this.game.invoke('LeaveTable', tableId)
    } catch (error) {
      console.warn('Error leaving table:', error)
    }
  }

  // JoinSeat normalizado:
  // - fuerza index 1-based (varios backends lo esperan así)
  // - intenta (tableId, position, playerId) y si falla usa (tableId, position)
  async joinSeat(tableId: string, position: number) {
    if (this.game?.state !== HubConnectionState.Connected) {
      throw new Error('Not connected to game hub')
    }
    try {
      const seatNumber = position <= 0 ? position + 1 : position
      const user = authService.getCurrentUser()
      const playerId = user?.id

      if (import.meta.env.DEV)
        console.info(
          `[Game] JoinSeat invocado: ${tableId}, posición ${seatNumber}, playerId: ${playerId}`,
        )

      try {
        await this.game.invoke('JoinSeat', tableId, seatNumber, playerId)
      } catch (err) {
        if (import.meta.env.DEV)
          console.warn('[Game] JoinSeat(3 args) falló, probando (2 args)...', err)
        await this.game.invoke('JoinSeat', tableId, seatNumber)
      }

      if (import.meta.env.DEV) console.info('[Game] JoinSeat exitoso')
    } catch (error) {
      console.error('Error joining seat:', error)
      throw error
    }
  }

  async sendMessage(tableId: string, message: string) {
    if (this.game?.state !== HubConnectionState.Connected) {
      throw new Error('Not connected to game hub')
    }
    try {
      await this.game.invoke('SendMessage', tableId, message)
    } catch (error) {
      console.error('Error sending message:', error)
      throw error
    }
  }

  async sendChatMessage(tableId: string, message: string) {
    return this.sendMessage(tableId, message)
  }

  // Métodos aún no implementados en tu GameHub
  async placeBet(_tableId: string, _amount: number) {
    console.warn('[Game] PlaceBet no está implementado en el GameHub actual')
    throw new Error('La funcionalidad de apostar no está implementada aún.')
  }
  async hit(_tableId: string) {
    console.warn('[Game] Hit no está implementado en el GameHub actual')
    throw new Error('La funcionalidad de pedir carta no está implementada aún.')
  }
  async stand(_tableId: string) {
    console.warn('[Game] Stand no está implementado en el GameHub actual')
    throw new Error('La funcionalidad de plantarse no está implementada aún.')
  }
  async doubleDown(_tableId: string) {
    console.warn('[Game] DoubleDown no está implementado en el GameHub actual')
    throw new Error('La funcionalidad de doblar no está implementada aún.')
  }
  async split(_tableId: string) {
    console.warn('[Game] Split no está implementado en el GameHub actual')
    throw new Error('La funcionalidad de dividir no está implementada aún.')
  }

  // ===== Suscripciones =====
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

  // ===== Getters =====
  get isLobbyConnected() {
    return this.lobby?.state === HubConnectionState.Connected
  }
  get isGameConnected() {
    return this.game?.state === HubConnectionState.Connected
  }
  get connectionState(): ConnectionState {
    const st = this.lobby?.state
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
