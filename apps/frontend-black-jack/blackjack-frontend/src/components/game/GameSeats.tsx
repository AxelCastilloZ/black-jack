// src/components/game/GameSeats.tsx - CORREGIDO: Sin estado local de loading
import React, { useCallback } from 'react'
import { signalRService } from '../../services/signalr'

interface RoomPlayer {
  playerId: string
  name: string
  position: number
  isReady: boolean
  isHost: boolean
  hasPlayedTurn: boolean
}

interface GameSeatsProps {
  players: RoomPlayer[]
  roomCode?: string
  gameStatus?: 'WaitingForPlayers' | 'InProgress' | 'Finished'
  currentPlayerTurn?: string
  currentUser: any
  isViewer: boolean
  seatHubConnected: boolean
  isComponentMounted: boolean
  onError: (error: string) => void
  // CORREGIDO: Loading state controlado desde GamePage
  seatClickLoading: number | null
  setSeatClickLoading: (loading: number | null) => void
}

export default function GameSeats({
  players,
  roomCode,
  gameStatus,
  currentPlayerTurn,
  currentUser,
  isViewer,
  seatHubConnected,
  isComponentMounted,
  onError,
  seatClickLoading,
  setSeatClickLoading
}: GameSeatsProps) {
  const getPlayerAtPosition = useCallback((position: number) => {
    return players?.find(p => p.position === position)
  }, [players])

  const currentPlayer = players?.find(p => p.playerId === currentUser?.id)
  const isPlayerSeated = !!currentPlayer

  const handleJoinSeat = useCallback(async (position: number) => {
    if (!seatHubConnected || !roomCode || seatClickLoading !== null || !isComponentMounted) {
      console.log('[GameSeats] Cannot join seat - conditions not met')
      return
    }
    
    try {
      setSeatClickLoading(position)
      console.log(`[GameSeats] Joining seat ${position} via SeatHub`)
      
      await signalRService.joinSeat(roomCode, position)
      // CORREGIDO: No reseteamos aquí - se resetea en handleSeatJoined de GamePage
      
    } catch (error) {
      if (!isComponentMounted) return
      console.error('[GameSeats] Error joining seat:', error)
      onError(error instanceof Error ? error.message : 'Error uniéndose al asiento')
      // CORREGIDO: Solo reseteamos en caso de error
      setSeatClickLoading(null)
    }
  }, [seatHubConnected, roomCode, seatClickLoading, isComponentMounted, onError, setSeatClickLoading])

  const handleLeaveSeat = useCallback(async () => {
    if (!seatHubConnected || !roomCode || seatClickLoading !== null || !isComponentMounted) {
      console.log('[GameSeats] Cannot leave seat - conditions not met')
      return
    }
    
    try {
      setSeatClickLoading(-1)
      console.log('[GameSeats] Leaving seat via SeatHub')
      
      await signalRService.leaveSeat(roomCode)
      // CORREGIDO: No reseteamos aquí - se resetea en handleSeatLeft de GamePage
      
    } catch (error) {
      if (!isComponentMounted) return
      console.error('[GameSeats] Error leaving seat:', error)
      onError(error instanceof Error ? error.message : 'Error saliendo del asiento')
      // CORREGIDO: Solo reseteamos en caso de error
      setSeatClickLoading(null)
    }
  }, [seatHubConnected, roomCode, seatClickLoading, isComponentMounted, onError, setSeatClickLoading])

  // CORREGIDO: Eliminado useEffect problemático de auto-reset

  return (
    <>
      <PlayerPosition 
        position={0}
        player={getPlayerAtPosition(0)}
        currentUser={currentUser}
        isCurrentUserSeated={isPlayerSeated}
        gameStatus={gameStatus}
        currentPlayerTurn={currentPlayerTurn}
        onJoinSeat={handleJoinSeat}
        onLeaveSeat={handleLeaveSeat}
        seatClickLoading={seatClickLoading}
        isViewer={isViewer}
        seatHubConnected={seatHubConnected}
        className="absolute top-[120px] left-10"
      />

      <PlayerPosition 
        position={1}
        player={getPlayerAtPosition(1)}
        currentUser={currentUser}
        isCurrentUserSeated={isPlayerSeated}
        gameStatus={gameStatus}
        currentPlayerTurn={currentPlayerTurn}
        onJoinSeat={handleJoinSeat}
        onLeaveSeat={handleLeaveSeat}
        seatClickLoading={seatClickLoading}
        isViewer={isViewer}
        seatHubConnected={seatHubConnected}
        className="absolute bottom-[120px] left-10"
      />

      <PlayerPosition 
        position={2}
        player={getPlayerAtPosition(2)}
        currentUser={currentUser}
        isCurrentUserSeated={isPlayerSeated}
        gameStatus={gameStatus}
        currentPlayerTurn={currentPlayerTurn}
        onJoinSeat={handleJoinSeat}
        onLeaveSeat={handleLeaveSeat}
        seatClickLoading={seatClickLoading}
        isViewer={isViewer}
        seatHubConnected={seatHubConnected}
        className="absolute bottom-10 left-1/2 transform -translate-x-1/2"
        isMainPosition={true}
      />

      <PlayerPosition 
        position={3}
        player={getPlayerAtPosition(3)}
        currentUser={currentUser}
        isCurrentUserSeated={isPlayerSeated}
        gameStatus={gameStatus}
        currentPlayerTurn={currentPlayerTurn}
        onJoinSeat={handleJoinSeat}
        onLeaveSeat={handleLeaveSeat}
        seatClickLoading={seatClickLoading}
        isViewer={isViewer}
        seatHubConnected={seatHubConnected}
        className="absolute bottom-[120px] right-10"
      />

      <PlayerPosition 
        position={4}
        player={getPlayerAtPosition(4)}
        currentUser={currentUser}
        isCurrentUserSeated={isPlayerSeated}
        gameStatus={gameStatus}
        currentPlayerTurn={currentPlayerTurn}
        onJoinSeat={handleJoinSeat}
        onLeaveSeat={handleLeaveSeat}
        seatClickLoading={seatClickLoading}
        isViewer={isViewer}
        seatHubConnected={seatHubConnected}
        className="absolute top-[120px] right-10"
      />

      <PlayerPosition 
        position={5}
        player={getPlayerAtPosition(5)}
        currentUser={currentUser}
        isCurrentUserSeated={isPlayerSeated}
        gameStatus={gameStatus}
        currentPlayerTurn={currentPlayerTurn}
        onJoinSeat={handleJoinSeat}
        onLeaveSeat={handleLeaveSeat}
        seatClickLoading={seatClickLoading}
        isViewer={isViewer}
        seatHubConnected={seatHubConnected}
        className="absolute top-[120px] left-1/2 transform -translate-x-1/2"
      />
    </>
  )
}

