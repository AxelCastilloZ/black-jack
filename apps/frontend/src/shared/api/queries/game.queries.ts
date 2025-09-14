import { queryOptions } from '@tanstack/react-query';
import { getRanking, getGameState } from '../game.api';
import type { RankingItem, GameStateView } from '../../models/game';

export const rankingQuery = (roomCode: string) =>
  queryOptions<RankingItem[]>({
    queryKey: ['ranking', roomCode],
    queryFn: () => getRanking(roomCode),
    staleTime: 1000 * 10, // 10s
  });

export const gameStateQuery = (roomCode: string) =>
  queryOptions<GameStateView>({
    queryKey: ['gameState', roomCode],
    queryFn: () => getGameState(roomCode),
    staleTime: 1000 * 5, // 5s
  });
