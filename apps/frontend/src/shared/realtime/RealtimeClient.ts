import { CS, SC } from "./event-names";
import type {
  ActionName,
  ActionPayload,
  JoinRoomPayload,
  PlaceBetPayload,
  RoomID,
  // payloads S→C
  RoomPlayersEvent,
  DealUpdate,
  TurnUpdate,
  HandUpdate,
  StateUpdate,
  RoundResult,
  BalanceUpdate,
  NotificationEvent,
} from "../models/events";

// Tipado de payload por evento S→C
type ServerPayloadMap = {
  [SC.ROOM_PLAYERS]: RoomPlayersEvent;
  [SC.DEAL_UPDATE]: DealUpdate;
  [SC.TURN_UPDATE]: TurnUpdate;
  [SC.HAND_UPDATE]: HandUpdate;
  [SC.STATE_UPDATE]: StateUpdate;
  [SC.ROUND_RESULT]: RoundResult;
  [SC.BALANCE_UPDATE]: BalanceUpdate;
  [SC.NOTIFICATION]: NotificationEvent;
};

type AnyHandler = (data: unknown) => void;

export class RealtimeClient {
  private static _instance?: RealtimeClient;
  private ws?: WebSocket;
  private url: string;                // puede estar vacío si no hay VITE_WS_URL
  private handlers = new Map<keyof ServerPayloadMap, Set<AnyHandler>>();
  private isOpen = false;

  private constructor(url: string) {
    this.url = url ?? "";
  }

  static get instance() {
    if (!this._instance) {
      // ⚠️ No lanzar error aquí: si no hay env, dejamos deshabilitado y evitamos pantallazo blanco
      const url = (import.meta as any)?.env?.VITE_WS_URL ?? "";
      this._instance = new RealtimeClient(url);
    }
    return this._instance;
  }

  connect() {
    if (!this.url) {
      console.warn("[Realtime] VITE_WS_URL no configurada; WS deshabilitado.");
      return;
    }
    if (
      this.ws &&
      (this.ws.readyState === WebSocket.OPEN ||
        this.ws.readyState === WebSocket.CONNECTING)
    ) return;

    this.ws = new WebSocket(this.url);
    this.ws.onopen = () => { this.isOpen = true; };
    this.ws.onclose = () => { this.isOpen = false; };
    this.ws.onmessage = (ev) => {
      try {
        const { event, payload } = JSON.parse(ev.data) as {
          event: keyof ServerPayloadMap;
          payload: unknown;
        };
        const set = this.handlers.get(event);
        if (set) set.forEach((fn) => fn(payload));
      } catch (e) {
        console.warn("WS message parse error", e);
      }
    };
  }

  disconnect() {
    this.ws?.close();
    this.isOpen = false;
  }

  on<E extends keyof ServerPayloadMap>(event: E, handler: (data: ServerPayloadMap[E]) => void) {
    if (!this.handlers.has(event)) this.handlers.set(event, new Set());
    this.handlers.get(event)!.add(handler as AnyHandler);
  }

  off<E extends keyof ServerPayloadMap>(event: E, handler: (data: ServerPayloadMap[E]) => void) {
    this.handlers.get(event)?.delete(handler as AnyHandler);
  }

  emit(event: string, payload?: unknown) {
    if (!this.url) return; // silenciar si WS deshabilitado
    if (!this.ws || !this.isOpen) this.connect();
    const send = () => this.ws!.send(JSON.stringify({ event, payload }));
    if (this.ws!.readyState === WebSocket.OPEN) send();
    else this.ws!.addEventListener("open", () => send(), { once: true });
  }

  // Helpers C→S
  joinRoom(payload: JoinRoomPayload) { this.emit(CS.JOIN_ROOM, payload); }
  placeBet(payload: PlaceBetPayload) { this.emit(CS.PLACE_BET, payload); }
  action(roomId: RoomID, action: ActionName, handId?: string) {
    const payload: ActionPayload = { roomId, action, handId };
    this.emit(CS.ACTION, payload);
  }
  requestState(roomId: RoomID) { this.emit(CS.REQUEST_STATE, { roomId }); }
}

// ⚠️ Exporta acceso LAZY (evita instanciar en tiempo de import)
export const getRealtime = () => RealtimeClient.instance;
