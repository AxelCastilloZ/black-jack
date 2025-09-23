// src/pages/LobbyPage.tsx - EXTENDIDO con MinBetPerRound display
import React, { useEffect, useMemo, useState } from 'react'
import { useNavigate } from '@tanstack/react-router'
import { signalRService } from '../services/signalr'
import { authService } from '../services/auth'

interface LobbyTable {
  id: string
  name: string
  playerCount: number
  maxPlayers: number
  minBet: number
  maxBet: number
  status: string
  // NUEVO: Auto-betting
  minBetPerRound?: number
  autoBettingEnabled?: boolean
}

function formatMoney(amount: number): string {
  return `$${amount.toLocaleString()}`
}

export default function LobbyPage() {
  const navigate = useNavigate()
  const currentUser = authService.getCurrentUser()

  const [allTables, setAllTables] = useState<LobbyTable[]>([])
  const [nameQuery, setNameQuery] = useState('')
  const [minBet, setMinBet] = useState(0)
  const [maxBet, setMaxBet] = useState(10000)
  const [creating, setCreating] = useState(false)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [isConnected, setIsConnected] = useState(false)

  // NUEVO: Filtro para auto-betting
  const [showOnlyAffordable, setShowOnlyAffordable] = useState(false)

  // Verificar conexión SignalR
  useEffect(() => {
    const checkConnection = () => {
      setIsConnected(signalRService.isLobbyConnected)
    }
    
    checkConnection()
    const interval = setInterval(checkConnection, 3000)
    
    return () => clearInterval(interval)
  }, [])

  // Cargar lobby
  useEffect(() => {
    let isMounted = true
    
    const loadLobby = async () => {
      try {
        setLoading(true)
        setError(null)
        
        // Verificar autenticación antes de continuar
        if (!authService.isAuthenticated()) {
          navigate({ to: '/' })
          return
        }

        // Conectar SignalR si no está conectado
        if (!signalRService.isLobbyConnected) {
          await signalRService.startConnections()
        }

        // Cargar mesas desde API
        const tables = await loadTablesFromAPI()
        
        if (isMounted) {
          setAllTables(tables)
        }
        
      } catch (e: any) {
        if (isMounted) {
          console.error('[LOBBY] Error loading:', e)
          setError(e?.message ?? 'No se pudo cargar el lobby')
        }
      } finally {
        if (isMounted) {
          setLoading(false)
        }
      }
    }

    loadLobby()
    
    // NUEVO: Escuchar cambios de usuario (balance)
    const onUserUpdated = () => {
      if (!isMounted) return
      // Forzar re-render leyendo de authService
      setAllTables(t => [...t])
    }
    window.addEventListener('auth:userUpdated', onUserUpdated)

    return () => {
      isMounted = false
      window.removeEventListener('auth:userUpdated', onUserUpdated)
    }
  }, [])

  // Cargar mesas desde API usando TableController
  const loadTablesFromAPI = async (): Promise<LobbyTable[]> => {
    try {
      const API_BASE = import.meta.env.VITE_API_BASE_URL || 'http://localhost:7102'
      
      console.log('[LOBBY] Loading tables from API...')
      console.log('[LOBBY] API_BASE:', API_BASE)
      console.log('[LOBBY] Using TableController endpoint: /api/table')
      
      const response = await fetch(`${API_BASE}/api/table`, {
        method: 'GET',
        headers: {
          'Content-Type': 'application/json',
          'Accept': 'application/json'
        }
      })

      console.log('[LOBBY] API Response status:', response.status)

      if (!response.ok) {
        const errorText = await response.text()
        console.error('[LOBBY] API Error:', response.status, errorText)
        throw new Error(`Error ${response.status}: ${response.statusText}`)
      }

      const data = await response.json()
      console.log('[LOBBY] API Response data:', data)

      // Mapear datos del TableController (TableSummaryDto)
      const tables: LobbyTable[] = Array.isArray(data) ? data.map((table: any) => ({
        id: table.id,
        name: table.name || 'Mesa Sin Nombre',
        playerCount: table.playerCount || 0,
        maxPlayers: table.maxPlayers || 6,
        minBet: table.minBet || 10,
        maxBet: table.maxBet || 1000,
        status: table.status || 'WaitingForPlayers',
        // NUEVO: Auto-betting fields - con valores por defecto si no vienen del API
        minBetPerRound: table.minBetPerRound || table.minBet || 10,
        autoBettingEnabled: table.autoBettingEnabled !== false // Default true
      })) : []

      console.log('[LOBBY] Parsed tables with auto-betting info:', tables)
      return tables

    } catch (error: any) {
      console.error('[LOBBY] Error loading from API:', error)
      
      // Datos de respaldo con auto-betting
      console.warn('[LOBBY] Using fallback data with auto-betting')
      return [
        {
          id: 'fallback-table-1',
          name: 'Mesa de Respaldo (API Error)',
          playerCount: 0,
          maxPlayers: 6,
          minBet: 10,
          maxBet: 1000,
          status: 'WaitingForPlayers',
          minBetPerRound: 25,
          autoBettingEnabled: true
        },
        {
          id: 'fallback-table-2',
          name: 'Mesa VIP (Fallback)',
          playerCount: 2,
          maxPlayers: 6,
          minBet: 100,
          maxBet: 5000,
          status: 'WaitingForPlayers',
          minBetPerRound: 200,
          autoBettingEnabled: true
        }
      ]
    }
  }

  // Filtrar mesas - EXTENDIDO con filtro de affordability
  const filteredTables = useMemo(() => {
    const query = nameQuery.trim().toLowerCase()
    const userBalance = currentUser?.balance || 0
    
    return allTables.filter(table => {
      if (query && !table.name.toLowerCase().includes(query)) return false
      if (table.minBet < minBet) return false
      if (table.maxBet > 0 && table.maxBet > maxBet) return false
      
      // NUEVO: Filtrar por affordability de auto-betting
      if (showOnlyAffordable && table.minBetPerRound && userBalance < table.minBetPerRound) {
        return false
      }
      
      return true
    })
  }, [allTables, nameQuery, minBet, maxBet, showOnlyAffordable, currentUser?.balance])

  // Crear nueva mesa usando TableController
  const handleCreateTable = async () => {
    const name = prompt('Nombre de la nueva mesa:', 'Mi Mesa VIP')
    if (!name?.trim()) return

    // NUEVO: Preguntar por auto-betting configuration
    const minBetPerRoundStr = prompt(
      'Apuesta automática por ronda (en $):',
      '50'
    )
    const minBetPerRound = parseFloat(minBetPerRoundStr || '50')

    if (isNaN(minBetPerRound) || minBetPerRound <= 0) {
      alert('La apuesta por ronda debe ser un número válido mayor a 0')
      return
    }

    // Verificar que el usuario pueda costear la apuesta
    const userBalance = currentUser?.balance || 0
    if (userBalance < minBetPerRound) {
      const proceed = confirm(
        `Tu balance actual ($${userBalance}) es menor a la apuesta por ronda ($${minBetPerRound}). ` +
        '¿Deseas crear la mesa de todos modos? Otros jugadores podrán unirse si tienen fondos suficientes.'
      )
      if (!proceed) return
    }
    
    try {
      setCreating(true)
      setError(null)
      
      const API_BASE = import.meta.env.VITE_API_BASE_URL || 'http://localhost:7102'
      
      console.log('[LOBBY] Creating table:', name, 'with minBetPerRound:', minBetPerRound)
      console.log('[LOBBY] Using TableController POST endpoint')
      
      const response = await fetch(`${API_BASE}/api/table`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({ 
          name: name.trim(),
          minBetPerRound: minBetPerRound
        })
      })

      console.log('[LOBBY] Create response status:', response.status)

      if (!response.ok) {
        const errorText = await response.text()
        console.error('[LOBBY] Create error:', errorText)
        throw new Error(`Error ${response.status}: ${errorText}`)
      }

      const newTable = await response.json()
      console.log('[LOBBY] Table created:', newTable)

      // Mapear respuesta del TableController (TableSummaryDto)
      const newLobbyTable: LobbyTable = {
        id: newTable.id,
        name: newTable.name,
        playerCount: newTable.playerCount || 0,
        maxPlayers: newTable.maxPlayers || 6,
        minBet: newTable.minBet || 10,
        maxBet: newTable.maxBet || 1000,
        status: newTable.status || 'WaitingForPlayers',
        minBetPerRound: newTable.minBetPerRound || minBetPerRound,
        autoBettingEnabled: true
      }

      setAllTables(prev => [newLobbyTable, ...prev])
      
      // Navegar usando el ID de la mesa
      console.log('[LOBBY] Navigating to created table:', newLobbyTable.id)
      navigate({ to: `/game/${newLobbyTable.id}` })
      
    } catch (e: any) {
      const errorMessage = e?.message || 'No se pudo crear la mesa'
      console.error('[LOBBY] Error creating table:', errorMessage)
      setError(errorMessage)
    } finally {
      setCreating(false)
    }
  }

  // Navegar a mesa - NUEVO: Con confirmación de auto-betting
  const handleJoinTable = (table: LobbyTable) => {
    const userBalance = currentUser?.balance || 0
    
    // Verificar si puede costear la apuesta automática
    if (table.autoBettingEnabled && table.minBetPerRound && userBalance < table.minBetPerRound) {
      alert(
        `No tienes fondos suficientes para unirte a esta mesa.\n\n` +
        `Tu balance: ${formatMoney(userBalance)}\n` +
        `Apuesta requerida por ronda: ${formatMoney(table.minBetPerRound)}\n\n` +
        `Necesitas al menos ${formatMoney(table.minBetPerRound - userBalance)} más para jugar.`
      )
      return
    }

    // NUEVO: Confirmación explícita de auto-betting (opcional, para claridad)
    if (table.autoBettingEnabled && table.minBetPerRound) {
      const message = 
        `🎰 MESA CON APUESTAS AUTOMÁTICAS 🎰\n\n` +
        `Mesa: ${table.name}\n` +
        `Costo por ronda: ${formatMoney(table.minBetPerRound)}\n` +
        `Tu balance actual: ${formatMoney(userBalance)}\n\n` +
        `Al unirte a esta mesa, aceptas:\n` +
        `• Apuestas automáticas de ${formatMoney(table.minBetPerRound)} por ronda\n` +
        `• Si te quedas sin fondos, serás removido automáticamente\n` +
        `• Las apuestas se deducen al inicio de cada ronda\n\n` +
        `¿Deseas continuar?`
      
      if (!confirm(message)) {
        return
      }
    }

    console.log('[LOBBY] Navigating to table:', table.id, 'with auto-betting:', table.autoBettingEnabled)
    navigate({ to: `/game/${table.id}` })
  }

  // Navegar a mesa como viewer
  const handleViewTable = (table: LobbyTable) => {
    console.log('[LOBBY] Navigating to viewer mode for table:', table.id)
    navigate({ to: `/viewer/${table.id}` })
  }

  // Recargar mesas
  const handleRefresh = async () => {
    console.log('[LOBBY] Refreshing tables...')
    setError(null)
    setLoading(true)
    
    try {
      const tables = await loadTablesFromAPI()
      setAllTables(tables)
    } catch (e: any) {
      setError(e?.message ?? 'Error al recargar')
    } finally {
      setLoading(false)
    }
  }

  // Logout
  const handleLogout = () => {
    console.log('[LOBBY] Logging out...')
    signalRService.stopConnections()
    authService.logout()
    navigate({ to: '/' })
  }

  // NUEVO: Calcular estadísticas de auto-betting
  const autoBettingStats = useMemo(() => {
    const userBalance = currentUser?.balance || 0
    const tablesWithAutoBetting = allTables.filter(t => t.autoBettingEnabled && t.minBetPerRound)
    const affordableTables = tablesWithAutoBetting.filter(t => userBalance >= (t.minBetPerRound || 0))
    const cheapestTable = tablesWithAutoBetting.reduce((min, table) => 
      (table.minBetPerRound || Infinity) < (min.minBetPerRound || Infinity) ? table : min
    , tablesWithAutoBetting[0])

    return {
      totalWithAutoBetting: tablesWithAutoBetting.length,
      affordableTables: affordableTables.length,
      cheapestMinBet: cheapestTable?.minBetPerRound || 0
    }
  }, [allTables, currentUser?.balance])

  return (
    <div className="min-h-screen bg-slate-900 text-white">
      {/* Header */}
      <header className="border-b border-slate-700 bg-slate-800/50 px-4 py-3">
        <div className="flex items-center justify-between max-w-7xl mx-auto">
          <div className="flex items-center gap-4">
            <button
              onClick={() => navigate({ to: '/home' })}
              className="text-slate-300 hover:text-white transition-colors"
            >
              ← Inicio
            </button>
            
            <div className="flex items-center gap-2">
              <div className="flex gap-1">
                <span className="text-xl">♠</span>
                <span className="text-xl text-red-500">♥</span>
                <span className="text-xl text-red-500">♦</span>
                <span className="text-xl">♣</span>
              </div>
              <h1 className="text-xl font-bold">Lobby de Mesas</h1>
              {/* NUEVO: Indicador de auto-betting */}
              <span className="px-2 py-1 bg-yellow-600 text-black text-xs font-bold rounded-full">
                🎰 AUTO-BET
              </span>
            </div>
          </div>

          <div className="flex items-center gap-4">
            {/* User info - MEJORADO con balance destacado */}
            <div className="text-right">
              <div className="text-white font-semibold">
                {currentUser?.displayName || 'Usuario'}
              </div>
              <div className="text-emerald-400 font-bold text-lg">
                {formatMoney(currentUser?.balance || 0)}
              </div>
              {/* NUEVO: Indicador de mesas que puede costear */}
              <div className="text-xs text-slate-400">
                Puedes jugar en {autoBettingStats.affordableTables}/{autoBettingStats.totalWithAutoBetting} mesas
              </div>
            </div>

            {/* Connection status */}
            <div className="flex items-center gap-2">
              <div className={`w-2 h-2 rounded-full ${isConnected ? 'bg-green-400' : 'bg-yellow-400'}`}></div>
              <span className="text-sm text-slate-300">
                {isConnected ? 'Conectado' : 'Conectando...'}
              </span>
            </div>

            {/* Actions */}
            <button
              onClick={handleRefresh}
              disabled={loading}
              className="px-3 py-2 rounded-lg bg-slate-700 hover:bg-slate-600 text-white text-sm transition-colors disabled:opacity-50"
            >
              {loading ? '🔄' : '↻'}
            </button>
            
            <button
              onClick={handleCreateTable}
              disabled={creating}
              className="px-4 py-2 rounded-lg bg-emerald-600 hover:bg-emerald-700 text-white font-semibold transition-colors disabled:opacity-50"
            >
              {creating ? 'Creando...' : '+ Nueva Mesa'}
            </button>
            
            <button
              onClick={handleLogout}
              className="text-red-400 hover:text-red-300 text-sm transition-colors"
            >
              Salir
            </button>
          </div>
        </div>
      </header>

      <main className="max-w-7xl mx-auto px-4 py-6">
        <div className="grid grid-cols-12 gap-6">
          {/* Sidebar - Filtros EXTENDIDOS */}
          <aside className="col-span-12 lg:col-span-3">
            <div className="bg-slate-800 rounded-xl p-4 border border-slate-700">
              <h3 className="font-semibold text-lg mb-4 flex items-center gap-2">
                🔍 Filtros
              </h3>

              <div className="space-y-4">
                {/* Search */}
                <div>
                  <label className="block text-sm text-slate-300 mb-2">
                    Buscar Mesa
                  </label>
                  <input
                    type="text"
                    value={nameQuery}
                    onChange={(e) => setNameQuery(e.target.value)}
                    placeholder="Nombre de la mesa..."
                    className="w-full px-3 py-2 rounded-lg bg-slate-700 border border-slate-600 text-white placeholder-slate-400 focus:outline-none focus:border-emerald-500"
                  />
                </div>

                {/* Min Bet */}
                <div>
                  <div className="flex justify-between text-sm text-slate-300 mb-2">
                    <span>Apuesta Mínima:</span>
                    <span>{formatMoney(minBet)}</span>
                  </div>
                  <input
                    type="range"
                    min={0}
                    max={1000}
                    step={25}
                    value={minBet}
                    onChange={(e) => setMinBet(Number(e.target.value))}
                    className="w-full accent-emerald-600"
                  />
                </div>

                {/* Max Bet */}
                <div>
                  <div className="flex justify-between text-sm text-slate-300 mb-2">
                    <span>Apuesta Máxima:</span>
                    <span>{formatMoney(maxBet)}</span>
                  </div>
                  <input
                    type="range"
                    min={100}
                    max={10000}
                    step={100}
                    value={maxBet}
                    onChange={(e) => setMaxBet(Number(e.target.value))}
                    className="w-full accent-emerald-600"
                  />
                </div>

                {/* NUEVO: Filtro de affordability */}
                <div>
                  <label className="flex items-center gap-2 text-sm text-slate-300 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={showOnlyAffordable}
                      onChange={(e) => setShowOnlyAffordable(e.target.checked)}
                      className="rounded bg-slate-700 border-slate-600 text-emerald-600 focus:ring-emerald-500"
                    />
                    <span>Solo mesas que puedo costear</span>
                  </label>
                  <div className="text-xs text-slate-400 mt-1">
                    Balance actual: {formatMoney(currentUser?.balance || 0)}
                  </div>
                </div>
              </div>

              {/* Stats EXTENDIDAS */}
              <div className="mt-6 pt-4 border-t border-slate-700">
                <h4 className="font-semibold mb-3">Estadísticas</h4>
                <div className="text-sm space-y-2 text-slate-300">
                  {/* NUEVO: Saldo del jugador */}
                  <div className="flex justify-between">
                    <span>Tu saldo:</span>
                    <span className="text-emerald-400 font-semibold">
                      {formatMoney(currentUser?.balance || 0)}
                    </span>
                  </div>
                  <div className="flex justify-between">
                    <span>Mesas Activas:</span>
                    <span className="text-emerald-400 font-semibold">
                      {allTables.length}
                    </span>
                  </div>
                  <div className="flex justify-between">
                    <span>Mesas Disponibles:</span>
                    <span className="text-emerald-400 font-semibold">
                      {allTables.filter(t => t.status === 'WaitingForPlayers').length}
                    </span>
                  </div>
                  <div className="flex justify-between">
                    <span>Filtradas:</span>
                    <span className="text-emerald-400 font-semibold">
                      {filteredTables.length}
                    </span>
                  </div>
                  {/* NUEVAS stats de auto-betting */}
                  <div className="flex justify-between">
                    <span>🎰 Con Auto-Betting:</span>
                    <span className="text-yellow-400 font-semibold">
                      {autoBettingStats.totalWithAutoBetting}
                    </span>
                  </div>
                  <div className="flex justify-between">
                    <span>💰 Puedes Costear:</span>
                    <span className="text-green-400 font-semibold">
                      {autoBettingStats.affordableTables}
                    </span>
                  </div>
                  {autoBettingStats.cheapestMinBet > 0 && (
                    <div className="flex justify-between">
                      <span>💵 Mesa Más Barata:</span>
                      <span className="text-blue-400 font-semibold">
                        {formatMoney(autoBettingStats.cheapestMinBet)}
                      </span>
                    </div>
                  )}
                  {/* NUEVO: Rondas que puedes costear en la mesa más barata */}
                  {autoBettingStats.cheapestMinBet > 0 && (
                    <div className="flex justify-between">
                      <span>🕒 Rondas Posibles:</span>
                      <span className="text-yellow-400 font-semibold">
                        {Math.floor((currentUser?.balance || 0) / (autoBettingStats.cheapestMinBet || 1))}
                      </span>
                    </div>
                  )}
                </div>
              </div>

              {/* NUEVO: Auto-betting info */}
              <div className="mt-4 p-3 bg-yellow-900/20 border border-yellow-700/50 rounded-lg">
                <div className="flex items-center gap-2 text-yellow-400 font-semibold text-sm mb-2">
                  🎰 Auto-Betting Activo
                </div>
                <div className="text-xs text-yellow-200 space-y-1">
                  <div>• Las apuestas se deducen automáticamente</div>
                  <div>• Se requiere balance suficiente</div>
                  <div>• Remoción automática sin fondos</div>
                  <div>• Aceptación implícita al unirse</div>
                </div>
              </div>
            </div>
          </aside>

          {/* Main Content */}
          <section className="col-span-12 lg:col-span-9">
            <div className="mb-6">
              <h2 className="text-2xl font-bold mb-2">Mesas Disponibles</h2>
              <p className="text-slate-400">
                Selecciona una mesa y comienza a jugar BlackJack con apuestas automáticas en tiempo real
              </p>
              {/* NUEVO: Info sobre auto-betting */}
              <div className="flex items-center gap-4 mt-3 p-3 bg-blue-900/20 border border-blue-700/50 rounded-lg">
                <div className="text-2xl">🎰</div>
                <div className="text-sm text-blue-200">
                  <div className="font-semibold">Sistema de Apuestas Automáticas</div>
                  <div>Al unirte a una mesa, aceptas apuestas automáticas por ronda. Las apuestas se deducen al inicio de cada ronda.</div>
                </div>
              </div>
              <div className="text-xs text-slate-500 mt-2">
                Usando endpoint: /api/table (TableController)
              </div>
            </div>

            {/* Error Message */}
            {error && (
              <div className="mb-4 bg-red-900/50 border border-red-700 rounded-xl p-4">
                <div className="flex justify-between items-center">
                  <div>
                    <h4 className="text-red-300 font-semibold">Error</h4>
                    <p className="text-red-400 text-sm">{error}</p>
                  </div>
                  <button
                    onClick={() => setError(null)}
                    className="text-red-400 hover:text-red-300"
                  >
                    ✕
                  </button>
                </div>
              </div>
            )}

            {/* Loading State */}
            {loading && (
              <div className="flex items-center justify-center py-12">
                <div className="flex items-center gap-3 text-slate-300">
                  <div className="w-6 h-6 border-2 border-slate-600 border-t-emerald-500 rounded-full animate-spin"></div>
                  Cargando mesas...
                </div>
              </div>
            )}

            {/* Empty State */}
            {!loading && !error && filteredTables.length === 0 && (
              <div className="text-center py-12">
                <div className="text-slate-400 mb-4">
                  {allTables.length === 0 
                    ? "No hay mesas activas" 
                    : "No hay mesas que coincidan con tus filtros"
                  }
                </div>
                <button
                  onClick={handleCreateTable}
                  className="px-6 py-3 rounded-lg bg-emerald-600 hover:bg-emerald-700 text-white font-semibold"
                >
                  Crear Primera Mesa
                </button>
              </div>
            )}

            {/* Tables List */}
            {!loading && filteredTables.length > 0 && (
              <div className="space-y-4">
                {filteredTables.map((table) => (
                  <TableCard
                    key={table.id}
                    table={table}
                    currentUserBalance={currentUser?.balance || 0}
                    onJoin={() => handleJoinTable(table)}
                    onView={() => handleViewTable(table)}
                  />
                ))}
              </div>
            )}
          </section>
        </div>
      </main>
    </div>
  )
}

