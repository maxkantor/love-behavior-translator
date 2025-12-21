import React, { useMemo, useState, useEffect } from 'react';
import { useCredits } from './CreditContext';
import { Link } from 'react-router-dom';
import { CreditModal } from './CreditModal';
import { HelpModal } from './HelpModal';

// Import images from assets
import female1 from '../assets/images/female-1.jpg';
import female2 from '../assets/images/female-2.jpg';
import female3 from '../assets/images/female-3.jpg';
import male1 from '../assets/images/male-1.jpg';
import male2 from '../assets/images/male-2.jpg';
import male3 from '../assets/images/male-3.jpg';

type AnalysisMode = 'gentle' | 'analytical' | 'brutally_honest' | 'light_funny';
type RelationshipType = 'dating' | 'married' | 'situationship' | 'friendship' | 'other';
type EmotionalState = 'anxious' | 'confused' | 'hurt' | 'hopeful' | 'neutral' | 'frustrated';

type AnalyzeRequest = {
  behavior_description: string;
  relationship_type?: RelationshipType;
  relationship_length?: string;
  emotional_state?: EmotionalState;
  analysis_mode: AnalysisMode;
  email_to?: string;
};

type AnalyzeResponse = {
  analysis: string;
  emotional_insight: string;
  practical_advice: string;
  reassurance: string;
  mode_used: string;
  credits_remaining?: string | number;
};

function getApiBaseUrl(): string {
  const envUrl = import.meta.env.VITE_API_BASE_URL as string | undefined;
  if (envUrl && envUrl.trim()) return envUrl.replace(/\/+$/, '');
  return '';
}

