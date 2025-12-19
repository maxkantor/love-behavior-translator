import React from 'react';
import { useCredits } from './CreditContext';

type CreditModalProps = {
  isOpen: boolean;
  onClose: () => void;
  currentCredits: number | null;
};

export function CreditModal({ isOpen, onClose, currentCredits }: CreditModalProps) {
  const { refreshCredits } = useCredits();
  
  // Refresh credits when modal opens
  React.useEffect(() => {
    if (isOpen) {
      refreshCredits();
    }
  }, [isOpen, refreshCredits]);
  
  if (!isOpen) return null;

  const creditPacks = [
    { 
      name: 'Starter Pack', 
      credits: 20, 
      price: 4.99, 
      perCredit: 0.249, 
      description: 'Perfect for occasional use',
      popular: false 
    },
    { 
      name: 'Pro Pack', 
      credits: 50, 
      price: 9.99, 
      perCredit: 0.200, 
      description: 'Best value for regular users',
      popular: true 
    },
    { 
      name: 'Ultra Pack', 
      credits: 120, 
      price: 19.99, 
      perCredit: 0.167, 
      description: 'For power users',
      popular: false 
    },
  ];

  async function handlePurchase(pack: typeof creditPacks[0]) {
    try {
      const apiBaseUrl = import.meta.env.VITE_API_BASE_URL?.replace(/\/+$/, '') || '';
      if (!apiBaseUrl) {
        alert('API not configured. Please contact support.');
        return;
      }

      const response = await fetch(`${apiBaseUrl}/stripe/create-checkout-session`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          credits: pack.credits,
          price: pack.price,
          successUrl: `${window.location.origin}/?payment=success&credits=${pack.credits}`,
          cancelUrl: `${window.location.origin}/?payment=cancelled`,
        }),
      });

      if (!response.ok) {
        const error = await response.json();
        alert(`Failed to create checkout: ${error.error || 'Unknown error'}`);
        return;
      }

      const data = await response.json();
      
      // Redirect to Stripe Checkout
      if (data.url) {
        window.location.href = data.url;
      } else {
        alert('Failed to get checkout URL');
      }
    } catch (error) {
      console.error('Error creating checkout session:', error);
      alert('Failed to start checkout. Please try again.');
    }
  }

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="credit-modal-content" onClick={(e) => e.stopPropagation()}>
        <div className="credit-modal-header">
          <h1>Unlock Clarity</h1>
          <p className="credit-subtitle">
            People usually come here when something feels off.
          </p>
        </div>

        <div className="benefits-section">
          <div className="benefit-card">
            <div className="benefit-icon">🎁</div>
            <div className="benefit-text">
              <strong>5 Free Relationship Insights</strong>
              <span>Start with 5 free deep readings - no credit card required.</span>
            </div>
          </div>
          <div className="benefit-card">
            <div className="benefit-icon">🐷</div>
            <div className="benefit-text">
              <strong>Insights Never Expire</strong>
              <span>Use your relationship insights whenever you need clarity.</span>
            </div>
          </div>
          <div className="benefit-card">
            <div className="benefit-icon">👑</div>
            <div className="benefit-text">
              <strong>Same Deep Analysis</strong>
              <span>All readings use the same advanced AI trained on relationship patterns.</span>
            </div>
          </div>
        </div>

        <div className="packs-section">
          <h2 className="packs-title">Choose Your Relationship Insight Pack</h2>
          <div className="packs-grid">
            {creditPacks.map((pack) => (
              <div key={pack.name} className={`pack-card ${pack.popular ? 'popular-pack' : ''}`}>
                {pack.popular && (
                  <div className="popular-badge-top">
                    <span className="crown-icon">👑</span>
                    Most Popular
                  </div>
                )}
                <h3 className="pack-name">{pack.name}</h3>
                <div className="pack-price">
                  ${pack.price}
                  <span className="price-label">one-time</span>
                </div>
                <div className="pack-credits-box">
                  <span className="coin-icon">🪙</span>
                  <span className="coin-icon">🪙</span>
                  <span className="credits-amount">{pack.credits} Deep Readings</span>
                </div>
                <p className="pack-description">{pack.description}</p>
                <div className="pack-per-credit">${pack.perCredit.toFixed(3)} per reading</div>
                <button
                  className={`pack-button ${pack.popular ? 'popular-button' : ''}`}
                  onClick={() => handlePurchase(pack)}
                >
                  <span className="check-icon">✓</span>
                  Get {pack.name}
                </button>
              </div>
            ))}
          </div>
        </div>

        <div className="credit-footer">
          <div className="secure-payment">
            <span className="lock-icon">🔒</span>
            Secure payment powered by Stripe
          </div>
          <button className="back-button" onClick={onClose}>
            <span className="back-icon">↻</span>
            Back to Translator
          </button>
        </div>
      </div>
    </div>
  );
}
