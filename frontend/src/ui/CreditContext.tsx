import React, { createContext, useContext, useState, useEffect, useCallback } from 'react';

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

  const refreshCredits = useCallback(async () => {
    try {
      const apiBaseUrl = import.meta.env.VITE_API_BASE_URL?.replace(/\/+$/, '') || '';
      if (!apiBaseUrl) {
        console.warn('No API base URL configured, skipping credit refresh');
        // Fallback to localStorage if API not configured
        const stored = localStorage.getItem('credits');
        if (stored) {
          const parsed = parseInt(stored, 10);
          if (!isNaN(parsed)) {
            setCredits(parsed);
          }
        }
        return;
      }
      
      console.log('🔄 Refreshing credits from server...', 'userId:', userId, 'url:', `${apiBaseUrl}/credits`);
      const startTime = Date.now();
      const resp = await fetch(`${apiBaseUrl}/credits`, {
        method: 'GET',
        headers: {
          'x-user-id': userId,
        },
        cache: 'no-cache', // Force fresh fetch
      });
      const duration = Date.now() - startTime;
      
      console.log(`📡 Response received in ${duration}ms:`, resp.status, resp.statusText);
      
      if (resp.ok) {
        const data = await resp.json();
        console.log('📦 Response data:', data);
        const creditsValue = data.credits ?? null;
        console.log('✅ Refreshed credits from server:', creditsValue, 'userId:', userId, 'response userId:', data.userId);
        
        // Verify userId matches
        if (data.userId && data.userId !== userId) {
          console.warn('⚠️ userId mismatch! Request:', userId, 'Response:', data.userId);
        }
        
        setCredits(creditsValue);
        if (creditsValue !== null) {
          localStorage.setItem('credits', creditsValue.toString());
          console.log('💾 Saved to localStorage:', creditsValue);
        } else {
          // If server returns null, clear localStorage too
          localStorage.removeItem('credits');
          console.log('🗑️ Cleared localStorage (null credits)');
        }
      } else {
        const errorText = await resp.text();
        console.error('❌ Failed to refresh credits:', resp.status, errorText);
        // On error, fallback to localStorage
        const stored = localStorage.getItem('credits');
        if (stored) {
          const parsed = parseInt(stored, 10);
          if (!isNaN(parsed)) {
            console.log('⚠️ Using cached credits from localStorage:', parsed);
            setCredits(parsed);
          }
        }
      }
    } catch (err) {
      console.error('❌ Error refreshing credits:', err);
      // On error, fallback to localStorage
      const stored = localStorage.getItem('credits');
      if (stored) {
        const parsed = parseInt(stored, 10);
        if (!isNaN(parsed)) {
          console.log('Using cached credits from localStorage:', parsed);
          setCredits(parsed);
        }
      }
    }
  }, [userId]); // Only recreate if userId changes

  // Refresh credits immediately on mount (before any component uses it)
  useEffect(() => {
    refreshCredits();
  }, [refreshCredits]); // Run when refreshCredits changes (which should only be on mount)

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

