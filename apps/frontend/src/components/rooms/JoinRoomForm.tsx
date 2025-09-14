import { useState } from 'react';
import { useJoinRoom } from '../../hooks/rooms/useJoinRoom';

export default function JoinRoomForm() {
  const [code, setCode] = useState('');
  const joinMutation = useJoinRoom();

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    joinMutation.mutate(code);
  };

  return (
    <form onSubmit={handleSubmit} className="flex gap-2">
      <input
        type="text"
        placeholder="Código de sala"
        className="border p-2 flex-1"
        value={code}
        onChange={(e) => setCode(e.target.value)}
      />
      <button
        type="submit"
        className="bg-blue-500 text-white px-4 py-2 rounded"
        disabled={joinMutation.isPending}
      >
        {joinMutation.isPending ? 'Uniendo...' : 'Unirse'}
      </button>
    </form>
  );
}
