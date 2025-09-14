import { useQueryClient } from "@tanstack/react-query";
import { getRealtime } from "../../shared/realtime/RealtimeClient";
import type { ActionName, RoomID } from "../../shared/models/events";
import { gameKeys } from "./useGameStateQuery";

export function usePlayerActions(roomId: RoomID) {
  const qc = useQueryClient();
  const rt = getRealtime();

  const getAllowed = () =>
    (qc.getQueryData(gameKeys.turn(roomId)) as any)?.allowedActions as ActionName[] | undefined;

  function can(action: ActionName) {
    const allowed = getAllowed();
    return !allowed || allowed.includes(action);
  }

  return {
    placeBet(amount: number) {
      rt.placeBet({ roomId, amount });
    },
    doAction(action: ActionName, handId?: string) {
      if (!can(action)) return;
      rt.action(roomId, action, handId);
    },
    can,
  };
}
