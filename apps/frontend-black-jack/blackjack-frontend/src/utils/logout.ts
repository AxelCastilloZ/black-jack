// src/utils/logout.ts - Helper para logout mejorado
import { authService } from '../services/auth'
import { signalRService } from '../services/signalr'

export const handleLogout = async () => {
  try {
    // Desconectar SignalR antes de cerrar sesión
    await signalRService.stopConnections()
    
    // Cerrar sesión
    authService.logout()
    
    // Redirigir se maneja automáticamente en authService.logout()
  } catch (error) {
    console.error('Error during logout:', error)
    // Aun así, cerrar sesión para evitar quedarse en un estado inconsistente
    authService.logout()
  }
}