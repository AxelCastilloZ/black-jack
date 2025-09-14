import type { PlayerSummary } from "../../shared/models/events";

export default function PlayerInfo({
  player,
  turnPlayerId,
}: {
  player: PlayerSummary;
  turnPlayerId?: string;
}) {
  const isTurn = turnPlayerId === player.id;
  return (
    <div className={`rounded-xl border p-3 ${isTurn ? "ring-2 ring-blue-500" : ""}`}>
      <div className="font-medium">{player.name ?? player.id}</div>
      <div className="text-sm">Balance: ₡{player.balance}</div>
      {player.bet != null && <div className="text-sm">Apuesta: ₡{player.bet}</div>}
    </div>
  );
}
