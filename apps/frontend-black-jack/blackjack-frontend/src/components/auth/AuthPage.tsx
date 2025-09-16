// src/components/auth/AuthPage.tsx
import React, { useState } from 'react'
import LoginForm from './LoginForm'
import RegisterForm from './RegisterForm'

export type AuthPageProps = {
  onAuthSuccess?: () => void
}

const AuthPage: React.FC<AuthPageProps> = ({ onAuthSuccess }) => {
  const [isLogin, setIsLogin] = useState(true)

  return (
    <div className="max-w-md mx-auto bg-gray-800/60 rounded-xl p-6 shadow-lg">
      <div className="flex items-center justify-between mb-4">
        <h1 className="text-xl font-semibold text-white">
          {isLogin ? 'Iniciar sesión' : 'Crear cuenta'}
        </h1>

        <div className="space-x-2">
          <button
            className={`px-3 py-1 rounded text-xs ${
              isLogin ? 'bg-gray-900 text-white' : 'bg-gray-200'
            }`}
            onClick={() => setIsLogin(true)}
          >
            Iniciar sesión
          </button>
          <button
            className={`px-3 py-1 rounded text-xs ${
              !isLogin ? 'bg-gray-900 text-white' : 'bg-gray-200'
            }`}
            onClick={() => setIsLogin(false)}
          >
            Crear cuenta
          </button>
        </div>
      </div>

      {isLogin ? (
        <LoginForm onSuccess={onAuthSuccess} />
      ) : (
        <RegisterForm onSuccess={onAuthSuccess} />
      )}
    </div>
  )
}

export default AuthPage