export function App() {
  const apiBaseUrl = useMemo(() => getApiBaseUrl(), []);
  const { credits, setCredits, userId, refreshCredits } = useCredits();
  const [behavior, setBehavior] = useState('');
  const [emailTo, setEmailTo] = useState('');
  const [relationshipType, setRelationshipType] = useState<RelationshipType | ''>('');
  const [relationshipLength, setRelationshipLength] = useState('');
  const [emotionalState, setEmotionalState] = useState<EmotionalState | ''>('');
  const [mode, setMode] = useState<AnalysisMode>('gentle');
  const [selectedChips, setSelectedChips] = useState<Set<string>>(new Set());

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<AnalyzeResponse | null>(null);
  const [showCreditModal, setShowCreditModal] = useState(false);
  const [showHelpModal, setShowHelpModal] = useState(false);

  const remaining = 2000 - behavior.length;

  // Handle payment success/cancellation
  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    const payment = params.get('payment');
    if (payment === 'success') {
      const creditsPurchased = params.get('credits');
      refreshCredits();
      // Remove payment params from URL
      window.history.replaceState({}, '', window.location.pathname);
      if (creditsPurchased) {
        // Show success message (optional)
        setTimeout(() => {
          alert(`Payment successful! ${creditsPurchased} credits have been added to your account.`);
        }, 500);
      }
    } else if (payment === 'cancelled') {
      // Remove payment params from URL
      window.history.replaceState({}, '', window.location.pathname);
    }
  }, [refreshCredits]);

  // Note: refreshCredits is already called in CreditContext on mount
  // This is just for periodic refresh

  // Refresh credits periodically and on window focus
  useEffect(() => {
    // Refresh every 10 seconds (more frequent for testing)
    const interval = setInterval(() => {
      refreshCredits();
    }, 10000);

    // Refresh when window gains focus (user comes back to tab)
    const handleFocus = () => {
      refreshCredits();
    };
    window.addEventListener('focus', handleFocus);

    return () => {
      clearInterval(interval);
      window.removeEventListener('focus', handleFocus);
    };
  }, [refreshCredits]);

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setResult(null);

    const trimmed = behavior.trim();
    if (trimmed.length < 10) {
      setError('Please describe the behavior in at least 10 characters.');
      return;
    }

    const payload: AnalyzeRequest = {
      behavior_description: trimmed,
      analysis_mode: 'gentle', // Default to gentle mode
    };

    if (emailTo) payload.email_to = emailTo;

    setLoading(true);
    try {
      const resp = await fetch(`${apiBaseUrl}/analyze`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'x-user-id': userId,
        },
        body: JSON.stringify(payload),
      });

      const data = (await resp.json()) as any;
      if (!resp.ok) {
        if (resp.status === 402) {
          // Insufficient insights
          setError(data?.detail ?? data?.error ?? 'You need more relationship insights to continue. Unlock clarity to get deeper understanding.');
          if (data.credits !== undefined) {
            setCredits(data.credits);
            localStorage.setItem('credits', data.credits.toString());
          }
        } else {
          setError(data?.detail ?? data?.error ?? 'Request failed.');
        }
        return;
      }
      setResult(data as AnalyzeResponse);
      
      // Update credits from response
      if (data.credits_remaining !== undefined) {
        const remaining = data.credits_remaining === 'unlimited' ? -1 : parseInt(data.credits_remaining.toString(), 10);
        if (!isNaN(remaining)) {
          setCredits(remaining);
          localStorage.setItem('credits', remaining.toString());
        }
      }
    } catch (err: any) {
      setError(err?.message ?? 'Network error.');
    } finally {
      setLoading(false);
    }
  }

  function onNew() {
    setResult(null);
    setError(null);
    setBehavior('');
    setRelationshipType('');
    setRelationshipLength('');
    setEmotionalState('');
    setMode('gentle');
    setEmailTo('');
  }

  function handleQuickAction(text: string) {
    setBehavior(text);
  }

  function handleChipClick(chipText: string, chipLabel: string) {
    const newSelected = new Set(selectedChips);
    if (newSelected.has(chipLabel)) {
      newSelected.delete(chipLabel);
    } else {
      newSelected.add(chipLabel);
      // Append to textarea if not already there
      if (!behavior.includes(chipText)) {
        setBehavior(prev => prev ? `${prev}\n\n${chipText}` : chipText);
      }
    }
    setSelectedChips(newSelected);
  }

  const displayCredits = credits === null ? '...' : credits === -1 ? 'Unlimited' : credits.toString();

  return (
    <div className="app-container">
      <div className="background-pattern"></div>
      
      <header className="app-header">
        <div className="header-content">
          <h1>
            <span className="heart-icon">💕</span>
            Understand What's Really Happening in Your Relationship
          </h1>
          <p className="tagline">You're probably not imagining it. Get clarity on your partner's behavior in minutes.</p>
          
          <div className="credits-header">
            <div className="credits-display-pill">
              <div className="credits-icon-wrapper">
                <span className="credits-icon">🪙</span>
                <span className="credits-icon">🪙</span>
                <span className="credits-icon">🪙</span>
              </div>
              <span className="credits-number">{displayCredits}</span>
              <span className="credits-label">Credits</span>
              <button 
                onClick={() => refreshCredits()} 
                className="refresh-credits-btn"
                title="Refresh credits"
              >
                🔄
              </button>
            </div>
            <div className="credits-explanation-text">
              1 credit = 1 behavior analysis
            </div>
            {import.meta.env.MODE === 'development' && (
              <div style={{ fontSize: '0.7rem', opacity: 0.7, marginTop: '4px' }}>
                User ID: {userId}
              </div>
            )}
          </div>
        </div>
      </header>

      <main className="app-main-wrapper">
        {/* Left Sidebar - on purple background */}
        <div className="main-sidebar main-sidebar-left">
          <div className="sidebar-section">
            <h3 className="sidebar-title">Popular Relationship Questions</h3>
            <ul className="sidebar-links-list">
              <li><button type="button" onClick={() => handleQuickAction("My partner has been pulling away. They used to text me all the time, but now I'm always the one initiating.")}>Is my partner pulling away?</button></li>
              <li><button type="button" onClick={() => handleQuickAction("I notice a pattern where I get anxious when my partner needs space, but they seem to pull away more when I try to get closer.")}>Avoidant vs anxious attachment</button></li>
              <li><button type="button" onClick={() => handleQuickAction("I'm worried my partner is losing interest. They don't make plans anymore and our conversations feel surface-level.")}>Are they losing interest?</button></li>
              <li><button type="button" onClick={() => handleQuickAction("I'm at a crossroads in my relationship. Part of me wants to stay and work through our issues, but another part wonders if I'm wasting my time.")}>Should I stay or leave?</button></li>
            </ul>
          </div>

          <div className="sidebar-section">
            <h3 className="sidebar-title">What This AI Can & Can't Do</h3>
            <div className="can-cannot">
              <div className="can-item">
                <span className="check-icon-green">✓</span>
                <span>Spot relationship patterns you might miss</span>
              </div>
              <div className="can-item">
                <span className="check-icon-green">✓</span>
                <span>Provide emotional insights and validation</span>
              </div>
              <div className="can-item">
                <span className="check-icon-green">✓</span>
                <span>Offer practical communication advice</span>
              </div>
              <div className="cannot-item">
                <span className="x-icon">✗</span>
                <span>Replace professional therapy or counseling</span>
              </div>
              <div className="cannot-item">
                <span className="x-icon">✗</span>
                <span>Diagnose mental health conditions</span>
              </div>
            </div>
          </div>

          <div className="sidebar-section trust-section-sidebar">
            <div className="trust-stats-group">
              <div className="stat-item">
                <span className="stat-number">1,247</span>
                <span className="stat-label">people found clarity this month</span>
              </div>
              <div className="stat-item">
                <span className="stat-number">87%</span>
                <span className="stat-label">say it helped them understand</span>
              </div>
            </div>
            <div className="testimonials-group">
              <div className="testimonial-item">
                <span className="testimonial-quote">"This helped me finally understand why he shut down."</span>
                <span className="testimonial-author">— Anonymous</span>
              </div>
              <div className="testimonial-item">
                <span className="testimonial-quote">"I thought I was overthinking. Turns out I wasn't."</span>
                <span className="testimonial-author">— Anonymous</span>
              </div>
            </div>
          </div>
        </div>

        {/* Center White Form */}
        <div className="app-main">
          <div className="main-content-center">
            {result ? (
              <div className="result-card-inline">
                <div className="result-header">
                  <h2>Analysis Results</h2>
                  <button onClick={onNew} className="new-analysis-btn">New Analysis</button>
                </div>
                
                <div className="result-content-scrollable">
                  <div className="result-section">
                    <h3>Analysis</h3>
                    <p>{result.analysis}</p>
                  </div>

                  <div className="result-section">
                    <h3>Emotional Insight</h3>
                    <p>{result.emotional_insight}</p>
                  </div>

                  <div className="result-section">
                    <h3>Practical Advice</h3>
                    <p>{result.practical_advice}</p>
                  </div>

                  <div className="result-section">
                    <h3>Reassurance</h3>
                    <p>{result.reassurance}</p>
                  </div>
                </div>

                <div className="result-footer">
                  <span className="mode-badge">Mode: {result.mode_used}</span>
                  {result.credits_remaining && (
                    <span className="credits-remaining">
                      Credits remaining: {result.credits_remaining === 'unlimited' ? 'Unlimited' : result.credits_remaining}
                    </span>
                  )}
                </div>
              </div>
            ) : (
              <>
            <div className="entry-points-section">
              <h3 className="entry-points-title">What's on your mind?</h3>
              
              {/* Main entry point cards */}
              <div className="entry-points-grid">
                <button
                  type="button"
                  className="entry-point-btn"
                  onClick={() => handleQuickAction("My partner has been pulling away. They used to text me all the time, but now I'm always the one initiating. When we're together, they seem distracted and less engaged. I'm worried they're losing interest.")}
                >
                  <span className="entry-icon">💔</span>
                  <span className="entry-text">Is My Partner Pulling Away?</span>
                </button>
                <button
                  type="button"
                  className="entry-point-btn"
                  onClick={() => handleQuickAction("I notice a pattern where I get anxious when my partner needs space, but they seem to pull away more when I try to get closer. I think I might be anxious-attached and they might be avoidant. How do I understand this dynamic?")}
                >
                  <span className="entry-icon">🔄</span>
                  <span className="entry-text">Avoidant vs Anxious Behavior</span>
                </button>
                <button
                  type="button"
                  className="entry-point-btn"
                  onClick={() => handleQuickAction("I'm worried my partner is losing interest. They don't make plans anymore, our conversations feel surface-level, and the intimacy has faded. I can't tell if this is just a rough patch or if they're checking out of the relationship.")}
                >
                  <span className="entry-icon">😰</span>
                  <span className="entry-text">Are They Losing Interest?</span>
                </button>
                <button
                  type="button"
                  className="entry-point-btn"
                  onClick={() => handleQuickAction("I'm at a crossroads in my relationship. Part of me wants to stay and work through our issues, but another part wonders if I'm wasting my time. The relationship has been rocky, and I'm not sure if the problems are fixable or if I should leave.")}
                >
                  <span className="entry-icon">🤔</span>
                  <span className="entry-text">Should I Stay or Leave?</span>
                </button>
              </div>

              {/* Quick action chips - merged into this section */}
              <div className="quick-chips-container">
                <p className="quick-chips-label">Or describe a specific situation:</p>
                <div className="quick-chips">
                  <button
                    type="button"
                    className={`quick-chip ${selectedChips.has('Canceling plans') ? 'selected' : ''}`}
                    onClick={() => handleChipClick("My partner has been canceling plans last minute", "Canceling plans")}
                  >
                    💔 Canceling plans
                  </button>
                  <button
                    type="button"
                    className={`quick-chip ${selectedChips.has('Feeling distant') ? 'selected' : ''}`}
                    onClick={() => handleChipClick("My partner seems distant and avoids deep conversations", "Feeling distant")}
                  >
                    😔 Feeling distant
                  </button>
                  <button
                    type="button"
                    className={`quick-chip ${selectedChips.has('Defensive behavior') ? 'selected' : ''}`}
                    onClick={() => handleChipClick("My partner gets defensive when I try to discuss our relationship", "Defensive behavior")}
                  >
                    🛡️ Defensive behavior
                  </button>
                  <button
                    type="button"
                    className={`quick-chip ${selectedChips.has('No initiation') ? 'selected' : ''}`}
                    onClick={() => handleChipClick("My partner never initiates contact or makes plans", "No initiation")}
                  >
                    📱 No initiation
                  </button>
                </div>
              </div>
            </div>

            <div className="textarea-wrapper">
              <label htmlFor="behavior-input" className="textarea-label">
                What's happening in your relationship?
              </label>
              <textarea
                id="behavior-input"
                className="behavior-input"
                rows={5}
                value={behavior}
                onChange={(e) => setBehavior(e.target.value)}
                placeholder="Share what you've noticed... For example: 'My partner used to text me throughout the day, but now I'm always the one reaching out. When we're together, they seem distracted and less engaged. I'm worried they're pulling away.'"
              />
              <p className="textarea-hint">Be as detailed as you're comfortable with. This helps us understand the full picture.</p>
            </div>

            <form onSubmit={onSubmit} className="analysis-form">
              {error && (
                <div className="error-message">
                  {error}
                </div>
              )}

              <div className="cta-section">
                <button
                  type="submit"
                  className="analyze-button"
                  disabled={loading || behavior.trim().length < 10}
                >
                  {loading ? (
                    <>
                      <span className="spinner"></span>
                      <span>Analyzing your situation...</span>
                    </>
                  ) : (
                    <>
                      <span className="heart-icon-small">💕</span>
                      <span>Get Clarity Now</span>
                    </>
                  )}
                </button>
                <p className="cta-microcopy">Takes less than 60 seconds. No judgment. Just clarity.</p>
              </div>
            </form>
            </>
            )}
          </div>
        </div>

        {/* Right Sidebar - on purple background */}
        <div className="main-sidebar main-sidebar-right">
          <div className="sidebar-section decorative-images-section">
            <div className="decorative-images-container">
              <img 
                src={female1}
                onError={(e) => {
                  const target = e.currentTarget as HTMLImageElement;
                  target.src = "https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=500&h=500&fit=crop&crop=face&auto=format&q=80";
                }}
                onClick={() => window.open(female1, '_blank')}
                alt="Decorative" 
                className="decorative-image"
                style={{ cursor: 'pointer' }}
              />
              <img 
                src={male1}
                onError={(e) => {
                  const target = e.currentTarget as HTMLImageElement;
                  target.src = "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=500&h=500&fit=crop&crop=face&auto=format&q=80";
                }}
                onClick={() => window.open(male1, '_blank')}
                alt="Decorative" 
                className="decorative-image"
                style={{ cursor: 'pointer' }}
              />
            </div>
          </div>

          <div className="sidebar-section trust-section-sidebar">
            <div className="trust-stats-group">
              <div className="stat-item">
                <span className="stat-number">1,247</span>
                <span className="stat-label">people found clarity this month</span>
              </div>
              <div className="stat-item">
                <span className="stat-number">87%</span>
                <span className="stat-label">say it helped them understand</span>
              </div>
            </div>
            <div className="testimonials-group">
              <div className="testimonial-item">
                <span className="testimonial-quote">"This helped me finally understand why he shut down."</span>
                <span className="testimonial-author">— Anonymous</span>
              </div>
              <div className="testimonial-item">
                <span className="testimonial-quote">"I thought I was overthinking. Turns out I wasn't."</span>
                <span className="testimonial-author">— Anonymous</span>
              </div>
            </div>
          </div>
        </div>
      </main>

      <footer className="app-footer">
        <div className="footer-bottom">
          <div className="footer-buttons">
            <button 
              className="footer-btn buy-credits"
              onClick={() => setShowCreditModal(true)}
            >
              💡 Unlock Clarity
            </button>
            <button 
              className="footer-btn need-help"
              onClick={() => setShowHelpModal(true)}
            >
              ❓ Need Help?
            </button>
          </div>
          <div className="footer-links">
            <Link to="/admin/login" className="admin-link-footer">Admin</Link>
          </div>
          <p className="copyright">© 2025 Love Behavior Translator. All rights reserved.</p>
          <p className="made-with">Made with ❤️ for couples</p>
          <p className="disclaimer-text">
            This app provides general relationship insights and is not professional therapy or counseling.
          </p>
        </div>
      </footer>

      <CreditModal
        isOpen={showCreditModal}
        onClose={() => setShowCreditModal(false)}
        currentCredits={credits}
      />

      <HelpModal
        isOpen={showHelpModal}
        onClose={() => setShowHelpModal(false)}
      />
    </div>
  );
}
