// src/pages/LobbyPage.tsx - Versión de Producción
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
          console.error('Error loading lobby:', e)
          setError(e?.message ?? 'No se pudo cargar el lobby')
        }
      } finally {
        if (isMounted) {
          setLoading(false)
        }
      }
    }

    loadLobby()
    
    return () => {
      isMounted = false
    }
  }, [])

  // Cargar mesas desde API
  const loadTablesFromAPI = async (): Promise<LobbyTable[]> => {
    try {
      const API_BASE = import.meta.env.VITE_API_BASE_URL || 'http://localhost:7102'
      const token = authService.getToken()

      console.log('[LOBBY] Loading tables from API...')
      console.log('[LOBBY] API_BASE:', API_BASE)
      console.log('[LOBBY] Token exists:', !!token)
      console.log('[LOBBY] User authenticated:', authService.isAuthenticated())

      if (!token || !authService.isAuthenticated()) {
        console.error('[LOBBY] No authentication token available')
        throw new Error('No hay token de autenticación válido')
      }

      // Limpiar token para asegurar formato correcto
      const cleanToken = token.replace(/^Bearer\s+/i, '').trim()
      
      const response = await fetch(`${API_BASE}/api/gameroom`, {
        headers: {
          'Authorization': `Bearer ${cleanToken}`,
          'Content-Type': 'application/json'
        }
      })

      console.log('[LOBBY] API Response status:', response.status)

      if (response.status === 401 || response.status === 403) {
        console.error('[LOBBY] Authentication failed - redirecting to login')
        authService.logout()
        navigate({ to: '/' }) // Redirigir a login
        throw new Error('Sesión expirada')
      }

      if (!response.ok) {
        const errorText = await response.text()
        console.error('[LOBBY] API Error:', response.status, errorText)
        throw new Error(`Error ${response.status}: ${response.statusText}`)
      }

      const data = await response.json()
      console.log('[LOBBY] API Response data:', data)

      // Mapear datos del GameRoomController (ActiveRoomResponse)
      const tables: LobbyTable[] = Array.isArray(data) ? data.map((room: any) => ({
        id: room.roomCode || room.id,
        name: room.name || 'Mesa Sin Nombre',
        playerCount: room.playerCount || 0,
        maxPlayers: room.maxPlayers || 6,
        minBet: 10, // Default values - puedes ajustar según tu backend
        maxBet: 1000,
        status: room.status || 'WaitingForPlayers'
      })) : []

      console.log('[LOBBY] Parsed tables:', tables)
      return tables

    } catch (error: any) {
      console.error('[LOBBY] Error loading from API:', error)
      
      // Si es error de autenticación, no usar fallback
      if (error.message?.includes('autenticación') || error.message?.includes('Sesión expirada')) {
        throw error
      }
      
      // Para otros errores, usar datos de respaldo
      console.warn('[LOBBY] Using fallback data due to error')
      return [
        {
          id: 'b9162ab8-0972-491e-b1eb-a4831410e720',
          name: 'Mesa de Prueba (Offline)',
          playerCount: 0,
          maxPlayers: 6,
          minBet: 10,
          maxBet: 1000,
          status: 'WaitingForPlayers'
        }
      ]
    }
  }

  // Filtrar mesas
  const filteredTables = useMemo(() => {
    const query = nameQuery.trim().toLowerCase()
    return allTables.filter(table => {
      if (query && !table.name.toLowerCase().includes(query)) return false
      if (table.minBet < minBet) return false
      if (table.maxBet > 0 && table.maxBet > maxBet) return false
      return true
    })
  }, [allTables, nameQuery, minBet, maxBet])

  // Crear nueva mesa
  const handleCreateTable = async () => {
    const name = prompt('Nombre de la nueva mesa:', 'Mi Mesa VIP')
    if (!name?.trim()) return
    
    try {
      setCreating(true)
      setError(null)
      
      const API_BASE = import.meta.env.VITE_API_BASE_URL || 'http://localhost:7102'
      const token = authService.getToken()

      if (!token || !authService.isAuthenticated()) {
        throw new Error('No hay token de autenticación válido')
      }

      const cleanToken = token.replace(/^Bearer\s+/i, '').trim()

      // Usar el formato correcto del GameRoomController
      const response = await fetch(`${API_BASE}/api/gameroom`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${cleanToken}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({ 
          RoomName: name.trim(),  // Nota: GameRoomController espera "RoomName"
          MaxPlayers: 6 
        })
      })

      if (response.status === 401 || response.status === 403) {
        console.error('[LOBBY] Authentication failed creating room')
        authService.logout()
        navigate({ to: '/' })
        throw new Error('Sesión expirada')
      }

      if (!response.ok) {
        const errorText = await response.text()
        console.error('[LOBBY] Error creating room:', errorText)
        throw new Error(`Error ${response.status}: ${errorText}`)
      }

      const newRoom = await response.json()
      console.log('[LOBBY] Room created:', newRoom)

      // Mapear respuesta del GameRoomController (RoomInfoResponse)
      const newTable: LobbyTable = {
        id: newRoom.roomCode,
        name: newRoom.name,
        playerCount: newRoom.playerCount || 0,
        maxPlayers: newRoom.maxPlayers || 6,
        minBet: 10,
        maxBet: 1000,
        status: newRoom.status || 'WaitingForPlayers'
      }

      setAllTables(prev => [newTable, ...prev])
      navigate({ to: `/game/${newTable.id}` })
      
    } catch (e: any) {
      const errorMessage = e?.message || 'No se pudo crear la mesa'
      console.error('[LOBBY] Error creating table:', errorMessage)
      setError(errorMessage)
    } finally {
      setCreating(false)
    }
  }

  // Navegar a mesa
  const handleJoinTable = (tableId: string) => {
    navigate({ to: `/game/${tableId}` })
  }

  // Recargar mesas
  const handleRefresh = async () => {
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
    signalRService.stopConnections()
    authService.logout()
    navigate({ to: '/' })
  }

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
            </div>
          </div>

          <div className="flex items-center gap-4">
            {/* User info */}
            <div className="text-right">
              <div className="text-white font-semibold">
                {currentUser?.displayName || 'Usuario'}
              </div>
              <div className="text-emerald-400 font-bold">
                {formatMoney(currentUser?.balance || 0)}
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
          {/* Sidebar - Filtros */}
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
              </div>

              {/* Stats */}
              <div className="mt-6 pt-4 border-t border-slate-700">
                <h4 className="font-semibold mb-3">Estadísticas</h4>
                <div className="text-sm space-y-2 text-slate-300">
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
                </div>
              </div>
            </div>
          </aside>

          {/* Main Content */}
          <section className="col-span-12 lg:col-span-9">
            <div className="mb-6">
              <h2 className="text-2xl font-bold mb-2">Mesas Disponibles</h2>
              <p className="text-slate-400">
                Selecciona una mesa y comienza a jugar BlackJack en tiempo real
              </p>
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
                    onJoin={() => handleJoinTable(table.id)}
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

// Componente para cada mesa
function TableCard({ table, onJoin }: { table: LobbyTable; onJoin: () => void }) {
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

  return (
    <div className="bg-gradient-to-r from-slate-800 to-slate-700 border border-slate-600 rounded-xl p-6 hover:border-slate-500 transition-all shadow-lg">
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
        </div>

        {/* Actions */}
        <div className="flex items-center gap-4">
          <div className="text-right text-sm">
            <div className="text-slate-300">Jugadores</div>
            <div className="text-white font-bold">
              {table.playerCount}/{table.maxPlayers}
            </div>
          </div>

          <button
            onClick={onJoin}
            disabled={!statusConfig.canJoin}
            className={`px-6 py-3 rounded-lg font-semibold transition-all ${
              statusConfig.canJoin
                ? 'bg-emerald-600 hover:bg-emerald-700 text-white'
                : 'bg-gray-600 text-gray-400 cursor-not-allowed'
            }`}
          >
            {statusConfig.canJoin ? 'Unirse' : 'Ocupado'}
          </button>
        </div>
      </div>
    </div>
  )
}