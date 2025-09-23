// src/components/game/GameChat.tsx
import React, { useState, useCallback, useRef, useEffect } from 'react'
import { signalRService } from '../../services/signalr'

interface ChatMessage {
  id: string
  playerName: string
  text: string
  timestamp: string
}

interface GameChatProps {
  currentUser: any
  isComponentMounted: boolean
  roomCode?: string
}

export default function GameChat({ currentUser, isComponentMounted, roomCode }: GameChatProps) {
  const [chatMessages, setChatMessages] = useState<ChatMessage[]>([])
  const [chatInput, setChatInput] = useState('')
  const mountedRef = useRef(false)

  useEffect(() => {
    mountedRef.current = true
    const handler = (msg: { roomCode: string; playerId: string; playerName: string; text: string; timestamp: string }) => {
      if (!mountedRef.current) return
      if (roomCode && msg.roomCode !== roomCode) return
      const message: ChatMessage = {
        id: `${msg.timestamp}-${msg.playerId}`,
        playerName: msg.playerName,
        text: msg.text,
        timestamp: msg.timestamp
      }
      setChatMessages(prev => [...prev.slice(-49), message])
    }
    signalRService.onMessageReceived = handler
    return () => {
      mountedRef.current = false
      if (signalRService.onMessageReceived === handler) {
        signalRService.onMessageReceived = undefined
      }
    }
  }, [roomCode])

  const handleSendMessage = useCallback(async () => {
    const text = chatInput.trim()
    if (!text || !currentUser || !isComponentMounted || !roomCode) return
    try {
      await signalRService.sendChatMessage(roomCode, text)
      setChatInput('')
    } catch (e) {
      // Optimistic local echo on failure
      const newMessage: ChatMessage = {
        id: `${Date.now()}-local` ,
        playerName: currentUser.displayName,
        text,
        timestamp: new Date().toISOString()
      }
      setChatMessages(prev => [...prev.slice(-49), newMessage])
      setChatInput('')
    }
  }, [chatInput, currentUser, isComponentMounted, roomCode])

  const handleKeyPress = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') {
      handleSendMessage()
    }
  }

  return (
    <div className="absolute bottom-5 right-5 bg-black/80 rounded-lg p-3 min-w-[250px] max-w-[300px] text-white">
      <div className="text-amber-400 font-bold text-xs mb-2">
        Chat
      </div>
      
      {/* Messages Area */}
      <div className="max-h-[120px] overflow-y-auto mb-2">
        {chatMessages.length === 0 ? (
          <div className="text-gray-400 text-xs">Sin mensajes</div>
        ) : (
          chatMessages.slice(-5).map((msg) => (
            <div key={msg.id} className="text-xs mb-1">
              <span className="text-amber-300">{msg.playerName}:</span>
              <span className="ml-1">{msg.text}</span>
            </div>
          ))
        )}
      </div>
      
      {/* Input Area */}
      <div className="flex gap-2">
        <input
          type="text"
          value={chatInput}
          onChange={e => setChatInput(e.target.value)}
          onKeyPress={handleKeyPress}
          placeholder="Escribe mensaje..."
          className="flex-1 px-2 py-1 text-xs bg-slate-700 border border-slate-600 rounded text-white placeholder-slate-400"
          maxLength={100}
        />
        <button
          onClick={handleSendMessage}
          className="px-2 py-1 bg-emerald-600 hover:bg-emerald-700 rounded text-xs transition-colors"
        >
          →
        </button>
      </div>
    </div>
  )
}