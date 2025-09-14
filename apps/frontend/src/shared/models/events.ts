export type PlayerID = string;
export type RoomID = string;

/* C → S */
export type JoinRoomPayload = { roomId: RoomID; playerName?: string };
export type PlaceBetPayload = { roomId: RoomID; amount: number };
export type ActionName = "hit" | "stand" | "double" | "split" | "surrender";
export type ActionPayload = { roomId: RoomID; action: ActionName; handId?: string };

/* Estado y entidades */
export type PlayerSummary = { id: PlayerID; name: string; balance: number; bet?: number };

export type Card = { rank: string; suit: "♠" | "♥" | "♦" | "♣"; value: number };
export type Hand = { id: string; cards: Card[]; total: number; bust: boolean; isDealer?: boolean };

/* S → C */
export type RoomPlayersEvent = { roomId: RoomID; players: PlayerSummary[] };
export type DealUpdate = { roomId: RoomID; hands: Hand[] };
export type TurnUpdate = {
  roomId: RoomID;
  playerId: PlayerID;
  handId?: string;
  turnEndsAt?: string; // ISO
  allowedActions: ActionName[];
};
export type HandUpdate = { roomId: RoomID; hand: Hand };

export type StateUpdate = {
  roomId: RoomID;
  roundId: string;
  hands: Hand[];
  players: PlayerSummary[];
  turn?: Omit<TurnUpdate, "roomId">;
  lastRoundResult?: RoundResult; // opcional: último resultado cacheado
};

export type RoundResult = {
  roomId: RoomID;
  roundId: string;
  results: Array<{
    playerId: PlayerID;
    handId: string;
    outcome: "win" | "lose" | "push" | "blackjack" | "surrender";
    delta: number;      // variación de balance
    finalTotal: number; // total de la mano al finalizar
  }>;
};

export type BalanceUpdate = { roomId: RoomID; playerId: PlayerID; balance: number };
export type NotificationEvent = { level: "info" | "warning" | "error"; message: string };

// Alias útil en el front (clave principal de caché):
export type GameState = StateUpdate;
