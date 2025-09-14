import type { PropsWithChildren } from "react";
import { useState } from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

// Exportación nombrada (para `import { AppProviders } from ...`)
export function AppProviders({ children }: PropsWithChildren) {
  const [client] = useState(() => new QueryClient());
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

// Exportación por defecto (para `import AppProviders from ...`)
export default AppProviders;
