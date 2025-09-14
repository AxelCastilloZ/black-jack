// Eventos C→S (cliente → servidor)
export const CS = {
  CONNECT: "connect",
  DISCONNECT: "disconnect",
  JOIN_ROOM: "room:join",
  PLACE_BET: "bet:place",
  ACTION: "action:do",           // hit | stand | double | split | surrender
  REQUEST_STATE: "state:request",
} as const;

// Eventos S→C (servidor → cliente)
export const SC = {
  ROOM_PLAYERS: "room:players",
  DEAL_UPDATE: "deal:update",
  TURN_UPDATE: "turn:update",
  HAND_UPDATE: "hand:update",
  STATE_UPDATE: "state:update",
  ROUND_RESULT: "round:result",
  BALANCE_UPDATE: "balance:update",
  NOTIFICATION: "notification",
} as const;

export type ClientEvent = typeof CS[keyof typeof CS];
export type ServerEvent = typeof SC[keyof typeof SC];
