import { useEffect } from "react";
import { useParams } from "react-router-dom";
import { getRealtime } from "../../shared/realtime/RealtimeClient";
import { useGameStateQuery } from "../../hooks/game/useGameStateQuery";
import { useGameChannel } from "../../hooks/game/useGameChannel";
import GameRoom from "../../components/game/GameRoom";

export default function GameRoomPage() {
  const { roomId = "" } = useParams<{ roomId: string }>();
  const rt = getRealtime();

  useEffect(() => {
    rt.connect();      // si no hay VITE_WS_URL, solo avisará por consola y no explota
    return () => rt.disconnect();
  }, [rt]);

  const { data: state, isFetching } = useGameStateQuery(roomId, !!roomId);
  useGameChannel(roomId);

  if (!roomId) return <p>RoomId ausente.</p>;
  if (!state && isFetching) return <p>Cargando estado…</p>;

  return <GameRoom roomId={roomId} state={state} />;
}
