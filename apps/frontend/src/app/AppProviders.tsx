import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Router, RouterProvider } from '@tanstack/react-router'
import { router } from './router';
import { Toaster } from "react-hot-toast";

export const queryClient = new QueryClient();

export function AppProviders() {
  return (
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
      <Toaster position="top-right" />
    </QueryClientProvider>
  );
}

