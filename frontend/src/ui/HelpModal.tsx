import React, { useState } from 'react';

type HelpModalProps = {
  isOpen: boolean;
  onClose: () => void;
};

export function HelpModal({ isOpen, onClose }: HelpModalProps) {
  const [email, setEmail] = useState('');
  const [subject, setSubject] = useState('');
  const [message, setMessage] = useState('');

  if (!isOpen) return null;

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    // TODO: Send to backend support endpoint
    alert('Support message sent! (Backend integration coming soon)');
    setEmail('');
    setSubject('');
    setMessage('');
    onClose();
  }

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="support-modal-content" onClick={(e) => e.stopPropagation()}>
        <div className="support-modal-header">
          <div className="headset-icon">🎧</div>
          <h1>Contact Support</h1>
        </div>

        <form className="support-form" onSubmit={handleSubmit}>
          <div className="form-field">
            <label>
              <span className="field-icon">✉</span>
              Email <span className="required">*</span>
            </label>
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="your@email.com"
              required
              className="support-input"
            />
          </div>

          <div className="form-field">
            <label>
              Subject <span className="required">*</span>
            </label>
            <input
              type="text"
              value={subject}
              onChange={(e) => setSubject(e.target.value)}
              placeholder="What's this about?"
              required
              className="support-input"
            />
          </div>

          <div className="form-field">
            <label>
              Message <span className="required">*</span>
            </label>
            <textarea
              value={message}
              onChange={(e) => setMessage(e.target.value)}
              placeholder="Describe your question or issue..."
              rows={6}
              required
              className="support-textarea"
            />
          </div>

          <button type="submit" className="send-message-button">
            <span className="envelope-icon">✉</span>
            Send Message
          </button>
        </form>

        <div className="response-times-box">
          <div className="response-times-title">Response Times:</div>
          <div className="response-time-item">
            <span className="premium-icon">👑</span>
            <strong>Premium:</strong> Within 24 hours
          </div>
          <div className="response-time-item">
            <strong>Free:</strong> Within 48 hours
          </div>
        </div>

        <button className="back-button support-back" onClick={onClose}>
          <span className="heart-icon-back">💕</span>
          Back to Translator
        </button>
      </div>
    </div>
  );
}
