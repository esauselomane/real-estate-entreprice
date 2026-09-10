import { createContext, useContext, useEffect, useState, useCallback } from 'react';
import { useAuth } from '../contexts/AuthContext.jsx';
import apiClient from '../services/apiClient.js';

const EpilepsySafeModeContext = createContext();

export function useEpilepsySafeMode() {
  const context = useContext(EpilepsySafeModeContext);
  if (!context) {
    throw new Error('useEpilepsySafeMode must be used within EpilepsySafeModeProvider');
  }
  return context;
}

export function EpilepsySafeModeProvider({ children }) {
  const { activeProfile } = useAuth();
  const [epilepsySafeMode, setEpilepsySafeMode] = useState(false);

  useEffect(() => {
    const loadPreference = async () => {
      if (!activeProfile?.userId) return;
      try {
        const prefs = await apiClient.get(`/api/user/profile`);
        if (prefs.data?.epilepsySafeMode !== undefined) {
          setEpilepsySafeMode(prefs.data.epilepsySafeMode);
        }
      } catch (err) {
        // If the profile endpoint rejects, default to false
        const stored = localStorage.getItem('epilepsy-safe-mode');
        setEpilepsySafeMode(stored === 'true');
      }
    };
    loadPreference();
    const stored = localStorage.getItem('epilepsy-safe-mode');
    if (stored !== null) {
      setEpilepsySafeMode(stored === 'true');
    }
  }, [activeProfile?.userId]);

  const toggleEpilepsySafeMode = useCallback(async (value) => {
    const newValue = typeof value === 'boolean' ? value : !epilepsySafeMode;
    setEpilepsySafeMode(newValue);
    localStorage.setItem('epilepsy-safe-mode', String(newValue));
    document.documentElement.classList.toggle('epilepsy-safe', newValue);

    // Persist to backend if user is authenticated
    if (activeProfile?.userId) {
      try {
        await apiClient.put('/api/user/profile', { epilepsySafeMode: newValue });
      } catch (err) {
        // Non-critical: local storage persists the preference
      }
    }
  }, [epilepsySafeMode, activeProfile?.userId]);

  return (
    <EpilepsySafeModeContext.Provider value={{ epilepsySafeMode, toggleEpilepsySafeMode }}>
      {children}
    </EpilepsySafeModeContext.Provider>
  );
}
