import { useParams, Link } from "react-router-dom";
import { useRoundResults } from "../../hooks/results/useRoundResults";
import RoundSummary from "../../components/results/RoundSummary";

export default function ResultsPage() {
  const { roomId = "", roundId = "" } = useParams<{ roomId: string; roundId: string }>();
  const { data, isLoading, error } = useRoundResults(roomId, roundId);

  if (isLoading) return <p>Cargando resultados…</p>;
  if (error || !data) return <p>No se pudieron obtener resultados.</p>;

  return (
    <div className="p-4">
      <RoundSummary result={data} />
      <div className="mt-4">
        <Link className="text-blue-600 underline" to={`/rooms/${roomId}`}>Volver a la mesa</Link>
      </div>
    </div>
  );
}
