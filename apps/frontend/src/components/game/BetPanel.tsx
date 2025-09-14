import { useState } from "react";
import type { ActionName, RoomID } from "../../shared/models/events";
import { usePlayerActions } from "../../hooks/game/usePlayerActions";

type Props = { roomId: RoomID; allowedActions: ActionName[] };

export default function BetPanel({ roomId, allowedActions }: Props) {
  const [amount, setAmount] = useState(100);
  const { placeBet, doAction, can } = usePlayerActions(roomId);
  const actions: ActionName[] = ["hit", "stand", "double", "split", "surrender"];

  return (
    <div className="rounded-xl border p-3 flex flex-wrap items-center gap-2">
      <div className="flex items-center gap-2">
        <input
          type="number"
          value={amount}
          onChange={(e) => setAmount(+e.target.value)}
          className="border rounded px-2 py-1 w-28"
        />
        <button onClick={() => placeBet(amount)} className="px-3 py-1 rounded bg-blue-600 text-white">
          Apostar
        </button>
      </div>

      <div className="ml-auto flex gap-2">
        {actions.map((a) => (
          <button
            key={a}
            disabled={!can(a)}
            onClick={() => doAction(a)}
            className={`px-3 py-1 rounded border ${
              allowedActions.includes(a) ? "bg-green-100" : "opacity-40 cursor-not-allowed"
            }`}
          >
            {a}
          </button>
        ))}
      </div>
    </div>
  );
}
