import { useQuery } from "@tanstack/react-query";
import type { GameState, RoomID } from "../../shared/models/events";

export const gameKeys = {
  state: (roomId: RoomID) => ["gameState", roomId] as const,
  turn: (roomId: RoomID) => ["turn", roomId] as const,
  players: (roomId: RoomID) => ["players", roomId] as const,
};

// REST inicial para hidratar
async function getGameState(roomId: RoomID): Promise<GameState> {
  const res = await fetch(`${import.meta.env.VITE_API_URL}/rooms/${roomId}/state`);
  if (!res.ok) throw new Error("No se pudo obtener el estado");
  return res.json();
}

export function useGameStateQuery(roomId: RoomID, enabled = true) {
  return useQuery({
    queryKey: gameKeys.state(roomId),
    queryFn: () => getGameState(roomId),
    enabled: !!roomId && enabled,
    staleTime: 1000,
  });
}
