(() => {
    'use strict';

    const API_BASE_URL = 'https://localhost:50949';
    const storageKey = 'archivero_auth';
    const $ = (selector) => document.querySelector(selector);
    const auth = {
        get() {
            try { return JSON.parse(localStorage.getItem(storageKey)) || null; }
            catch { localStorage.removeItem(storageKey); return null; }
        },
        save(value) { localStorage.setItem(storageKey, JSON.stringify(value)); },
        clear() { localStorage.removeItem(storageKey); }
    };

    function isExpired(session) {
        return !session?.token || !session.expiresAtUtc || Date.parse(session.expiresAtUtc) <= Date.now();
    }

    function setLoggedIn(session) {
        const loggedIn = Boolean(session);
        document.body.classList.toggle('login-mode', !loggedIn);
        $('#login-view').hidden = loggedIn;
        $('#application').hidden = !loggedIn;
        if (!loggedIn) { $('#login-username').focus(); return; }

        const username = session.username || 'User';
        $('#current-user').textContent = username;
        $('#user-initial').textContent = username.charAt(0).toUpperCase();
        showPage(location.hash.replace('#', '') || 'greeting');
    }

    async function restoreSession() {
        const session = auth.get();
        if (isExpired(session)) { auth.clear(); setLoggedIn(null); return; }
        try {
            const response = await fetch(`${API_BASE_URL}/api/auth/me`, { cache: 'no-store', headers: { Authorization: `Bearer ${session.token}` } });
            if (!response.ok) throw new Error('Session expired');
            const user = await response.json();
            const verified = { ...session, username: user.username || session.username };
            auth.save(verified);
            setLoggedIn(verified);
        } catch { auth.clear(); setLoggedIn(null); }
    }

    function showPage(name) {
        const page = name === 'archivero' ? 'archivero' : 'greeting';
        document.querySelectorAll('[data-page]').forEach((element) => { element.hidden = element.dataset.page !== page; });
        document.querySelectorAll('[data-page-link]').forEach((link) => {
            const selected = link.dataset.pageLink === page;
            link.classList.toggle('active', selected);
            link.toggleAttribute('aria-current', selected);
        });
        closeMobileMenu();
    }

    function toggleSidebar() {
        const sidebar = $('#sidebar');
        const collapsed = sidebar.classList.toggle('collapsed');
        $('#sidebar-toggle').setAttribute('aria-expanded', String(!collapsed));
        $('#sidebar-toggle').setAttribute('aria-label', collapsed ? 'Expand menu' : 'Collapse menu');
    }

    function closeMobileMenu() {
        $('#sidebar').classList.remove('mobile-open');
        $('#mobile-menu-button').setAttribute('aria-expanded', 'false');
    }

    function wireEvents() {
        $('#login-form').addEventListener('submit', async (event) => {
            event.preventDefault();
            const username = $('#login-username').value.trim();
            const password = $('#login-password').value;
            const error = $('#login-error');
            const submit = $('#login-submit');
            error.textContent = '';
            if (!username || !password) { error.textContent = 'Enter your username and password.'; return; }

            submit.disabled = true;
            submit.textContent = 'Logging in…';
            try {
                const response = await fetch(`${API_BASE_URL}/api/auth/login`, {
                    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ username, password })
                });
                if (!response.ok) throw new Error();
                const result = await response.json();
                if (!result.token) throw new Error();
                auth.save(result);
                $('#login-form').reset();
                setLoggedIn(result);
            } catch { error.textContent = 'Unable to log in. Please check your username and password.'; }
            finally { submit.disabled = false; submit.textContent = 'Log in'; }
        });

        $('#logout-button').addEventListener('click', () => { auth.clear(); closeMobileMenu(); setLoggedIn(null); });
        document.querySelectorAll('[data-page-link]').forEach((link) => link.addEventListener('click', (event) => {
            event.preventDefault();
            const page = link.dataset.pageLink;
            history.replaceState(null, '', `#${page}`);
            showPage(page);
        }));
        $('#sidebar-toggle').addEventListener('click', toggleSidebar);
        $('#mobile-menu-button').addEventListener('click', () => {
            const opened = $('#sidebar').classList.toggle('mobile-open');
            $('#mobile-menu-button').setAttribute('aria-expanded', String(opened));
        });
        window.addEventListener('hashchange', () => showPage(location.hash.replace('#', '')));
    }

    window.addEventListener('DOMContentLoaded', () => { wireEvents(); restoreSession(); });
})();
