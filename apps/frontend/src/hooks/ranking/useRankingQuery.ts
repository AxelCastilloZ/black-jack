import { useQuery } from '@tanstack/react-query';
import { getRanking } from '../../shared/api/game.api';
import type { RankingItem } from '../../shared/models/game';

export function useRankingQuery(roomCode: string) {
  return useQuery<RankingItem[]>({
    queryKey: ['ranking', roomCode],
    queryFn: () => getRanking(roomCode),
    staleTime: 1000 * 10,
    refetchInterval: 10000, // refresco cada 10s
  });
}
