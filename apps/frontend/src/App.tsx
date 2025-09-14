import { BrowserRouter, Routes, Route } from "react-router-dom";
import LobbyPage from "./pages/home/LobbyPage";
import GameRoomPage from "./pages/game/GameRoomPage";
import ResultsPage from "./pages/results/ResultsPage";

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<LobbyPage />} />
        <Route path="/rooms/:roomId" element={<GameRoomPage />} />
        <Route path="/rooms/:roomId/results/:roundId" element={<ResultsPage />} />
        <Route path="*" element={<div className="p-4">Ruta no encontrada</div>} />
      </Routes>
    </BrowserRouter>
  );
}
