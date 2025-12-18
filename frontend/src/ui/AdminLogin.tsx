import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';

function getApiBaseUrl(): string {
  const envUrl = import.meta.env.VITE_API_BASE_URL as string | undefined;
  if (envUrl && envUrl.trim()) return envUrl.replace(/\/+$/, '');
  return '';
}

export function AdminLogin() {
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const navigate = useNavigate();
  const apiBaseUrl = getApiBaseUrl();

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setLoading(true);

    try {
      const resp = await fetch(`${apiBaseUrl}/admin/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ password }),
      });

      const data = await resp.json();
      if (!resp.ok) {
        setError(data?.error ?? 'Login failed');
        return;
      }

      // Store admin token
      localStorage.setItem('adminToken', data.token);
      navigate('/admin');
    } catch (err: any) {
      setError(err?.message ?? 'Network error');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="admin-login-page">
      <div className="admin-login-card">
        <div className="admin-login-header">
          <div className="admin-icon">👤🛡️</div>
          <h1>Admin Login</h1>
          <p>Enter your credentials to access the admin dashboard</p>
        </div>

        <form onSubmit={handleSubmit}>
          <div className="admin-form-group">
            <label>
              <span className="admin-icon-small">👤</span>
              Username
            </label>
            <input
              type="text"
              placeholder="Enter username"
              value="admin"
              disabled
              className="admin-input"
            />
          </div>

          <div className="admin-form-group">
            <label>
              <span className="admin-icon-small">🔒</span>
              Password
            </label>
            <input
              type="password"
              placeholder="Enter password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="admin-input"
              required
            />
          </div>

          {error && <div className="admin-error">{error}</div>}

          <button type="submit" className="admin-login-button" disabled={loading}>
            {loading ? 'Logging in...' : '🔒 Login'}
          </button>
        </form>
      </div>
    </div>
  );
}

