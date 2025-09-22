// src/components/game/CardDisplay.tsx
import React from 'react'

interface Card {
  suit: 'Hearts' | 'Diamonds' | 'Clubs' | 'Spades'
  rank: 'Ace' | '2' | '3' | '4' | '5' | '6' | '7' | '8' | '9' | '10' | 'Jack' | 'Queen' | 'King'
  value: number
}

interface CardDisplayProps {
  cards: Card[]
  isDealer?: boolean
  showAllCards?: boolean
  handValue?: number
  className?: string
}

const getCardSymbol = (suit: string) => {
  switch (suit) {
    case 'Hearts': return '♥'
    case 'Diamonds': return '♦'
    case 'Clubs': return '♣'
    case 'Spades': return '♠'
    default: return '?'
  }
}

const getCardColor = (suit: string) => {
  return suit === 'Hearts' || suit === 'Diamonds' ? 'text-red-600' : 'text-black'
}

const getCardDisplay = (rank: string) => {
  switch (rank) {
    case 'Ace': return 'A'
    case 'Jack': return 'J'
    case 'Queen': return 'Q'
    case 'King': return 'K'
    default: return rank
  }
}

export default function CardDisplay({ 
  cards, 
  isDealer = false, 
  showAllCards = true, 
  handValue = 0,
  className = '' 
}: CardDisplayProps) {
  const displayCards = isDealer && !showAllCards ? cards.slice(0, 1) : cards
  const hiddenCardsCount = isDealer && !showAllCards ? cards.length - 1 : 0

  return (
    <div className={`flex flex-col items-center ${className}`}>
      {/* Cards Container */}
      <div className="flex gap-1 mb-2">
        {displayCards.map((card, index) => (
          <div
            key={index}
            className="w-12 h-16 bg-white border border-gray-300 rounded-md flex flex-col items-center justify-center text-xs font-bold shadow-sm"
          >
            <div className={`${getCardColor(card.suit)} -mt-1`}>
              {getCardDisplay(card.rank)}
            </div>
            <div className={`${getCardColor(card.suit)} text-lg`}>
              {getCardSymbol(card.suit)}
            </div>
          </div>
        ))}
        
        {/* Hidden cards for dealer */}
        {hiddenCardsCount > 0 && (
          <div className="w-12 h-16 bg-blue-600 border border-blue-700 rounded-md flex items-center justify-center text-white text-xs font-bold shadow-sm">
            ?
          </div>
        )}
      </div>

      {/* Hand Value */}
      {handValue > 0 && (
        <div className="text-white text-sm font-bold bg-black/50 px-2 py-1 rounded">
          {handValue}
        </div>
      )}

      {/* Card Count */}
      {cards.length > 0 && (
        <div className="text-gray-300 text-xs">
          {cards.length} carta{cards.length !== 1 ? 's' : ''}
        </div>
      )}
    </div>
  )
}

