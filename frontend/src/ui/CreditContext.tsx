import React, { createContext, useContext, useState, useEffect } from 'react';

type CreditContextType = {
  credits: number | null;
  setCredits: (credits: number | null) => void;
  refreshCredits: () => Promise<void>;
  userId: string;
};

const CreditContext = createContext<CreditContextType | null>(null);

export function CreditProvider({ children }: { children: React.ReactNode }) {
  const [credits, setCredits] = useState<number | null>(null);
  const [userId] = useState(() => {
    // Get or create userId from localStorage
    let id = localStorage.getItem('userId');
    if (!id) {
      id = `user_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
      localStorage.setItem('userId', id);
    }
    return id;
  });

  async function refreshCredits() {
    try {
      const apiBaseUrl = import.meta.env.VITE_API_BASE_URL?.replace(/\/+$/, '') || '';
      if (!apiBaseUrl) {
        console.warn('No API base URL configured, skipping credit refresh');
        return;
      }
      
      const resp = await fetch(`${apiBaseUrl}/credits`, {
        method: 'GET',
        headers: {
          'x-user-id': userId,
        },
      });
      
      if (resp.ok) {
        const data = await resp.json();
        const creditsValue = data.credits ?? null;
        console.log('Refreshed credits from server:', creditsValue, 'userId:', userId);
        setCredits(creditsValue);
        if (creditsValue !== null) {
          localStorage.setItem('credits', creditsValue.toString());
        } else {
          // If server returns null, clear localStorage too
          localStorage.removeItem('credits');
        }
      } else {
        const errorText = await resp.text();
        console.error('Failed to refresh credits:', resp.status, errorText);
      }
    } catch (err) {
      console.error('Error refreshing credits:', err);
    }
  }

  useEffect(() => {
    // Initialize credits (will be set from first API call)
    const stored = localStorage.getItem('credits');
    if (stored) {
      setCredits(parseInt(stored, 10));
    }
  }, []);

  return (
    <CreditContext.Provider value={{ credits, setCredits, refreshCredits, userId }}>
      {children}
    </CreditContext.Provider>
  );
}

export function useCredits() {
  const context = useContext(CreditContext);
  if (!context) throw new Error('useCredits must be used within CreditProvider');
  return context;
}

