import React, { useState, useEffect } from 'react'
import { useNavigate } from '@tanstack/react-router'
import { authService } from '../services/auth'
import { useGameTables, useCreateTable, type CreateTableRequest } from '../hooks/useGameApi'
import { useLobbySignalR } from '../hooks/useSignalR'

export default function LobbyPage() {
  const user = authService.getCurrentUser()
  const navigate = useNavigate()

  // Query hooks
  const { data: tables, isLoading, error, refetch } = useGameTables()
  const createTableMutation = useCreateTable()

  // SignalR hook
  const {
    connectionState,
    isConnected,
    isConnecting,
    isReconnecting,
    connect,
    disconnect,
    joinTable,
    createTable: createTableSignalR,
    isLobbyConnected,
    isGameConnected,
  } = useLobbySignalR()

  // Estado local
  const [selectedTable, setSelectedTable] = useState<string | null>(null)
  const [showCreateForm, setShowCreateForm] = useState(false)
  const [createForm, setCreateForm] = useState<CreateTableRequest>({
    name: '',
    minBet: 10,
    maxBet: 100,
    maxPlayers: 6,
  })

  // DEBUG: Información para diagnosticar el error 401
  useEffect(() => {
    console.log('🔍 Debug Info:')
    console.log('- API Base URL:', import.meta.env.VITE_API_BASE_URL)
    console.log('- Auth Token:', authService.getToken())
    console.log('- User:', authService.getCurrentUser())
    console.log('- Token length:', authService.getToken()?.length)
  }, [])

  const handleCreateTable = async (e: React.FormEvent) => {
    e.preventDefault()
    try {
      if (isLobbyConnected) {
        await createTableSignalR(createForm)
      } else {
        await createTableMutation.mutateAsync(createForm)
      }
      setShowCreateForm(false)
      setCreateForm({ name: '', minBet: 10, maxBet: 100, maxPlayers: 6 })
      refetch()
    } catch (err) {
      console.error('Error creating table:', err)
    }
  }

  const handleJoinTable = async (tableId: string) => {
    try {
      // Navegar directamente a la mesa de juego
      navigate({ to: '/game/$tableId', params: { tableId } })
    } catch (err) {
      console.error('Error joining table:', err)
      alert('Error al unirse a la mesa')
    }
  }

  const handleUpdateForm = (field: keyof CreateTableRequest, value: string | number) => {
    setCreateForm(prev => ({
      ...prev,
      [field]: value
    }))
  }

  const getStatusColor = (status: string) =>
    status === 'InProgress'
      ? 'bg-red-100 text-red-800'
      : status === 'Waiting'
      ? 'bg-green-100 text-green-800'
      : 'bg-gray-100 text-gray-800'

  const getStatusText = (status: string) =>
    status === 'InProgress' ? 'En juego'
    : status === 'Waiting'  ? 'Esperando jugadores'
    : status === 'Finished' ? 'Terminada'
    : status

  const getConnectionColor = (state: string) =>
    state === 'Connected'
      ? 'text-green-600'
      : state === 'Connecting' || state === 'Reconnecting'
      ? 'text-yellow-600'
      : 'text-red-600'

  const getConnectionIcon = (state: string) =>
    state === 'Connected' ? '🟢'
    : state === 'Connecting' || state === 'Reconnecting' ? '🟡'
    : '🔴'

  return (
    <div className="max-w-6xl mx-auto">
      <div className="text-center mb-8">
        <h1 className="text-4xl font-bold text-white mb-2">Lobby del Casino</h1>
        <p className="text-casino-gold-400 text-lg">
          Bienvenido de vuelta, {user?.displayName || user?.email}
        </p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Lista de mesas */}
        <div className="lg:col-span-2">
          <div className="bg-white rounded-lg shadow-xl p-6">
            <div className="flex justify-between items-center mb-6">
              <h2 className="text-2xl font-semibold text-gray-800">Mesas Disponibles</h2>
              <div className="flex gap-2">
                <button
                  onClick={() => refetch()}
                  className="bg-gray-500 text-white px-3 py-2 rounded hover:bg-gray-600 transition-colors text-sm"
                  disabled={isLoading}
                >
                  {isLoading ? 'Cargando...' : 'Refrescar'}
                </button>
                <button
                  onClick={() => setShowCreateForm(true)}
                  className="bg-casino-green-600 text-white px-4 py-2 rounded hover:bg-casino-green-700 transition-colors"
                >
                  + Nueva Mesa
                </button>
              </div>
            </div>

            {/* Estado SignalR */}
            <div className="mb-4 p-3 bg-gray-50 rounded-lg">
              <div className="flex justify-between items-center">
                <div className="flex items-center gap-2">
                  <span>{getConnectionIcon(connectionState)}</span>
                  <span className={`text-sm font-medium ${getConnectionColor(connectionState)}`}>
                    SignalR: {connectionState}
                  </span>
                </div>
                <div className="flex gap-2">
                  {!isConnected && !isConnecting && (
                    <button onClick={connect} className="text-xs bg-blue-500 text-white px-2 py-1 rounded hover:bg-blue-600">
                      Conectar
                    </button>
                  )}
                  {isConnected && (
                    <button onClick={disconnect} className="text-xs bg-red-500 text-white px-2 py-1 rounded hover:bg-red-600">
                      Desconectar
                    </button>
                  )}
                </div>
              </div>
              <div className="text-xs text-gray-600 mt-1">
                Lobby: {isLobbyConnected ? '✅ Conectado' : '❌ Desconectado'} ·
                Game: {isGameConnected ? '✅ Conectado' : '❌ Desconectado'}
              </div>
            </div>

            {error && <div className="mb-4 p-3 bg-red-100 border border-red-400 text-red-700 rounded">
              Error al cargar las mesas: {(error as any).message}
            </div>}

            {isLoading && (
              <div className="flex justify-center py-8">
                <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-casino-green-600"></div>
              </div>
            )}

            {tables && (
              <div className="space-y-4">
                {tables.map((table) => (
                  <div
                    key={table.id}
                    className={`border rounded-lg p-4 cursor-pointer transition-colors ${
                      selectedTable === table.id
                        ? 'border-casino-green-500 bg-casino-green-50'
                        : 'border-gray-200 hover:border-gray-300'
                    }`}
                    onClick={() => setSelectedTable(table.id)}
                  >
                    <div className="flex justify-between items-start">
                      <div>
                        <h3 className="font-semibold text-gray-800">{table.name}</h3>
                        <p className="text-sm text-gray-600">{table.playerCount}/{table.maxPlayers} jugadores</p>
                        <p className="text-sm text-gray-600">Apuesta: ${table.minBet} - ${table.maxBet}</p>
                      </div>
                      <div className="text-right">
                        <span className={`px-2 py-1 rounded text-xs ${getStatusColor(table.status)}`}>
                          {getStatusText(table.status)}
                        </span>
                      </div>
                    </div>

                    {selectedTable === table.id && (
                      <div className="mt-4 pt-4 border-t flex gap-2">
                        <button
                          onClick={(e) => { e.stopPropagation(); handleJoinTable(table.id) }}
                          disabled={table.playerCount >= table.maxPlayers}
                          className="bg-casino-green-600 text-white px-4 py-2 rounded hover:bg-casino-green-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                        >
                          Unirse
                        </button>
                        <button className="bg-gray-200 text-gray-800 px-4 py-2 rounded hover:bg-gray-300 transition-colors">
                          Observar
                        </button>
                      </div>
                    )}
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>

        {/* Panel lateral: Perfil */}
        <div className="space-y-6">
          <div className="bg-white rounded-lg shadow-xl p-6">
            <h3 className="text-lg font-semibold text-gray-800 mb-4">Tu Perfil</h3>
            <div className="space-y-3">
              <div className="flex justify-between"><span className="text-gray-600">Usuario:</span><span className="font-medium">{user?.displayName}</span></div>
              <div className="flex justify-between"><span className="text-gray-600">Email:</span><span className="font-medium text-sm">{user?.email}</span></div>
              <div className="flex justify-between"><span className="text-gray-600">Saldo:</span><span className="font-bold text-casino-green-600">$5,000.00</span></div>
            </div>
          </div>
        </div>
      </div>

      {/* Modal crear mesa */}
      {showCreateForm && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center p-4 z-50">
          <div className="bg-white rounded-lg p-6 w-full max-w-md">
            <h3 className="text-lg font-semibold mb-4">Crear Nueva Mesa</h3>
            <div>
              <div className="space-y-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Nombre de la mesa
                  </label>
                  <input
                    type="text"
                    value={createForm.name}
                    onChange={(e) => handleUpdateForm('name', e.target.value)}
                    className="w-full border border-gray-300 rounded px-3 py-2 text-gray-900"
                    placeholder="Mi Mesa de BlackJack"
                    required
                  />
                </div>

                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <label className="block text-sm font-medium text-gray-700 mb-1">
                      Apuesta Mínima
                    </label>
                    <input
                      type="number"
                      value={createForm.minBet}
                      onChange={(e) => handleUpdateForm('minBet', Number(e.target.value))}
                      className="w-full border border-gray-300 rounded px-3 py-2 text-gray-900"
                      min="1"
                      required
                    />
                  </div>

                  <div>
                    <label className="block text-sm font-medium text-gray-700 mb-1">
                      Apuesta Máxima
                    </label>
                    <input
                      type="number"
                      value={createForm.maxBet}
                      onChange={(e) => handleUpdateForm('maxBet', Number(e.target.value))}
                      className="w-full border border-gray-300 rounded px-3 py-2 text-gray-900"
                      min={createForm.minBet}
                      required
                    />
                  </div>
                </div>

                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Máximo de jugadores
                  </label>
                  <select
                    value={createForm.maxPlayers}
                    onChange={(e) => handleUpdateForm('maxPlayers', Number(e.target.value))}
                    className="w-full border border-gray-300 rounded px-3 py-2 text-gray-900"
                  >
                    <option value={2}>2 jugadores</option>
                    <option value={4}>4 jugadores</option>
                    <option value={6}>6 jugadores</option>
                  </select>
                </div>
              </div>

              <div className="flex gap-2 mt-6">
                <button
                  onClick={handleCreateTable}
                  disabled={createTableMutation.isPending || !createForm.name.trim()}
                  className="flex-1 bg-casino-green-600 text-white py-2 rounded hover:bg-casino-green-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  {createTableMutation.isPending ? 'Creando...' : 'Crear Mesa'}
                </button>
                <button
                  onClick={() => setShowCreateForm(false)}
                  className="flex-1 bg-gray-300 text-gray-800 py-2 rounded hover:bg-gray-400 transition-colors"
                >
                  Cancelar
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}