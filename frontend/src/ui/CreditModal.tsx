import React from 'react';

type CreditModalProps = {
  isOpen: boolean;
  onClose: () => void;
  currentCredits: number | null;
};

export function CreditModal({ isOpen, onClose, currentCredits }: CreditModalProps) {
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

  function handlePurchase(pack: typeof creditPacks[0]) {
    // TODO: Integrate with Stripe
    alert(`Stripe integration coming soon! This would purchase ${pack.credits} credits for $${pack.price}`);
    onClose();
  }

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="credit-modal-content" onClick={(e) => e.stopPropagation()}>
        <div className="credit-modal-header">
          <h1>Buy Credits</h1>
          <p className="credit-subtitle">
            Purchase credits to continue analyzing relationship behaviors after your free searches are used up!
          </p>
        </div>

        <div className="benefits-section">
          <div className="benefit-card">
            <div className="benefit-icon">🎁</div>
            <div className="benefit-text">
              <strong>5 Free Searches</strong>
              <span>Start with 5 free translations - no credit card required.</span>
            </div>
          </div>
          <div className="benefit-card">
            <div className="benefit-icon">🐷</div>
            <div className="benefit-text">
              <strong>Credits Never Expire</strong>
              <span>Use your credits whenever you need them.</span>
            </div>
          </div>
          <div className="benefit-card">
            <div className="benefit-icon">👑</div>
            <div className="benefit-text">
              <strong>Same Quality Analysis</strong>
              <span>All translations use the same advanced AI analysis.</span>
            </div>
          </div>
        </div>

        <div className="packs-section">
          <h2 className="packs-title">Choose Your Credit Pack</h2>
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
                  <span className="credits-amount">{pack.credits} Credits</span>
                </div>
                <p className="pack-description">{pack.description}</p>
                <div className="pack-per-credit">${pack.perCredit.toFixed(3)} per credit</div>
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
