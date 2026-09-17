// Client-side auth for the admin area. Per the design doc's Auth &
// Security Architecture: the client decides nothing about permissions —
// this just gets a Supabase-issued JWT into fetch() calls against our own
// API, which is the only place any authorization actually happens.
// window.CARSHELL_CONFIG (supabaseUrl, anonKey) is injected by each admin
// page from server-side config; the anon key is Supabase's public,
// rate-limited key, safe to ship to the browser.
const CarShellAdmin = (() => {
    const TOKEN_KEY = 'carshell_admin_token';

    function getToken() {
        try {
            return sessionStorage.getItem(TOKEN_KEY);
        } catch {
            return null;
        }
    }

    function setToken(token) {
        try {
            sessionStorage.setItem(TOKEN_KEY, token);
        } catch {
            // Private browsing etc. — the session just won't persist across reload.
        }
    }

    function clearToken() {
        try {
            sessionStorage.removeItem(TOKEN_KEY);
        } catch {
            // Nothing to do.
        }
    }

    function requireAuth() {
        if (!getToken()) {
            window.location.href = '/admin/login';
        }
    }

    function logout() {
        clearToken();
        window.location.href = '/admin/login';
    }

    async function login(email, password) {
        const { supabaseUrl, anonKey } = window.CARSHELL_CONFIG;
        const response = await fetch(`${supabaseUrl}/auth/v1/token?grant_type=password`, {
            method: 'POST',
            headers: { apikey: anonKey, 'Content-Type': 'application/json' },
            body: JSON.stringify({ email, password }),
        });
        const payload = await response.json();
        if (!response.ok) {
            throw new Error(payload.error_description || payload.msg || 'Login failed.');
        }
        setToken(payload.access_token);
    }

    // fetch() wrapper that attaches the bearer token and redirects to login
    // on a 401 — the API is always the real authority, this is just UX.
    async function apiFetch(url, options = {}) {
        const token = getToken();
        const headers = new Headers(options.headers || {});
        if (token) {
            headers.set('Authorization', `Bearer ${token}`);
        }
        const response = await fetch(url, { ...options, headers });
        if (response.status === 401) {
            clearToken();
            window.location.href = '/admin/login';
            throw new Error('Not authenticated.');
        }
        return response;
    }

    return { getToken, setToken, clearToken, requireAuth, logout, login, apiFetch };
})();
