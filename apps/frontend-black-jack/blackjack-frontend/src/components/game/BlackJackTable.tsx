// src/components/game/BlackjackTable.tsx
import React, { useState, useCallback } from 'react'
import { motion } from 'framer-motion'
import { signalRService, type GameState } from '../../services/signalr'
import { authService } from '../../services/auth'

// Tipos simplificados para tu backend actual
interface Player {
  id: string
  displayName: string
  balance: number
  currentBet: number
  position: number
  isActive: boolean
}

interface BlackjackTableProps {
  gameState: GameState
  onGameAction?: (action: string, data?: any) => void
}

// Componente de chip de apuesta
const BettingChip: React.FC<{ 
  value: number
  isSelected?: boolean
  onClick: () => void 
}> = ({ value, isSelected = false, onClick }) => {
  const getChipColor = (val: number) => {
    if (val >= 100) return 'from-purple-500 to-purple-700 border-purple-400'
    if (val >= 50) return 'from-red-500 to-red-700 border-red-400'
    if (val >= 25) return 'from-green-500 to-green-700 border-green-400'
    if (val >= 10) return 'from-blue-500 to-blue-700 border-blue-400'
    return 'from-gray-500 to-gray-700 border-gray-400'
  }

  return (
    <motion.button
      onClick={onClick}
      className={`w-12 h-12 rounded-full border-4 bg-gradient-to-br ${getChipColor(value)} 
                  flex items-center justify-center font-bold text-white text-xs shadow-lg
                  ${isSelected ? 'ring-4 ring-yellow-400 scale-110' : ''}`}
      whileHover={{ scale: isSelected ? 1.15 : 1.1 }}
      whileTap={{ scale: 0.95 }}
    >
      ${value}
    </motion.button>
  )
}

// Componente de asiento de jugador
const PlayerSeat: React.FC<{
  player: Player | null
  position: number
  isCurrentTurn: boolean
  onJoinSeat: (position: number) => void
  currentUser: any
  gameStatus: string
}> = ({ player, position, isCurrentTurn, onJoinSeat, currentUser, gameStatus }) => {
  const isEmpty = !player
  const isCurrentUser = player?.id === currentUser?.id

  const getPositionClasses = (pos: number) => {
    // Posiciones alrededor de una mesa ovalada (6 asientos)
    const positions = [
      'absolute bottom-16 left-1/2 transform -translate-x-1/2', // Posición 0
      'absolute bottom-24 left-16', // Posición 1
      'absolute bottom-32 left-4', // Posición 2
      'absolute top-32 left-4', // Posición 3
      'absolute bottom-32 right-4', // Posición 4
      'absolute bottom-24 right-16', // Posición 5
    ]
    return positions[pos] || positions[0]
  }

  return (
    <div className={getPositionClasses(position)}>
      <motion.div
        className={`w-20 h-20 rounded-full border-4 ${
          isCurrentTurn 
            ? 'border-yellow-400 bg-yellow-100 shadow-xl' 
            : isEmpty 
              ? 'border-gray-600 bg-gray-800 border-dashed opacity-60 hover:opacity-100 hover:border-green-400'
              : 'border-green-600 bg-green-100'
        } flex items-center justify-center transition-all duration-300 cursor-pointer relative`}
        whileHover={isEmpty && gameStatus === 'WaitingForPlayers' ? { 
          scale: 1.05, 
          borderColor: '#10b981',
        } : {}}
        animate={isCurrentTurn ? { 
          boxShadow: [
            '0 0 20px #fbbf24', 
            '0 0 40px #fbbf24', 
            '0 0 20px #fbbf24'
          ],
          scale: [1, 1.02, 1]
        } : {}}
        transition={{ 
          boxShadow: { duration: 1, repeat: Infinity },
          scale: { duration: 1, repeat: Infinity }
        }}
        onClick={isEmpty && gameStatus === 'WaitingForPlayers' ? () => onJoinSeat(position) : undefined}
      >
        {isEmpty ? (
          <div className="text-gray-400 text-xs text-center">
            <div className="text-2xl mb-1">+</div>
            <div>Asiento {position + 1}</div>
            {gameStatus === 'WaitingForPlayers' && (
              <div className="text-[10px] text-green-400 mt-1">Click para unirse</div>
            )}
          </div>
        ) : (
          <div className="text-center">
            <div className={`font-bold text-xs ${isCurrentUser ? 'text-blue-600' : 'text-gray-800'}`}>
              {player.displayName.length > 8 ? 
                player.displayName.slice(0, 8) + '...' : 
                player.displayName
              }
              {isCurrentUser && <div className="text-[10px]">(Tú)</div>}
            </div>
            <div className="text-[10px] text-gray-600">${player.balance}</div>
          </div>
        )}

        {/* Indicador de estado del jugador */}
        {player && (
          <div className="absolute -top-2 -right-2">
            <div className={`w-4 h-4 rounded-full ${
              isCurrentTurn ? 'bg-green-500 animate-pulse' :
              player.isActive ? 'bg-blue-500' :
              'bg-gray-400'
            }`} />
          </div>
        )}
      </motion.div>

      {/* Apuesta del jugador */}
      {player && player.currentBet && player.currentBet > 0 && (
        <div className="absolute -bottom-8 left-1/2 transform -translate-x-1/2">
          <motion.div 
            className="w-8 h-8 bg-gradient-to-br from-red-500 to-red-700 border-2 border-red-400 
                       rounded-full flex items-center justify-center text-white text-[10px] font-bold shadow-lg"
            initial={{ scale: 0, rotate: 0 }}
            animate={{ scale: 1, rotate: 360 }}
            transition={{ type: "spring", stiffness: 500, damping: 25 }}
          >
            ${player.currentBet}
          </motion.div>
        </div>
      )}
    </div>
  )
}

