import type { Hand } from "../../shared/models/events";

export default function GameBoard({ hands }: { hands: Hand[] }) {
  const dealer = hands.find((h) => h.isDealer);
  const players = hands.filter((h) => !h.isDealer);

  return (
    <div className="rounded-2xl p-4 border">
      <div className="mb-4">
        <h2 className="font-semibold">Crupier</h2>
        <HandView hand={dealer} />
      </div>
      <div className="grid md:grid-cols-2 gap-3">
        {players.map((h) => (
          <HandView key={h.id} hand={h} />
        ))}
      </div>
    </div>
  );
}

function HandView({ hand }: { hand?: Hand }) {
  if (!hand) return <div className="italic text-sm text-gray-500">Sin cartas aún</div>;
  return (
    <div className={`rounded-xl border p-3 ${hand.bust ? "border-red-500" : ""}`}>
      <div className="flex gap-2 flex-wrap">
        {hand.cards.map((c, i) => (
          <span key={i} className="px-2 py-1 border rounded bg-white">
            {c.rank}
            {c.suit}
          </span>
        ))}
      </div>
      <div className="text-sm mt-2">
        Total: <b>{hand.total}</b> {hand.bust && "(Bust)"}
      </div>
    </div>
  );
}
