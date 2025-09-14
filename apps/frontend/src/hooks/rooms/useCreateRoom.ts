import { useMutation, useQueryClient } from '@tanstack/react-query';
import { createRoom } from '../../shared/api/rooms.api';
import type { Room, CreateRoomDto } from '../../shared/models/rooms';

export function useCreateRoom() {
  const queryClient = useQueryClient();

  return useMutation<Room, Error, CreateRoomDto>({
    mutationFn: createRoom,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['rooms'] });
    },
  });
}
