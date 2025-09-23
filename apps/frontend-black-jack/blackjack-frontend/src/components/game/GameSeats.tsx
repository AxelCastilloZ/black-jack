//GameSeats.tsx - ALINEADO CON BACKEND REFACTORIZADO

import React, { useCallback } from 'react'

// Interfaces combinadas - ALINEADAS CON BACKEND
interface RoomPlayer {
  playerId: string
  name: string
  position: number
  isReady: boolean
  isHost: boolean
  hasPlayedTurn: boolean
  // Auto-betting stats integrados
  currentBalance?: number
  totalBetThisSession?: number
  canAffordBet?: boolean
}

// Interfaces para cartas
interface Hand {
  id: string
  cards: Array<{
    suit: string
    rank: string
  }>
  value: number
  status: string
}

interface PlayerWithHand extends RoomPlayer {
  hand?: Hand | null
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
  seatClickLoading: number | null
  setSeatClickLoading: (loading: number | null) => void
  // Props para auto-betting
  autoBettingActive?: boolean
  minBetPerRound?: number
  // Props para cartas
  playersWithHands?: PlayerWithHand[]
  // NUEVOS: Handlers exactos desde GamePage
  onJoinSeat?: (position: number) => Promise<void>
  onLeaveSeat?: () => Promise<void>
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
  setSeatClickLoading,
  autoBettingActive = false,
  minBetPerRound = 0,
  playersWithHands = [],
  // RECIBIR handlers desde GamePage
  onJoinSeat,
  onLeaveSeat
}: GameSeatsProps) {
  const getPlayerAtPosition = useCallback((position: number) => {
    return players?.find(p => p.position === position)
  }, [players])

  const getPlayerWithHandAtPosition = useCallback((position: number) => {
    const player = playersWithHands?.find(p => p.position === position)
    return player
  }, [playersWithHands])

  const currentPlayer = players?.find(p => p.playerId === currentUser?.id)
  const isPlayerSeated = !!currentPlayer

  // REMOVIDO: handleJoinSeat y handleLeaveSeat locales
  // Ahora usamos los handlers que vienen desde GamePage (onJoinSeat, onLeaveSeat)

  // Layout semicircular
  return (
    <div className="absolute inset-0 pointer-events-none">
      {/* Container principal */}
      <div className="relative w-full h-full flex items-center justify-center">
        
        {/* Asientos superiores - Fila de arriba */}
        <div className="absolute top-[15%] w-full max-w-[900px] flex justify-between items-center px-8">
          {/* Position 0 - Superior izquierda */}
          <div className="pointer-events-auto">
            <PlayerPosition 
              position={0}
              player={getPlayerAtPosition(0)}
              playerWithHand={getPlayerWithHandAtPosition(0)}
              currentUser={currentUser}
              isCurrentUserSeated={isPlayerSeated}
              gameStatus={gameStatus}
              currentPlayerTurn={currentPlayerTurn}
              onJoinSeat={onJoinSeat} // USAR handler desde GamePage
              onLeaveSeat={onLeaveSeat} // USAR handler desde GamePage
              seatClickLoading={seatClickLoading}
              isViewer={isViewer}
              seatHubConnected={seatHubConnected}
              autoBettingActive={autoBettingActive}
              minBetPerRound={minBetPerRound}
            />
          </div>

          {/* Position 1 - Superior derecha */}
          <div className="pointer-events-auto">
            <PlayerPosition 
              position={1}
              player={getPlayerAtPosition(1)}
              playerWithHand={getPlayerWithHandAtPosition(1)}
              currentUser={currentUser}
              isCurrentUserSeated={isPlayerSeated}
              gameStatus={gameStatus}
              currentPlayerTurn={currentPlayerTurn}
              onJoinSeat={onJoinSeat}
              onLeaveSeat={onLeaveSeat}
              seatClickLoading={seatClickLoading}
              isViewer={isViewer}
              seatHubConnected={seatHubConnected}
              autoBettingActive={autoBettingActive}
              minBetPerRound={minBetPerRound}
            />
          </div>
        </div>

        {/* Asientos laterales - Fila media */}
        <div className="absolute top-1/2 -translate-y-1/2 w-full max-w-[1100px] flex justify-between items-center px-4">
          {/* Position 5 - Lateral izquierda */}
          <div className="pointer-events-auto">
            <PlayerPosition 
              position={5}
              player={getPlayerAtPosition(5)}
              playerWithHand={getPlayerWithHandAtPosition(5)}
              currentUser={currentUser}
              isCurrentUserSeated={isPlayerSeated}
              gameStatus={gameStatus}
              currentPlayerTurn={currentPlayerTurn}
              onJoinSeat={onJoinSeat}
              onLeaveSeat={onLeaveSeat}
              seatClickLoading={seatClickLoading}
              isViewer={isViewer}
              seatHubConnected={seatHubConnected}
              autoBettingActive={autoBettingActive}
              minBetPerRound={minBetPerRound}
            />
          </div>

          {/* Position 2 - Lateral derecha */}
          <div className="pointer-events-auto">
            <PlayerPosition 
              position={2}
              player={getPlayerAtPosition(2)}
              playerWithHand={getPlayerWithHandAtPosition(2)}
              currentUser={currentUser}
              isCurrentUserSeated={isPlayerSeated}
              gameStatus={gameStatus}
              currentPlayerTurn={currentPlayerTurn}
              onJoinSeat={onJoinSeat}
              onLeaveSeat={onLeaveSeat}
              seatClickLoading={seatClickLoading}
              isViewer={isViewer}
              seatHubConnected={seatHubConnected}
              autoBettingActive={autoBettingActive}
              minBetPerRound={minBetPerRound}
            />
          </div>
        </div>

        {/* Asientos inferiores - Fila de abajo */}
        <div className="absolute bottom-[8%] w-full max-w-[900px] flex justify-between items-center px-8">
          {/* Position 4 - Inferior izquierda */}
          <div className="pointer-events-auto">
            <PlayerPosition 
              position={4}
              player={getPlayerAtPosition(4)}
              playerWithHand={getPlayerWithHandAtPosition(4)}
              currentUser={currentUser}
              isCurrentUserSeated={isPlayerSeated}
              gameStatus={gameStatus}
              currentPlayerTurn={currentPlayerTurn}
              onJoinSeat={onJoinSeat}
              onLeaveSeat={onLeaveSeat}
              seatClickLoading={seatClickLoading}
              isViewer={isViewer}
              seatHubConnected={seatHubConnected}
              autoBettingActive={autoBettingActive}
              minBetPerRound={minBetPerRound}
            />
          </div>

          {/* Position 3 - Inferior derecha (asiento principal) */}
          <div className="pointer-events-auto">
            <PlayerPosition 
              position={3}
              player={getPlayerAtPosition(3)}
              playerWithHand={getPlayerWithHandAtPosition(3)}
              currentUser={currentUser}
              isCurrentUserSeated={isPlayerSeated}
              gameStatus={gameStatus}
              currentPlayerTurn={currentPlayerTurn}
              onJoinSeat={onJoinSeat}
              onLeaveSeat={onLeaveSeat}
              seatClickLoading={seatClickLoading}
              isViewer={isViewer}
              seatHubConnected={seatHubConnected}
              autoBettingActive={autoBettingActive}
              minBetPerRound={minBetPerRound}
              isMainPosition={true}
            />
          </div>
        </div>
      </div>
    </div>
  )
}

