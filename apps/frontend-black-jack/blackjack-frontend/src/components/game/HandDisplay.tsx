// src/components/game/HandDisplay.tsx
import React from 'react'
import CardDisplay from './CardDisplay'

interface Card {
  suit: 'Hearts' | 'Diamonds' | 'Clubs' | 'Spades'
  rank: 'Ace' | '2' | '3' | '4' | '5' | '6' | '7' | '8' | '9' | '10' | 'Jack' | 'Queen' | 'King'
  value: number
}

interface Hand {
  id: string
  cards: Card[]
  value: number
  status: 'Active' | 'Stand' | 'Bust' | 'Blackjack'
}

interface HandDisplayProps {
  hand: Hand | null
  isDealer?: boolean
  showAllCards?: boolean
  playerName?: string
  className?: string
}

export default function HandDisplay({
  hand,
  isDealer = false,
  showAllCards = true,
  playerName = '',
  className = ''
}: HandDisplayProps) {
  if (!hand || !hand.cards.length) {
    return (
      <div className={`text-center ${className}`}>
        <div className="text-gray-400 text-sm">
          {isDealer ? 'Dealer' : playerName || 'Player'}
        </div>
        <div className="text-gray-500 text-xs">No cards</div>
      </div>
    )
  }

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'Bust': return 'text-red-400'
      case 'Blackjack': return 'text-yellow-400'
      case 'Stand': return 'text-blue-400'
      default: return 'text-white'
    }
  }

  const getStatusText = (status: string) => {
    switch (status) {
      case 'Bust': return 'BUST!'
      case 'Blackjack': return 'BLACKJACK!'
      case 'Stand': return 'STAND'
      default: return 'ACTIVE'
    }
  }

  return (
    <div className={`text-center ${className}`}>
      {/* Player/Dealer Name */}
      <div className="text-white text-sm font-bold mb-2">
        {isDealer ? 'Dealer' : playerName || 'Player'}
      </div>

      {/* Cards */}
      <CardDisplay
        cards={hand.cards}
        isDealer={isDealer}
        showAllCards={showAllCards}
        handValue={hand.value}
      />

      {/* Hand Status */}
      <div className={`text-xs font-bold mt-1 ${getStatusColor(hand.status)}`}>
        {getStatusText(hand.status)}
      </div>
    </div>
  )
}

