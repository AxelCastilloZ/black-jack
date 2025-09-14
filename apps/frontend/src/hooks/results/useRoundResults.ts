import { useQuery, useQueryClient } from "@tanstack/react-query";
import type { RoomID, RoundResult } from "../../shared/models/events";
import { gameKeys } from "../game/useGameStateQuery";

async function fetchRound(roomId: RoomID, roundId: string): Promise<RoundResult> {
  const res = await fetch(`${import.meta.env.VITE_API_URL}/rooms/${roomId}/rounds/${roundId}`);
  if (!res.ok) throw new Error("No se pudo cargar el resultado");
  return res.json();
}

export function useRoundResults(roomId: RoomID, roundId: string) {
  const qc = useQueryClient();
  return useQuery({
    queryKey: ["roundResult", roomId, roundId],
    queryFn: () => fetchRound(roomId, roundId),
    initialData: () => {
      const state = qc.getQueryData<any>(gameKeys.state(roomId));
      return state?.lastRoundResult?.roundId === roundId ? state.lastRoundResult : undefined;
    },
  });
}
