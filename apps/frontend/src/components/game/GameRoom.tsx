import GameBoard from "./GameBoard";
import PlayerInfo from "./PlayerInfo";
import TurnIndicator from "./TurnIndicator";
import BetPanel from "./BetPanel";
import type { GameState, RoomID } from "../../shared/models/events";

type Props = { roomId: RoomID; state?: GameState };

export default function GameRoom({ roomId, state }: Props) {
  const players = state?.players ?? [];
  const turn = state?.turn;

  return (
    <div className="grid gap-4 p-4">
      <header className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">Mesa {roomId}</h1>
        <TurnIndicator turn={turn} />
      </header>

      <GameBoard hands={state?.hands ?? []} />

      <section className="grid md:grid-cols-2 gap-3">
        {players.map((p) => (
          <PlayerInfo key={p.id} player={p} turnPlayerId={turn?.playerId} />
        ))}
      </section>

      <BetPanel roomId={roomId} allowedActions={turn?.allowedActions ?? []} />

      {state?.lastRoundResult && (
        <a
          className="text-blue-600 underline"
          href={`/rooms/${roomId}/results/${state.lastRoundResult.roundId}`}
        >
          Ver resultados de la ronda
        </a>
      )}
    </div>
  );
}
