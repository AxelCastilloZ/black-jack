// Tipos para Estado de Juego y Ranking

export interface RankingItem {
  position: number;   // 1, 2, 3, ...
  userId: string;
  username: string;
  points: number;     // puntaje acumulado
}

export type GameStage = 'LOBBY' | 'PLAYING' | 'ENDED';

export interface PlayerState {
  userId: string;
  username: string;
  score: number;
  isHost?: boolean;
}

export interface GameStateView {
  roomCode: string;
  stage: GameStage;
  round: number;
  players: PlayerState[];
  updatedAt: string;  // ISO date string
}
