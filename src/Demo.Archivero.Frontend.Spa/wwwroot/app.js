(() => {
    'use strict';

    const API_BASE_URL = 'https://localhost:50949';
    const storageKey = 'archivero_auth';
    const $ = (selector) => document.querySelector(selector);
    const auth = {
        get() { try { return JSON.parse(localStorage.getItem(storageKey)) || null; } catch { localStorage.removeItem(storageKey); return null; } },
        save(value) { localStorage.setItem(storageKey, JSON.stringify(value)); },
        clear() { localStorage.removeItem(storageKey); }
    };

    function setLoading(loading) {
        $('#loading-overlay').hidden = !loading;
        $('#loading-overlay').setAttribute('aria-hidden', String(!loading));
    }

    function apiHeaders(includeJson = false) {
        const headers = {};
        const token = auth.get()?.token;
        if (token) headers.Authorization = `Bearer ${token}`;
        if (includeJson) headers['Content-Type'] = 'application/json';
        return headers;
    }

    async function request(path, options = {}) {
        setLoading(true);
        try { return await fetch(`${API_BASE_URL}${path}`, { ...options, headers: { ...apiHeaders(Boolean(options.body)), ...options.headers } }); }
        finally { setLoading(false); }
    }

    function isExpired(session) { return !session?.token || !session.expiresAtUtc || Date.parse(session.expiresAtUtc) <= Date.now(); }

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
            const response = await request('/api/auth/me');
            if (!response.ok) throw new Error();
            const user = await response.json();
            const verified = { ...session, username: user.username || session.username };
            auth.save(verified); setLoggedIn(verified);
        } catch { auth.clear(); setLoggedIn(null); }
    }

    function setFilesMessage(message, error = false) {
        const element = $('#files-status');
        element.textContent = message;
        element.classList.toggle('error', error);
    }

    function formatDate(value) {
        const date = new Date(value);
        return Number.isNaN(date.getTime()) ? '—' : date.toLocaleString('en-US', { dateStyle: 'short', timeStyle: 'short' });
    }

    function renderFiles(files) {
        const body = $('#files-table-body');
        body.replaceChildren();
        if (!files.length) { setFilesMessage('No files yet.'); return; }
        setFilesMessage('');
        files.forEach((file) => {
            const row = document.createElement('tr');
            const creation = document.createElement('td'); creation.textContent = formatDate(file.createdAtUtc);
            const title = document.createElement('td'); title.className = 'file-title'; title.textContent = file.title || 'Untitled'; title.title = file.title || '';
            const downloadCell = document.createElement('td');
            const download = document.createElement('a'); download.className = 'table-button download-button'; download.textContent = 'Download'; download.href = new URL(file.blobUrl, API_BASE_URL).href; download.target = '_blank'; download.rel = 'noopener'; downloadCell.append(download);
            const deleteCell = document.createElement('td');
            const remove = document.createElement('button'); remove.className = 'table-button delete-button'; remove.type = 'button'; remove.textContent = 'Delete'; remove.dataset.fileId = file.id; remove.addEventListener('click', () => deleteFile(file.id, row)); deleteCell.append(remove);
            row.append(creation, title, downloadCell, deleteCell); body.append(row);
        });
    }

    async function loadFiles() {
        setFilesMessage('Loading files…');
        $('#files-table-body').replaceChildren();
        try {
            const response = await request('/api/files');
            if (response.status === 404) { renderFiles([]); return; }
            if (!response.ok) throw new Error();
            const files = await response.json();
            renderFiles(Array.isArray(files) ? files : []);
        } catch { setFilesMessage('Unable to load your files. Please try again.', true); }
    }

    async function deleteFile(fileId, row) {
        if (!Number.isInteger(fileId) || fileId < 1) { setFilesMessage('This file cannot be deleted because its ID is missing.', true); return; }
        if (!window.confirm('Delete this file?')) return;
        try {
            const response = await request(`/api/files/${fileId}`, { method: 'DELETE' });
            if (!response.ok) throw new Error();
            row.remove();
            if (!$('#files-table-body').children.length) setFilesMessage('No files yet.');
        } catch { setFilesMessage('Unable to delete the file. Please try again.', true); }
    }

    function resetCreateForm() {
        $('#create-archive-form').reset();
        $('#create-archive-message').textContent = '';
        $('#create-archive-message').classList.remove('success');
        ['#archive-title', '#archive-content', '#create-archive-submit'].forEach((selector) => { $(selector).disabled = false; });
        updateCharacterCounts();
    }

    function updateCharacterCounts() {
        const title = $('#archive-title');
        const content = $('#archive-content');
        $('#archive-title-limit').textContent = `${title.value.length} / ${title.maxLength} characters`;
        $('#archive-content-limit').textContent = `${content.value.length.toLocaleString('en-US')} / ${content.maxLength.toLocaleString('en-US')} characters`;
    }

    function showPage(name) {
        const validPages = ['greeting', 'archivero', 'create-archive'];
        const page = validPages.includes(name) ? name : 'greeting';
        document.querySelectorAll('[data-page]').forEach((element) => { element.hidden = element.dataset.page !== page; });
        document.querySelectorAll('[data-page-link]').forEach((link) => {
            const selected = link.dataset.pageLink === page;
            link.classList.toggle('active', selected); link.toggleAttribute('aria-current', selected);
        });
        closeMobileMenu();
        if (page === 'archivero') loadFiles();
        if (page === 'create-archive') resetCreateForm();
    }

    function toggleSidebar() {
        const collapsed = $('#sidebar').classList.toggle('collapsed');
        $('#sidebar-toggle').setAttribute('aria-expanded', String(!collapsed));
        $('#sidebar-toggle').setAttribute('aria-label', collapsed ? 'Expand menu' : 'Collapse menu');
    }
    function closeMobileMenu() { $('#sidebar').classList.remove('mobile-open'); $('#mobile-menu-button').setAttribute('aria-expanded', 'false'); }

    function wireEvents() {
        $('#login-form').addEventListener('submit', async (event) => {
            event.preventDefault();
            const username = $('#login-username').value.trim(); const password = $('#login-password').value;
            const error = $('#login-error'); const submit = $('#login-submit'); error.textContent = '';
            if (!username || !password) { error.textContent = 'Enter your username and password.'; return; }
            submit.disabled = true; submit.textContent = 'Logging in…';
            try {
                const response = await request('/api/auth/login', { method: 'POST', body: JSON.stringify({ username, password }) });
                if (!response.ok) throw new Error();
                const result = await response.json(); if (!result.token) throw new Error();
                auth.save(result); $('#login-form').reset(); setLoggedIn(result);
            } catch { error.textContent = 'Unable to log in. Please check your username and password.'; }
            finally { submit.disabled = false; submit.textContent = 'Log in'; }
        });
        $('#create-archive-form').addEventListener('submit', async (event) => {
            event.preventDefault();
            const title = $('#archive-title').value.trim(); const content = $('#archive-content').value;
            const message = $('#create-archive-message'); message.textContent = ''; message.classList.remove('success');
            if (!title || !content) { message.textContent = 'Enter a title and text.'; return; }
            try {
                const response = await request('/api/files', { method: 'POST', body: JSON.stringify({ title, content }) });
                if (!response.ok) throw new Error();
                ['#archive-title', '#archive-content', '#create-archive-submit'].forEach((selector) => { $(selector).disabled = true; });
                message.textContent = 'Successful, creation process started, please check later your Archivero.'; message.classList.add('success');
            } catch { message.textContent = 'Unable to create the archive. Please try again.'; }
        });
        $('#archive-title').addEventListener('input', updateCharacterCounts);
        $('#archive-content').addEventListener('input', updateCharacterCounts);
        $('#logout-button').addEventListener('click', () => { auth.clear(); closeMobileMenu(); setLoggedIn(null); });
        document.querySelectorAll('[data-page-link]').forEach((link) => link.addEventListener('click', (event) => { event.preventDefault(); const page = link.dataset.pageLink; history.replaceState(null, '', `#${page}`); showPage(page); }));
        $('#sidebar-toggle').addEventListener('click', toggleSidebar);
        $('#mobile-menu-button').addEventListener('click', () => { const opened = $('#sidebar').classList.toggle('mobile-open'); $('#mobile-menu-button').setAttribute('aria-expanded', String(opened)); });
        window.addEventListener('hashchange', () => showPage(location.hash.replace('#', '')));
    }
    window.addEventListener('DOMContentLoaded', () => { wireEvents(); restoreSession(); });
})();
