import { useEffect, useState } from "react";
import type { TurnUpdate } from "../../shared/models/events";

export default function TurnIndicator({
  turn,
}: {
  turn?: Omit<TurnUpdate, "roomId">;
}) {
  const [left, setLeft] = useState<number | null>(null);

  useEffect(() => {
    if (!turn?.turnEndsAt) {
      setLeft(null);
      return;
    }
    const ends = new Date(turn.turnEndsAt).getTime();
    const id = setInterval(() => setLeft(Math.max(0, ends - Date.now())), 250);
    return () => clearInterval(id);
  }, [turn?.turnEndsAt]);

  if (!turn) return <div className="text-sm text-gray-500">Esperando turno…</div>;
  return (
    <div className="text-sm">
      Turno de <b>{turn.playerId}</b>
      {left != null && <> — {Math.ceil(left / 1000)}s</>}
    </div>
  );
}
