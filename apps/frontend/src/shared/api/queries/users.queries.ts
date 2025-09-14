import { queryOptions } from '@tanstack/react-query';
import { me } from '../users.api';
import type { User } from '../../models/users';

export const meQuery = queryOptions<User>({
  queryKey: ['me'],
  queryFn: me,
  staleTime: 1000 * 60 * 5, // 5 min
});
