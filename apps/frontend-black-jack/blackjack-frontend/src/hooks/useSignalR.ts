// src/hooks/useSignalR.ts
import { useCallback, useEffect, useState } from 'react'
import {
  signalRService,
  type ConnectionState,
  type LobbyUpdate,
  type NewTableCreated,
} from '../services/signalr'
import { authService } from '../services/auth'

export function useSignalR() {
  const [connectionState, setConnectionState] = useState<ConnectionState>('Disconnected')
  const [isConnecting, setIsConnecting] = useState(false)

  // Suscripciones con cleanup (evita acumulación)
  useEffect(() => {
    const off1 = signalRService.onConnectionStateChange(setConnectionState)
    const off2 = signalRService.onHubsStatus(() => {})
    const off3 = signalRService.onLobbyUpdate((_u: LobbyUpdate) => {})
    const off4 = signalRService.onNewTable((_t: NewTableCreated) => {})
    return () => {
      off1()
      off2()
      off3()
      off4()
    }
  }, [])

  // Autoconectar una vez si está autenticado
  useEffect(() => {
    if (authService.isAuthenticated() && connectionState === 'Disconnected' && !isConnecting) {
      void connect()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [connectionState])

  const connect = useCallback(async () => {
    if (isConnecting || connectionState === 'Connected') return
    setIsConnecting(true)
    try {
      await signalRService.startConnections()
    } catch (e) {
      console.error('Failed to connect to SignalR:', e)
    } finally {
      setIsConnecting(false)
    }
  }, [isConnecting, connectionState])

  const disconnect = useCallback(async () => {
    try {
      await signalRService.leaveLobby()
      await signalRService.stopConnections()
    } catch (e) {
      console.error('Error disconnecting from SignalR:', e)
    }
  }, [])

  // Proxies
  const joinTable = useCallback(async (tableId: string) => {
    await signalRService.joinTable(tableId)
  }, [])
  const createTable = useCallback(async (tableData: any) => {
    await signalRService.createTable(tableData)
  }, [])
  const joinSeat = useCallback(async (tableId: string, position: number) => {
    await signalRService.joinSeat(tableId, position)
  }, [])
  const placeBet = useCallback(async (tableId: string, amount: number) => {
    await signalRService.placeBet(tableId, amount)
  }, [])
  const hit = useCallback(async (tableId: string) => {
    await signalRService.hit(tableId)
  }, [])
  const stand = useCallback(async (tableId: string) => {
    await signalRService.stand(tableId)
  }, [])
  const doubleDown = useCallback(async (tableId: string) => {
    await signalRService.doubleDown(tableId)
  }, [])
  const split = useCallback(async (tableId: string) => {
    await signalRService.split(tableId)
  }, [])
  const sendChatMessage = useCallback(async (tableId: string, message: string) => {
    await signalRService.sendChatMessage(tableId, message)
  }, [])

  return {
    connectionState,
    isConnected: connectionState === 'Connected',
    isConnecting: isConnecting || connectionState === 'Connecting',
    isReconnecting: connectionState === 'Reconnecting',

    connect,
    disconnect,
    joinTable,
    createTable,

    // juego
    joinSeat,
    placeBet,
    hit,
    stand,
    doubleDown,
    split,
    sendChatMessage,

    isLobbyConnected: signalRService.isLobbyConnected,
    isGameConnected: signalRService.isGameConnected,
  }
}

export function useLobbySignalR() {
  const s = useSignalR()
  return {
    ...s,
    joinLobby: signalRService.joinLobby.bind(signalRService),
    leaveLobby: signalRService.leaveLobby.bind(signalRService),
  }
}
