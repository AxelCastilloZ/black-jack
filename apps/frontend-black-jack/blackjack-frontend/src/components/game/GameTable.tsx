// src/components/game/GameTable.tsx - CORREGIDO
import React from 'react'
import HandDisplay from './HandDisplay'
import GameActions from './GameActions'

interface Card {
  suit: 'Hearts' | 'Diamonds' | 'Clubs' | 'Spades'
  rank: 'Ace' | '2' | '3' | '4' | '5' | '6' | '7' | '8' | '9' | '10' | 'Jack' | 'Queen' | 'King'
  value: number
}

interface Hand {
  id: string
  cards: Card[]
  value: number
  status: 'Active' | 'Stand' | 'Bust' | 'Blackjack'
}

interface GameTableProps {
  gameStatus?: string
  canStart: boolean
  isPlayerSeated: boolean
  isViewer: boolean
  isCurrentPlayerHost: boolean
  gameControlConnected: boolean
  isStarting?: boolean
  onStartRound: () => Promise<void>
  // New game state props
  dealerHand?: Hand | null
  playerHand?: Hand | null
  isPlayerTurn?: boolean
  onHit?: () => void
  onStand?: () => void
}

export default function GameTable({
  gameStatus,
  canStart,
  isPlayerSeated,
  isViewer,
  isCurrentPlayerHost,
  gameControlConnected,
  isStarting = false,
  onStartRound,
  dealerHand,
  playerHand,
  isPlayerTurn = false,
  onHit,
  onStand
}: GameTableProps) {
  // CORREGIDO: El botón debe ocultarse cuando el juego está en progreso
  const showStartButton = !isViewer &&
                          canStart &&
                          gameStatus !== 'InProgress' &&  // ← Esta condición está bien
                          isCurrentPlayerHost &&
                          gameControlConnected

  // DEBUGGING: Agregar logs para diagnosticar
  console.log('[GameTable] Render state:', {
    gameStatus,
    canStart,
    isPlayerSeated,
    showStartButton,
    hasPlayerHand: !!playerHand,
    isPlayerTurn,
    playerHandStatus: playerHand?.status
  })

  return (
    <>
      {/* Dealer Hand */}
      <div className="absolute top-20 left-1/2 transform -translate-x-1/2">
        <HandDisplay
          hand={dealerHand ?? null}
          isDealer={true}
          showAllCards={gameStatus !== 'InProgress'}  // CORREGIDO: Solo mostrar todas las cartas del dealer cuando el juego no esté en progreso
        />
      </div>

      {/* Player Hand - SOLO mostrar si el jugador tiene cartas */}
      {isPlayerSeated && !isViewer && playerHand && (
        <div className="absolute bottom-32 left-1/2 transform -translate-x-1/2">
          <HandDisplay
            hand={playerHand}
            isDealer={false}
            showAllCards={true}
            playerName="You"
          />
        </div>
      )}

      {/* Game Actions - CORREGIDO: Condiciones más específicas */}
      {isPlayerSeated && 
       !isViewer && 
       gameStatus === 'InProgress' && 
       playerHand && 
       playerHand.status === 'Active' &&
       onHit && 
       onStand && (
        <div className="absolute bottom-20 left-1/2 transform -translate-x-1/2">
          <GameActions
            isPlayerTurn={isPlayerTurn}
            canHit={true}  // Si llegamos aquí, siempre puede hacer hit
            canStand={true}  // Si llegamos aquí, siempre puede hacer stand
            isGameActive={true}  // Si llegamos aquí, el juego está activo
            onHit={onHit}
            onStand={onStand}
          />
        </div>
      )}

      {/* Banner Central - CORREGIDO: Solo mostrar cuando no hay juego activo O cuando el jugador no está sentado */}
      {(gameStatus !== 'InProgress' || !isPlayerSeated) && (
        <div className="absolute top-1/2 left-1/2 transform -translate-x-1/2 -translate-y-1/2 bg-white/95 border-l-4 border-amber-500 rounded-lg p-6 shadow-[0_10px_25px_rgba(0,0,0,0.3)] min-w-[400px] flex items-center">
          <div className="w-8 h-8 rounded-full bg-amber-500 flex items-center justify-center mr-4 text-white font-bold">
            {gameStatus === 'InProgress' ? '🎯' : '!'}
          </div>
          
          <div className="flex-1">
            <h3 className="m-0 mb-2 text-lg font-bold text-amber-800">
              {gameStatus === 'InProgress' ? 'Partida en Curso' : 'Esperando Jugadores'}
            </h3>
            
            <p className="m-0 text-gray-700">
              {gameStatus === 'InProgress' 
                ? 'La partida está en progreso. ¡Buena suerte!'
                : 'Se necesitan mínimo 2 jugadores para comenzar'
              }
            </p>
            
            {!isPlayerSeated && !isViewer && (
              <p className="m-0 text-gray-600 text-sm mt-2">
                Haz clic en un asiento libre para unirte a la mesa
              </p>
            )}
            
            {isViewer && (
              <p className="m-0 text-gray-600 text-sm mt-2">
                Modo espectador - Observando la partida
              </p>
            )}
          </div>
          
          {showStartButton && (
            <button
              onClick={onStartRound}
              disabled={isStarting}
              className={`ml-4 px-4 py-2 rounded-lg font-semibold transition-colors text-white ${
                isStarting ? 'bg-gray-600 cursor-not-allowed' : 'bg-emerald-600 hover:bg-emerald-700'
              }`}
            >
              {isStarting ? 'Iniciando...' : 'Iniciar Ronda'}
            </button>
          )}
        </div>
      )}
    </>
  )
}