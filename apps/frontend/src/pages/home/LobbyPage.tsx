import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
// 👇 ajusta el import si tu archivo está en otra ruta
import { listRooms, createRoom, joinRoom } from "../../shared/api/rooms.api";
import type { Room, CreateRoomDto } from "../../shared/models/rooms";

export default function LobbyPage() {
  const nav = useNavigate();

  const [playerName, setPlayerName] = useState(
    () => localStorage.getItem("bj.playerName") ?? ""
  );
  const [roomCode, setRoomCode] = useState("");
  const [rooms, setRooms] = useState<Room[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    let ok = true;
    listRooms()
      .then((r) => ok && setRooms(r))
      .catch(() => {/* opcional: toast */});
    return () => { ok = false; };
  }, []);

  async function handleCreate() {
    try {
      setLoading(true);
      // Si tu CreateRoomDto requiere campos, complétalos aquí:
      // p. ej.: const dto: CreateRoomDto = { ownerName: playerName.trim() as any };
      const dto = {} as CreateRoomDto;
      const room = await createRoom(dto);
      if (playerName.trim()) localStorage.setItem("bj.playerName", playerName.trim());
      // Navegamos por code si existe, si no por id
      nav(`/rooms/${(room as any).code ?? (room as any).id}`);
    } catch (e: any) {
      alert(e?.message ?? "No se pudo crear la sala");
    } finally {
      setLoading(false);
    }
  }

  async function handleJoin() {
    if (!roomCode.trim()) return alert("Ingresa el código/ID de sala");
    try {
      setLoading(true);
      const room = await joinRoom(roomCode.trim());
      if (playerName.trim()) localStorage.setItem("bj.playerName", playerName.trim());
      nav(`/rooms/${(room as any).code ?? (room as any).id}`);
    } catch (e: any) {
      alert(e?.message ?? "No se pudo unir a la sala");
    } finally {
      setLoading(false);
    }
  }

  return (
    <main className="min-h-screen flex items-center justify-center p-6">
      <div className="w-full max-w-2xl space-y-6 rounded-2xl border bg-white p-6 shadow-sm">
        <h1 className="text-2xl font-semibold">Blackjack — Lobby</h1>

        <div className="grid md:grid-cols-2 gap-4">
          <label className="block">
            <span className="text-sm text-zinc-600">Tu nombre (opcional)</span>
            <input
              className="mt-1 w-full rounded-lg border px-3 py-2 outline-none focus:ring-2"
              value={playerName}
              onChange={(e) => setPlayerName(e.target.value)}
              placeholder="Ej. Melina"
            />
          </label>

          <label className="block">
            <span className="text-sm text-zinc-600">Código o ID de sala</span>
            <div className="flex gap-2 mt-1">
              <input
                className="w-full rounded-lg border px-3 py-2 outline-none focus:ring-2"
                value={roomCode}
                onChange={(e) => setRoomCode(e.target.value)}
                placeholder="Ej. ABC123 o 7f2c..."
              />
              <button
                onClick={handleJoin}
                disabled={loading}
                className="whitespace-nowrap rounded-lg border px-4 py-2 hover:bg-zinc-50 disabled:opacity-50"
              >
                Unirse
              </button>
            </div>
          </label>
        </div>

        <div className="flex items-center gap-3">
          <button
            onClick={handleCreate}
            disabled={loading}
            className="rounded-lg bg-black text-white px-4 py-2 hover:opacity-90 disabled:opacity-50"
          >
            Crear sala
          </button>
          <span className="text-sm text-zinc-600">
            o elige una de la lista
          </span>
        </div>

        <RoomsList rooms={rooms} onJoin={(code) => nav(`/rooms/${code}`)} />
      </div>
    </main>
  );
}

function RoomsList({ rooms, onJoin }: { rooms: Room[]; onJoin: (code: string) => void }) {
  if (!rooms?.length) {
    return <div className="text-sm text-zinc-500">No hay salas disponibles.</div>;
  }
  return (
    <div className="rounded-xl border">
      <table className="w-full text-sm">
        <thead>
          <tr className="bg-zinc-50">
            <th className="text-left p-2">Código</th>
            <th className="text-left p-2">Jugadores</th>
            <th className="text-left p-2">Estado</th>
            <th className="text-left p-2"></th>
          </tr>
        </thead>
        <tbody>
          {rooms.map((r, i) => (
            <tr key={i} className="border-t">
              <td className="p-2">{(r as any).code ?? (r as any).id}</td>
              <td className="p-2">{(r as any).playersCount ?? "-"}</td>
              <td className="p-2">{(r as any).status ?? "-"}</td>
              <td className="p-2">
                <button
                  onClick={() => onJoin((r as any).code ?? (r as any).id)}
                  className="rounded-lg border px-3 py-1 hover:bg-zinc-50"
                >
                  Entrar
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
