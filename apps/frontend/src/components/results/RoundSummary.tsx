import type { RoundResult } from "../../shared/models/events";
import HandResultList from "./HandResultList";

export default function RoundSummary({ result }: { result: RoundResult }) {
  const totalDelta = result.results.reduce((acc, r) => acc + r.delta, 0);
  return (
    <section className="rounded-xl border p-4">
      <h2 className="text-lg font-semibold">Resultados ronda #{result.roundId}</h2>
      <p className="text-sm mt-1">Variación total (todos los jugadores): <b>{totalDelta}</b></p>
      <HandResultList items={result.results} />
    </section>
  );
}
