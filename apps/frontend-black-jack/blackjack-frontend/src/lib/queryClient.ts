import { QueryClient } from '@tanstack/react-query'

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // Tiempo que los datos se consideran "frescos"
      staleTime: 5 * 60 * 1000, // 5 minutos
      // Tiempo que los datos se mantienen en caché
      gcTime: 10 * 60 * 1000, // 10 minutos (antes cacheTime)
      // Reintentos automáticos
      retry: (failureCount, error: any) => {
        // No reintentar errores de autenticación
        if (error?.response?.status === 401) return false
        // Reintentar hasta 3 veces para otros errores
        return failureCount < 3
      },
      // Refetch cuando la ventana vuelve a tener foco
      refetchOnWindowFocus: false,
    },
    mutations: {
      // Reintentos para mutations
      retry: 1,
    },
  },
})