// Componente para cada mesa - EXTENDIDO con auto-betting display
function TableCard({ 
  table, 
  currentUserBalance, 
  onJoin, 
  onView 
}: { 
  table: LobbyTable
  currentUserBalance: number
  onJoin: () => void
  onView: () => void 
}) {
  const getStatusConfig = (status: string) => {
    switch (status.toLowerCase()) {
      case 'waitingforplayers':
        return { 
          text: 'Esperando Jugadores', 
          className: 'bg-green-600 text-white',
          canJoin: true 
        }
      case 'inprogress':
        return { 
          text: 'Partida en Curso', 
          className: 'bg-yellow-600 text-black',
          canJoin: false 
        }
      case 'finished':
        return { 
          text: 'Finalizada', 
          className: 'bg-gray-600 text-white',
          canJoin: true 
        }
      default:
        return { 
          text: status, 
          className: 'bg-gray-600 text-white',
          canJoin: true 
        }
    }
  }

  const statusConfig = getStatusConfig(table.status)
  const isVip = table.maxBet >= 1000
  
  // NUEVO: Lógica de auto-betting
  const autoBetAmount = table.minBetPerRound || 0
  const canAffordAutoBet = currentUserBalance >= autoBetAmount
  const fundingGap = autoBetAmount - currentUserBalance
  const estimatedRounds = autoBetAmount > 0 ? Math.floor(currentUserBalance / autoBetAmount) : 0

  return (
    <div className={`
      bg-gradient-to-r from-slate-800 to-slate-700 border border-slate-600 rounded-xl p-6 
      hover:border-slate-500 transition-all shadow-lg
      ${!canAffordAutoBet ? 'opacity-75 border-red-600/30' : ''}
    `}>
      <div className="flex items-center justify-between">
        {/* Mesa Info */}
        <div className="flex-1">
          <div className="flex items-center gap-3 mb-3">
            <h3 className="text-xl font-bold text-white">{table.name}</h3>
            
            <span className={`px-3 py-1 rounded-full text-xs font-semibold ${statusConfig.className}`}>
              {statusConfig.text}
            </span>
            
            {isVip && (
              <span className="px-2 py-1 rounded-full bg-yellow-600 text-black text-xs font-bold">
                👑 VIP
              </span>
            )}

            {/* NUEVO: Indicador de auto-betting */}
            {table.autoBettingEnabled && (
              <span className="px-2 py-1 rounded-full bg-purple-600 text-white text-xs font-bold">
                🎰 AUTO
              </span>
            )}

            {/* NUEVO: Indicador de affordability */}
            {!canAffordAutoBet && (
              <span className="px-2 py-1 rounded-full bg-red-600 text-white text-xs font-bold">
                ❌ Sin Fondos
              </span>
            )}
          </div>

          <div className="flex items-center gap-6 text-sm text-slate-300">
            <div className="flex items-center gap-1">
              <span>💵</span>
              <span>{formatMoney(table.minBet)} - {formatMoney(table.maxBet)}</span>
            </div>
            
            <div className="flex items-center gap-1">
              <span>👥</span>
              <span>{table.playerCount}/{table.maxPlayers} jugadores</span>
            </div>
            
            <div className="flex items-center gap-1">
              <span>🎯</span>
              <span>ID: {table.id.slice(0, 8)}...</span>
            </div>
          </div>

          {/* NUEVO: Info de auto-betting destacada */}
          {table.autoBettingEnabled && autoBetAmount > 0 && (
            <div className="mt-3 p-3 bg-purple-900/20 border border-purple-600/30 rounded-lg">
              <div className="flex items-center justify-between">
                <div>
                  <div className="text-purple-300 font-semibold text-sm flex items-center gap-2">
                    🎰 Apuesta Automática por Ronda
                  </div>
                  <div className="text-white font-bold text-lg">
                    {formatMoney(autoBetAmount)}
                  </div>
                </div>
                <div className="text-right">
                  {canAffordAutoBet ? (
                    <div className="text-green-400 text-sm">
                      ✅ Puedes jugar
                      <div className="text-xs text-slate-400">
                        ~{estimatedRounds} rondas
                      </div>
                    </div>
                  ) : (
                    <div className="text-red-400 text-sm">
                      ❌ Fondos insuficientes
                      <div className="text-xs text-slate-400">
                        Faltan {formatMoney(fundingGap)}
                      </div>
                    </div>
                  )}
                </div>
              </div>
              
              {/* NUEVO: Detalles de balance */}
              <div className="mt-2 text-xs text-purple-200 space-y-1">
                <div className="flex justify-between">
                  <span>Tu balance:</span>
                  <span className={canAffordAutoBet ? 'text-green-400' : 'text-red-400'}>
                    {formatMoney(currentUserBalance)}
                  </span>
                </div>
                {canAffordAutoBet && estimatedRounds > 0 && (
                  <div className="flex justify-between">
                    <span>Rondas estimadas:</span>
                    <span className="text-yellow-400">~{estimatedRounds}</span>
                  </div>
                )}
              </div>
            </div>
          )}
        </div>

        {/* Actions */}
        <div className="flex items-center gap-4">
          <div className="text-right text-sm">
            <div className="text-slate-300">Jugadores</div>
            <div className="text-white font-bold">
              {table.playerCount}/{table.maxPlayers}
            </div>
          </div>

          <div className="flex gap-2">
            <button
              onClick={onJoin}
              disabled={!statusConfig.canJoin}
              className={`px-6 py-3 rounded-lg font-semibold transition-all ${
                statusConfig.canJoin
                  ? canAffordAutoBet
                    ? 'bg-emerald-600 hover:bg-emerald-700 text-white'
                    : 'bg-orange-600 hover:bg-orange-700 text-white'
                  : 'bg-gray-600 text-gray-400 cursor-not-allowed'
              }`}
              title={
                !statusConfig.canJoin 
                  ? 'Mesa no disponible'
                  : !canAffordAutoBet
                    ? `Necesitas ${formatMoney(fundingGap)} más para jugar`
                    : 'Unirse y aceptar apuestas automáticas'
              }
            >
              {!statusConfig.canJoin 
                ? 'Ocupado'
                : !canAffordAutoBet
                  ? 'Sin Fondos'
                  : 'Unirse'}
            </button>

            <button
              onClick={onView}
              className="px-6 py-3 rounded-lg font-semibold transition-all bg-blue-600 hover:bg-blue-700 text-white"
              title="Ver como espectador (sin apuestas)"
            >
              Ver
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}