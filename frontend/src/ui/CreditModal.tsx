import React from 'react';
import { useCredits } from './CreditContext';

type CreditModalProps = {
  isOpen: boolean;
  onClose: () => void;
  currentCredits: number | null;
};

export function CreditModal({ isOpen, onClose, currentCredits }: CreditModalProps) {
  const { refreshCredits, userId } = useCredits();
  const [email, setEmail] = React.useState('');
  const [showEmailInput, setShowEmailInput] = React.useState(false);
  
  // Refresh credits when modal opens
  React.useEffect(() => {
    if (isOpen) {
      refreshCredits();
      // Reset email input when modal opens
      setEmail('');
      setShowEmailInput(false);
    }
  }, [isOpen, refreshCredits]);
  
  if (!isOpen) return null;

  const creditPacks = [
    { 
      name: 'Starter Pack', 
      credits: 20, 
      price: 1.99, 
      perCredit: 0.100, 
      description: 'Perfect for occasional use',
      popular: false 
    },
    { 
      name: 'Pro Pack', 
      credits: 50, 
      price: 3.99, 
      perCredit: 0.080, 
      description: 'Best value for regular users',
      popular: true 
    },
    { 
      name: 'Ultra Pack', 
      credits: 120, 
      price: 7.99, 
      perCredit: 0.067, 
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

      console.log('Creating checkout session for pack:', pack);
      console.log('API URL:', `${apiBaseUrl}/stripe/create-checkout-session`);

      const response = await fetch(`${apiBaseUrl}/stripe/create-checkout-session`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'x-user-id': userId, // Send userId in header so backend uses it instead of IP
        },
        body: JSON.stringify({
          credits: pack.credits,
          price: pack.price,
          successUrl: `${window.location.origin}/?payment=success&credits=${pack.credits}`,
          cancelUrl: `${window.location.origin}/?payment=cancelled`,
          email: email.trim() || undefined, // Optional email for credit restoration
        }),
      });

      console.log('Response status:', response.status, response.statusText);

      if (!response.ok) {
        let errorMessage = 'Unknown error';
        try {
          const error = await response.json();
          errorMessage = error.error || error.message || JSON.stringify(error);
          console.error('Error response:', error);
        } catch (e) {
          const text = await response.text();
          errorMessage = text || `HTTP ${response.status}: ${response.statusText}`;
          console.error('Error response (text):', text);
        }
        alert(`Failed to create checkout: ${errorMessage}\n\nPlease check:\n1. STRIPE_SECRET_KEY is set in Lambda\n2. Stripe endpoint is created in API Gateway\n3. Check CloudWatch logs for details`);
        return;
      }

      const data = await response.json();
      console.log('Checkout session response:', data);
      
      // Redirect to Stripe Checkout
      if (data.url) {
        console.log('Redirecting to Stripe Checkout:', data.url);
        window.location.href = data.url;
      } else if (data.sessionId) {
        // Fallback: construct Stripe Checkout URL if only sessionId is returned
        console.warn('No URL in response, only sessionId:', data.sessionId);
        alert('Checkout session created but URL missing. Please check backend logs.');
      } else {
        console.error('Invalid response format:', data);
        alert(`Invalid response from server: ${JSON.stringify(data)}\n\nExpected: { url: "..." } or { sessionId: "..." }`);
      }
    } catch (error) {
      console.error('Error creating checkout session:', error);
      alert(`Failed to start checkout: ${error instanceof Error ? error.message : 'Network error'}\n\nPlease check your browser console for details.`);
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
          
          {/* Optional email input for credit restoration */}
          <div style={{ marginBottom: '20px', padding: '15px', backgroundColor: '#f8f9fa', borderRadius: '8px', border: '1px solid #e0e0e0' }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '10px' }}>
              <label style={{ fontSize: '14px', fontWeight: '500', color: '#333' }}>
                💾 Optional: Add email to restore credits on new devices
              </label>
              <button
                type="button"
                onClick={() => setShowEmailInput(!showEmailInput)}
                style={{
                  background: 'none',
                  border: 'none',
                  color: '#666',
                  cursor: 'pointer',
                  fontSize: '12px',
                  textDecoration: 'underline'
                }}
              >
                {showEmailInput ? 'Hide' : 'Show'}
              </button>
            </div>
            {showEmailInput && (
              <input
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="your@email.com (optional)"
                style={{
                  width: '100%',
                  padding: '10px',
                  border: '1px solid #ddd',
                  borderRadius: '4px',
                  fontSize: '14px'
                }}
              />
            )}
            <p style={{ fontSize: '12px', color: '#666', marginTop: '8px', marginBottom: '0' }}>
              If you provide your email, you can restore credits on any device by verifying your email address.
            </p>
          </div>

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
            <span className="back-icon">💕</span>
            Back to Translator
          </button>
        </div>
      </div>
    </div>
  );
}
