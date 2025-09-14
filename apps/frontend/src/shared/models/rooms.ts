// Tipos para Salas (Lobby)

export type RoomStatus = 'WAITING' | 'IN_PROGRESS' | 'FINISHED';

export interface Room {
  id: string;           // UUID o numérico representado como string
  code: string;         // Código público para unirse (ej. ABC123)
  name: string;
  status: RoomStatus;
  playersCount: number;
  maxPlayers: number;
  hostId: string;
  createdAt: string;    // ISO date string
}

export interface CreateRoomDto {
  name: string;
  maxPlayers?: number;  // opcional si el backend define un default
}
