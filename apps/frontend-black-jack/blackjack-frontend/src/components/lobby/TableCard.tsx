// src/components/lobby/TableCard.tsx
import React from 'react'
import { formatMoney } from '../../utils/format'
import type { LobbyTable } from '../../services/signalr'

export default function TableCard({
  table,
  statusBadge,
  onView,
  onJoin,
}: {
  table: LobbyTable
  statusBadge: { text: string; className: string }
  onView: () => void
  onJoin: () => void
}) {
  return (
    <div className="bg-green-950/70 border border-green-900 rounded-xl px-5 py-4 shadow-lg">
      <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
        <div className="flex-1">
          <div className="flex items-center gap-3">
            <h3 className="text-xl font-bold">{table.name}</h3>
            <span
              className={`text-xs px-2 py-1 rounded-full ${statusBadge.className}`}
            >
              {statusBadge.text}
            </span>
          </div>

          <div className="mt-2 flex flex-wrap items-center gap-4 text-sm text-neutral-300">
            <span>💵 {formatMoney(table.minBet)} – {formatMoney(table.maxBet)}</span>
            <span>⏱️ Turno: 30s</span>
          </div>
        </div>

        <div className="flex items-center gap-6">
          <div className="text-sm text-neutral-300">
            <div className="flex items-center gap-2">
              <span className="opacity-80">👥</span>
              <span>{table.playerCount}/{table.maxPlayers}</span>
            </div>
          </div>

          <div className="flex gap-2">
            <button
              onClick={onView}
              className="px-4 py-2 rounded-lg bg-neutral-800 hover:bg-neutral-700 border border-neutral-700"
            >
              Ver Mesa
            </button>
            <button
              onClick={onJoin}
              className="px-4 py-2 rounded-lg bg-emerald-700 hover:bg-emerald-600"
            >
              Unirse
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}
