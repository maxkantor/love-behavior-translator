import React, { useState } from 'react';

function getApiBaseUrl(): string {
  const envUrl = import.meta.env.VITE_API_BASE_URL as string | undefined;
  if (envUrl && envUrl.trim()) return envUrl.replace(/\/+$/, '');
  return '';
}

function isValidEmail(email: string): boolean {
  if (!email || email.length > 254) return false;
  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  return emailRegex.test(email);
}

type HelpModalProps = {
  isOpen: boolean;
  onClose: () => void;
};

export function HelpModal({ isOpen, onClose }: HelpModalProps) {
  const [email, setEmail] = useState('');
  const [subject, setSubject] = useState('');
  const [message, setMessage] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitStatus, setSubmitStatus] = useState<'idle' | 'success' | 'error'>('idle');
  const [emailError, setEmailError] = useState('');

  if (!isOpen) return null;

  const faqs = [
    {
      question: "How does the AI analysis work?",
      answer: "Our AI analyzes the relationship behavior you describe and provides evidence-based insights, emotional understanding, practical advice, and gentle reassurance. It's designed to help you understand patterns and communicate better."
    },
    {
      question: "What analysis styles are available?",
      answer: "We offer four analysis styles: Gentle (compassionate and supportive), Analytical (fact-based and structured), Brutally Honest (direct and unfiltered), and Light & Funny (uplifting with humor)."
    },
    {
      question: "How do Relationship Insights work?",
      answer: "Each deep relationship reading costs 1 Relationship Insight. New users receive 5 free insights to get started. You can unlock additional insights anytime. Your insights never expire."
    },
    {
      question: "Is this professional therapy?",
      answer: "No. This app provides general relationship insights and is not professional therapy or counseling. For serious relationship issues, please consult a licensed therapist or counselor."
    },
    {
      question: "Can I get a refund?",
      answer: "If you're not satisfied with your analysis, please contact support. We offer refunds for unused relationship insights within 30 days of purchase."
    },
    {
      question: "How do I contact support?",
      answer: "You can reach our support team by filling out the contact form below or emailing support@lovebehaviortranslator.com. We typically respond within 24 hours for premium users and 48 hours for free users."
    }
  ];

  function handleEmailChange(e: React.ChangeEvent<HTMLInputElement>) {
    const value = e.target.value;
    setEmail(value);
    
    if (value && !isValidEmail(value)) {
      setEmailError('Please enter a valid email address');
    } else {
      setEmailError('');
    }
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    
    // Validate email
    if (!isValidEmail(email)) {
      setEmailError('Please enter a valid email address');
      return;
    }

    setIsSubmitting(true);
    setSubmitStatus('idle');
    setEmailError('');

    try {
      const apiBaseUrl = getApiBaseUrl();
      if (!apiBaseUrl) {
        throw new Error('API base URL not configured');
      }

      const resp = await fetch(`${apiBaseUrl}/contact`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          email: email.trim(),
          subject: subject.trim(),
          message: message.trim(),
        }),
      });

      if (!resp.ok) {
        const errorData = await resp.json();
        throw new Error(errorData.error || 'Failed to send message');
      }
      
      setSubmitStatus('success');
      setEmail('');
      setSubject('');
      setMessage('');
      
      // Reset status after 3 seconds
      setTimeout(() => {
        setSubmitStatus('idle');
        onClose();
      }, 3000);
    } catch (error: any) {
      setSubmitStatus('error');
      console.error('Error submitting contact form:', error);
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="support-modal-content" onClick={(e) => e.stopPropagation()}>
        <div className="support-modal-header">
          <div className="headset-icon">🎧</div>
          <h1>Need Help?</h1>
        </div>

        <div className="help-content-wrapper">
          {/* FAQ Section */}
          <section className="faq-section">
            <h2 className="section-title">Frequently Asked Questions</h2>
            <div className="faq-list">
              {faqs.map((faq, index) => (
                <div key={index} className="faq-item">
                  <div className="faq-question">{faq.question}</div>
                  <div className="faq-answer">{faq.answer}</div>
                </div>
              ))}
            </div>
          </section>

          {/* Contact Form Section */}
          <section className="contact-section">
            <h2 className="section-title">Contact Support</h2>
            <form className="support-form" onSubmit={handleSubmit}>
              <div className="form-field">
                <label>
                  <span className="field-icon">✉</span>
                  Email <span className="required">*</span>
                </label>
                <input
                  type="email"
                  value={email}
                  onChange={handleEmailChange}
                  onBlur={() => {
                    if (email && !isValidEmail(email)) {
                      setEmailError('Please enter a valid email address');
                    }
                  }}
                  placeholder="your@email.com"
                  required
                  className={`support-input ${emailError ? 'input-error' : ''}`}
                  disabled={isSubmitting}
                />
                {emailError && (
                  <span className="field-error">{emailError}</span>
                )}
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
                  disabled={isSubmitting}
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
                  disabled={isSubmitting}
                />
              </div>

              {submitStatus === 'success' && (
                <div className="submit-success">
                  ✓ Message sent successfully! We'll get back to you soon.
                </div>
              )}

              {submitStatus === 'error' && (
                <div className="submit-error">
                  ✗ Failed to send message. Please try again.
                </div>
              )}

              <button 
                type="submit" 
                className="send-message-button"
                disabled={isSubmitting}
              >
                {isSubmitting ? (
                  <>
                    <span className="spinner-small"></span>
                    Sending...
                  </>
                ) : (
                  <>
                    <span className="envelope-icon">✉</span>
                    Send Message
                  </>
                )}
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
          </section>
        </div>

        <button className="back-button support-back" onClick={onClose}>
          <span className="heart-icon-back">💕</span>
          Back to Translator
        </button>
      </div>
    </div>
  );
}
