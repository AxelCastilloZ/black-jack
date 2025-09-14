import {
  createRootRoute,
  createRoute,
  createRouter,
  Outlet,
} from '@tanstack/react-router';
import { queryClient } from './AppProviders';

import LoginPage from '../pages/auth/LoginPage';
import RoomsListPage from '../pages/rooms/RoomsListPage';
import RoomCreatePage from '../pages/rooms/RoomsCreatePage';
import RankingPage from '../pages/ranking/RankingPage';

import { listRooms } from '../shared/api/rooms.api';
import { getRanking } from '../shared/api/game.api';
import { getToken } from '../shared/utils/tokenStorage';


const requireAuth = () => {
  if (!getToken()) {
    throw new Error('Unauthorized');
  }
};


const rootRoute = createRootRoute({
  component: () => <Outlet />,
  notFoundComponent: () => <div>Página no encontrada</div>,
});

const loginRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/login',
  component: LoginPage,
});

const roomsRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/rooms',
  beforeLoad: requireAuth,
  loader: () =>
    queryClient.ensureQueryData({
      queryKey: ['rooms'],
      queryFn: listRooms,
    }),
  component: RoomsListPage,
});

const createRoomRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/rooms/create',
  component: RoomCreatePage,
});

const rankingRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/rooms/$code/ranking',
  beforeLoad: requireAuth,
  loader: ({ params }) =>
    queryClient.ensureQueryData({
      queryKey: ['ranking', params.code],
      queryFn: () => getRanking(params.code),
    }),
  component: RankingPage,
});

// ruta inicial (redirect de "/" a "/login")
const indexRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/',
  component: () => <LoginPage />,
});

const routeTree = rootRoute.addChildren([
  indexRoute,
  loginRoute,
  roomsRoute,
  createRoomRoute,
  rankingRoute,
]);

export const router = createRouter({ routeTree });
