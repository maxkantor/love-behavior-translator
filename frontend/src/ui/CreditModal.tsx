import React from 'react';

type CreditModalProps = {
  isOpen: boolean;
  onClose: () => void;
  currentCredits: number | null;
};

export function CreditModal({ isOpen, onClose, currentCredits }: CreditModalProps) {
  if (!isOpen) return null;

  const creditPacks = [
    { credits: 10, price: 4.99, popular: false },
    { credits: 25, price: 9.99, popular: true },
    { credits: 50, price: 16.99, popular: false },
    { credits: 100, price: 29.99, popular: false },
    { credits: 250, price: 69.99, popular: false },
    { credits: 500, price: 119.99, popular: false },
  ];

  function handlePurchase(pack: typeof creditPacks[0]) {
    // TODO: Integrate with Stripe
    alert(`Stripe integration coming soon! This would purchase ${pack.credits} credits for $${pack.price}`);
    onClose();
  }

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-content" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h2>💰 Buy Credits</h2>
          <button className="modal-close" onClick={onClose}>×</button>
        </div>
        
        <div className="modal-body">
          <p className="current-credits-display">
            Your current balance: <strong>{currentCredits === null ? '...' : currentCredits === -1 ? 'Unlimited' : `${currentCredits} credits`}</strong>
          </p>

          <div className="credit-packs">
            {creditPacks.map((pack) => (
              <div key={pack.credits} className={`credit-pack ${pack.popular ? 'popular' : ''}`}>
                {pack.popular && <div className="popular-badge">Most Popular</div>}
                <div className="pack-credits">{pack.credits} Credits</div>
                <div className="pack-price">${pack.price}</div>
                <div className="pack-per-credit">
                  ${(pack.price / pack.credits).toFixed(2)} per credit
                </div>
                <button
                  className="pack-buy-btn"
                  onClick={() => handlePurchase(pack)}
                >
                  Buy Now
                </button>
              </div>
            ))}
          </div>

          <div className="credit-info">
            <p>💡 <strong>How it works:</strong></p>
            <ul>
              <li>1 credit = 1 behavior analysis</li>
              <li>Credits never expire</li>
              <li>Secure payment via Stripe</li>
              <li>Instant credit delivery</li>
            </ul>
          </div>
        </div>
      </div>
    </div>
  );
}

