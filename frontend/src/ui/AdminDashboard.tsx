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

type Contact = {
  contactId: string;
  email: string;
  subject: string;
  message: string;
  createdAt: string;
  status: string;
  userId?: string;
  repliedAt?: string;
};

type PurchaseActivity = {
  activityId: string;
  userId: string;
  customerName: string;
  customerEmail: string;
  credits: number;
  amount: number;
  paymentId: string;
  sessionId: string;
  purchaseDate: string;
  cardLast4?: string;
};

export function AdminDashboard() {
  const [users, setUsers] = useState<User[]>([]);
  const [dashboard, setDashboard] = useState<DashboardSummary | null>(null);
  const [contacts, setContacts] = useState<Contact[]>([]);
  const [activities, setActivities] = useState<PurchaseActivity[]>([]);
  const [selectedUserId, setSelectedUserId] = useState('');
  const [creditAmount, setCreditAmount] = useState('');
  const [grantAmount, setGrantAmount] = useState('100');
  const [loading, setLoading] = useState(false);
  const [selectedContact, setSelectedContact] = useState<Contact | null>(null);
  const [replyMessage, setReplyMessage] = useState('');
  const [replying, setReplying] = useState(false);
  const navigate = useNavigate();
  const apiBaseUrl = getApiBaseUrl();

  const adminToken = localStorage.getItem('adminToken');

  useEffect(() => {
    if (!adminToken) {
      navigate('/admin/login');
      return;
    }
    loadData();
    // Load activities by default
    if (adminToken) {
      fetchActivities();
    }
  }, [adminToken, navigate]);

  async function loadData() {
    if (!adminToken) return;
    setLoading(true);
    try {
      const [usersResp, dashboardResp, contactsResp] = await Promise.all([
        fetch(`${apiBaseUrl}/admin/users`, {
          headers: { Authorization: `Bearer ${adminToken}` },
        }),
        fetch(`${apiBaseUrl}/admin/dashboard`, {
          headers: { Authorization: `Bearer ${adminToken}` },
        }),
        fetch(`${apiBaseUrl}/admin/contacts`, {
          headers: { Authorization: `Bearer ${adminToken}` },
        }),
      ]);

      if (usersResp.ok) {
        const usersData = await usersResp.json();
        setUsers(usersData.users || []);
      } else {
        const errorText = await usersResp.text();
        console.error(`❌ Failed to load users (${usersResp.status}):`, errorText);
        if (usersResp.status === 404) {
          console.error('⚠️ /admin/users endpoint not found. Check API Gateway configuration.');
        }
      }

      if (dashboardResp.ok) {
        const dashboardData = await dashboardResp.json();
        setDashboard(dashboardData);
      } else {
        const errorText = await dashboardResp.text();
        console.error(`❌ Failed to load dashboard (${dashboardResp.status}):`, errorText);
        if (dashboardResp.status === 404) {
          console.error('⚠️ /admin/dashboard endpoint not found. Check API Gateway configuration.');
        }
      }

      if (contactsResp.ok) {
        const contactsData = await contactsResp.json();
        setContacts(contactsData.contacts || []);
      } else {
        const errorText = await contactsResp.text();
        console.error(`❌ Failed to load contacts (${contactsResp.status}):`, errorText);
        if (contactsResp.status === 404) {
          console.error('⚠️ /admin/contacts endpoint not found. Check API Gateway configuration.');
        }
      }
    } catch (err) {
      console.error('Error loading data:', err);
    } finally {
      setLoading(false);
    }
  }

  async function fetchActivities() {
    if (!adminToken) return;
    try {
      const resp = await fetch(`${apiBaseUrl}/admin/activities`, {
        headers: { Authorization: `Bearer ${adminToken}` },
      });
      if (resp.ok) {
        const data = await resp.json();
        setActivities(data.activities || []);
      } else {
        const errorText = await resp.text();
        console.error(`❌ Failed to load activities (${resp.status}):`, errorText);
        if (resp.status === 404) {
          console.error('⚠️ /admin/activities endpoint not found. Make sure Lambda function is uploaded.');
        }
      }
    } catch (err) {
      console.error('Error loading activities:', err);
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

  async function handleReplyContact(contactId: string) {
    if (!adminToken || !replyMessage.trim()) return;
    setReplying(true);
    try {
      const resp = await fetch(`${apiBaseUrl}/admin/contacts/${contactId}/reply`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${adminToken}`,
        },
        body: JSON.stringify({ replyMessage: replyMessage.trim() }),
      });
      if (resp.ok) {
        await loadData();
        setSelectedContact(null);
        setReplyMessage('');
        alert('Reply sent successfully!');
      } else {
        let errorMessage = 'Unknown error';
        try {
          const error = await resp.json();
          errorMessage = error.error || error.message || 'Unknown error';
        } catch {
          errorMessage = `HTTP ${resp.status}: ${resp.statusText}`;
        }
        alert(`Failed to send reply: ${errorMessage}`);
      }
    } catch (err: any) {
      console.error('Error replying to contact:', err);
      alert(`Failed to send reply: ${err?.message || 'Network error'}`);
    } finally {
      setReplying(false);
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
        <div className="admin-form-cards">
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

        </div>

        {/* Middle Column - Data Views */}
        <div className="admin-data-cards">
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

          {/* Contact Messages */}
          <div className="admin-card">
            <h2>📧 Contact Messages ({contacts.length})</h2>
            <div className="admin-contacts-list">
              {contacts.length === 0 ? (
                <p style={{ color: 'var(--muted)', padding: '20px', textAlign: 'center' }}>No contact messages yet.</p>
              ) : (
                contacts.map((contact) => (
                  <div key={contact.contactId} className="admin-contact-item">
                    <div className="admin-contact-header">
                      <div>
                        <div className="admin-contact-email">{contact.email}</div>
                        <div className="admin-contact-subject">{contact.subject}</div>
                        <div className="admin-contact-meta">
                          {new Date(contact.createdAt).toLocaleString()} • 
                          Status: <span className={`contact-status ${contact.status}`}>{contact.status}</span>
                          {contact.repliedAt && ` • Replied: ${new Date(contact.repliedAt).toLocaleString()}`}
                        </div>
                      </div>
                      <button
                        onClick={() => setSelectedContact(contact)}
                        className="admin-reply-btn"
                        title={contact.status === 'replied' ? 'Reply again (previous reply will be replaced)' : 'Reply to contact'}
                      >
                        {contact.status === 'replied' ? 'Reply Again' : 'Reply'}
                      </button>
                    </div>
                    <div className="admin-contact-message">{contact.message}</div>
                  </div>
                ))
              )}
            </div>
          </div>
        </div>

        {/* Right Column - Dashboard Summary and Purchase Activities */}
        <div className="admin-right-column">
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

          {/* Quick Actions */}
          <div className="admin-card">
            <h2>⚡ Quick Actions</h2>
            <div className="admin-action-section">
              <p>Pull all user data from the server.</p>
              <button onClick={loadData} className="admin-action-btn" disabled={loading}>
                Refresh Users
              </button>
            </div>
          </div>

          {/* Purchase Activities */}
          <div className="admin-card admin-purchase-activities-card">
            <h2>💰 Purchase Activities ({activities.length})</h2>
            <div className="admin-contacts-list">
              {activities.length === 0 ? (
                <p style={{ color: 'var(--muted)', padding: '20px', textAlign: 'center' }}>No purchase activities yet.</p>
              ) : (
                activities.map((activity) => {
                  // Try to find user info from users list
                  const user = users.find(u => u.userId === activity.userId);
                  const displayName = activity.customerName && activity.customerName !== 'Unknown' 
                    ? activity.customerName 
                    : (user ? `User: ${activity.userId}` : 'Unknown User');
                  const displayEmail = activity.customerEmail || 'No email provided';
                  
                  return (
                    <div key={activity.activityId} className="admin-contact-item">
                      <div className="admin-contact-header">
                        <div>
                          <div className="admin-contact-email">
                            {displayName}
                            {activity.customerName && activity.customerName !== 'Unknown' && (
                              <span style={{ marginLeft: '8px', fontSize: '0.9rem', fontWeight: 'normal', color: 'var(--muted)' }}>
                                ({activity.userId})
                              </span>
                            )}
                          </div>
                          <div className="admin-contact-subject" style={{ fontWeight: '600', color: 'var(--text-soft)' }}>
                            📧 {displayEmail}
                          </div>
                          <div className="admin-contact-meta">
                            {new Date(activity.purchaseDate).toLocaleString()} • 
                            ${activity.amount.toFixed(2)} • {activity.credits} credits
                            {activity.cardLast4 && ` • Card: •••• ${activity.cardLast4}`}
                          </div>
                          {!activity.customerName || activity.customerName === 'Unknown' ? (
                            <div style={{ marginTop: '8px', fontSize: '0.85rem', color: 'var(--muted)' }}>
                              User ID: {activity.userId}
                            </div>
                          ) : null}
                          <div style={{ fontSize: '0.75rem', color: 'var(--muted)', marginTop: '4px' }}>
                            Payment: {activity.paymentId} | Session: {activity.sessionId}
                          </div>
                        </div>
                      </div>
                    </div>
                  );
                })
              )}
            </div>
          </div>
        </div>
      </div>

      {/* Reply Modal */}
      {selectedContact && (
        <div className="admin-modal-overlay" onClick={() => setSelectedContact(null)}>
          <div className="admin-modal-content" onClick={(e) => e.stopPropagation()}>
            <h2>Reply to Contact</h2>
            <div className="admin-reply-info">
              <p><strong>From:</strong> {selectedContact.email}</p>
              <p><strong>Subject:</strong> {selectedContact.subject}</p>
              <p><strong>Original Message:</strong></p>
              <div className="admin-original-message">{selectedContact.message}</div>
            </div>
            <div className="admin-reply-form">
              <label>Your Reply:</label>
              <textarea
                value={replyMessage}
                onChange={(e) => setReplyMessage(e.target.value)}
                rows={8}
                className="admin-reply-textarea"
                placeholder="Type your reply here..."
              />
              <div className="admin-modal-buttons">
                <button
                  onClick={() => {
                    setSelectedContact(null);
                    setReplyMessage('');
                  }}
                  className="admin-cancel-btn"
                >
                  Cancel
                </button>
                <button
                  onClick={() => handleReplyContact(selectedContact.contactId)}
                  className="admin-send-btn"
                  disabled={!replyMessage.trim() || replying}
                >
                  {replying ? 'Sending...' : 'Send Reply'}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

