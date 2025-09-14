import { useMutation } from '@tanstack/react-query';
import { joinRoom } from '../../shared/api/rooms.api';
import type { Room } from '../../shared/models/rooms';
import { useNavigate } from '@tanstack/react-router';

export function useJoinRoom() {
  const navigate = useNavigate();

  return useMutation<Room, Error, string>({
    mutationFn: joinRoom,
    onSuccess: (room) => {
      navigate({ to: `/rooms/${room.code}/ranking` });
    },
  });
}
