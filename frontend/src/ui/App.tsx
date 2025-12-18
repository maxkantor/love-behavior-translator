import React, { useMemo, useState, useEffect } from 'react';
import { useCredits } from './CreditContext';
import { Link } from 'react-router-dom';
import { CreditModal } from './CreditModal';
import { HelpModal } from './HelpModal';

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
  const { credits, setCredits, userId } = useCredits();
  const [behavior, setBehavior] = useState('');
  const [relationshipType, setRelationshipType] = useState<RelationshipType | ''>('');
  const [relationshipLength, setRelationshipLength] = useState('');
  const [emotionalState, setEmotionalState] = useState<EmotionalState | ''>('');
  const [mode, setMode] = useState<AnalysisMode>('gentle');
  const [emailTo, setEmailTo] = useState('');

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<AnalyzeResponse | null>(null);
  const [showCreditModal, setShowCreditModal] = useState(false);
  const [showHelpModal, setShowHelpModal] = useState(false);

  const remaining = 2000 - behavior.length;

  // Update credits from localStorage on mount
  useEffect(() => {
    const stored = localStorage.getItem('credits');
    if (stored) {
      const parsed = parseInt(stored, 10);
      if (!isNaN(parsed)) {
        setCredits(parsed);
      }
    }
  }, [setCredits]);

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
      analysis_mode: mode,
    };

    if (relationshipType) payload.relationship_type = relationshipType;
    if (relationshipLength) payload.relationship_length = relationshipLength;
    if (emotionalState) payload.emotional_state = emotionalState;
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

  const displayCredits = credits === null ? '...' : credits === -1 ? 'Unlimited' : credits.toString();

  return (
    <div className="app-container">
      <div className="background-pattern"></div>
      
      <header className="app-header">
        <div className="header-content">
          <h1>
            <span className="heart-icon">💕</span>
            AI Love Behavior Translator
          </h1>
          <p className="tagline">Get AI-powered relationship insights and actionable advice for your love life.</p>
          
          <div className="credits-header">
            <div className="credits-badge-large">
              {displayCredits} Relationship Insights
            </div>
            <span className="credits-explanation">1 Deep Relationship Reading</span>
          </div>
        </div>
      </header>

      <main className="app-main">
        <div className="main-card">
          <div className="entry-points-section">
            <h3 className="entry-points-title">What's on your mind?</h3>
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
          </div>

          <textarea
            className="behavior-input"
            rows={6}
            value={behavior}
            onChange={(e) => setBehavior(e.target.value)}
            placeholder="Describe your relationship behavior... e.g., 'My partner has been canceling plans last minute and seems distant when we do spend time together'"
          />

          <div className="emotional-trigger">
            <p className="trigger-text">
              Most people miss the real meaning behind their partner's behavior.
            </p>
            <p className="trigger-text">
              This AI is trained to spot patterns humans ignore.
            </p>
          </div>

          <div className="trust-indicators">
            <div className="social-proof-stats">
              <div className="stat-item">
                <span className="stat-number">1,247</span>
                <span className="stat-label">people used this month</span>
              </div>
              <div className="stat-item">
                <span className="stat-number">87%</span>
                <span className="stat-label">say it clarified their situation</span>
              </div>
            </div>
            <div className="testimonials">
              <div className="testimonial-item">
                <span className="testimonial-quote">"This helped me finally understand why he shut down."</span>
                <span className="testimonial-author">— Anonymous</span>
              </div>
              <div className="testimonial-item">
                <span className="testimonial-quote">"I thought I was overthinking. Turns out I wasn't."</span>
                <span className="testimonial-author">— Anonymous</span>
              </div>
            </div>
            <div className="support-badges">
              <span>💑 Dating</span>
              <span>💍 Married</span>
              <span>💕 All relationships</span>
            </div>
          </div>

          <div className="quick-actions">
            <span className="quick-actions-label">Quick actions:</span>
            <div className="quick-action-buttons">
              <button
                type="button"
                className="quick-action-btn"
                onClick={() => handleQuickAction("My partner has been canceling plans last minute")}
              >
                💔 Canceling plans
              </button>
              <button
                type="button"
                className="quick-action-btn"
                onClick={() => handleQuickAction("My partner seems distant and avoids deep conversations")}
              >
                😔 Feeling distant
              </button>
              <button
                type="button"
                className="quick-action-btn"
                onClick={() => handleQuickAction("My partner gets defensive when I try to discuss our relationship")}
              >
                🛡️ Defensive behavior
              </button>
              <button
                type="button"
                className="quick-action-btn"
                onClick={() => handleQuickAction("My partner never initiates contact or makes plans")}
              >
                📱 No initiation
              </button>
            </div>
          </div>

          <form onSubmit={onSubmit} className="analysis-form">
            <div className="form-row">
              <div className="form-group">
                <label>Relationship type</label>
                <select
                  className="form-input"
                  value={relationshipType}
                  onChange={(e) => setRelationshipType(e.target.value as any)}
                >
                  <option value="">Optional</option>
                  <option value="dating">Dating</option>
                  <option value="married">Married</option>
                  <option value="situationship">Situationship</option>
                  <option value="friendship">Friendship</option>
                  <option value="other">Other</option>
                </select>
              </div>

              <div className="form-group">
                <label>Relationship length</label>
                <input
                  type="text"
                  className="form-input"
                  value={relationshipLength}
                  onChange={(e) => setRelationshipLength(e.target.value)}
                  placeholder="e.g., 3 months, 2 years"
                />
              </div>

              <div className="form-group">
                <label>Your emotional state</label>
                <select
                  className="form-input"
                  value={emotionalState}
                  onChange={(e) => setEmotionalState(e.target.value as any)}
                >
                  <option value="">Optional</option>
                  <option value="anxious">Anxious</option>
                  <option value="confused">Confused</option>
                  <option value="hurt">Hurt</option>
                  <option value="hopeful">Hopeful</option>
                  <option value="neutral">Neutral</option>
                  <option value="frustrated">Frustrated</option>
                </select>
              </div>

              <div className="form-group">
                <label>Analysis style</label>
                <select
                  className="form-input"
                  value={mode}
                  onChange={(e) => setMode(e.target.value as AnalysisMode)}
                >
                  <option value="gentle">Gentle</option>
                  <option value="analytical">Analytical</option>
                  <option value="brutally_honest">Brutally Honest</option>
                  <option value="light_funny">Light & Funny</option>
                </select>
              </div>
            </div>

          {error && (
            <div className="error-message">
              {error}
            </div>
          )}

          <div className="reassurance-text">
            You're probably not overthinking it.
          </div>

          <button
            type="submit"
            className="analyze-button"
            disabled={loading || behavior.trim().length < 10}
          >
              {loading ? (
                <>
                  <span className="spinner"></span>
                  Analyzing...
                </>
              ) : (
                <>
                  <span className="heart-icon-small">💕</span>
                  Analyze My Relationship Behavior
                </>
              )}
            </button>
          </form>

          <div className="how-it-works">
            <h3>How This Works</h3>
            <p>
              Our AI analyzes your relationship behavior patterns and provides evidence-based insights, not literal translation. 
              You'll receive likely causes, emotional insights, practical advice, and guidance on when to consult a relationship counselor.
            </p>
          </div>
        </div>

        {result && (
          <div className="result-card">
            <div className="result-header">
              <h2>Analysis Results</h2>
              <button onClick={onNew} className="new-analysis-btn">New Analysis</button>
            </div>
            
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

            <div className="result-footer">
              <span className="mode-badge">Mode: {result.mode_used}</span>
              {result.credits_remaining && (
                <span className="credits-remaining">
                  Relationship Insights remaining: {result.credits_remaining === 'unlimited' ? 'Unlimited' : result.credits_remaining}
                </span>
              )}
            </div>
          </div>
        )}
      </main>

      <footer className="app-footer">
        <div className="footer-content">
          <div className="footer-left">
            <div className="footer-section">
              <h3 className="footer-title">Start with 5 Free Analysis</h3>
              <p className="footer-text">
                Get 5 free deep relationship readings to understand what's really happening in your relationship. No credit card required.
              </p>
            </div>

            <div className="footer-section">
              <h3 className="footer-title">Popular Relationship Questions</h3>
              <ul className="footer-links-list">
                <li><button type="button" onClick={() => handleQuickAction("My partner has been pulling away. They used to text me all the time, but now I'm always the one initiating.")}>Is my partner pulling away?</button></li>
                <li><button type="button" onClick={() => handleQuickAction("I notice a pattern where I get anxious when my partner needs space, but they seem to pull away more when I try to get closer.")}>Avoidant vs anxious attachment</button></li>
                <li><button type="button" onClick={() => handleQuickAction("I'm worried my partner is losing interest. They don't make plans anymore and our conversations feel surface-level.")}>Are they losing interest?</button></li>
                <li><button type="button" onClick={() => handleQuickAction("I'm at a crossroads in my relationship. Part of me wants to stay and work through our issues, but another part wonders if I'm wasting my time.")}>Should I stay or leave?</button></li>
              </ul>
            </div>
          </div>

          <div className="footer-right">
            <div className="footer-section">
              <h3 className="footer-title">What This AI Can & Can't Do</h3>
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
          </div>
        </div>

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
