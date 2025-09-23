// src/components/game/GameTable.tsx
import React from 'react'

interface GameTableProps {
  gameStatus?: string
  canStart: boolean
  isPlayerSeated: boolean
  isViewer: boolean
  isCurrentPlayerHost: boolean
  gameControlConnected: boolean
  onStartRound: () => void
  // AGREGAR ESTAS:
  onProcessAutoBets?: () => void          
  autoBettingActive?: boolean              
  autoBettingProcessing?: boolean          
}

export default function GameTable({
  gameStatus,
  canStart,
  isPlayerSeated,
  isViewer,
  isCurrentPlayerHost,
  gameControlConnected,
  onStartRound
}: GameTableProps) {
  const showStartButton = !isViewer && 
                          canStart && 
                          gameStatus !== 'InProgress' && 
                          isCurrentPlayerHost && 
                          gameControlConnected

  return (
    <>
      {/* Dealer */}
      <div className="absolute top-20 left-1/2 transform -translate-x-1/2 text-center">
        <div className="w-[60px] h-[60px] rounded-full bg-amber-400 flex items-center justify-center text-2xl font-bold text-black mb-2 mx-auto">
          D
        </div>
        <div className="text-amber-400 font-bold">Dealer</div>
      </div>

      {/* Banner Central */}
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
            className="ml-4 bg-emerald-600 hover:bg-emerald-700 text-white px-4 py-2 rounded-lg font-semibold transition-colors"
          >
            Iniciar Ronda
          </button>
        )}
      </div>
    </>
  )
}