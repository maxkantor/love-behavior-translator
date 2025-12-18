import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';

function getApiBaseUrl(): string {
  const envUrl = import.meta.env.VITE_API_BASE_URL as string | undefined;
  if (envUrl && envUrl.trim()) return envUrl.replace(/\/+$/, '');
  return '';
}

type User = {
  userId: string;
  credits: number;
  createdAt?: string;
  lastAnalysisAt?: string;
  totalAnalyses?: number;
};

type DashboardSummary = {
  todaysTranslations: number;
  todaysPurchases: number;
  activeTokens: number;
  freeSearchLimit: number;
};

export function AdminDashboard() {
  const [users, setUsers] = useState<User[]>([]);
  const [dashboard, setDashboard] = useState<DashboardSummary | null>(null);
  const [selectedUserId, setSelectedUserId] = useState('');
  const [creditAmount, setCreditAmount] = useState('');
  const [grantAmount, setGrantAmount] = useState('100');
  const [loading, setLoading] = useState(false);
  const navigate = useNavigate();
  const apiBaseUrl = getApiBaseUrl();

  const adminToken = localStorage.getItem('adminToken');

  useEffect(() => {
    if (!adminToken) {
      navigate('/admin/login');
      return;
    }
    loadData();
  }, [adminToken, navigate]);

  async function loadData() {
    if (!adminToken) return;
    setLoading(true);
    try {
      const [usersResp, dashboardResp] = await Promise.all([
        fetch(`${apiBaseUrl}/admin/users`, {
          headers: { Authorization: `Bearer ${adminToken}` },
        }),
        fetch(`${apiBaseUrl}/admin/dashboard`, {
          headers: { Authorization: `Bearer ${adminToken}` },
        }),
      ]);

      if (usersResp.ok) {
        const usersData = await usersResp.json();
        setUsers(usersData.users || []);
      } else {
        console.error('Failed to load users:', usersResp.status, await usersResp.text());
      }

      if (dashboardResp.ok) {
        const dashboardData = await dashboardResp.json();
        setDashboard(dashboardData);
      } else {
        console.error('Failed to load dashboard:', dashboardResp.status, await dashboardResp.text());
      }
    } catch (err) {
      console.error('Error loading data:', err);
    } finally {
      setLoading(false);
    }
  }

  async function setUserCredits(userId: string, credits: number) {
    if (!adminToken) return;
    try {
      const resp = await fetch(`${apiBaseUrl}/admin/users/${userId}/credits`, {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${adminToken}`,
        },
        body: JSON.stringify({ credits }),
      });
      if (resp.ok) {
        await loadData();
        setCreditAmount('');
      }
    } catch (err) {
      console.error('Error setting credits:', err);
    }
  }

  async function grantUserCredits(userId: string, credits: number) {
    if (!adminToken) return;
    try {
      const resp = await fetch(`${apiBaseUrl}/admin/users/${userId}/credits`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${adminToken}`,
        },
        body: JSON.stringify({ credits }),
      });
      if (resp.ok) {
        await loadData();
        setGrantAmount('100');
      }
    } catch (err) {
      console.error('Error granting credits:', err);
    }
  }

  async function setMyCredits(credits: number) {
    if (!adminToken) return;
    try {
      const resp = await fetch(`${apiBaseUrl}/admin/me/credits`, {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${adminToken}`,
        },
        body: JSON.stringify({ credits }),
      });
      if (resp.ok) {
        await loadData();
      }
    } catch (err) {
      console.error('Error setting my credits:', err);
    }
  }

  function handleLogout() {
    localStorage.removeItem('adminToken');
    navigate('/admin/login');
  }

  const quickCredits = [0, 1, 5, 10, 20, 50, 100, 250, 500];
  const myCredits = [-1, 0, 1, 5, 10, 20, 50, 100];

  return (
    <div className="admin-dashboard">
      <div className="admin-header-bar">
        <span>Manage users, credentials, and test premium features</span>
        <button onClick={handleLogout} className="admin-logout-btn">
          👤 Logout
        </button>
      </div>

      <div className="admin-content">
        {/* Set Credits for Any User */}
        <div className="admin-card">
          <h2>⚙️ Set Credits for Any User</h2>
          <div className="admin-form-group">
            <label>Select a user and set their credits:</label>
            <select
              value={selectedUserId}
              onChange={(e) => setSelectedUserId(e.target.value)}
              className="admin-select"
            >
              <option value="">Select user...</option>
              {users.map((u) => (
                <option key={u.userId} value={u.userId}>
                  {u.userId} ({u.credits} credits)
                </option>
              ))}
            </select>
          </div>

          <div className="admin-quick-buttons">
            {quickCredits.map((amt) => (
              <button
                key={amt}
                onClick={() => selectedUserId && setUserCredits(selectedUserId, amt)}
                className="admin-quick-btn"
                disabled={!selectedUserId}
              >
                {amt}
              </button>
            ))}
          </div>

          <div className="admin-form-group">
            <label>Enter credit amount</label>
            <div className="admin-input-group">
              <input
                type="number"
                value={creditAmount}
                onChange={(e) => setCreditAmount(e.target.value)}
                placeholder="Custom amount"
                className="admin-input"
              />
              <button
                onClick={() => selectedUserId && setUserCredits(selectedUserId, parseInt(creditAmount) || 0)}
                className="admin-set-btn"
                disabled={!selectedUserId || !creditAmount}
              >
                Set Credits
              </button>
            </div>
            <p className="admin-hint">This will completely replace the user's credit balance with the exact amount you enter.</p>
          </div>

          <div className="admin-form-group">
            <label>Grant Credits (Add to existing)</label>
            <div className="admin-input-group">
              <input
                type="number"
                value={grantAmount}
                onChange={(e) => setGrantAmount(e.target.value)}
                className="admin-input"
              />
              <button
                onClick={() => selectedUserId && grantUserCredits(selectedUserId, parseInt(grantAmount) || 0)}
                className="admin-grant-btn"
                disabled={!selectedUserId}
              >
                Grant Credits
              </button>
            </div>
            <p className="admin-hint">This will add credits to the user's existing balance.</p>
          </div>
        </div>

        {/* Set Your Credits */}
        <div className="admin-card">
          <h2>⚙️ Set Your Credits</h2>
          <p>Choose your credit tier or set unlimited admin access.</p>
          <div className="admin-quick-buttons">
            {myCredits.map((amt) => (
              <button
                key={amt}
                onClick={() => setMyCredits(amt)}
                className="admin-quick-btn admin-purple"
              >
                {amt === -1 ? 'Set to Admin (Unlimited)' : `Set to ${amt} Credits`}
              </button>
            ))}
          </div>
        </div>

        {/* Quick Actions */}
        <div className="admin-card">
          <h2>⚡ Quick Actions</h2>
          <div className="admin-action-section">
            <p>Pull all user data from the server.</p>
            <button onClick={loadData} className="admin-action-btn" disabled={loading}>
              Refresh Users
            </button>
          </div>
          <div className="admin-action-section">
            <p>View and manage user activities.</p>
            <button className="admin-action-btn">Show Activities</button>
            <button className="admin-action-btn admin-danger">Reset All</button>
          </div>
        </div>

        {/* Dashboard Summary */}
        {dashboard && (
          <div className="admin-card">
            <h2>📊 Dashboard Summary</h2>
            <div className="admin-stats">
              <div className="admin-stat-box">
                <div className="admin-stat-label">Today's Translations</div>
                <div className="admin-stat-value">{dashboard.todaysTranslations}</div>
              </div>
              <div className="admin-stat-box">
                <div className="admin-stat-label">Today's Purchases</div>
                <div className="admin-stat-value">{dashboard.todaysPurchases}</div>
              </div>
              <div className="admin-stat-box">
                <div className="admin-stat-label">Active Tokens (Approx)</div>
                <div className="admin-stat-value">{dashboard.activeTokens}</div>
              </div>
              <div className="admin-stat-box">
                <div className="admin-stat-label">Free Search Limit</div>
                <div className="admin-stat-value">{dashboard.freeSearchLimit}</div>
              </div>
            </div>
          </div>
        )}

        {/* All Users */}
        <div className="admin-card">
          <h2>👥 All Users ({users.length})</h2>
          <div className="admin-users-list">
            {users.map((user) => (
              <div key={user.userId} className="admin-user-item">
                <div className="admin-user-id">{user.userId}</div>
                <div className="admin-user-credits">{user.credits === -1 ? 'Unlimited' : `${user.credits} credits`}</div>
                {user.totalAnalyses !== undefined && (
                  <div className="admin-user-stats">{user.totalAnalyses} analyses</div>
                )}
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}

