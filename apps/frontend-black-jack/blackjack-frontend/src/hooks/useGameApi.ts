import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { apiService } from '../api/apiService'

// —— Tipos de dominio (ajústalos si tu backend usa otros nombres) ——
export type TableStatus = 'Waiting' | 'InProgress' | 'Finished'

export interface GameTable {
  id: string
  name: string
  playerCount: number
  maxPlayers: number
  minBet: number
  maxBet: number
  status: TableStatus | string
}

// ✅ Exportamos este tipo: lo pide tu LobbyPage
export interface CreateTableRequest {
  name: string
  minBet: number
  maxBet: number
  maxPlayers: number
}

// Keys centralizadas
export const gameKeys = {
  all: ['game'] as const,
  tables: () => [...gameKeys.all, 'tables'] as const,
}

// GET /api/Game/tables
async function fetchTables(): Promise<GameTable[]> {
  return apiService.get<GameTable[]>('/Game/tables')
}

// POST /api/Game/tables
async function createTableApi(payload: CreateTableRequest): Promise<GameTable> {
  return apiService.post<GameTable>('/Game/tables', payload)
}

// ---- Hooks ----
export function useGameTables() {
  return useQuery({
    queryKey: gameKeys.tables(),
    queryFn: fetchTables,
    staleTime: 30_000,
    refetchOnWindowFocus: false,
  })
}

export function useCreateTable() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: createTableApi,
    onSuccess: () => {
      // Refresca la lista después de crear
      qc.invalidateQueries({ queryKey: gameKeys.tables() })
    },
  })
}
