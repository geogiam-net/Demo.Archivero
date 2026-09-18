// Dead Km front-end. Login workflow mirrors the WPS app: the JWT returned by
// /api/auth/login is kept client-side in localStorage and sent as a Bearer
// token on API calls. The login dialog gates the UI client-side (login-mode);
// the static files themselves are served without any token.

const qs = (selector) => document.querySelector(selector);

const auth = {
    get token() {
        return localStorage.getItem('deadkm_token');
    },
    set token(value) {
        value
            ? localStorage.setItem('deadkm_token', value)
            : localStorage.removeItem('deadkm_token');
    },
    get user() {
        const raw = localStorage.getItem('deadkm_user');
        return raw ? JSON.parse(raw) : null;
    },
    set user(value) {
        value
            ? localStorage.setItem('deadkm_user', JSON.stringify(value))
            : localStorage.removeItem('deadkm_user');
    }
};

function authHeaders() {
    return auth.token ? { Authorization: `Bearer ${auth.token}` } : {};
}

// Verify a stored token by fetching the current user; drop it if it is invalid.
async function loadCurrentUser() {
    if (!auth.token) {
        return;
    }
    try {
        const response = await fetch('/api/auth/me', {
            cache: 'no-store',
            headers: authHeaders()
        });
        if (!response.ok) {
            throw new Error('unauthorized');
        }
        auth.user = await response.json();
    } catch {
        auth.token = null;
        auth.user = null;
    }
}

function updateAuthUi() {
    const user = auth.user;
    const loggedIn = !!auth.token && !!user;

    document.body.classList.toggle('login-mode', !loggedIn);
    qs('#auth-user-label').textContent = loggedIn ? `Signed in as ${user.username}` : 'Not signed in';
    qs('#auth-role-label').textContent = loggedIn ? user.role : 'Login required';
    qs('#logout-btn')?.classList.toggle('hidden', !loggedIn);

    // Bring the dashboard up (or tear it down) alongside the auth state.
    Dashboard.onAuthChange(loggedIn);
}

function requireLogin() {
    const dialog = qs('#login-dialog');
    if (dialog && !dialog.open) {
        dialog.showModal();
    }
}

function logout() {
    auth.token = null;
    auth.user = null;
    updateAuthUi();
    requireLogin();
}

function wireAuthUi() {
    qs('#logout-btn')?.addEventListener('click', logout);

    // While unauthenticated, block Esc from dismissing the modal login dialog.
    qs('#login-dialog')?.addEventListener('cancel', (e) => {
        if (!auth.token || !auth.user) {
            e.preventDefault();
        }
    });

    qs('#login-form')?.addEventListener('submit', async (e) => {
        e.preventDefault();
        const username = qs('#login-username').value.trim();
        const password = qs('#login-password').value;
        const error = qs('#login-error');
        error.textContent = '';

        try {
            const response = await fetch('/api/auth/login', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ username, password })
            });

            if (!response.ok) {
                throw new Error('Invalid username or password.');
            }

            const result = await response.json();
            auth.token = result.token;
            auth.user = {
                username: result.username,
                role: result.role,
                permissions: result.permissions || []
            };

            qs('#login-dialog').close();
            qs('#login-form').reset();
            updateAuthUi();
        } catch (ex) {
            error.textContent = ex.message || 'Login failed. Please try again.';
        }
    });
}

window.addEventListener('DOMContentLoaded', async () => {
    wireAuthUi();
    await loadCurrentUser();
    updateAuthUi();
    if (!auth.token || !auth.user) {
        requireLogin();
    }
});

// =====================================================================
// Dead KM dashboard
// ---------------------------------------------------------------------
// Everything below drives the main application once the user is signed
// in: the date-range selector, the resume, the filters and List 1 (the
// list of vehicle shifts). It talks to two endpoints:
//   GET /api/vehicle-shifts/{startDate}/{endDate}  -> List 1
//   GET /api/vehicle-shifts/{id}                    -> a shift's detail
// All display formatting follows the README rules (Luxembourg number /
// date formats, distances in km with 1 decimal, severity colours).
// The OpenStreetMap map is rendered from scratch (OSM tiles + an SVG
// overlay) so the front-end depends on no framework or library.
// =====================================================================