// PlayerPosition component extraído del GamePage original
function PlayerPosition({ 
  position, 
  player, 
  currentUser, 
  isCurrentUserSeated,
  gameStatus,
  currentPlayerTurn,
  onJoinSeat,
  onLeaveSeat,
  seatClickLoading,
  isViewer,
  seatHubConnected,
  className, 
  isMainPosition = false 
}: {
  position: number
  player?: RoomPlayer
  currentUser: any
  isCurrentUserSeated: boolean
  gameStatus?: string
  currentPlayerTurn?: string
  onJoinSeat: (position: number) => Promise<void>
  onLeaveSeat: () => Promise<void>
  seatClickLoading: number | null
  isViewer: boolean
  seatHubConnected: boolean
  className: string
  isMainPosition?: boolean
}) {
  const isCurrentUser = player?.playerId === currentUser?.id
  const isEmpty = !player
  const isLoading = seatClickLoading === position || (isCurrentUser && seatClickLoading === -1)
  
  const canJoinSeat = isEmpty && !isLoading && !isViewer && seatHubConnected

  const handleSeatClick = useCallback(async () => {
    if (canJoinSeat && !isViewer) {
      await onJoinSeat(position)
    }
  }, [canJoinSeat, position, onJoinSeat, isViewer])

  const handleLeaveSeat = useCallback(async () => {
    if (isCurrentUser && !isLoading && gameStatus !== 'InProgress' && !isViewer && seatHubConnected) {
      await onLeaveSeat()
    }
  }, [isCurrentUser, isLoading, gameStatus, onLeaveSeat, isViewer, seatHubConnected])

  // Asiento vacío
  if (isEmpty) {
    return (
      <div className={className}>
        <div className="flex items-center mb-2">
          <div 
            className={`w-10 h-10 rounded-full border-2 border-dashed flex items-center justify-center mr-3 font-bold transition-all ${
              canJoinSeat 
                ? 'bg-gray-600 border-gray-400 text-gray-300 hover:bg-gray-500 hover:border-gray-300 cursor-pointer transform hover:scale-105' 
                : 'bg-gray-700 border-gray-500 text-gray-500 cursor-not-allowed'
            }`}
            onClick={handleSeatClick}
          >
            {isLoading ? (
              <div className="w-4 h-4 border border-gray-400 border-t-transparent rounded-full animate-spin"></div>
            ) : (
              position + 1
            )}
          </div>
          <div className="bg-gray-700/70 px-3 py-1 rounded text-gray-300">
            <div className="font-bold text-sm">
              {isLoading ? 'Uniéndose...' : 'Asiento libre'}
            </div>
            <div className="text-gray-400 text-xs">
              {isViewer ? 'Asiento vacío' :
               !seatHubConnected ? 'SeatHub desconectado' :
               canJoinSeat ? 'Clic para unirse' : 
               isLoading ? 'Procesando...' : 'No disponible'}
            </div>
          </div>
        </div>
      </div>
    )
  }

  // Jugador sentado
  return (
    <div className={className}>
      <div className="flex items-center mb-2">
        <div className={`w-10 h-10 rounded-full bg-white flex items-center justify-center mr-3 font-bold text-black border-2 relative transition-all ${
          isCurrentUser ? 'border-red-500 shadow-lg' : 'border-gray-300'
        }`}>
          {isLoading ? (
            <div className="w-4 h-4 border border-gray-600 border-t-transparent rounded-full animate-spin"></div>
          ) : (
            player.name.substring(0, 2).toUpperCase()
          )}
          
          {isCurrentUser && !isLoading && (
            <div className="absolute -top-1 -right-1 w-3 h-3 bg-red-500 rounded-full flex items-center justify-center">
              <div className="w-1.5 h-1.5 bg-red-600 rounded-full"></div>
            </div>
          )}
          {player.isHost && (
            <div className="absolute -top-2 -left-2 text-yellow-400 text-lg">👑</div>
          )}
        </div>
        
        <div className="bg-black/70 px-3 py-1 rounded text-white">
          <div className="font-bold text-sm">
            {isCurrentUser ? `${player.name} (TÚ)` : player.name}
          </div>
          <div className="text-emerald-400 text-xs">$1,000</div>
          {gameStatus === 'InProgress' && currentPlayerTurn === player.name && (
            <div className="text-yellow-400 text-xs animate-pulse">Su turno</div>
          )}
          {isLoading && (
            <div className="text-orange-400 text-xs">
              {seatClickLoading === -1 ? 'Saliendo...' : 'Procesando...'}
            </div>
          )}
        </div>
      </div>

      <div className="ml-[52px] space-y-1">
        {player.isReady && (
          <div className="px-2 py-1 bg-green-500 rounded text-xs font-bold text-white inline-block">
            ✓ Listo
          </div>
        )}
        
        {player.hasPlayedTurn && gameStatus === 'InProgress' && (
          <div className="text-xs text-white bg-blue-500 rounded px-2 py-1 inline-block">
            Turno jugado
          </div>
        )}

        {isCurrentUser && !isLoading && gameStatus !== 'InProgress' && !isViewer && seatHubConnected && (
          <div>
            <button
              onClick={handleLeaveSeat}
              className="text-xs bg-red-500/80 hover:bg-red-500 text-white px-2 py-1 rounded transition-colors"
            >
              Salir del asiento
            </button>
          </div>
        )}
      </div>
    </div>
  )
}