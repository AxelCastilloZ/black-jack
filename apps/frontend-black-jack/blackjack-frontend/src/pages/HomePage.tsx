// src/pages/HomePage.tsx - Optimizado
import React from 'react'
import { useNavigate } from '@tanstack/react-router'
import { authService } from '../services/auth'

export default function HomePage() {
  const navigate = useNavigate()
  const currentUser = authService.getCurrentUser()

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900">
      {/* Header */}
      <header className="flex items-center justify-between p-6 border-b border-slate-700">
        <div className="flex items-center gap-3">
          <div className="flex gap-1">
            <span className="text-2xl">♠</span>
            <span className="text-2xl text-red-500">♥</span>
            <span className="text-2xl text-red-500">♦</span>
            <span className="text-2xl">♣</span>
          </div>
          <h1 className="text-2xl font-bold text-white">BlackJack Casino</h1>
        </div>
        
        <div className="flex items-center gap-4">
          <div className="text-right">
            <div className="text-white font-semibold">
              {currentUser?.displayName}
            </div>
            <div className="text-emerald-400 font-bold">
              ${currentUser?.balance?.toLocaleString()}
            </div>
          </div>
          
          <div className="flex gap-2">
            <button 
              onClick={() => navigate({ to: '/perfil' })}
              className="flex items-center gap-2 text-slate-300 hover:text-white transition-colors px-3 py-2 rounded-lg hover:bg-slate-700"
            >
              <span>👤</span>
              <span>Perfil</span>
            </button>
            
            <button 
              onClick={() => authService.logout()}
              className="text-red-400 hover:text-red-300 transition-colors px-3 py-2 rounded-lg hover:bg-slate-700"
            >
              Salir
            </button>
          </div>
        </div>
      </header>

      {/* Main Content */}
      <main className="container mx-auto px-6 py-20">
        {/* Hero Section */}
        <div className="text-center mb-20">
          <h2 className="text-6xl font-bold text-white mb-6">
            El Mejor <span className="text-emerald-400">BlackJack</span> Multijugador
          </h2>
          <p className="text-xl text-slate-300 mb-12">
            Juega en tiempo real con jugadores de todo el mundo.
            <br />
            Demuestra tus habilidades y escala en el ranking global.
          </p>
          
          <div className="flex items-center justify-center gap-6">
            <button 
              onClick={() => navigate({ to: '/lobby' })}
              className="bg-emerald-600 hover:bg-emerald-700 px-8 py-4 rounded-xl text-white font-bold text-lg flex items-center gap-3 transition-all transform hover:scale-105 shadow-lg hover:shadow-emerald-500/25"
            >
              <span className="text-2xl">🎯</span>
              Jugar Ahora
            </button>
            
            <button 
              onClick={() => {
                const element = document.getElementById('how-to-play')
                element?.scrollIntoView({ behavior: 'smooth' })
              }}
              className="border-2 border-slate-600 hover:border-slate-500 hover:bg-slate-800/50 px-8 py-4 rounded-xl text-white font-bold text-lg transition-all"
            >
              Cómo Jugar
            </button>
          </div>
        </div>

        {/* Features Grid */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-8 max-w-6xl mx-auto mb-20">
          <FeatureCard
            icon="👥"
            title="Multijugador en Tiempo Real"
            description="Hasta 6 jugadores por mesa. Partidas rápidas y emocionantes con chat en vivo y conexión instantánea."
          />
          
          <FeatureCard
            icon="🏆"
            title="Sistema de Ranking"
            description="Compite por el primer lugar. Gana puntos, sube de nivel y desbloquea logros exclusivos."
          />
          
          <FeatureCard
            icon="⭐"
            title="Experiencia Premium"
            description="Gráficos elegantes, animaciones fluidas y una experiencia de casino auténtica desde tu navegador."
          />
        </div>

        {/* How to Play Section */}
        <section id="how-to-play" className="bg-slate-800/50 rounded-3xl p-12 border border-slate-700 mb-20">
          <div className="text-center mb-12">
            <h3 className="text-4xl font-bold text-white mb-4">¿Cómo se Juega?</h3>
            <p className="text-xl text-slate-300">Es fácil, solo sigue estos pasos</p>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-4 gap-8">
            <StepCard
              number={1}
              title="Únete a una Mesa"
              description="Selecciona una mesa que se ajuste a tu presupuesto"
            />
            
            <StepCard
              number={2}
              title="Haz tu Apuesta"
              description="Decide cuánto dinero quieres apostar en la ronda"
            />
            
            <StepCard
              number={3}
              title="Juega tus Cartas"
              description="Pide carta, plántate o duplica según tu estrategia"
            />
            
            <StepCard
              number={4}
              title="¡Gana Dinero!"
              description="Vence al dealer y multiplica tus ganancias"
            />
          </div>

          <div className="text-center mt-12">
            <button 
              onClick={() => navigate({ to: '/lobby' })}
              className="bg-emerald-600 hover:bg-emerald-700 px-8 py-4 rounded-xl text-white font-bold text-lg transition-all transform hover:scale-105"
            >
              ¡Empezar a Jugar!
            </button>
          </div>
        </section>

        {/* Stats Section */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-8 text-center">
          <StatCard 
            value="1,247"
            label="Jugadores Online"
          />
          <StatCard 
            value="2,341"
            label="Partidas Hoy"
          />
          <StatCard 
            value="$125K"
            label="Ganado Esta Semana"
          />
        </div>
      </main>
    </div>
  )
}

// Componente reutilizable para características
function FeatureCard({ 
  icon, 
  title, 
  description 
}: { 
  icon: string
  title: string
  description: string 
}) {
  return (
    <div className="bg-gradient-to-br from-slate-800 to-slate-900 p-8 rounded-2xl border border-slate-700 hover:border-slate-600 transition-all hover:transform hover:scale-105">
      <div className="w-16 h-16 bg-emerald-500 rounded-full flex items-center justify-center mb-6 mx-auto">
        <span className="text-2xl">{icon}</span>
      </div>
      <h3 className="text-2xl font-bold text-white mb-4 text-center">
        {title}
      </h3>
      <p className="text-slate-300 text-center leading-relaxed">
        {description}
      </p>
    </div>
  )
}

// Componente para los pasos
function StepCard({
  number,
  title,
  description
}: {
  number: number
  title: string
  description: string
}) {
  return (
    <div className="text-center">
      <div className="w-16 h-16 bg-emerald-500 rounded-full flex items-center justify-center mx-auto mb-4 text-white text-2xl font-bold">
        {number}
      </div>
      <h4 className="text-lg font-semibold text-white mb-2">{title}</h4>
      <p className="text-slate-400 text-sm">{description}</p>
    </div>
  )
}

// Componente para estadísticas
function StatCard({
  value,
  label
}: {
  value: string
  label: string
}) {
  return (
    <div>
      <div className="text-4xl font-bold text-emerald-400 mb-2">{value}</div>
      <div className="text-slate-300">{label}</div>
    </div>
  )
}