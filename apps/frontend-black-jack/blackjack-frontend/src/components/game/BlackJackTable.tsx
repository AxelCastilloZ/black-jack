// src/components/game/BlackJackTable.tsx
import React, { useMemo, useState } from 'react'
import { signalRService, type GameState } from '../../services/signalr'
import { authService } from '../../services/auth'

type Props = {
  gameState: GameState
  onGameAction?: () => void
}

// Componente para carta elegante
function PlayingCard({ suit, rank, isHidden = false }: { suit: string; rank: string; isHidden?: boolean }) {
  if (isHidden) {
    return (
      <div className="w-12 h-16 bg-gradient-to-br from-blue-800 to-blue-900 border border-blue-600 rounded-lg flex items-center justify-center shadow-md">
        <div className="text-blue-300 text-lg">🂠</div>
      </div>
    )
  }

  const isRed = suit.toLowerCase() === 'hearts' || suit.toLowerCase() === 'diamonds'
  const suitSymbol = {
    hearts: '♥',
    diamonds: '♦', 
    clubs: '♣',
    spades: '♠'
  }[suit.toLowerCase()] || suit.charAt(0).toUpperCase()

  const displayRank = {
    'ace': 'A',
    'jack': 'J', 
    'queen': 'Q',
    'king': 'K'
  }[rank.toLowerCase()] || rank

  return (
    <div className={`w-12 h-16 bg-white border border-gray-300 rounded-lg flex flex-col items-center justify-center shadow-md font-bold ${isRed ? 'text-red-600' : 'text-black'}`}>
      <div className="text-xs font-bold">{displayRank}</div>
      <div className="text-sm leading-none">{suitSymbol}</div>
    </div>
  )
}

// Componente para asiento de jugador
function PlayerSeat({ 
  position, 
  player, 
  isMe, 
  isEmpty, 
  canJoin, 
  isActive,
  onJoin 
}: {
  position: number
  player?: any
  isMe: boolean
  isEmpty: boolean
  canJoin: boolean
  isActive: boolean
  onJoin: () => void
}) {
  if (isEmpty) {
    return (
      <div className="flex flex-col items-center">
        <div className="w-20 h-20 rounded-full border-2 border-dashed border-green-400/50 bg-green-800/30 flex items-center justify-center mb-2">
          {canJoin ? (
            <button
              onClick={onJoin}
              className="w-16 h-16 rounded-full bg-yellow-600 hover:bg-yellow-500 text-black font-bold text-xs shadow-lg transition-all transform hover:scale-105"
            >
              Sentarse
            </button>
          ) : (
            <span className="text-green-500 text-xs">Vacío</span>
          )}
        </div>
        <div className="text-xs text-green-300">Pos. {position}</div>
      </div>
    )
  }

  return (
    <div className="flex flex-col items-center relative">
      {/* Círculo del jugador */}
      <div className={`w-20 h-20 rounded-full flex items-center justify-center mb-2 border-2 ${
        isActive ? 'border-yellow-400 bg-yellow-900/40 shadow-lg shadow-yellow-400/30' : 
        isMe ? 'border-blue-400 bg-blue-900/40' : 
        'border-green-400 bg-green-800/40'
      }`}>
        <div className="w-14 h-14 rounded-full bg-white flex items-center justify-center">
          <span className="text-lg font-bold text-gray-800">
            {isMe ? 'TÚ' : player.displayName.slice(0, 2).toUpperCase()}
          </span>
        </div>
        
        {/* Indicador de turno activo */}
        {isActive && (
          <div className="absolute -top-1 -right-1 w-6 h-6 bg-yellow-400 rounded-full flex items-center justify-center animate-pulse">
            <div className="w-3 h-3 bg-yellow-600 rounded-full"></div>
          </div>
        )}
      </div>

      {/* Información del jugador */}
      <div className="text-center text-white">
        <div className={`font-bold text-sm ${isMe ? 'text-blue-300' : 'text-white'}`}>
          {isMe ? 'Tú' : player.displayName}
        </div>
        <div className="text-xs text-green-300">${player.balance?.toLocaleString()}</div>
        
        {/* Apuesta actual */}
        {player.currentBet > 0 && (
          <div className="mt-1 px-2 py-1 bg-yellow-600 rounded-full text-xs font-bold text-black">
            ${player.currentBet}
          </div>
        )}
      </div>

      {/* Cartas del jugador */}
      {player.hand?.cards && player.hand.cards.length > 0 && (
        <div className="mt-2 flex flex-col items-center">
          <div className="flex gap-1 mb-1">
            {player.hand.cards.map((card: any, idx: number) => (
              <PlayingCard key={idx} suit={card.suit} rank={card.rank} />
            ))}
          </div>
          <div className="text-center">
            <div className={`font-bold text-sm ${
              player.hand.hasBlackjack ? 'text-yellow-300' : 
              player.hand.isBusted ? 'text-red-400' : 
              'text-white'
            }`}>
              {player.hand.handValue}
            </div>
            {player.hand.isBusted && <div className="text-xs text-red-400 font-bold">BUST</div>}
            {player.hand.hasBlackjack && <div className="text-xs text-yellow-400 font-bold">21!</div>}
          </div>
        </div>
      )}
    </div>
  )
}

