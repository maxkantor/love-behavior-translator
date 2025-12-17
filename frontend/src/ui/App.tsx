import React, { useMemo, useState } from 'react';

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
};

function getApiBaseUrl(): string {
  const envUrl = import.meta.env.VITE_API_BASE_URL as string | undefined;
  if (envUrl && envUrl.trim()) return envUrl.replace(/\/+$/, '');
  return '';
}

export function App() {
  const apiBaseUrl = useMemo(() => getApiBaseUrl(), []);
  const [behavior, setBehavior] = useState('');
  const [relationshipType, setRelationshipType] = useState<RelationshipType | ''>('');
  const [relationshipLength, setRelationshipLength] = useState('');
  const [emotionalState, setEmotionalState] = useState<EmotionalState | ''>('');
  const [mode, setMode] = useState<AnalysisMode>('gentle');
  const [emailTo, setEmailTo] = useState('');

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<AnalyzeResponse | null>(null);

  const remaining = 2000 - behavior.length;

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setResult(null);

    const trimmed = behavior.trim();
    if (trimmed.length < 10) {
      setError('Please describe the behavior in at least 10 characters.');
      return;
    }
    if (trimmed.length > 2000) {
      setError('Please keep the description to 2000 characters or less.');
      return;
    }
    if (!apiBaseUrl) {
      setError('Missing API configuration. Set VITE_API_BASE_URL in Amplify environment variables.');
      return;
    }

    const payload: AnalyzeRequest = {
      behavior_description: trimmed,
      analysis_mode: mode,
    };

    if (relationshipType) payload.relationship_type = relationshipType;
    if (relationshipLength.trim()) payload.relationship_length = relationshipLength.trim();
    if (emotionalState) payload.emotional_state = emotionalState;
    if (emailTo.trim()) payload.email_to = emailTo.trim();

    setLoading(true);
    try {
      const resp = await fetch(`${apiBaseUrl}/analyze`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      });

      const data = (await resp.json()) as any;
      if (!resp.ok) {
        setError(data?.detail ?? data?.error ?? 'Request failed.');
        return;
      }
      setResult(data as AnalyzeResponse);
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

  return (
    <div className="page">
      <div className="shell">
        <header className="header">
          <h1>Love Behavior Translator</h1>
          <p className="sub">
            Compassionate relationship insights based on the behavior you describe.
          </p>
        </header>

        <div className="disclaimer">
          <strong>Disclaimer:</strong> This app provides general relationship insights and is not
          professional therapy or counseling.
        </div>

        <main className="main">
          {!result ? (
            <form className="card" onSubmit={onSubmit}>
              <label className="label">
                Describe the behavior you’d like to understand <span className="req">*</span>
              </label>
              <textarea
                className="textarea"
                rows={7}
                value={behavior}
                onChange={(e) => setBehavior(e.target.value)}
                placeholder="Example: My partner has been canceling plans last minute and seems distant when we do spend time together..."
              />
              <div className={`hint ${remaining < 0 ? 'bad' : ''}`}>
                {behavior.length} / 2000 characters
              </div>

              <div className="grid2">
                <div>
                  <label className="label">Relationship type</label>
                  <select
                    className="input"
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
                <div>
                  <label className="label">Relationship length</label>
                  <input
                    className="input"
                    value={relationshipLength}
                    onChange={(e) => setRelationshipLength(e.target.value)}
                    placeholder="e.g., 3 months, 2 years"
                  />
                </div>
              </div>

              <div className="grid2">
                <div>
                  <label className="label">Your emotional state</label>
                  <select
                    className="input"
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
                <div>
                  <label className="label">Analysis style</label>
                  <select className="input" value={mode} onChange={(e) => setMode(e.target.value as any)}>
                    <option value="gentle">Gentle</option>
                    <option value="analytical">Analytical</option>
                    <option value="brutally_honest">Brutally honest</option>
                    <option value="light_funny">Light & funny</option>
                  </select>
                </div>
              </div>

              <div>
                <label className="label">Email me the result (optional)</label>
                <input
                  className="input"
                  value={emailTo}
                  onChange={(e) => setEmailTo(e.target.value)}
                  placeholder="name@example.com"
                />
                <div className="hint">
                  Requires SES verified sender in your AWS account/region.
                </div>
              </div>

              {error ? <div className="error">{error}</div> : null}

              <button className="button" disabled={loading}>
                {loading ? 'Analyzing…' : 'Analyze behavior'}
              </button>

              <div className="hint">
                API: {apiBaseUrl ? apiBaseUrl : '(not configured)'}
              </div>
            </form>
          ) : (
            <div className="card">
              <div className="resultHeader">
                <h2>Results</h2>
                <span className="pill">{result.mode_used}</span>
              </div>

              <Section title="Analysis" text={result.analysis} />
              <Section title="Emotional Insight" text={result.emotional_insight} />
              <Section title="Practical Next Steps" text={result.practical_advice} />
              <Section title="Gentle Reassurance" text={result.reassurance} />

              <button className="button secondary" onClick={onNew}>
                New analysis
              </button>
            </div>
          )}
        </main>

        <footer className="footer">
          <span>Built for clarity, calm communication, and self-respect.</span>
        </footer>
      </div>
    </div>
  );
}

function Section(props: { title: string; text: string }) {
  return (
    <section className="section">
      <h3>{props.title}</h3>
      <p>{props.text}</p>
    </section>
  );
}


