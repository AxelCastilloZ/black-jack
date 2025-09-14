import { http } from '../api/https';
import type { Room, CreateRoomDto } from '../models/rooms';

export async function listRooms(): Promise<Room[]> {
  const { data } = await http.get('/rooms');
  return data;
}

export async function getRoom(code: string): Promise<Room> {
  const { data } = await http.get(`/rooms/${code}`);
  return data;
}

export async function createRoom(dto: CreateRoomDto): Promise<Room> {
  const { data } = await http.post('/rooms', dto);
  return data;
}

export async function joinRoom(code: string): Promise<Room> {
  const { data } = await http.post(`/rooms/${code}/join`);
  return data;
}
