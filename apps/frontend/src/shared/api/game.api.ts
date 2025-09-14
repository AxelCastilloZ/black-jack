import { http } from "../api/https";
import type { RankingItem, GameStateView } from "../models/game";

// Obtener ranking de una sala
export async function getRanking(roomCode: string): Promise<RankingItem[]> {
  const { data } = await http.get(`/rooms/${roomCode}/ranking`);
  return data;
}

// Obtener estado del juego (hidratación inicial)
export async function getGameState(roomCode: string): Promise<GameStateView> {
  const { data } = await http.get(`/rooms/${roomCode}/state`);
  return data;
}