// Componente principal de la mesa
const BlackjackTable: React.FC<BlackjackTableProps> = ({ 
  gameState, 
  onGameAction 
}) => {
  const currentUser = authService.getCurrentUser()
  const [selectedBetAmount, setSelectedBetAmount] = useState(10)

  // Encontrar el jugador actual
  const currentPlayer = gameState.players?.find((p: Player) => p.id === currentUser?.id)
  const isCurrentPlayerTurn = false // Simplificado por ahora

  // Opciones de apuesta disponibles
  const minBet = gameState.minBet || 10
  const maxBet = gameState.maxBet || 500
  const userBalance = currentUser?.balance || 5000
  
  const betOptions = [5, 10, 25, 50, 100, 250].filter(amount => 
    amount >= minBet && 
    amount <= maxBet && 
    amount <= userBalance
  )

  // Handlers para acciones del juego
  const handleJoinSeat = useCallback(async (position: number) => {
    try {
      console.log(`🎮 Intentando unirse al asiento ${position}`)
      console.log('🔍 Datos de la mesa:', { tableId: gameState.id, position, currentUser: currentUser?.id })
      
      const result = await signalRService.joinSeat(gameState.id, position)
      console.log('✅ Resultado joinSeat:', result)
      
      onGameAction?.('joinSeat', { position })
    } catch (error) {
      console.error('❌ Error detallado joining seat:', error)
      console.error('🔍 Error type:', typeof error)
      console.error('🔍 Error message:', (error as any)?.message)
      
      const errorMessage = (error as any)?.message || 'Error desconocido'
      alert(`Error al unirse al asiento ${position + 1}: ${errorMessage}`)
    }
  }, [gameState.id, onGameAction, currentUser])

  const handlePlaceBet = useCallback(async () => {
    try {
      console.log(`💰 Intentando apostar $${selectedBetAmount}`)
      await signalRService.placeBet(gameState.id, selectedBetAmount)
      onGameAction?.('placeBet', { amount: selectedBetAmount })
    } catch (error) {
      console.error('Error placing bet:', error)
      alert(`Error al apostar: ${(error as any)?.message}`)
    }
  }, [gameState.id, selectedBetAmount, onGameAction])

  return (
    <div className="relative w-full max-w-5xl mx-auto px-4">
      {/* Información del juego */}
      <div className="text-center mb-4">
        <div className="bg-gray-800/80 backdrop-blur-sm rounded-lg p-3 inline-block">
          <div className="text-white text-sm">
            Mesa: <span className="font-bold text-yellow-400">{gameState.id.slice(0, 8)}</span> | 
            Estado: <span className="font-bold text-green-400">{gameState.status || 'Esperando'}</span>
          </div>
        </div>
      </div>

      {/* Área del dealer */}
      <div className="text-center mb-8">
        <div className="inline-flex items-center gap-2 mb-4">
          <div className="w-10 h-10 bg-gradient-to-br from-gray-700 to-gray-900 rounded-full flex items-center justify-center text-xl border-2 border-yellow-500">
            🎩
          </div>
          <h3 className="text-xl font-bold text-white">Dealer</h3>
        </div>
        
        <div className="flex justify-center space-x-2 mb-4 min-h-[80px]">
          <div className="text-gray-500 text-sm flex items-center">
            Esperando cartas...
          </div>
        </div>
      </div>

      {/* Mesa de juego */}
      <div className="relative mx-auto w-full max-w-4xl h-[450px] mb-8">
        {/* Superficie de la mesa */}
        <motion.div 
          className="absolute inset-0 bg-gradient-to-br from-green-600 via-green-700 to-green-800 
                     rounded-full border-8 border-yellow-600 shadow-2xl"
          initial={{ scale: 0, opacity: 0 }}
          animate={{ scale: 1, opacity: 1 }}
          transition={{ duration: 0.8, type: "spring" }}
        />
        
        {/* Patrón de fieltro */}
        <div className="absolute inset-12 rounded-full border-4 border-yellow-500 border-dashed opacity-30" />
        
        {/* Texto central de la mesa */}
        <div className="absolute inset-0 flex items-center justify-center">
          <div className="text-yellow-400 font-bold text-2xl opacity-40 transform -rotate-12">
            BLACKJACK
          </div>
        </div>
        
        {/* Información del pot */}
        <div className="absolute top-4 left-1/2 transform -translate-x-1/2">
          <div className="bg-black/60 text-white px-4 py-2 rounded-lg text-sm">
            <div className="text-center">
              <div className="text-yellow-400 font-bold">Pot Total</div>
              <div className="text-2xl">${gameState.pot || 0}</div>
              <div className="text-xs text-gray-300">
                Min: ${minBet} - Max: ${maxBet}
              </div>
            </div>
          </div>
        </div>
        
        {/* Asientos de jugadores */}
        {Array.from({ length: 6 }, (_, i) => {
          const player = gameState.players?.find((p: Player) => p.position === i) || null
          return (
            <PlayerSeat
              key={i}
              player={player}
              position={i}
              isCurrentTurn={isCurrentPlayerTurn && currentPlayer?.position === i}
              onJoinSeat={handleJoinSeat}
              currentUser={currentUser}
              gameStatus={gameState.status || 'WaitingForPlayers'}
            />
          )
        })}
      </div>

      {/* Controles del juego */}
      <div className="text-center">
        {/* Esperando jugadores */}
        {gameState.status === 'WaitingForPlayers' && (
          <motion.div 
            className="bg-gray-800/90 backdrop-blur-sm p-6 rounded-xl max-w-md mx-auto"
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
          >
            <h4 className="text-xl font-bold text-white mb-3">
              Esperando jugadores...
            </h4>
            <p className="text-gray-300 text-sm mb-4">
              {currentPlayer ? 
                'Esperando que se unan más jugadores para comenzar' : 
                'Haz clic en un asiento vacío para unirte a la mesa'
              }
            </p>
            <div className="text-sm text-gray-400 mb-4">
              <div>Jugadores conectados: {gameState.players?.length || 0}/6</div>
              <div>Mínimo para jugar: 2 jugadores</div>
            </div>
            {currentPlayer && (
              <div className="text-xs bg-green-800/50 text-green-300 p-3 rounded">
                Ya estás sentado en el asiento {(currentPlayer.position || 0) + 1}
              </div>
            )}
          </motion.div>
        )}

        {/* Fase de apuestas */}
        {gameState.status === 'Betting' && currentPlayer && (
          <motion.div 
            className="bg-gray-800/90 backdrop-blur-sm p-6 rounded-xl max-w-lg mx-auto"
            initial={{ opacity: 0, scale: 0.9 }}
            animate={{ opacity: 1, scale: 1 }}
          >
            <h4 className="text-xl font-bold text-white mb-4">
              Haz tu apuesta
            </h4>
            
            {/* Chips de apuesta */}
            <div className="flex justify-center gap-3 mb-6 flex-wrap">
              {betOptions.map((amount) => (
                <BettingChip
                  key={amount}
                  value={amount}
                  isSelected={selectedBetAmount === amount}
                  onClick={() => setSelectedBetAmount(amount)}
                />
              ))}
            </div>
            
            <div className="text-center mb-6 space-y-2">
              <div className="text-white text-lg">
                Apuesta seleccionada: 
                <span className="text-yellow-400 font-bold ml-2">${selectedBetAmount}</span>
              </div>
              <div className="text-sm text-gray-400">
                Tu balance: <span className="text-green-400 font-bold">${currentUser?.balance || 5000}</span>
              </div>
              <div className="text-xs text-gray-500">
                Rango: ${minBet} - ${maxBet}
              </div>
            </div>
            
            <motion.button
              onClick={handlePlaceBet}
              className="bg-gradient-to-r from-green-600 to-green-700 hover:from-green-700 hover:to-green-800 
                       px-8 py-4 rounded-lg font-bold text-lg shadow-lg transition-all duration-200 text-white
                       disabled:from-gray-600 disabled:to-gray-700 disabled:cursor-not-allowed"
              whileHover={{ scale: 1.05 }}
              whileTap={{ scale: 0.95 }}
              disabled={selectedBetAmount > (currentUser?.balance || 5000)}
            >
              Apostar ${selectedBetAmount}
            </motion.button>
          </motion.div>
        )}

        {/* Estado por defecto */}
        {!gameState.status && (
          <motion.div 
            className="bg-gray-800/90 backdrop-blur-sm p-6 rounded-xl max-w-md mx-auto"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
          >
            <h4 className="text-xl font-bold text-white mb-4">Conectando...</h4>
            <p className="text-gray-300 text-sm">
              Cargando estado del juego...
            </p>
            <div className="flex justify-center mt-4">
              <div className="animate-spin w-6 h-6 border-2 border-white border-t-transparent rounded-full"></div>
            </div>
          </motion.div>
        )}
      </div>
    </div>
  )
}

export default BlackjackTable