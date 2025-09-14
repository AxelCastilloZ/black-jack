import { http } from './https';
import type { User } from '../models/users';
import { setToken } from '../utils/tokenStorage';

type LoginDto = {
  username: string;
  password: string;
};

type LoginResponse = {
  token: string;
  user: User;
};

// Login → guarda token
export async function login(dto: LoginDto): Promise<User> {
  const { data } = await http.post<LoginResponse>('/auth/login', dto);
  setToken(data.token);
  return data.user;
}

// Obtener usuario autenticado
export async function me(): Promise<User> {
  const { data } = await http.get<User>('/auth/me');
  return data;
}
