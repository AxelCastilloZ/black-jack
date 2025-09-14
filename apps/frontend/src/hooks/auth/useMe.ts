import { useQuery } from '@tanstack/react-query';
import { me } from '../../shared/api/users.api';

export function useMe() {
  return useQuery({
    queryKey: ['me'],
    queryFn: me,
    staleTime: 1000 * 60 * 5, // 5 min
  });
}
