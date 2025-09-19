// src/components/auth/AuthPage.tsx
import React, { useState } from 'react'
import LoginForm from './LoginForm'
import RegisterForm from './RegisterForm'

export type AuthPageProps = {
  onAuthSuccess?: () => void
}

const AuthPage: React.FC<AuthPageProps> = ({ onAuthSuccess }) => {
  const [isLogin, setIsLogin] = useState(true)

  const handleAuthSuccess = () => {
    onAuthSuccess?.()
  }

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900 flex items-center justify-center p-4">
      <div className="w-full max-w-md">
        {/* Logo/Header */}
        <div className="text-center mb-8">
          <div className="flex items-center justify-center gap-2 mb-4">
            <div className="flex gap-1">
              <span className="text-3xl">♠</span>
              <span className="text-3xl text-red-500">♥</span>
              <span className="text-3xl text-red-500">♦</span>
              <span className="text-3xl">♣</span>
            </div>
          </div>
          <h1 className="text-3xl font-bold text-white mb-2">BlackJack Casino</h1>
          <p className="text-slate-400">Bienvenido al mejor casino online</p>
        </div>

        {/* Auth Card */}
        <div className="bg-slate-800/90 backdrop-blur-sm rounded-2xl p-8 shadow-2xl border border-slate-700">
          {/* Toggle Buttons */}
          <div className="flex bg-slate-700/50 rounded-xl p-1 mb-6">
            <button
              className={`flex-1 px-4 py-3 rounded-lg font-semibold transition-all duration-200 ${
                isLogin 
                  ? 'bg-emerald-600 text-white shadow-lg' 
                  : 'text-slate-300 hover:text-white'
              }`}
              onClick={() => setIsLogin(true)}
            >
              Iniciar Sesión
            </button>
            <button
              className={`flex-1 px-4 py-3 rounded-lg font-semibold transition-all duration-200 ${
                !isLogin 
                  ? 'bg-emerald-600 text-white shadow-lg' 
                  : 'text-slate-300 hover:text-white'
              }`}
              onClick={() => setIsLogin(false)}
            >
              Crear Cuenta
            </button>
          </div>

          {/* Forms */}
          {isLogin ? (
            <LoginForm onSuccess={handleAuthSuccess} />
          ) : (
            <RegisterForm onSuccess={handleAuthSuccess} />
          )}

          {/* Footer */}
          <div className="mt-6 text-center text-sm text-slate-400">
            Al continuar, aceptas nuestros términos y condiciones
          </div>
        </div>

        {/* Demo Info */}
        <div className="mt-6 text-center text-sm text-slate-500">
          <p>Datos de prueba:</p>
          <p>Email: alice@test.com | Contraseña: secret</p>
        </div>
      </div>
    </div>
  )
}

export default AuthPage