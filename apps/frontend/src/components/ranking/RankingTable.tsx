import type { RankingItem } from '../../shared/models/game';

export default function RankingTable({ items }: { items: RankingItem[] }) {
  return (
    <table className="w-full border-collapse border">
      <thead className="bg-gray-100">
        <tr>
          <th className="border p-2">Posición</th>
          <th className="border p-2">Usuario</th>
          <th className="border p-2">Puntos</th>
        </tr>
      </thead>
      <tbody>
        {items.map((item) => (
          <tr key={item.userId}>
            <td className="border p-2">{item.position}</td>
            <td className="border p-2">{item.username}</td>
            <td className="border p-2">{item.points}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
