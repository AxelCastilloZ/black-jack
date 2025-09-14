import { useState } from 'react';
import { useCreateRoom } from '../../hooks/rooms/useCreateRoom';
import { useNavigate } from '@tanstack/react-router';

export default function RoomCreatePage() {
  const [name, setName] = useState('');
  const [maxPlayers, setMaxPlayers] = useState(4);
  const createRoomMutation = useCreateRoom();
  const navigate = useNavigate();

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    createRoomMutation.mutate(
      { name, maxPlayers },
      {
        onSuccess: (room) => {
          navigate({ to: `/rooms/${room.code}/ranking` });
        },
      }
    );
  };

  return (
    <div className="p-6">
      <h1 className="text-xl font-bold mb-4">Crear sala</h1>
      <form onSubmit={handleSubmit} className="space-y-4">
        <input
          type="text"
          placeholder="Nombre de la sala"
          className="border p-2 w-full"
          value={name}
          onChange={(e) => setName(e.target.value)}
        />

        <input
          type="number"
          placeholder="Máx. jugadores"
          className="border p-2 w-full"
          value={maxPlayers}
          onChange={(e) => setMaxPlayers(Number(e.target.value))}
        />

        <button
          type="submit"
          className="bg-blue-500 text-white px-4 py-2 rounded"
          disabled={createRoomMutation.isPending}
        >
          {createRoomMutation.isPending ? 'Creando...' : 'Crear sala'}
        </button>

        {createRoomMutation.isError && (
          <p className="text-red-500 mt-2 text-sm">Error al crear la sala</p>
        )}

      </form>
    </div>
  );
}
