// src/pages/GamePage.tsx - PRUEBA SIMPLE
import React from 'react'
import { useParams, useNavigate } from '@tanstack/react-router'

export default function GamePage() {
  const { tableId } = useParams({ strict: false }) as { tableId: string }
  const navigate = useNavigate()

  console.log('GamePage renderizado con tableId:', tableId)

  return (
    <div className="w-full h-screen bg-red-500 flex items-center justify-center">
      <div className="bg-white p-8 rounded-lg shadow-lg text-center">
        <h1 className="text-3xl font-bold text-gray-800 mb-4">
          GamePage Funciona! 🎮
        </h1>
        <p className="text-gray-600 mb-4">
          Table ID: <span className="font-mono bg-gray-100 px-2 py-1 rounded">{tableId}</span>
        </p>
        <button
          onClick={() => navigate({ to: '/lobby' })}
          className="bg-blue-500 hover:bg-blue-600 text-white px-6 py-2 rounded transition-colors"
        >
          ← Ir al Lobby
        </button>
      </div>
    </div>
  )
}