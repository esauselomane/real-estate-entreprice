import { createContext, useContext, useReducer, useCallback } from 'react';

const initialUiState = {
  currentView: 'dashboard',
  selectedPropertyId: null,
  selectedClientData: { id: null, type: '', name: '' },
  selectedWorkspace: 'properties',
  activeBrochureDealTarget: null,
};

const uiReducer = (state, action) => {
  switch (action.type) {
    case 'SET_CURRENT_VIEW':
      return { ...state, currentView: action.payload.view };
    case 'SET_SELECTED_PROPERTY':
      return {
        ...state,
        selectedPropertyId: action.payload,
        currentView: 'details',
        activeBrochureDealTarget: null,
      };
    case 'SET_SELECTED_CLIENT':
      return {
        ...state,
        selectedClientData: action.payload,
        currentView: 'client',
        activeBrochureDealTarget: null,
      };
    case 'SET_SELECTED_WORKSPACE':
      return { ...state, selectedWorkspace: action.payload };
    case 'SET_BROCHURE_DEAL_TARGET':
      return { ...state, activeBrochureDealTarget: action.payload };
    case 'CLEAR_BROCHURE_DEAL_TARGET':
      return { ...state, activeBrochureDealTarget: null };
    case 'NAVIGATE_TO':
      return {
        ...state,
        currentView: action.payload,
        activeBrochureDealTarget: null,
      };
    default:
      return state;
  }
};

const AppUiContext = createContext(null);

export function AppUiProvider({ children }) {
  const [state, dispatch] = useReducer(uiReducer, initialUiState);

  const navigateTo = useCallback((view) => {
    dispatch({ type: 'NAVIGATE_TO', payload: view });
  }, []);

  const handleSelectProperty = useCallback((propertyId) => {
    dispatch({ type: 'SET_SELECTED_PROPERTY', payload: propertyId });
  }, []);

  const handleNavigateToClient = useCallback((clientOrId, type, name) => {
    if (clientOrId && typeof clientOrId === 'object') {
      dispatch({
        type: 'SET_SELECTED_CLIENT',
        payload: {
          id: clientOrId.id || '',
          type: clientOrId.type || 'Client',
          name: clientOrId.name || 'Client Profile',
        },
      });
    } else {
      dispatch({
        type: 'SET_SELECTED_CLIENT',
        payload: {
          id: clientOrId || '',
          type: type || 'Client',
          name: name || 'Client Profile',
        },
      });
    }
  }, []);

  const value = {
    ...state,
    navigateTo,
    selectProperty: handleSelectProperty,
    navigateToClient: handleNavigateToClient,
    setBrochureDealTarget: (deal) => dispatch({ type: 'SET_BROCHURE_DEAL_TARGET', payload: deal }),
    clearBrochureDealTarget: () => dispatch({ type: 'CLEAR_BROCHURE_DEAL_TARGET' }),
  };

  return <AppUiContext.Provider value={value}>{children}</AppUiContext.Provider>;
}

// eslint-disable-next-line react-refresh/only-export-components
export const useAppUi = () => {
  const context = useContext(AppUiContext);
  if (!context) {
    throw new Error('useAppUi must be used within an AppUiProvider');
  }
  return context;
};

export default AppUiContext;
