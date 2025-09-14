import { useQuery } from '@tanstack/react-query';
import { listRooms } from '../../shared/api/rooms.api';

export function useRoomsQuery() {
  return useQuery({
    queryKey: ['rooms'],
    queryFn: listRooms,
    staleTime: 1000 * 60,
  });
}
