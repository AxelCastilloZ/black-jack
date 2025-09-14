import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import GameRoomPage from "./pages/game/GameRoomPage";
import ResultsPage from "./pages/results/ResultsPage";

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<Navigate to="/rooms/demo" replace />} />
        <Route path="/rooms/:roomId" element={<GameRoomPage />} />
        <Route path="/rooms/:roomId/results/:roundId" element={<ResultsPage />} />
        <Route path="*" element={<div style={{padding:16}}>Ruta no encontrada</div>} />
      </Routes>
    </BrowserRouter>
  );
}
