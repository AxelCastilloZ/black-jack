import { useParams } from '@tanstack/react-router';
import { useRankingQuery } from '../../hooks/ranking/useRankingQuery';
import RankingTable from '../../components/ranking/RankingTable';

export default function RankingPage() {
  const { code } = useParams({ from: '/rooms/$code/ranking' });
  const { data, isLoading } = useRankingQuery(code);

  if (isLoading) return <p>Cargando ranking...</p>;

  return (
    <div className="p-6">
      <h1 className="text-2xl font-bold mb-4">Ranking — Sala {code}</h1>
      <RankingTable items={data ?? []} />
    </div>
  );
}
