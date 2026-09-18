// Client-side auth for the admin area. Per the design doc's Auth &
// Security Architecture: the client decides nothing about permissions —
// this just gets a Supabase-issued JWT into fetch() calls against our own
// API, which is the only place any authorization actually happens. The
// server separately enforces that the token carries aal2 (completed 2FA)
// on every admin action — see AdminAuthorizationHandler — so this client
// flow is UX, not the real security boundary.
// window.CARSHELL_CONFIG (supabaseUrl, anonKey) is injected by each admin
// page from server-side config; the anon key is Supabase's public,
// rate-limited key, safe to ship to the browser.
const CarShellAdmin = (() => {
    const TOKEN_KEY = 'carshell_admin_token';
    const PENDING_TOKEN_KEY = 'carshell_pending_token';

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

    // The pending token is an aal1 session — password verified, second
    // factor not yet completed. It's only ever used to call the MFA
    // enroll/challenge/verify endpoints directly, never sent to our own API.
    function getPendingToken() {
        try {
            return sessionStorage.getItem(PENDING_TOKEN_KEY);
        } catch {
            return null;
        }
    }

    function setPendingToken(token) {
        try {
            sessionStorage.setItem(PENDING_TOKEN_KEY, token);
        } catch {
            // Nothing to do.
        }
    }

    function clearPendingToken() {
        try {
            sessionStorage.removeItem(PENDING_TOKEN_KEY);
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
        clearPendingToken();
        window.location.href = '/admin/login';
    }

    async function readJsonOrThrow(response, fallbackMessage) {
        const payload = await response.json().catch(() => ({}));
        if (!response.ok) {
            throw new Error(payload.error_description || payload.msg || fallbackMessage);
        }
        return payload;
    }

    // Supabase Auth itself only ever signs in by email (or phone) — a
    // username is resolved to its email via our own API first, so from
    // Supabase's point of view this is always an ordinary email+password
    // grant. The lookup endpoint never sees the password.
    async function resolveIdentifierToEmail(identifier) {
        if (identifier.includes('@')) {
            return identifier;
        }
        const response = await fetch(`/api/auth/resolve-username?username=${encodeURIComponent(identifier)}`);
        if (!response.ok) {
            throw new Error('No account found with that username.');
        }
        const payload = await response.json();
        return payload.email;
    }

    // Password step only — never returns a token usable against our API.
    // The caller decides what to do next based on whether a verified TOTP
    // factor already exists (prompt for a code) or not (force enrollment).
    // `identifier` can be an email or a username — see resolveIdentifierToEmail.
    async function login(identifier, password) {
        const email = await resolveIdentifierToEmail(identifier);
        const { supabaseUrl, anonKey } = window.CARSHELL_CONFIG;
        const response = await fetch(`${supabaseUrl}/auth/v1/token?grant_type=password`, {
            method: 'POST',
            headers: { apikey: anonKey, 'Content-Type': 'application/json' },
            body: JSON.stringify({ email, password }),
        });
        const payload = await readJsonOrThrow(response, 'Login failed.');

        setPendingToken(payload.access_token);

        const totpFactor = (payload.user?.factors || []).find(
            (f) => f.factor_type === 'totp' && f.status === 'verified');

        return totpFactor
            ? { needsCode: true, factorId: totpFactor.id }
            : { needsCode: false, factorId: null };
    }

    // Starts TOTP enrollment against the pending aal1 session — returns the
    // QR code (as an SVG string) and manual-entry secret to show the user.
    async function mfaEnroll() {
        const { supabaseUrl, anonKey } = window.CARSHELL_CONFIG;
        const response = await fetch(`${supabaseUrl}/auth/v1/factors`, {
            method: 'POST',
            headers: {
                apikey: anonKey,
                Authorization: `Bearer ${getPendingToken()}`,
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({ factor_type: 'totp', friendly_name: 'Authenticator app' }),
        });
        const payload = await readJsonOrThrow(response, 'Could not start MFA enrollment.');
        return { factorId: payload.id, qrCodeSvg: payload.totp.qr_code, secret: payload.totp.secret };
    }

    // Proves the user has the authenticator app: used both to finish a
    // first-time enrollment and for a returning login's code prompt. On
    // success this is what actually upgrades the session to aal2 — the
    // token that comes back is the one apiFetch attaches to every call.
    async function mfaVerify(factorId, code) {
        const { supabaseUrl, anonKey } = window.CARSHELL_CONFIG;
        const pendingToken = getPendingToken();
        if (!pendingToken) {
            throw new Error('Your sign-in has expired — please log in again.');
        }

        const challengeResponse = await fetch(`${supabaseUrl}/auth/v1/factors/${factorId}/challenge`, {
            method: 'POST',
            headers: { apikey: anonKey, Authorization: `Bearer ${pendingToken}`, 'Content-Type': 'application/json' },
        });
        const challenge = await readJsonOrThrow(challengeResponse, 'Could not start verification.');

        const verifyResponse = await fetch(`${supabaseUrl}/auth/v1/factors/${factorId}/verify`, {
            method: 'POST',
            headers: { apikey: anonKey, Authorization: `Bearer ${pendingToken}`, 'Content-Type': 'application/json' },
            body: JSON.stringify({ challenge_id: challenge.id, code }),
        });
        const result = await readJsonOrThrow(verifyResponse, 'Invalid code.');

        setToken(result.access_token);
        clearPendingToken();
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

    // Cosmetic only: decodes the JWT's payload (base64url, no signature
    // check — the server is the real authority on every request) purely to
    // show who's signed in in the top bar. Never used for authorization.
    function decodeJwtPayload(token) {
        try {
            const base64 = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
            const json = decodeURIComponent(
                atob(base64).split('').map((c) => '%' + c.charCodeAt(0).toString(16).padStart(2, '0')).join(''));
            return JSON.parse(json);
        } catch {
            return null;
        }
    }

    function getIdentity() {
        const token = getToken();
        if (!token) {
            return null;
        }
        const claims = decodeJwtPayload(token);
        return claims ? { email: claims.email || null } : null;
    }

    return {
        getToken, setToken, clearToken, getPendingToken, requireAuth, logout,
        login, mfaEnroll, mfaVerify, apiFetch, getIdentity,
    };
})();
