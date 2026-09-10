import { createContext, useContext, useState, useCallback } from 'react';

const CelebrationContext = createContext();

export function useCelebration() {
  const context = useContext(CelebrationContext);
  if (!context) {
    throw new Error('useCelebration must be used within CelebrationProvider');
  }
  return context;
}

export function CelebrationProvider({ children }) {
  const [isActive, setIsActive] = useState(false);
  const [payload, setPayload] = useState(null);

  const triggerCelebration = useCallback((data) => {
    setPayload(data);
    setIsActive(true);
    const timer = setTimeout(() => {
      setIsActive(false);
      setPayload(null);
    }, 3000);
    return () => clearTimeout(timer);
  }, []);

  return (
    <CelebrationContext.Provider value={{ triggerCelebration, isActive, payload }}>
      {children}
      {isActive && <CelebrationOverlay {...payload} />}
    </CelebrationContext.Provider>
  );
}

function CelebrationOverlay({ brokerName, brokerAvatar, dealAddress }) {
  return (
    <div className="fixed inset-0 z-50 pointer-events-none flex items-center justify-center">
      <div className="absolute inset-0 bg-transparent" />
      <div className="relative z-10 flex flex-col items-center text-center">
        <div className="text-4xl md:text-6xl font-bold text-white mb-6 drop-shadow-2xl">
          🎉 Deal Closed!
        </div>

        <div className="relative mb-4">
          {brokerAvatar ? (
            <img
              src={brokerAvatar}
              alt={brokerName || 'Broker'}
              className="w-24 h-24 md:w-32 md:h-32 rounded-full border-4 border-white shadow-2xl object-cover animate-bounce"
            />
          ) : (
            <div className="w-24 h-24 md:w-32 md:h-32 rounded-full bg-indigo-500 border-4 border-white shadow-2xl flex items-center justify-center text-white text-3xl font-bold">
              {(brokerName || '?').charAt(0).toUpperCase()}
            </div>
          )}
        </div>

        <div className="text-xl md:text-2xl font-semibold text-white drop-shadow-lg">
          {brokerName || 'A broker'} just closed a deal
        </div>
        {dealAddress && (
          <div className="text-sm text-slate-200 drop-shadow-lg mt-1">
            at {dealAddress}
          </div>
        )}
      </div>
    </div>
  );
}
