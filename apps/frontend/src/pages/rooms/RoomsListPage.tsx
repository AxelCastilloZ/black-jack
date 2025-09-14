import { useRoomsQuery } from '../../hooks/rooms/useRoomsQuery';
import RoomCard from '../../components/rooms/RoomCard';
import JoinRoomForm from '../../components/rooms/JoinRoomForm';
import { Link } from '@tanstack/react-router';

export default function RoomsListPage() {
  const { data: rooms, isLoading } = useRoomsQuery();

  if (isLoading) return <p>Cargando salas...</p>;

  return (
    <div className="p-6">
      <div className="flex justify-between mb-4">
        <h1 className="text-2xl font-bold">Salas disponibles</h1>
        <Link to="/rooms/create" className="bg-green-500 text-white px-3 py-1 rounded">
          Crear sala
        </Link>
      </div>

      <div className="grid gap-4">
        {rooms?.map((room) => (
          <RoomCard key={room.id} room={room} />
        ))}
      </div>

      <div className="mt-6">
        <JoinRoomForm />
      </div>
    </div>
  );
}
