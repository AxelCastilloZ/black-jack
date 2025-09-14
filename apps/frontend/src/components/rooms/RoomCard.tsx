import type { Room } from '../../shared/models/rooms';

export default function RoomCard({ room }: { room: Room }) {
  return (
    <div className="border p-4 rounded shadow">
      <h2 className="font-bold text-lg">{room.name}</h2>
      <p>Código: {room.code}</p>
      <p>
        Jugadores: {room.playersCount}/{room.maxPlayers}
      </p>
      <p>Estado: {room.status}</p>
    </div>
  );
}
