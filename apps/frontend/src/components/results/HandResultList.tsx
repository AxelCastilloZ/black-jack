type Item = {
  playerId: string;
  handId: string;
  outcome: "win" | "lose" | "push" | "blackjack" | "surrender";
  delta: number;
  finalTotal: number;
};

export default function HandResultList({ items }: { items: Item[] }) {
  return (
    <ul className="mt-3 grid gap-2">
      {items.map((it, idx) => (
        <li key={idx} className="border rounded p-2 flex justify-between">
          <div>
            <div className="text-sm">Jugador <b>{it.playerId}</b> — Mano <b>{it.handId}</b></div>
            <div className="text-xs text-gray-600">Total: {it.finalTotal}</div>
          </div>
          <div className="text-sm">
            {it.outcome.toUpperCase()} {it.delta >= 0 ? `+${it.delta}` : it.delta}
          </div>
        </li>
      ))}
    </ul>
  );
}
