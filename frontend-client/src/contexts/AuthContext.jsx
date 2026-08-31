import { createContext, useContext, useReducer, useEffect, useCallback } from 'react';
import { authService } from '../services/authService.js';

// eslint-disable-next-line react-refresh/only-export-components
export const normalizeRole = (role) =>
  ({ SuperAdmin: 'PlatformAdmin', Manager: 'AgencyAdmin', Agent: 'Broker', Admin: 'AgencyAdmin' }[role] || role || 'Broker');

const initialAuthState = {
  isLoggedIn: false,
  userRole: 'Agent',
  activeProfile: null,
  isRegistering: false,
  errorMessage: '',
  isConnecting: false,
};

const authReducer = (state, action) => {
  switch (action.type) {
    case 'RESTORE_SESSION':
      return {
        ...state,
        activeProfile: action.payload.profile,
        userRole: action.payload.role,
        isLoggedIn: true,
      };
    case 'LOGIN_START':
      return { ...state, isConnecting: true, errorMessage: '' };
    case 'LOGIN_SUCCESS':
      return {
        ...state,
        isLoggedIn: true,
        activeProfile: action.payload.profile,
        userRole: action.payload.role,
        isConnecting: false,
        errorMessage: '',
      };
    case 'LOGIN_ERROR':
      return { ...state, errorMessage: action.payload, isConnecting: false };
    case 'REGISTER_START':
      return { ...state, isConnecting: true, errorMessage: '' };
    case 'REGISTER_SUCCESS':
      return {
        ...state,
        isRegistering: false,
        errorMessage: action.payload,
        isConnecting: false,
      };
    case 'REGISTER_ERROR':
      return { ...state, errorMessage: action.payload, isConnecting: false };
    case 'SET_REGISTERING':
      return { ...state, isRegistering: action.payload };
    case 'CLEAR_ERROR':
      return { ...state, errorMessage: '' };
    case 'LOGOUT':
      return { ...initialAuthState };
    default:
      return state;
  }
};

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [state, dispatch] = useReducer(authReducer, initialAuthState);

  useEffect(() => {
    const rawSavedProfile = localStorage.getItem('user_profile_data');
    const storedToken = localStorage.getItem('user_session_jwt');
    if (rawSavedProfile && storedToken) {
      const parsedProfile = JSON.parse(rawSavedProfile);
      dispatch({
        type: 'RESTORE_SESSION',
        payload: {
          profile: { ...parsedProfile, role: normalizeRole(parsedProfile.role) },
          role: normalizeRole(parsedProfile.role),
        },
      });
    }
  }, []);

  const login = useCallback(
    async (email, password) => {
      dispatch({ type: 'LOGIN_START' });
      try {
        const loginPayload = await authService.login(email, password);
        const normalizedProfile = { ...loginPayload.profile, role: normalizeRole(loginPayload.profile.role) };
        dispatch({
          type: 'LOGIN_SUCCESS',
          payload: { profile: normalizedProfile, role: normalizedProfile.role },
        });
        return normalizedProfile;
      } catch (err) {
        dispatch({ type: 'LOGIN_ERROR', payload: err || 'Connection timed out. Ensure your Ocelot gateway service container is active.' });
        throw err;
      }
    },
    []
  );

  const register = useCallback(
    async (fullName, email, password, role = 'Broker') => {
      dispatch({ type: 'REGISTER_START' });
      try {
        await authService.registerUser({
          fullName,
          email,
          passwordHash: password,
          systemRole: role,
          tenantId: 'TEN-001',
        });
        dispatch({ type: 'REGISTER_SUCCESS', payload: 'Registration successful. You can now sign in.' });
      } catch (err) {
        dispatch({ type: 'REGISTER_ERROR', payload: err || 'Registration failed. Please try again.' });
        throw err;
      }
    },
    []
  );

  const logout = useCallback(() => {
    authService.logout();
  }, []);

  const value = {
    ...state,
    login,
    register,
    logout,
    setRegistering: (value) => dispatch({ type: 'SET_REGISTERING', payload: value }),
    clearError: () => dispatch({ type: 'CLEAR_ERROR' }),
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

// eslint-disable-next-line react-refresh/only-export-components
export const useAuth = () => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};

export default AuthContext;
