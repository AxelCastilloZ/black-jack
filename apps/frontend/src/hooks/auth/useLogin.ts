import { useMutation } from '@tanstack/react-query';
import { login } from '../../shared/api/users.api';
import { useNavigate } from '@tanstack/react-router';
import toast from "react-hot-toast";

export function useLogin() {
  const navigate = useNavigate();

  return useMutation({
    mutationFn: login,
    onSuccess: () => {
      navigate({ to: '/rooms' });
    },
    onError: (err: any) => {
      toast.error('No se pudo iniciar seción', err);
    },
  });
}