const Dashboard = (() => {
    // ---- reference data (enums come back from the API as numbers) ----
    const VEHICLE_TYPES = {
        0: { label: 'Unknown', icon: '❓' },
        1: { label: 'Bus', icon: '🚌' },      // 🚌
        2: { label: 'Shuttle', icon: '🚐' }    // 🚐
    };
    const SEVERITIES = { 0: 'None', 1: 'Weak', 2: 'Moderate', 3: 'Critical' };

    // Trip kinds (DetailedVehicleTripDto.tripType, sent as a number).
    //  - `color`: trip-bar segment colour.
    //  - `commercial`: drawn on the commercial (cyan) map layer; empty trips use
    //    the empty (yellow) layer.
    //  - `onMap`: whether the trip's path is drawn on the map at all. Only
    //    Commercial and Empty trips are; Dead km and Unknown never are.
    const TRIP_TYPES = {
        0: { label: 'Unknown', color: 'var(--trip-unknown)', commercial: false, onMap: false },
        1: { label: 'Dead km', color: 'var(--trip-deadkm)', commercial: false, onMap: false },
        2: { label: 'Empty trip', color: 'var(--trip-under)', commercial: false, onMap: true },
        3: { label: 'Commercial', color: 'var(--trip-commercial)', commercial: true, onMap: true }
    };
    const tripInfo = (t) => TRIP_TYPES[t.tripType] || TRIP_TYPES[0];
    const isCommercialTrip = (t) => tripInfo(t).commercial;
    const isTripOnMap = (t) => tripInfo(t).onMap;

    // Enum values needed for the resume / filters
    const VT_BUS = 1;
    const VT_SHUTTLE = 2;

    const MAP_HEIGHT = 460; // keep in sync with .map height in styles.css

    // ---- Luxembourg number / distance formatting -------------------
    // "1.234.567,89" grouping with comma decimals -> de-DE matches.
    const nfKm = new Intl.NumberFormat('de-DE', {
        minimumFractionDigits: 1,
        maximumFractionDigits: 1
    });
    const nfInt = new Intl.NumberFormat('de-DE');

    // Distance from meters, always km with 1 decimal + " km".
    function kmStr(meters) {
        return nfKm.format((meters || 0) / 1000) + ' km';
    }
    // Same, but with an explicit polarity symbol (used for the trip diff).
    function kmSigned(meters) {
        const m = meters || 0;
        const sign = m > 0 ? '+' : m < 0 ? '−' : '';
        return sign + nfKm.format(Math.abs(m) / 1000) + ' km';
    }

    // ---- date helpers (Luxembourg DD.MM.YYYY, ISO for the API) ------
    const pad2 = (n) => String(n).padStart(2, '0');
    const toIso = (d) => `${d.getFullYear()}-${pad2(d.getMonth() + 1)}-${pad2(d.getDate())}`;
    const fmtDateLb = (d) => `${pad2(d.getDate())}.${pad2(d.getMonth() + 1)}.${d.getFullYear()}`;
    const isoToLb = (iso) => {
        const [y, m, d] = String(iso).split('-');
        return `${d}.${m}.${y}`;
    };
    const addDays = (d, n) => {
        const r = new Date(d);
        r.setDate(r.getDate() + n);
        return r;
    };
    const startOfToday = () => {
        const d = new Date();
        d.setHours(0, 0, 0, 0);
        return d;
    };
    const startOfYesterday = () => addDays(startOfToday(), -1);
    const dayDiff = (a, b) => Math.round((b - a) / 86400000);

    // ---- component state -------------------------------------------
    const state = {
        built: false,
        period: { mode: 'yesterday', from: startOfYesterday(), to: startOfYesterday() },
        shifts: [],           // List 1 (kept whole in memory)
        loadError: null,
        filters: { vehicleType: 'all', severity: 'all', plate: '' },
        expandedId: null,     // id of the currently open box (accordion)
        detail: null,         // detailed shift dto for the open box
        detailError: null     // error message when the detail failed to load
    };

    // ---- small DOM helpers -----------------------------------------
    const el = (sel) => document.querySelector(sel);
    const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) =>
        ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

    function showOverlay() { el('#loading-overlay')?.classList.remove('hidden'); }
    function hideOverlay() { el('#loading-overlay')?.classList.add('hidden'); }

    // ---- derived values --------------------------------------------
    // DeadKM (in meters) comes straight from the API: VehicleShiftDto.DeadMeters.
    function deadKmMeters(shift) {
        return shift.deadMeters || 0;
    }
    // "SL3668" -> "SL 3668" (space between 2nd and 3rd character)
    function displayPlate(plate) {
        const p = plate || '';
        return p.length > 2 ? `${p.slice(0, 2)} ${p.slice(2)}` : p;
    }

    // =================================================================
    // API
    // =================================================================
    // Read a response body as JSON when possible, otherwise as raw text.
    async function readBody(res) {
        const text = await res.text();
        if (!text) {
            return null;
        }
        try {
            return JSON.parse(text);
        } catch {
            return text;
        }
    }

    // The ResultDto carries an `errors` array that may be populated even on a
    // 200 OK. Surface the first error whenever one is present, and fall back
    // to the body / status when the request itself failed. Returns null when
    // there is nothing to report.
    function firstError(res, body) {
        if (body && typeof body === 'object' && Array.isArray(body.errors) && body.errors.length) {
            return String(body.errors[0]);
        }
        if (!res.ok) {
            if (typeof body === 'string' && body.trim()) {
                return body.trim();
            }
            if (body && typeof body === 'object' && (body.detail || body.title)) {
                return String(body.detail || body.title);
            }
            return `Request failed (${res.status}).`;
        }
        return null;
    }

    async function fetchList(fromIso, toIso) {
        const res = await fetch(`/api/vehicle-shifts/${fromIso}/${toIso}`, {
            cache: 'no-store',
            headers: authHeaders()
        });
        const body = await readBody(res);
        const shifts = (body && typeof body === 'object' && Array.isArray(body.result))
            ? body.result
            : [];
        return { shifts, error: firstError(res, body) };
    }
    async function fetchDetail(id) {
        const res = await fetch(`/api/vehicle-shifts/${id}`, {
            cache: 'no-store',
            headers: authHeaders()
        });
        const body = await readBody(res);
        const result = (body && typeof body === 'object' && body.result) ? body.result : null;
        return { result, error: firstError(res, body) };
    }

    // =================================================================
    // Period resolution + loading List 1
    // =================================================================
    function resolvePeriod() {
        const today = startOfToday();
        switch (state.period.mode) {
            case '7d': return { from: addDays(today, -6), to: today };
            case '30d': return { from: addDays(today, -29), to: today };
            case 'custom': return { from: state.period.from, to: state.period.to };
            default: { // 'yesterday'
                const yesterday = addDays(today, -1);
                return { from: yesterday, to: yesterday };
            }
        }
    }

    async function loadList() {
        const { from, to } = resolvePeriod();
        state.loadError = null;
        // Reset the accordion whenever a new List 1 is loaded.
        state.expandedId = null;
        state.detail = null;

        showOverlay();
        try {
            const { shifts, error } = await fetchList(toIso(from), toIso(to));
            state.shifts = shifts;
            state.loadError = error;
        } catch (ex) {
            state.shifts = [];
            state.loadError = ex.message || 'Could not load vehicle shifts.';
        } finally {
            hideOverlay();
        }

        renderSubtitle(from, to);
        renderResume();
        renderList();
    }

    // =================================================================
    // Rendering — shell (built once), then partial updates
    // =================================================================
    function renderShell() {
        const host = el('#dashboard');
        host.innerHTML = `
            <div class="period-bar" id="period-bar"></div>
            <div class="range-hint" id="range-hint"></div>
            <h2 class="dash-title">Dead KM &mdash; Control room</h2>
            <div class="dash-sub" id="dash-sub"></div>
            <div class="resume" id="resume"></div>
            <div class="filters" id="filters"></div>
            <div class="shift-list" id="shift-list"></div>`;

        renderPeriodBar();
        renderFilters();
        state.built = true;
    }

    function renderPeriodBar() {
        const periods = [
            ['yesterday', 'Yesterday'],
            ['7d', 'Past 7 days'],
            ['30d', 'Past 30 days'],
            ['custom', 'Custom range']
        ];
        const chips = periods.map(([k, label]) =>
            `<span class="chip ${state.period.mode === k ? 'on' : ''}" data-period="${k}">${label}</span>`
        ).join('');

        // In custom mode the range is picked with the mouse: two clickable
        // date labels that open a calendar popup (see the calendar section).
        const custom = state.period.mode === 'custom'
            ? `<span class="custom-range">
                    <button type="button" class="date-field" id="range-from"
                            title="Click to pick the range">${fmtDateLb(state.period.from)}</button>
                    <span class="arrow">&rarr;</span>
                    <button type="button" class="date-field" id="range-to"
                            title="Click to pick the range">${fmtDateLb(state.period.to)}</button>
                    <div class="range-cal" id="range-cal" hidden></div>
               </span>`
            : '';

        el('#period-bar').innerHTML =
            `<span class="label">Date range:</span>${chips}${custom}`;

        el('#period-bar').querySelectorAll('[data-period]').forEach((chip) => {
            chip.addEventListener('click', () => {
                const mode = chip.dataset.period;
                if (mode === state.period.mode) {
                    return;
                }
                closeCalendar(true);
                state.period.mode = mode;
                if (mode === 'custom') {
                    // Seed the custom range with a single day; the user then
                    // picks a range from the calendar (no request until then).
                    state.period.from = startOfYesterday();
                    state.period.to = startOfYesterday();
                    renderPeriodBar();
                    setRangeHint('Click a date label to pick a range (up to 30 days).', false);
                } else {
                    setRangeHint('', false);
                    renderPeriodBar();
                    loadList();
                }
            });
        });

        if (state.period.mode === 'custom') {
            el('#range-from').addEventListener('click', () => openCalendar('from'));
            el('#range-to').addEventListener('click', () => openCalendar('to'));
        }
    }

    function setRangeHint(text, isError) {
        const hint = el('#range-hint');
        if (!hint) {
            return;
        }
        hint.textContent = text;
        hint.classList.toggle('error', !!isError);
    }

    // =================================================================
    // Custom range — mouse-driven calendar popup
    // -----------------------------------------------------------------
    // Flow: click a date label -> calendar opens. First day click sets the
    // start; moving the mouse highlights the range; the second click (within
    // 30 days of the start) finalises it, closes the calendar, refreshes the
    // labels and triggers a SINGLE List 1 request. Clicking outside (or Esc)
    // cancels without changing anything.
    // =================================================================
    const MONTHS = ['January', 'February', 'March', 'April', 'May', 'June',
        'July', 'August', 'September', 'October', 'November', 'December'];
    const WEEKDAYS = ['Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa', 'Su']; // Monday-first
    const MAX_RANGE_DAYS = 30;

    const picker = {
        open: false,
        viewYear: 0,
        viewMonth: 0,
        anchor: null,   // Date of the first click (start)
        hover: null,    // Date currently hovered (end preview)
        outsideHandler: null,
        keyHandler: null
    };

    const sameDay = (a, b) =>
        a && b && a.getFullYear() === b.getFullYear() &&
        a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
    const dateKey = (d) => `${d.getFullYear()}-${pad2(d.getMonth() + 1)}-${pad2(d.getDate())}`;
    const parseKey = (s) => {
        const [y, m, d] = s.split('-').map(Number);
        return new Date(y, m - 1, d);
    };

    function openCalendar(which) {
        picker.open = true;
        picker.anchor = null;
        picker.hover = null;
        const base = which === 'to' ? state.period.to : state.period.from;
        picker.viewYear = base.getFullYear();
        picker.viewMonth = base.getMonth();

        setRangeHint('Pick the start date, then the end date (max 30 days).', false);
        renderCalendar();
        el('#range-cal').hidden = false;

        // Defer wiring the outside/Esc handlers so the opening click itself
        // does not immediately close the calendar.
        setTimeout(() => {
            if (!picker.open) {
                return;
            }
            picker.outsideHandler = (ev) => {
                const cal = el('#range-cal');
                if (!cal) {
                    return;
                }
                if (!cal.contains(ev.target) && !ev.target.closest('.date-field')) {
                    closeCalendar(true);
                }
            };
            picker.keyHandler = (ev) => {
                if (ev.key === 'Escape') {
                    closeCalendar(true);
                }
            };
            document.addEventListener('mousedown', picker.outsideHandler, true);
            document.addEventListener('touchstart', picker.outsideHandler, true);
            document.addEventListener('keydown', picker.keyHandler, true);
        }, 0);
    }

    function closeCalendar(/* cancel */) {
        if (picker.outsideHandler) {
            document.removeEventListener('mousedown', picker.outsideHandler, true);
            document.removeEventListener('touchstart', picker.outsideHandler, true);
            picker.outsideHandler = null;
        }
        if (picker.keyHandler) {
            document.removeEventListener('keydown', picker.keyHandler, true);
            picker.keyHandler = null;
        }
        picker.open = false;
        picker.anchor = null;
        picker.hover = null;
        const cal = el('#range-cal');
        if (cal) {
            cal.hidden = true;
            cal.innerHTML = '';
        }
    }

    // True when a day is not selectable as the *second* date: only the
    // window [anchor, anchor + 30 days] is allowed once the start is set.
    function dayDisabled(date) {
        if (!picker.anchor) {
            return false;
        }
        return date < picker.anchor || dayDiff(picker.anchor, date) > MAX_RANGE_DAYS;
    }

    function renderCalendar() {
        const cal = el('#range-cal');
        if (!cal) {
            return;
        }
        const y = picker.viewYear;
        const m = picker.viewMonth;

        const years = [];
        const nowY = new Date().getFullYear();
        for (let yr = nowY - 6; yr <= nowY + 1; yr++) {
            years.push(yr);
        }
        const monthOpts = MONTHS.map((name, i) =>
            `<option value="${i}" ${i === m ? 'selected' : ''}>${name}</option>`).join('');
        const yearOpts = years.map((yr) =>
            `<option value="${yr}" ${yr === y ? 'selected' : ''}>${yr}</option>`).join('');

        // Monday-first leading offset for the 1st of the month.
        const firstDow = (new Date(y, m, 1).getDay() + 6) % 7;
        const daysInMonth = new Date(y, m + 1, 0).getDate();

        let cells = '';
        for (let i = 0; i < firstDow; i++) {
            cells += `<div class="cal-blank"></div>`;
        }
        for (let d = 1; d <= daysInMonth; d++) {
            const date = new Date(y, m, d);
            cells += `<div class="cal-day" data-date="${dateKey(date)}">${d}</div>`;
        }

        cal.innerHTML = `
            <div class="cal-head">
                <button type="button" class="cal-nav" data-nav="-1" title="Previous month">&lsaquo;</button>
                <select class="cal-month">${monthOpts}</select>
                <select class="cal-year">${yearOpts}</select>
                <button type="button" class="cal-nav" data-nav="1" title="Next month">&rsaquo;</button>
            </div>
            <div class="cal-grid cal-dow-row">
                ${WEEKDAYS.map((w) => `<div class="cal-dow">${w}</div>`).join('')}
            </div>
            <div class="cal-grid cal-days">${cells}</div>`;

        // navigation
        cal.querySelectorAll('[data-nav]').forEach((btn) => {
            btn.addEventListener('click', () => {
                let nm = picker.viewMonth + Number(btn.dataset.nav);
                let ny = picker.viewYear;
                if (nm < 0) { nm = 11; ny--; }
                if (nm > 11) { nm = 0; ny++; }
                picker.viewMonth = nm;
                picker.viewYear = ny;
                renderCalendar();
            });
        });
        cal.querySelector('.cal-month').addEventListener('change', (e) => {
            picker.viewMonth = +e.target.value;
            renderCalendar();
        });
        cal.querySelector('.cal-year').addEventListener('change', (e) => {
            picker.viewYear = +e.target.value;
            renderCalendar();
        });

        // day interaction
        cal.querySelectorAll('.cal-day[data-date]').forEach((cell) => {
            const date = parseKey(cell.dataset.date);
            cell.addEventListener('mouseenter', () => {
                if (picker.anchor && !dayDisabled(date)) {
                    picker.hover = date;
                    paintSelection();
                }
            });
            cell.addEventListener('click', () => {
                if (dayDisabled(date)) {
                    return;
                }
                if (!picker.anchor) {
                    // first click -> start date
                    picker.anchor = date;
                    picker.hover = date;
                    paintSelection();
                    setRangeHint('Now pick the end date (up to 30 days later).', false);
                } else {
                    // second click -> finalise
                    finalizeRange(picker.anchor, date);
                }
            });
        });

        paintSelection();
    }

    // Applies the selection / hover highlight classes in place (no rebuild).
    function paintSelection() {
        const cal = el('#range-cal');
        if (!cal) {
            return;
        }
        const lo = picker.anchor;
        const hi = picker.hover;
        cal.querySelectorAll('.cal-day[data-date]').forEach((cell) => {
            const date = parseKey(cell.dataset.date);
            cell.classList.remove('in-range', 'range-start', 'range-end', 'disabled');
            cell.classList.toggle('disabled', dayDisabled(date));
            if (!lo) {
                return;
            }
            const inRange = hi && date >= lo && date <= hi;
            if (inRange) {
                cell.classList.add('in-range');
            }
            if (sameDay(date, lo)) {
                cell.classList.add('range-start');
            }
            if (hi && sameDay(date, hi)) {
                cell.classList.add('range-end');
            }
        });
    }

    function finalizeRange(from, to) {
        state.period.from = from;
        state.period.to = to;
        closeCalendar(false);
        // Refresh the labels without rebuilding the whole period bar.
        const fromEl = el('#range-from');
        const toEl = el('#range-to');
        if (fromEl) { fromEl.textContent = fmtDateLb(from); }
        if (toEl) { toEl.textContent = fmtDateLb(to); }
        setRangeHint(`${dayDiff(from, to) + 1} day(s) selected.`, false);
        loadList();
    }

    function renderSubtitle(from, to) {
        const sub = el('#dash-sub');
        if (!sub) {
            return;
        }
        const days = dayDiff(from, to) + 1;
        const range = days === 1
            ? fmtDateLb(from)
            : `${fmtDateLb(from)} &rarr; ${fmtDateLb(to)}`;
        sub.innerHTML = `${range} &middot; ${days} day(s) &middot; ${state.shifts.length} shift(s) in List&nbsp;1`;
    }

    // ---- resume (over the whole List 1, never filtered) -------------
    function renderResume() {
        const host = el('#resume');
        if (!host) {
            return;
        }
        const busDead = state.shifts
            .filter((s) => s.vehicleType === VT_BUS)
            .reduce((sum, s) => sum + deadKmMeters(s), 0);
        const shuttleDead = state.shifts
            .filter((s) => s.vehicleType === VT_SHUTTLE)
            .reduce((sum, s) => sum + deadKmMeters(s), 0);
        const withDeviation = state.shifts
            .filter((s) => s.deadKmSeverity !== 0).length;

        host.innerHTML = `
            <div class="resume-card">
                <div class="l">Dead KM &mdash; Bus</div>
                <div class="v">${kmStr(busDead)}</div>
                <div class="d">Sum over all bus shifts in List 1</div>
            </div>
            <div class="resume-card">
                <div class="l">Dead KM &mdash; Shuttle</div>
                <div class="v">${kmStr(shuttleDead)}</div>
                <div class="d">Sum over all shuttle shifts in List 1</div>
            </div>
            <div class="resume-card">
                <div class="l">Deviations</div>
                <div class="v">${nfInt.format(withDeviation)}</div>
                <div class="d">Shifts with severity &ne; None</div>
            </div>`;
    }

    // ---- filters (built once; only the list re-renders on change) ---
    function renderFilters() {
        const host = el('#filters');
        const vtOptions = [['all', 'All vehicle types'], [VT_BUS, 'Bus'], [VT_SHUTTLE, 'Shuttle']]
            .map(([v, l]) => `<option value="${v}">${l}</option>`).join('');
        const sevOptions = [['all', 'All severities'], [3, 'Critical'], [2, 'Moderate'], [1, 'Weak'], [0, 'None']]
            .map(([v, l]) => `<option value="${v}">${l}</option>`).join('');

        host.innerHTML = `
            <label>Vehicle type
                <select id="filter-vt">${vtOptions}</select>
            </label>
            <label>Dead KM severity
                <select id="filter-sev">${sevOptions}</select>
            </label>
            <label>Car plate
                <input id="filter-plate" type="text" placeholder="e.g. SL3668"
                       autocomplete="off" spellcheck="false" />
            </label>`;

        el('#filter-vt').addEventListener('change', (e) => {
            state.filters.vehicleType = e.target.value;
            renderList();
        });
        el('#filter-sev').addEventListener('change', (e) => {
            state.filters.severity = e.target.value;
            renderList();
        });
        el('#filter-plate').addEventListener('input', (e) => {
            // English letters + digits only, forced uppercase.
            const cleaned = e.target.value.toUpperCase().replace(/[^A-Z0-9]/g, '');
            if (cleaned !== e.target.value) {
                e.target.value = cleaned;
            }
            state.filters.plate = cleaned;
            renderList();
        });
    }

    function filteredShifts() {
        const f = state.filters;
        return state.shifts.filter((s) => {
            if (f.vehicleType !== 'all' && s.vehicleType !== +f.vehicleType) {
                return false;
            }
            if (f.severity !== 'all' && s.deadKmSeverity !== +f.severity) {
                return false;
            }
            if (f.plate && !(s.carPlate || '').toUpperCase().includes(f.plate)) {
                return false;
            }
            return true;
        });
    }

    // ---- List 1 --------------------------------------------------
    function renderList() {
        const host = el('#shift-list');
        if (!host) {
            return;
        }

        const list = filteredShifts();

        // If the open box was filtered out, collapse it (and drop its map).
        if (state.expandedId !== null && !list.some((s) => s.id === state.expandedId)) {
            state.expandedId = null;
            state.detail = null;
            state.detailError = null;
        }

        // The API can report an error (first entry of ResultDto.errors) even
        // with a 200 OK; always surface it, and still show any shifts returned.
        let html = '';
        if (state.loadError) {
            html += `<div class="empty-msg load-error">${esc(state.loadError)}</div>`;
        }
        if (list.length === 0 && !state.loadError) {
            html += state.shifts.length === 0
                ? `<div class="empty-msg">No vehicle shifts for the selected date range.</div>`
                : `<div class="empty-msg">No shifts match the current filters.</div>`;
        } else if (list.length > 0) {
            html += list.map(renderShiftCard).join('');
        }
        host.innerHTML = html;

        // Wire the accordion titles.
        host.querySelectorAll('[data-toggle]').forEach((headEl) => {
            headEl.addEventListener('click', () => toggle(+headEl.dataset.toggle));
        });

        // Render the detail (trip bar + map) for the open box, or its error.
        if (state.expandedId !== null) {
            const detailHost = host.querySelector(`[data-detail="${state.expandedId}"]`);
            if (detailHost) {
                if (state.detail) {
                    detailHost.innerHTML = renderDetailInner(state.detail);
                    const mapEl = detailHost.querySelector('.map');
                    if (mapEl) {
                        renderMap(mapEl, state.detail);
                    }
                } else if (state.detailError) {
                    detailHost.innerHTML =
                        `<div class="empty-msg load-error" style="margin:0">${esc(state.detailError)}</div>`;
                }
            }
        }
    }

    function renderShiftCard(shift) {
        const open = state.expandedId === shift.id;
        const sev = shift.deadKmSeverity ?? 0;
        const dead = deadKmMeters(shift);
        const vt = VEHICLE_TYPES[shift.vehicleType] || VEHICLE_TYPES[0];

        return `
            <div class="shift-card ${open ? 'open' : ''}">
                <div class="shift-head" data-toggle="${shift.id}">
                    <span class="icon" title="${esc(vt.label)}">${vt.icon}</span>
                    <span class="plate">${esc(displayPlate(shift.carPlate))}</span>
                    <span class="grow">
                        <span class="date">${esc(isoToLb(shift.date))}</span>
                    </span>
                    <span class="sev sev-${sev}">${esc(SEVERITIES[sev] || 'None')}</span>
                    <span class="deadkm sev-${sev}">${kmStr(dead)}</span>
                </div>
                ${open ? `<div class="shift-detail" data-detail="${shift.id}">
                    <div class="empty-msg" style="border:none;padding:8px">Loading&hellip;</div>
                </div>` : ''}
            </div>`;
    }

    // Trip bar + details table + map for an expanded shift (from the detailed DTO).
    function renderDetailInner(detail) {
        return `
            <div class="section-title">Trips</div>
            ${renderTripBar(detail)}
            <div class="detail-split">
                <div class="trip-details">
                    <div class="section-title">Trip details</div>
                    ${renderTripDetails(detail)}
                </div>
                <div class="map-wrap">
                    <div class="section-title">Trip paths</div>
                    <div class="map"></div>
                    <div class="map-controls">
                    <button type="button" class="map-toggle on" data-layer="driven" aria-pressed="true">
                        <span class="sw" style="background:var(--map-driven)"></span>Actual path</button>
                    <button type="button" class="map-toggle on" data-layer="commercial" aria-pressed="true">
                        <span class="sw" style="background:var(--map-commercial)"></span>Commercial paths</button>
                    <button type="button" class="map-toggle on" data-layer="deadRun" aria-pressed="true">
                        <span class="sw" style="background:var(--map-deadrun)"></span>Empty paths</button>
                    <div class="map-anim-group">
                        <button type="button" class="map-anim" data-anim="play"
                                title="Play bus animation" aria-label="Play bus animation">&#9654;</button>
                        <button type="button" class="map-anim" data-anim="stop"
                                title="Stop bus animation" aria-label="Stop bus animation">&#9632;</button>
                    </div>
                    </div>
                </div>
            </div>`;
    }

    function renderTripBar(detail) {
        const trips = detail.tripList || [];
        if (trips.length === 0) {
            return `<div class="empty-msg" style="border:none;padding:8px">No trips recorded for this shift.</div>`;
        }
        // Each segment's width is proportional to its DrivenMeters over the
        // shift total. Missing-data trips (driven <= 0) contribute nothing to
        // the total; a CSS min-width keeps them visible so "NO DATA" shows.
        const driven = (t) => Math.max(0, t.drivenMeters || 0);
        const total = trips.reduce((sum, t) => sum + driven(t), 0);

        const segments = trips.map((t, i) => {
            const width = total > 0 ? (driven(t) / total) * 100 : 100 / trips.length;

            const info = tripInfo(t);
            const title = `${info.label} &middot; Line ${esc(t.line)} &middot; ${esc(t.startTime)}–${esc(t.endTime)}`;

            // The bar shows the trip's dead meters (signed), bold and white.
            const label = `<span class="m">${kmSigned(t.deadMeters)}</span>`;

            return `<div class="trip" data-trip="${i}" title="${title}" style="width:${width}%;background:${info.color}">
                        ${label}
                    </div>`;
        }).join('');

        return `
            <div class="trip-bar">${segments}</div>
            <div class="trip-legend">
                <span><span class="sw" style="background:var(--trip-commercial)"></span>Commercial trip</span>
                <span><span class="sw" style="background:var(--trip-under)"></span>Empty trip</span>
                <span><span class="sw" style="background:var(--trip-deadkm)"></span>Dead km trip</span>
            </div>`;
    }

    // Trip details table (left column of the split): one row per trip, in the
    // same order as the trip bar. From/To use the long names; Actual Km shows
    // "NO DATA" when the trip has no driven distance.
    function renderTripDetails(detail) {
        const trips = detail.tripList || [];
        if (trips.length === 0) {
            return `<div class="empty-msg" style="border:none;padding:8px">No trips recorded for this shift.</div>`;
        }

        const rows = trips.map((t) => {
            const actual = (t.drivenMeters || 0) > 0
                ? kmStr(t.drivenMeters)
                : '<span class="nodata">NO DATA</span>';
            return `<tr>
                        <td>${esc(t.line)}</td>
                        <td class="wrap">${esc(t.fromLongName)}</td>
                        <td class="wrap">${esc(t.toLongName)}</td>
                        <td class="time">${esc(t.startTime)}</td>
                        <td class="time">${esc(t.endTime)}</td>
                        <td class="num">${kmStr(t.plannedMeters)}</td>
                        <td class="num">${actual}</td>
                    </tr>`;
        }).join('');

        return `
            <div class="trip-table-scroll">
                <table class="trip-table">
                    <thead>
                        <tr>
                            <th>Line</th><th>From</th><th>To</th>
                            <th>Start Time</th><th>End Time</th>
                            <th class="num">Planned Km</th><th class="num">Actual Km</th>
                        </tr>
                    </thead>
                    <tbody>${rows}</tbody>
                </table>
            </div>`;
    }

    // =================================================================
    // Accordion toggle (fetches the detail on expand; drops it on
    // collapse so only one map ever lives in the DOM).
    // =================================================================
    async function toggle(id) {
        if (state.expandedId === id) {
            state.expandedId = null;
            state.detail = null;
            state.detailError = null;
            renderList();
            return;
        }

        // Collapse the previous one first, then open the new one.
        state.expandedId = null;
        state.detail = null;
        state.detailError = null;

        showOverlay();
        let detail = null;
        let detailError = null;
        try {
            const res = await fetchDetail(id);
            detail = res.result;
            detailError = res.error;
        } catch (ex) {
            detailError = ex.message || 'Could not load the shift detail.';
        } finally {
            hideOverlay();
        }

        state.expandedId = id;
        state.detail = detail;
        // Only report an error when there is no detail to show.
        state.detailError = detail ? null : (detailError || 'No detail available for this shift.');
        renderList();

        // Scroll the page so the just-expanded box title sits at the top of
        // the viewport (a small scroll-margin keeps it just below the edge).
        const head = document.querySelector(`.shift-head[data-toggle="${id}"]`);
        if (head) {
            head.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
    }

    // =================================================================
    // Map — OpenStreetMap tiles + SVG overlay, built from scratch.
    // Vector2 comes back as {x: longitude, y: latitude}.
    // =================================================================
    const TILE = 256;

    // Handle of the in-flight bus-animation frame request (module-scoped so a
    // re-render of the map cancels any animation left running by the old one).
    let busRaf = 0;

    function project(lat, lon, zoom) {
        const scale = TILE * Math.pow(2, zoom);
        const x = ((lon + 180) / 360) * scale;
        const latRad = (lat * Math.PI) / 180;
        const y = ((1 - Math.log(Math.tan(latRad) + 1 / Math.cos(latRad)) / Math.PI) / 2) * scale;
        return { x, y };
    }

    function pickZoom(bbox, w, h) {
        for (let z = 18; z >= 1; z--) {
            const a = project(bbox.maxLat, bbox.minLon, z);
            const b = project(bbox.minLat, bbox.maxLon, z);
            const spanX = Math.abs(b.x - a.x);
            const spanY = Math.abs(b.y - a.y);
            if (spanX <= w * 0.85 && spanY <= h * 0.85) {
                return z;
            }
        }
        return 1;
    }

    // Build the list of geographic points for a path, preferring real
    // vertices when the API provides them and falling back to the trip
    // start/end geopositions otherwise. The API serializes a Path as a
    // plain array of Vector2 ({x: lon, y: lat}); older builds wrapped it
    // in {vertices: [...]}, which is still accepted here.
    function pathToPoints(path) {
        const verts = Array.isArray(path)
            ? path
            : (path && Array.isArray(path.vertices) ? path.vertices : null);
        if (verts && verts.length) {
            return verts
                .filter((v) => v && isFinite(v.x) && isFinite(v.y))
                .map((v) => ({ lat: v.y, lon: v.x }));
        }
        return null;
    }

    function drivenPoints(detail) {
        const fromPath = pathToPoints(detail.drivenPath);
        if (fromPath) {
            return fromPath;
        }
        const pts = [];
        (detail.tripList || []).forEach((t) => {
            if (t.startGeoposition) {
                pts.push({ lat: t.startGeoposition.y, lon: t.startGeoposition.x });
            }
            if (t.endGeoposition) {
                pts.push({ lat: t.endGeoposition.y, lon: t.endGeoposition.x });
            }
        });
        // collapse consecutive duplicates
        return pts.filter((p, i) => i === 0 || p.lat !== pts[i - 1].lat || p.lon !== pts[i - 1].lon);
    }

    // Every trip carries `paths`: an array of polylines (each a list of
    // Vector2). Only Commercial and Empty trips are drawn on the map: commercial
    // paths (cyan) and empty / non-commercial paths (yellow). Dead km and Unknown
    // trips are never rendered. Commercial trips fall back to a straight
    // start->end line when they have no paths.
    // `byTrip` keeps each trip's own segments (aligned to tripList index) so a
    // single trip can be highlighted on hover; commercial/deadRun are the same
    // segments flattened per kind for the base layers. Non-map trips get empty
    // segments, so hovering them highlights nothing.
    function plannedSegments(detail) {
        const commercial = [];
        const deadRun = [];
        const byTrip = [];
        (detail.tripList || []).forEach((t) => {
            const segs = [];
            if (isTripOnMap(t)) {
                const commercialTrip = isCommercialTrip(t);
                const target = commercialTrip ? commercial : deadRun;
                const groups = Array.isArray(t.paths) ? t.paths : [];
                groups.forEach((group) => {
                    const pts = pathToPoints(group);
                    if (pts && pts.length >= 2) {
                        segs.push(pts);
                    }
                });
                if (segs.length === 0 && commercialTrip) {
                    const seg = [];
                    if (t.startGeoposition) {
                        seg.push({ lat: t.startGeoposition.y, lon: t.startGeoposition.x });
                    }
                    if (t.endGeoposition) {
                        seg.push({ lat: t.endGeoposition.y, lon: t.endGeoposition.x });
                    }
                    if (seg.length >= 2) {
                        segs.push(seg);
                    }
                }
                segs.forEach((s) => target.push(s));
            }
            byTrip.push({ segments: segs });
        });
        return { commercial, deadRun, byTrip };
    }

    function boundsOf(pointGroups) {
        let minLat = Infinity, maxLat = -Infinity, minLon = Infinity, maxLon = -Infinity;
        pointGroups.forEach((group) => group.forEach((p) => {
            if (!isFinite(p.lat) || !isFinite(p.lon)) {
                return;
            }
            minLat = Math.min(minLat, p.lat);
            maxLat = Math.max(maxLat, p.lat);
            minLon = Math.min(minLon, p.lon);
            maxLon = Math.max(maxLon, p.lon);
        }));
        if (!isFinite(minLat)) {
            return null;
        }
        // pad a degenerate (single-point) bbox so a zoom can be chosen
        if (minLat === maxLat) { minLat -= 0.002; maxLat += 0.002; }
        if (minLon === maxLon) { minLon -= 0.002; maxLon += 0.002; }
        return { minLat, maxLat, minLon, maxLon };
    }

    const MIN_ZOOM = 2;
    const MAX_ZOOM = 19;

    function renderMap(mapEl, detail) {
        // A previous expanded map may still be animating; stop it before we
        // build a new one so two buses can never run at once.
        if (busRaf) {
            cancelAnimationFrame(busRaf);
            busRaf = 0;
        }

        const driven = drivenPoints(detail);
        const { commercial, deadRun, byTrip } = plannedSegments(detail);
        const groups = [driven, ...commercial, ...deadRun].filter((g) => g.length);
        const bbox = boundsOf(groups);

        if (!bbox) {
            mapEl.innerHTML = `<div class="map-empty">No geographic data available for this shift.</div>`;
            return;
        }

        const w = mapEl.clientWidth || 600;
        const h = mapEl.clientHeight || MAP_HEIGHT;

        // Persistent view state: integer zoom + world-pixel top-left origin.
        // The initial view is fitted to the shift's geometry.
        const fitZoom = Math.min(16, pickZoom(bbox, w, h));
        const c0 = project((bbox.minLat + bbox.maxLat) / 2, (bbox.minLon + bbox.maxLon) / 2, fitZoom);
        const view = { zoom: fitZoom, originX: c0.x - w / 2, originY: c0.y - h / 2 };

        mapEl.innerHTML = `
            <div class="map-pan">
                <div class="tiles"></div>
                <svg width="${w}" height="${h}" viewBox="0 0 ${w} ${h}"></svg>
            </div>
            <div class="map-zoom">
                <button type="button" data-zoom="1" aria-label="Zoom in" title="Zoom in">+</button>
                <button type="button" data-zoom="-1" aria-label="Zoom out" title="Zoom out">&minus;</button>
            </div>
            <div class="attribution">&copy; OpenStreetMap contributors</div>`;

        const pan = mapEl.querySelector('.map-pan');
        const tilesEl = mapEl.querySelector('.tiles');
        const svgEl = mapEl.querySelector('svg');

        // Which path layers are currently shown (toggled by the buttons below).
        const visible = { commercial: true, deadRun: true, driven: true };

        // Index of the trip currently hovered in the trip bar (null = none). Its
        // own path is drawn in red on top, regardless of layer visibility.
        let highlightTrip = null;

        // ---- bus animation (Play / Stop) ----
        // One emoji marker that travels the driven (actual) path over 45 s. It
        // lives inside .map-pan and is positioned in the same world-pixel space
        // as the tiles, so it tracks pan/zoom exactly and animates even when the
        // driven layer is hidden.
        const ANIM_MS = 45000;
        const busEl = document.createElement('div');
        busEl.className = 'map-bus';
        busEl.textContent = '🚌';
        busEl.style.display = 'none';
        pan.appendChild(busEl);

        const bus = { active: false, at: null, cum: [], total: 0, startTs: 0 };

        // Cumulative along-path distances (equirectangular-ish, so the bus keeps a
        // uniform real-world speed regardless of zoom). Computed once per play.
        function buildCumulative() {
            bus.cum = [0];
            let total = 0;
            for (let i = 1; i < driven.length; i++) {
                const a = driven[i - 1];
                const b = driven[i];
                const midLat = ((a.lat + b.lat) / 2) * Math.PI / 180;
                const dLon = (b.lon - a.lon) * Math.cos(midLat);
                const dLat = b.lat - a.lat;
                total += Math.hypot(dLon, dLat);
                bus.cum.push(total);
            }
            bus.total = total;
        }

        // Point at progress p in [0,1] along the driven path.
        function busPointAt(p) {
            if (driven.length === 1 || bus.total === 0) {
                return driven[0];
            }
            const target = p * bus.total;
            let i = 1;
            while (i < bus.cum.length - 1 && bus.cum[i] < target) {
                i++;
            }
            const segLen = (bus.cum[i] - bus.cum[i - 1]) || 1;
            const f = (target - bus.cum[i - 1]) / segLen;
            const a = driven[i - 1];
            const b = driven[i];
            return { lat: a.lat + (b.lat - a.lat) * f, lon: a.lon + (b.lon - a.lon) * f };
        }

        // Places the bus at its current lat/lon in the current view.
        function positionBus() {
            if (!bus.active || !bus.at) {
                busEl.style.display = 'none';
                return;
            }
            const q = project(bus.at.lat, bus.at.lon, view.zoom);
            busEl.style.left = `${(q.x - view.originX).toFixed(1)}px`;
            busEl.style.top = `${(q.y - view.originY).toFixed(1)}px`;
            busEl.style.display = '';
        }

        function stopBusRaf() {
            if (busRaf) {
                cancelAnimationFrame(busRaf);
                busRaf = 0;
            }
        }

        function busFrame(ts) {
            if (!bus.startTs) {
                bus.startTs = ts;
            }
            const p = Math.min(1, (ts - bus.startTs) / ANIM_MS);
            bus.at = busPointAt(p);
            positionBus();
            if (p < 1) {
                busRaf = requestAnimationFrame(busFrame);
            } else {
                busRaf = 0; // reached the end; the bus rests there until Stop / replay
            }
        }

        // Play: drop the bus on the start and animate to the end. Clicking again
        // while it runs restarts from the beginning.
        function playBus() {
            if (driven.length === 0) {
                return; // no actual path to follow
            }
            stopBusRaf();
            buildCumulative();
            bus.active = true;
            bus.startTs = 0;
            bus.at = driven[0];
            positionBus();
            busRaf = requestAnimationFrame(busFrame);
        }

        // Stop: halt the animation and hide the bus.
        function stopBus() {
            stopBusRaf();
            bus.active = false;
            bus.at = null;
            positionBus();
        }

        // Renders tiles + path overlay for the current view.
        function draw() {
            const z = view.zoom;
            const worldTiles = Math.pow(2, z);
            const ox = view.originX;
            const oy = view.originY;

            const tx0 = Math.floor(ox / TILE);
            const tx1 = Math.floor((ox + w) / TILE);
            const ty0 = Math.floor(oy / TILE);
            const ty1 = Math.floor((oy + h) / TILE);
            let tilesHtml = '';
            for (let tx = tx0; tx <= tx1; tx++) {
                for (let ty = ty0; ty <= ty1; ty++) {
                    if (ty < 0 || ty >= worldTiles) {
                        continue;
                    }
                    const nx = ((tx % worldTiles) + worldTiles) % worldTiles;
                    const left = tx * TILE - ox;
                    const top = ty * TILE - oy;
                    tilesHtml += `<img alt="" src="https://tile.openstreetmap.org/${z}/${nx}/${ty}.png"
                                       style="left:${left}px;top:${top}px" onerror="this.style.display='none'">`;
                }
            }
            tilesEl.innerHTML = tilesHtml;

            const toXY = (p) => {
                const q = project(p.lat, p.lon, z);
                return `${(q.x - ox).toFixed(1)},${(q.y - oy).toFixed(1)}`;
            };
            const polyline = (pts, color, width, dash, opacity = 1) =>
                `<polyline points="${pts.map(toXY).join(' ')}" fill="none" stroke="${color}"
                           stroke-width="${width}" stroke-linecap="round" stroke-linejoin="round"
                           stroke-opacity="${opacity}"
                           ${dash ? `stroke-dasharray="${dash}"` : ''} />`;

            // Draw order = stacking (later renders on top): commercial paths at
            // the bottom, empty (non-commercial) paths in the middle, and the
            // actual driven path on top. Commercial (cyan) and empty (yellow)
            // paths are both 10% transparent; the actual path is black at 15%
            // transparency, dashed and thinner than the planned paths. Each
            // layer is hidden when its toggle button is off.
            let svgPaths = '';
            if (visible.commercial) {
                commercial.forEach((seg) => {
                    svgPaths += polyline(seg, 'var(--map-commercial)', 8, '', 0.9);
                });
            }
            if (visible.deadRun) {
                deadRun.forEach((seg) => {
                    svgPaths += polyline(seg, 'var(--map-deadrun)', 8, '', 0.9);
                });
            }
            if (visible.driven && driven.length >= 2) {
                svgPaths += polyline(driven, 'var(--map-driven)', 4, '8 6', 0.85);
            }
            // Hover highlight: the hovered trip's own path in red, drawn on top
            // and shown even when that kind's layer toggle is off.
            if (highlightTrip !== null && byTrip[highlightTrip]) {
                byTrip[highlightTrip].segments.forEach((seg) => {
                    svgPaths += polyline(seg, 'var(--map-highlight)', 9, '', 1);
                });
            }
            // start/end markers belong to the driven (actual) path only
            let markers = '';
            if (visible.driven && driven.length) {
                const s = project(driven[0].lat, driven[0].lon, z);
                const e = project(driven[driven.length - 1].lat, driven[driven.length - 1].lon, z);
                // start: green triangle
                const sx = s.x - ox;
                const sy = s.y - oy;
                const r = 8;
                const tri = [
                    `${sx.toFixed(1)},${(sy - r).toFixed(1)}`,
                    `${(sx - r * 0.87).toFixed(1)},${(sy + r * 0.5).toFixed(1)}`,
                    `${(sx + r * 0.87).toFixed(1)},${(sy + r * 0.5).toFixed(1)}`
                ].join(' ');
                markers += `<polygon points="${tri}" fill="#22c55e" stroke="#0b1220"
                                     stroke-width="2" stroke-linejoin="round" />`;
                // end: blue circle
                markers += `<circle cx="${(e.x - ox).toFixed(1)}" cy="${(e.y - oy).toFixed(1)}" r="6"
                                    fill="#60a5fa" stroke="#0b1220" stroke-width="2" />`;
            }
            svgEl.innerHTML = svgPaths + markers;

            // Keep the bus glued to its geographic point after a view change.
            positionBus();
        }

        // Zoom by `delta` levels while keeping the point at (cx, cy) fixed.
        function zoomAt(cx, cy, delta) {
            const nz = Math.max(MIN_ZOOM, Math.min(MAX_ZOOM, view.zoom + delta));
            if (nz === view.zoom) {
                return;
            }
            const f = Math.pow(2, nz - view.zoom);
            view.originX = (view.originX + cx) * f - cx;
            view.originY = (view.originY + cy) * f - cy;
            view.zoom = nz;
            draw();
        }

        const localPoint = (clientX, clientY) => {
            const r = mapEl.getBoundingClientRect();
            return { x: clientX - r.left, y: clientY - r.top };
        };

        // ---- zoom controls ----
        mapEl.querySelectorAll('.map-zoom [data-zoom]').forEach((btn) => {
            btn.addEventListener('click', (ev) => {
                ev.stopPropagation();
                zoomAt(w / 2, h / 2, Number(btn.dataset.zoom));
            });
        });

        // ---- wheel zoom (at cursor) ----
        mapEl.addEventListener('wheel', (ev) => {
            ev.preventDefault();
            const p = localPoint(ev.clientX, ev.clientY);
            zoomAt(p.x, p.y, ev.deltaY < 0 ? 1 : -1);
        }, { passive: false });

        // ---- double-click zoom in ----
        mapEl.addEventListener('dblclick', (ev) => {
            const p = localPoint(ev.clientX, ev.clientY);
            zoomAt(p.x, p.y, 1);
        });

        // ---- drag to pan (mouse). Content is translated live and the origin
        //      is committed on release; move/up handlers live only for the
        //      duration of the drag so nothing leaks between maps. ----
        let dx = 0, dy = 0, sx = 0, sy = 0;
        function onMove(ev) {
            dx = ev.clientX - sx;
            dy = ev.clientY - sy;
            pan.style.transform = `translate(${dx}px, ${dy}px)`;
        }
        function onUp() {
            view.originX -= dx;
            view.originY -= dy;
            pan.style.transform = '';
            mapEl.classList.remove('dragging');
            window.removeEventListener('mousemove', onMove);
            window.removeEventListener('mouseup', onUp);
            draw();
        }
        mapEl.addEventListener('mousedown', (ev) => {
            if (ev.button !== 0 || ev.target.closest('.map-zoom')) {
                return;
            }
            ev.preventDefault();
            sx = ev.clientX;
            sy = ev.clientY;
            dx = 0;
            dy = 0;
            mapEl.classList.add('dragging');
            window.addEventListener('mousemove', onMove);
            window.addEventListener('mouseup', onUp);
        });

        // ---- touch: one finger pans, two fingers pinch-zoom (stepwise) ----
        let touchMode = null;   // 'pan' | 'pinch'
        let pinchDist = 0;
        const dist = (t0, t1) => Math.hypot(t0.clientX - t1.clientX, t0.clientY - t1.clientY);
        function onTouchMove(ev) {
            ev.preventDefault();
            if (touchMode === 'pinch' && ev.touches.length >= 2) {
                const d = dist(ev.touches[0], ev.touches[1]);
                const mid = localPoint(
                    (ev.touches[0].clientX + ev.touches[1].clientX) / 2,
                    (ev.touches[0].clientY + ev.touches[1].clientY) / 2);
                if (d / pinchDist > 1.5) { zoomAt(mid.x, mid.y, 1); pinchDist = d; }
                else if (d / pinchDist < 0.66) { zoomAt(mid.x, mid.y, -1); pinchDist = d; }
            } else if (touchMode === 'pan' && ev.touches.length === 1) {
                dx = ev.touches[0].clientX - sx;
                dy = ev.touches[0].clientY - sy;
                pan.style.transform = `translate(${dx}px, ${dy}px)`;
            }
        }
        function onTouchEnd(ev) {
            if (ev.touches.length > 0) {
                return; // still touching (e.g. lifted one finger of a pinch)
            }
            if (touchMode === 'pan') {
                view.originX -= dx;
                view.originY -= dy;
                pan.style.transform = '';
                draw();
            }
            touchMode = null;
            window.removeEventListener('touchmove', onTouchMove);
            window.removeEventListener('touchend', onTouchEnd);
            window.removeEventListener('touchcancel', onTouchEnd);
        }
        mapEl.addEventListener('touchstart', (ev) => {
            if (ev.target.closest('.map-zoom')) {
                return;
            }
            if (ev.touches.length >= 2) {
                touchMode = 'pinch';
                pinchDist = dist(ev.touches[0], ev.touches[1]);
            } else {
                touchMode = 'pan';
                sx = ev.touches[0].clientX;
                sy = ev.touches[0].clientY;
                dx = 0;
                dy = 0;
            }
            window.addEventListener('touchmove', onTouchMove, { passive: false });
            window.addEventListener('touchend', onTouchEnd);
            window.addEventListener('touchcancel', onTouchEnd);
        }, { passive: true });

        // ---- layer toggles (buttons below the map) ----
        const wrap = mapEl.closest('.map-wrap');
        if (wrap) {
            wrap.querySelectorAll('.map-controls [data-layer]').forEach((btn) => {
                btn.addEventListener('click', () => {
                    const layer = btn.dataset.layer;
                    visible[layer] = !visible[layer];
                    btn.classList.toggle('on', visible[layer]);
                    btn.setAttribute('aria-pressed', String(visible[layer]));
                    draw();
                });
            });

            // ---- bus animation controls ----
            wrap.querySelectorAll('.map-controls [data-anim]').forEach((btn) => {
                btn.addEventListener('click', () => {
                    if (btn.dataset.anim === 'play') {
                        playBus();
                    } else {
                        stopBus();
                    }
                });
            });
        }

        // ---- hover a trip in the bar → highlight its path on the map ----
        const detailRoot = mapEl.closest('[data-detail]');
        if (detailRoot) {
            detailRoot.querySelectorAll('.trip-bar .trip[data-trip]').forEach((seg) => {
                const idx = Number(seg.dataset.trip);
                seg.addEventListener('mouseenter', () => { highlightTrip = idx; draw(); });
                seg.addEventListener('mouseleave', () => { highlightTrip = null; draw(); });
            });
        }

        draw();
    }

    // =================================================================
    // Auth lifecycle hook (called from updateAuthUi)
    // =================================================================
    function onAuthChange(loggedIn) {
        if (loggedIn) {
            if (!state.built) {
                renderShell();
                loadList();
            }
        } else {
            // Tear the dashboard down on logout.
            const host = el('#dashboard');
            if (host) {
                host.innerHTML = '';
            }
            hideOverlay();
            state.built = false;
            state.shifts = [];
            state.loadError = null;
            state.expandedId = null;
            state.detail = null;
            state.detailError = null;
            state.period = { mode: 'yesterday', from: startOfYesterday(), to: startOfYesterday() };
            state.filters = { vehicleType: 'all', severity: 'all', plate: '' };
        }
    }

    return { onAuthChange };
})();
