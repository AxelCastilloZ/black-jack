import { useEffect } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { SC } from "../../shared/realtime/event-names";
import { getRealtime } from "../../shared/realtime/RealtimeClient";
import type {
  RoomID,
  RoomPlayersEvent,
  DealUpdate,
  TurnUpdate,
  HandUpdate,
  StateUpdate,
  RoundResult,
  BalanceUpdate,
} from "../../shared/models/events";
import { gameKeys } from "./useGameStateQuery";

export function useGameChannel(roomId: RoomID | undefined) {
  const qc = useQueryClient();

  useEffect(() => {
    if (!roomId) return;
    const rt = getRealtime();

    const onState = (p: StateUpdate) => {
      qc.setQueryData(gameKeys.state(roomId), (prev?: StateUpdate) => ({ ...prev, ...p }));
      if (p.turn) qc.setQueryData(gameKeys.turn(roomId), p.turn);
      if (p.players) qc.setQueryData(gameKeys.players(roomId), p.players);
    };

    const onPlayers = (p: RoomPlayersEvent) => {
      qc.setQueryData(gameKeys.players(roomId), p.players);
    };

    const onDeal = (p: DealUpdate) => {
      qc.setQueryData(gameKeys.state(roomId), (prev: any) => ({ ...(prev ?? {}), hands: p.hands }));
    };

    const onTurn = (p: TurnUpdate) => {
      qc.setQueryData(gameKeys.turn(roomId), p);
    };

    const onHand = (p: HandUpdate) => {
      qc.setQueryData(gameKeys.state(roomId), (prev: any) => {
        const hands = [...(prev?.hands ?? [])];
        const idx = hands.findIndex((h: any) => h.id === p.hand.id);
        if (idx >= 0) hands[idx] = p.hand; else hands.push(p.hand);
        return { ...(prev ?? {}), hands };
      });
    };

    const onBalance = (p: BalanceUpdate) => {
      qc.setQueryData(gameKeys.players(roomId), (prev: any[] = []) =>
        prev.map((pl) => (pl.id === p.playerId ? { ...pl, balance: p.balance } : pl))
      );
    };

    const onRound = (p: RoundResult) => {
      qc.setQueryData(gameKeys.state(roomId), (prev: any) => ({ ...(prev ?? {}), lastRoundResult: p }));
    };

    rt.on(SC.STATE_UPDATE, onState);
    rt.on(SC.ROOM_PLAYERS, onPlayers);
    rt.on(SC.DEAL_UPDATE, onDeal);
    rt.on(SC.TURN_UPDATE, onTurn);
    rt.on(SC.HAND_UPDATE, onHand);
    rt.on(SC.BALANCE_UPDATE, onBalance);
    rt.on(SC.ROUND_RESULT, onRound);

    // rehidratación al montar
    rt.requestState(roomId);

    return () => {
      rt.off(SC.STATE_UPDATE, onState);
      rt.off(SC.ROOM_PLAYERS, onPlayers);
      rt.off(SC.DEAL_UPDATE, onDeal);
      rt.off(SC.TURN_UPDATE, onTurn);
      rt.off(SC.HAND_UPDATE, onHand);
      rt.off(SC.BALANCE_UPDATE, onBalance);
      rt.off(SC.ROUND_RESULT, onRound);
    };
  }, [roomId, qc]);
}
