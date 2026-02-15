import React, { useState, useCallback, useRef } from 'react';
import { useNavigate, Link } from 'react-router-dom';

const CENTRAL_PLATFORM_URL =
  'https://mk-ai-global-page.s3.us-east-1.amazonaws.com/platform/index.html';
const LOAD_TIMEOUT_MS = 8000;

export function Platform() {
  const navigate = useNavigate();
  const [loading, setLoading] = useState(true);
  const [loadFailed, setLoadFailed] = useState(false);
  const timeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const loadedRef = useRef(false);

  const handleLoad = useCallback(() => {
    if (timeoutRef.current) {
      clearTimeout(timeoutRef.current);
      timeoutRef.current = null;
    }
    loadedRef.current = true;
    setLoading(false);
    setLoadFailed(false);
  }, []);

  const handleError = useCallback(() => {
    if (timeoutRef.current) {
      clearTimeout(timeoutRef.current);
      timeoutRef.current = null;
    }
    setLoading(false);
    setLoadFailed(true);
  }, []);

  React.useEffect(() => {
    if (loadedRef.current) return;
    timeoutRef.current = setTimeout(() => {
      if (!loadedRef.current) {
        setLoading(false);
        setLoadFailed(true);
      }
      timeoutRef.current = null;
    }, LOAD_TIMEOUT_MS);
    return () => {
      if (timeoutRef.current) clearTimeout(timeoutRef.current);
    };
  }, []);

  return (
    <div className="platform-page">
      <div className="platform-top-bar" />
      <header className="platform-nav">
        <div className="platform-nav-inner">
          <div className="platform-nav-brand">
            <span className="platform-nav-title">Platform</span>
            <span className="platform-nav-subtitle">MK AI &amp; Performance Systems</span>
          </div>
          <button
            type="button"
            className="platform-back-btn"
            onClick={() => navigate('/')}
            aria-label="Back to Translator"
          >
            <span className="platform-back-icon">💕</span>
            Back to Translator
          </button>
        </div>
      </header>

      <div className="platform-iframe-container">
        {loading && !loadFailed && (
          <div className="platform-loading">
            <span className="platform-spinner" aria-hidden="true" />
            <span>Loading platform...</span>
          </div>
        )}
        {loadFailed && (
          <div className="platform-fallback">
            <p>Unable to load platform page. Open in a new tab.</p>
            <a
              href={CENTRAL_PLATFORM_URL}
              target="_blank"
              rel="noopener noreferrer"
              className="platform-fallback-link"
            >
              Open Platform
            </a>
          </div>
        )}
        {!loadFailed && (
          <iframe
            src={CENTRAL_PLATFORM_URL}
            title="MK AI &amp; Performance Systems"
            loading="lazy"
            referrerPolicy="no-referrer-when-downgrade"
            sandbox="allow-same-origin allow-scripts allow-popups allow-forms"
            allow="clipboard-read; clipboard-write"
            className={`platform-iframe ${loading ? 'platform-iframe-loading' : ''}`}
            onLoad={handleLoad}
            onError={handleError}
          />
        )}
      </div>

      <footer className="platform-footer">
        <div className="platform-footer-content">
          <Link to="/" className="platform-footer-link">💕 Back to Translator</Link>
          <p className="platform-footer-copyright">
            © {new Date().getFullYear()} MK AI &amp; Performance Systems
          </p>
        </div>
      </footer>
    </div>
  );
}
