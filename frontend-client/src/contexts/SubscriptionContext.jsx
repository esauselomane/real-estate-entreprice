import { createContext, useContext } from 'react';

export const SubscriptionContext = createContext(null);

export function useSubscription() {
  return useContext(SubscriptionContext);
}
