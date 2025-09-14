import { queryOptions } from '@tanstack/react-query';
import { listRooms, getRoom } from '../rooms.api';

// Lista de rooms
export const roomsQuery = queryOptions({
  queryKey: ['rooms'],
  queryFn: listRooms,
  staleTime: 1000 * 60, // 1 minuto
});

// Room individual (por código)
export const roomQuery = (code: string) =>
  queryOptions({
    queryKey: ['rooms', code],
    queryFn: () => getRoom(code),
    staleTime: 1000 * 30, // 30s
  });