// PlayerPosition component - ACTUALIZADO para usar handlers desde GamePage
function PlayerPosition({ 
  position, 
  player, 
  playerWithHand,
  currentUser, 
  isCurrentUserSeated,
  gameStatus,
  currentPlayerTurn,
  onJoinSeat, // RECIBIR desde GamePage
  onLeaveSeat, // RECIBIR desde GamePage
  seatClickLoading,
  isViewer,
  seatHubConnected,
  autoBettingActive = false,
  minBetPerRound = 0,
  isMainPosition = false 
}: {
  position: number
  player?: RoomPlayer
  playerWithHand?: PlayerWithHand
  currentUser: any
  isCurrentUserSeated: boolean
  gameStatus?: string
  currentPlayerTurn?: string
  onJoinSeat?: (position: number) => Promise<void> // OPCIONAL: puede venir de GamePage
  onLeaveSeat?: () => Promise<void> // OPCIONAL: puede venir de GamePage
  seatClickLoading: number | null
  isViewer: boolean
  seatHubConnected: boolean
  autoBettingActive?: boolean
  minBetPerRound?: number
  isMainPosition?: boolean
}) {
  const isCurrentUser = player?.playerId === currentUser?.id
  const isEmpty = !player
  const isLoading = seatClickLoading === position || (isCurrentUser && seatClickLoading === -1)
  
  const canJoinSeat = isEmpty && !isLoading && !isViewer && seatHubConnected && onJoinSeat

  // Variables para auto-betting
  const currentBalance = player?.currentBalance || 0
  const totalBetThisSession = player?.totalBetThisSession || 0
  const canAffordNextBet = !minBetPerRound || currentBalance >= minBetPerRound
  const estimatedRounds = minBetPerRound > 0 ? Math.floor(currentBalance / minBetPerRound) : 0

  const shouldShowStats = !isEmpty && minBetPerRound > 0

  const handleSeatClick = useCallback(async () => {
    if (canJoinSeat && !isViewer && onJoinSeat) {
      await onJoinSeat(position)
    }
  }, [canJoinSeat, position, onJoinSeat, isViewer])

  const handleLeaveSeatClick = useCallback(async () => {
    if (isCurrentUser && !isLoading && gameStatus !== 'InProgress' && !isViewer && seatHubConnected && onLeaveSeat) {
      await onLeaveSeat()
    }
  }, [isCurrentUser, isLoading, gameStatus, onLeaveSeat, isViewer, seatHubConnected])

  // Asiento vacío
  if (isEmpty) {
    return (
      <div className="flex flex-col items-center">
        <div className="flex items-center mb-2">
          <div 
            className={`w-12 h-12 rounded-full border-2 border-dashed flex items-center justify-center mr-3 font-bold transition-all duration-200 ${
              canJoinSeat 
                ? 'bg-gray-600/80 border-gray-400 text-gray-300 hover:bg-gray-500 hover:border-gray-300 cursor-pointer transform hover:scale-110 hover:shadow-lg backdrop-blur-sm' 
                : 'bg-gray-700/60 border-gray-500 text-gray-500 cursor-not-allowed backdrop-blur-sm'
            } ${isMainPosition ? 'w-14 h-14 text-lg' : ''}`}
            onClick={handleSeatClick}
          >
            {isLoading ? (
              <div className="w-4 h-4 border border-gray-400 border-t-transparent rounded-full animate-spin"></div>
            ) : (
              position + 1
            )}
          </div>
          
          <div className="bg-gray-800/90 backdrop-blur-sm px-3 py-2 rounded-lg border border-gray-600 text-gray-300">
            <div className="font-bold text-sm">
              {isLoading ? 'Uniéndose...' : 'Asiento libre'}
            </div>
            <div className="text-gray-400 text-xs">
              {isViewer ? 'Asiento vacío' :
               !seatHubConnected ? 'GameRoomHub desconectado' :
               !onJoinSeat ? 'Handler no disponible' :
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
    <div className="flex flex-col items-center">
      <div className="flex items-center mb-2">
        <div className={`rounded-full bg-white flex items-center justify-center mr-3 font-bold text-black border-2 relative transition-all duration-200 ${
          isCurrentUser ? 'border-red-500 shadow-lg shadow-red-500/25' : 'border-gray-300'
        } ${isMainPosition ? 'w-14 h-14 text-lg' : 'w-12 h-12'}`}>
          {isLoading ? (
            <div className="w-4 h-4 border border-gray-600 border-t-transparent rounded-full animate-spin"></div>
          ) : (
            player.name.substring(0, 2).toUpperCase()
          )}
          
          {isCurrentUser && !isLoading && (
            <div className="absolute -top-1 -right-1 w-3 h-3 bg-red-500 rounded-full flex items-center justify-center">
              <div className="w-1.5 h-1.5 bg-red-600 rounded-full animate-pulse"></div>
            </div>
          )}
          {player.isHost && (
            <div className="absolute -top-2 -left-2 text-yellow-400 text-lg drop-shadow-lg">👑</div>
          )}
        </div>
        
        <div className="bg-black/90 backdrop-blur-sm px-4 py-2 rounded-lg border border-gray-700 text-white min-w-[180px]">
          <div className={`font-bold ${isMainPosition ? 'text-base' : 'text-sm'} mb-2`}>
            {isCurrentUser ? `${player.name} (Tú)` : player.name}
          </div>
          
          {/* Stats de auto-betting */}
          {shouldShowStats ? (
            <div className="space-y-1">
              {isCurrentUser ? (
                <>
                  <div className="flex justify-between text-xs bg-slate-800/50 px-2 py-1 rounded">
                    <span className="text-slate-300">Balance actual:</span>
                    <span className={`font-semibold ${canAffordNextBet ? 'text-emerald-400' : 'text-red-400'}`}>
                      ${currentBalance.toLocaleString()}
                    </span>
                  </div>
                  
                  <div className="flex justify-between text-xs bg-slate-800/50 px-2 py-1 rounded">
                    <span className="text-slate-300">Costo por ronda:</span>
                    <span className="text-purple-300 font-semibold">
                      ${minBetPerRound.toLocaleString()}
                    </span>
                  </div>
                  
                  <div className="flex justify-between text-xs bg-slate-800/50 px-2 py-1 rounded">
                    <span className="text-slate-300">Total apostado:</span>
                    <span className="text-blue-300 font-semibold">
                      ${totalBetThisSession.toLocaleString()}
                    </span>
                  </div>
                  
                  <div className="flex justify-between text-xs bg-slate-800/50 px-2 py-1 rounded">
                    <span className="text-slate-300">Rondas restantes:</span>
                    <span className={`font-semibold ${estimatedRounds > 3 ? 'text-green-400' : estimatedRounds > 0 ? 'text-yellow-400' : 'text-red-400'}`}>
                      ~{estimatedRounds}
                    </span>
                  </div>
                </>
              ) : (
                <>
                  <div className="flex justify-between text-xs">
                    <span className="text-slate-400">Balance:</span>
                    <span className="text-emerald-400 font-semibold">${currentBalance.toLocaleString()}</span>
                  </div>
                  <div className="flex justify-between text-xs">
                    <span className="text-slate-400">Por ronda:</span>
                    <span className="text-purple-300">${minBetPerRound.toLocaleString()}</span>
                  </div>
                  <div className="flex justify-between text-xs">
                    <span className="text-slate-400">Apostado:</span>
                    <span className="text-blue-300">${totalBetThisSession.toLocaleString()}</span>
                  </div>
                </>
              )}
            </div>
          ) : (
            <div className="text-emerald-400 text-sm font-semibold">
              {currentBalance > 0 ? `$${currentBalance.toLocaleString()}` : 'Sin balance'}
            </div>
          )}
          
          {gameStatus === 'InProgress' && currentPlayerTurn === player.name && (
            <div className="text-yellow-400 text-xs animate-pulse font-medium mt-2 text-center bg-yellow-900/20 rounded px-2 py-1">
              Su turno ⏱️
            </div>
          )}
          {isLoading && (
            <div className="text-orange-400 text-xs mt-2 text-center bg-orange-900/20 rounded px-2 py-1">
              {seatClickLoading === -1 ? 'Saliendo...' : 'Procesando...'}
            </div>
          )}
        </div>
      </div>

      {/* Player Hand Display */}
      {playerWithHand?.hand && gameStatus === 'InProgress' && (
        <div className="mt-2 mb-2">
          <div className="flex items-center justify-center space-x-1 bg-black/60 backdrop-blur-sm p-2 rounded-lg border border-gray-600">
            {playerWithHand.hand.cards.map((card, index) => (
              <div key={index} className="w-8 h-12 bg-white border border-gray-300 rounded text-black text-xs flex flex-col items-center justify-center shadow-sm">
                <div className="font-bold">{card.rank}</div>
                <div className="text-xs">
                  {card.suit === 'Hearts' ? '♥' : 
                   card.suit === 'Diamonds' ? '♦' : 
                   card.suit === 'Clubs' ? '♣' : '♠'}
                </div>
              </div>
            ))}
            <div className="ml-2 text-white text-sm font-bold bg-blue-600/80 px-2 py-1 rounded backdrop-blur-sm">
              {playerWithHand.hand.value}
            </div>
          </div>
        </div>
      )}

      {/* Estados y acciones */}
      <div className="flex flex-col items-center space-y-1">
        <div className="flex gap-2">
          {player.isReady && (
            <div className="px-2 py-1 bg-green-500/90 backdrop-blur-sm rounded text-xs font-bold text-white inline-flex items-center gap-1 border border-green-400">
              ✓ Listo
            </div>
          )}
          
          {player.hasPlayedTurn && gameStatus === 'InProgress' && (
            <div className="text-xs text-white bg-blue-500/90 backdrop-blur-sm rounded px-2 py-1 inline-flex items-center border border-blue-400">
              Turno jugado
            </div>
          )}
        </div>

        {isCurrentUser && !isLoading && gameStatus !== 'InProgress' && !isViewer && seatHubConnected && onLeaveSeat && (
          <button
            onClick={handleLeaveSeatClick}
            className="text-xs bg-orange-500/90 hover:bg-orange-600 backdrop-blur-sm text-white px-3 py-1 rounded-full transition-all duration-200 border border-orange-400 hover:shadow-lg hover:shadow-orange-500/25"
          >
            Salir del asiento
          </button>
        )}
      </div>
    </div>
  )
}