export default function BlackjackTable({ gameState, onGameAction }: Props) {
  const me = authService.getCurrentUser()
  const mySeat = useMemo(
    () => gameState.players?.find((p) => p.id === me?.id) ?? null,
    [gameState, me],
  )

  const seatedCount = gameState.players?.length ?? 0
  const isWaiting = gameState.status === 'WaitingForPlayers'
  const isInProgress = gameState.status === 'InProgress'
  const isBetting = gameState.status === 'Betting'
  
  // TEMPORAL: Reducido a 2 jugadores para facilitar pruebas
  const canStartRound = isWaiting && seatedCount >= 2
  const isMyTurn = Boolean(mySeat?.isActive && isInProgress)

  const [err, setErr] = useState<string | null>(null)
  const [busyStart, setBusyStart] = useState(false)
  const [joinBusy, setJoinBusy] = useState<number | null>(null)
  const [actionBusy, setActionBusy] = useState<string | null>(null)

  const handleAction = async (actionName: string, actionFn: () => Promise<void>) => {
    setErr(null)
    setActionBusy(actionName)
    try {
      await actionFn()
      onGameAction?.()
    } catch (e: any) {
      setErr(e?.message || `Error en ${actionName}.`)
    } finally {
      setActionBusy(null)
    }
  }

  async function handleStartRound() {
    setErr(null)
    setBusyStart(true)
    try {
      await signalRService.startRound(gameState.id)
      onGameAction?.()
    } catch (e: any) {
      setErr(e?.message || 'No se pudo iniciar la ronda.')
    } finally {
      setBusyStart(false)
    }
  }

  async function handleJoinSeat(pos: number) {
    setErr(null)
    setJoinBusy(pos)
    try {
      await signalRService.joinSeat(gameState.id, pos)
      onGameAction?.()
    } catch (e: any) {
      setErr(e?.message || 'No se pudo unirse al asiento.')
    } finally {
      setJoinBusy(null)
    }
  }

  // Crear array de asientos
  const seats = Array.from({ length: 6 }, (_, index) => {
    const position = index + 1
    const player = gameState.players?.find(p => p.position === position)
    const isMyPosition = mySeat?.position === position
    const isEmpty = !player
    const canJoin = isEmpty && !mySeat && seatedCount < 6

    return {
      position,
      player,
      isMyPosition,
      isEmpty,
      canJoin,
      isActive: player?.isActive || false
    }
  })

  return (
    <div className="relative w-full h-[600px] bg-gradient-to-br from-green-700 via-green-800 to-green-900 rounded-xl border-4 border-yellow-600 shadow-2xl overflow-hidden">
      
      {/* Patrón de fondo tipo mesa de casino */}
      <div className="absolute inset-0 opacity-20">
        <div className="absolute top-1/2 left-1/2 transform -translate-x-1/2 -translate-y-1/2 w-64 h-32 border-4 border-yellow-600 rounded-full"></div>
        <div className="absolute top-1/2 left-1/2 transform -translate-x-1/2 -translate-y-1/2 w-48 h-24 border-2 border-yellow-500 rounded-full"></div>
      </div>

      {/* Información de la mesa - esquina superior izquierda */}
      <div className="absolute top-4 left-4 bg-black/60 backdrop-blur-sm rounded-lg p-3 text-white text-sm">
        <div className="flex items-center gap-2 mb-1">
          <span className="text-yellow-400 font-bold">♠ BLACKJACK ♣</span>
        </div>
        <div>Límites: ${gameState.minBet} - ${gameState.maxBet}</div>
      </div>

      {/* DEALER AREA - Parte superior */}
      <div className="absolute top-16 left-1/2 transform -translate-x-1/2">
        <div className="flex flex-col items-center">
          {/* Círculo del dealer */}
          <div className="w-24 h-24 rounded-full bg-gradient-to-br from-red-700 to-red-800 border-4 border-yellow-500 flex items-center justify-center shadow-xl mb-2">
            <span className="text-2xl font-bold text-white">D</span>
          </div>
          <div className="text-yellow-400 font-bold text-sm mb-2">DEALER</div>
          
          {/* Cartas del dealer */}
          {gameState.dealer?.hand && gameState.dealer.hand.length > 0 && (
            <div className="flex flex-col items-center">
              <div className="flex gap-1 mb-2">
                {gameState.dealer.hand.map((card: any, idx: number) => (
                  <PlayingCard 
                    key={idx} 
                    suit={card.suit} 
                    rank={card.rank} 
                    isHidden={card.isHidden}
                  />
                ))}
              </div>
              {gameState.dealer.handValue !== undefined && !gameState.dealer.hand.some((c: any) => c.isHidden) && (
                <div className="text-center">
                  <div className={`font-bold ${gameState.dealer.isBusted ? 'text-red-300' : 'text-white'}`}>
                    {gameState.dealer.handValue}
                  </div>
                  {gameState.dealer.isBusted && <div className="text-red-300 text-xs font-bold">BUST!</div>}
                  {gameState.dealer.hasBlackjack && <div className="text-yellow-400 text-xs font-bold">BLACKJACK!</div>}
                </div>
              )}
            </div>
          )}
        </div>
      </div>

      {/* CENTRO - Banner de estado y acciones */}
      <div className="absolute top-1/2 left-1/2 transform -translate-x-1/2 -translate-y-1/2 z-10">
        {/* Banner de espera */}
        {isWaiting && (
          <div className="bg-yellow-600/95 backdrop-blur-sm border-2 border-yellow-400 rounded-xl p-4 text-center shadow-xl">
            <div className="text-black font-bold text-lg mb-2">Esperando Jugadores</div>
            <div className="text-black mb-3">{seatedCount}/2 jugadores listos</div>
            {canStartRound ? (
              <button
                onClick={handleStartRound}
                disabled={busyStart}
                className="px-6 py-3 rounded-lg bg-green-700 hover:bg-green-800 text-white font-bold shadow-lg transform hover:scale-105 transition-all disabled:opacity-50"
              >
                {busyStart ? 'Iniciando...' : '🎲 INICIAR PARTIDA'}
              </button>
            ) : (
              <div className="text-black text-sm">
                Se necesitan mínimo 2 jugadores
              </div>
            )}
          </div>
        )}

        {/* Acciones de juego */}
        {isMyTurn && (
          <div className="bg-black/80 backdrop-blur-sm border-2 border-yellow-400 rounded-xl p-4 text-center shadow-xl">
            <div className="text-yellow-400 font-bold text-lg mb-3">¡ES TU TURNO!</div>
            <div className="flex gap-3">
              <button
                onClick={() => handleAction('Hit', () => signalRService.hit(gameState.id))}
                disabled={actionBusy === 'Hit'}
                className="px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-700 text-white font-bold shadow-md transition-all disabled:opacity-50"
              >
                {actionBusy === 'Hit' ? 'Pidiendo...' : 'PEDIR'}
              </button>
              <button
                onClick={() => handleAction('Stand', () => signalRService.stand(gameState.id))}
                disabled={actionBusy === 'Stand'}
                className="px-4 py-2 rounded-lg bg-red-600 hover:bg-red-700 text-white font-bold shadow-md transition-all disabled:opacity-50"
              >
                {actionBusy === 'Stand' ? 'Plantándose...' : 'PLANTARSE'}
              </button>
              <button
                onClick={() => handleAction('Double', () => signalRService.doubleDown(gameState.id))}
                disabled={actionBusy === 'Double'}
                className="px-4 py-2 rounded-lg bg-purple-600 hover:bg-purple-700 text-white font-bold shadow-md transition-all disabled:opacity-50"
              >
                {actionBusy === 'Double' ? 'Doblando...' : 'DOBLAR'}
              </button>
            </div>
          </div>
        )}
      </div>

      {/* JUGADORES - Distribuidos en arco en la parte inferior */}
      <div className="absolute bottom-8 left-1/2 transform -translate-x-1/2">
        <div className="flex justify-center items-end gap-6" style={{ width: '700px' }}>
          {seats.map((seat) => (
            <PlayerSeat
              key={seat.position}
              position={seat.position}
              player={seat.player}
              isMe={seat.isMyPosition}
              isEmpty={seat.isEmpty}
              canJoin={seat.canJoin}
              isActive={seat.isActive}
              onJoin={() => handleJoinSeat(seat.position)}
            />
          ))}
        </div>
      </div>

      {/* Errores */}
      {err && (
        <div className="absolute top-4 right-4 bg-red-600/90 backdrop-blur-sm border border-red-400 rounded-lg p-3 text-white text-sm shadow-lg max-w-xs">
          ⚠️ {err}
        </div>
      )}

      {/* Botón de reset (solo si hay problemas) */}
      {(!mySeat && seatedCount >= 6) && (
        <div className="absolute bottom-4 right-4">
          <button
            onClick={async () => {
              try {
                await signalRService.resetTable(gameState.id)
                onGameAction?.()
              } catch (e: any) {
                setErr(e?.message || 'Error al resetear')
              }
            }}
            className="px-3 py-2 rounded-lg bg-orange-600 hover:bg-orange-700 text-white font-bold text-sm shadow-md"
          >
            Reset Mesa
          </button>
        </div>
      )}
    </div>
  )
}