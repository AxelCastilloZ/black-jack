// src/utils/format.ts
export function formatMoney(amount: number): string {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
    minimumFractionDigits: 0,
    maximumFractionDigits: 0,
  }).format(amount)
}

export function statusBadge(status: string) {
  switch (status.toLowerCase()) {
    case 'waitingforplayers':
      return {
        text: 'Esperando',
        className: 'bg-green-600 text-white px-3 py-1 rounded-full text-xs font-semibold'
      }
    case 'inprogress':
      return {
        text: 'En Juego',
        className: 'bg-yellow-600 text-black px-3 py-1 rounded-full text-xs font-semibold'
      }
    case 'finished':
      return {
        text: 'Terminado',
        className: 'bg-gray-600 text-white px-3 py-1 rounded-full text-xs font-semibold'
      }
    default:
      return {
        text: status,
        className: 'bg-gray-600 text-white px-3 py-1 rounded-full text-xs font-semibold'
      }
  }
}

export function formatTimeAgo(timestamp: string | Date): string {
  const date = typeof timestamp === 'string' ? new Date(timestamp) : timestamp
  const now = new Date()
  const diffMs = now.getTime() - date.getTime()
  const diffMins = Math.floor(diffMs / (1000 * 60))
  const diffHours = Math.floor(diffMs / (1000 * 60 * 60))
  const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24))

  if (diffMins < 1) return 'ahora'
  if (diffMins < 60) return `hace ${diffMins}m`
  if (diffHours < 24) return `hace ${diffHours}h`
  if (diffDays < 30) return `hace ${diffDays}d`
  
  return date.toLocaleDateString()
}