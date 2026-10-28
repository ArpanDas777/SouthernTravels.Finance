// Southern Travels Finance — progressive enhancement for the server-rendered reports.
(function () {
    'use strict';

    const $ = (sel, root) => (root || document).querySelector(sel);
    const $$ = (sel, root) => Array.from((root || document).querySelectorAll(sel));
    const returnUrl = () => location.pathname + location.search;
    const html = document.documentElement, desktop = matchMedia('(min-width: 1280px)');

    /* ---------- sidebar ---------- */
    $$('[data-side-toggle]').forEach(b => b.addEventListener('click', () => {
        const closed = document.documentElement.classList.toggle('side-closed');
        try { localStorage.setItem('fin.side', closed ? 'closed' : 'open'); } catch (e) { /* storage unavailable */ }
    }));

    // Desktop: clicking the main area collapses an open rail (the toggle brings it back). Tablets and phones use the drawer instead.
    $('[data-main]').addEventListener('click', () => {
        if (!desktop.matches || html.classList.contains('side-closed') || html.classList.contains('rail-min')) return;
        html.classList.add('side-closed');
        try { localStorage.setItem('fin.side', 'closed'); } catch (e) { /* storage unavailable */ }
    });

    /* ---------- drawer menu (below 1280px) ---------- */
    const drawerBtn = $('[data-drawer-toggle]'), drawerScrim = $('[data-drawer-close]');
    function setDrawer(open) {
        html.classList.toggle('drawer-open', open);
        drawerScrim.hidden = !open;
        drawerBtn.setAttribute('aria-expanded', String(open));
    }
    drawerBtn.addEventListener('click', () => setDrawer(!html.classList.contains('drawer-open')));
    drawerScrim.addEventListener('click', () => setDrawer(false));
    $$('.side-item').forEach(a => a.addEventListener('click', () => setDrawer(false)));
    matchMedia('(min-width: 1280px)').addEventListener('change', e => { if (e.matches) setDrawer(false); });
    document.addEventListener('keydown', e => { if (e.key === 'Escape') setDrawer(false); });

    /* ---------- pinned bar: the table header sticks right under it ---------- */
    const stickyBar = $('[data-sticky-bar]');
    if (stickyBar && window.ResizeObserver) {
        // visible height of the bar = its height minus the 8px of main padding it hangs over (and 1px so the header overlaps it)
        const setStick = () => document.documentElement.style.setProperty('--stick-h', Math.max(0, stickyBar.offsetHeight - 9) + 'px');
        new ResizeObserver(setStick).observe(stickyBar);
        setStick();
    }

    /* ---------- drop-downs inside a report grid that save on change ---------- */
    // A value with an Edit button: Edit swaps in a [data-post] drop-down, which posts { sid: its row key, mode: the new value, all: 1 }
    // with the page's antiforgery token and reloads so the toast shows.
    document.addEventListener('click', e => {
        const open = e.target.closest('[data-edit-open]'), cancel = e.target.closest('[data-edit-cancel]');
        const box = (open || cancel) && (open || cancel).closest('[data-edit-sel]');
        if (!box) return;
        $('.edit-sel-view', box).hidden = !!open;
        $('.edit-sel-form', box).hidden = !open;
        if (open) $('select', box).focus();
    });
    document.addEventListener('change', async e => {
        const s = e.target.closest('[data-post]');
        if (!s) return;
        const fd = new FormData();
        fd.set('__RequestVerificationToken', $('input[name=__RequestVerificationToken]').value);
        fd.set('sid', s.dataset.key); fd.set('mode', s.value); fd.set('all', '1');
        fd.set('returnUrl', location.pathname + location.search);
        s.disabled = true;
        try { await fetch(s.dataset.post, { method: 'POST', body: fd, redirect: 'manual' }); } catch (err) { /* reload shows the real state */ }
        location.reload();
    });

    // A [data-act] button POSTs to its URL with the page's antiforgery token and reloads, so the toast shows the result.
    document.addEventListener('click', async e => {
        const b = e.target.closest('[data-act]');
        if (!b || b.disabled) return;
        const fd = new FormData();
        fd.set('__RequestVerificationToken', $('input[name=__RequestVerificationToken]').value);
        fd.set('returnUrl', location.pathname + location.search);
        b.disabled = true;
        try { await fetch(b.dataset.act, { method: 'POST', body: fd, redirect: 'manual' }); } catch (err) { /* reload shows the real state */ }
        location.reload();
    });

    /* ---------- auto-submitting controls ---------- */
    document.addEventListener('change', e => {
        const el = e.target;
        if (el.matches('[data-autosubmit]') && el.form) el.form.requestSubmit ? el.form.requestSubmit() : el.form.submit();
    });

    /* ---------- period picker (Today · This Month · Custom) ---------- */
    const periodForm = $('#period-form');
    if (periodForm) {
        const pop = $('[data-period-pop]', periodForm), calsEl = $('[data-period-cals]', periodForm), rangeEl = $('[data-period-range]', periodForm), btn = $('[data-period-btn]', periodForm);
        const today = periodForm.dataset.today, MON = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
        const ymd = (y, m, d) => y + '-' + String(m + 1).padStart(2, '0') + '-' + String(d).padStart(2, '0');
        const fd = s => { const [y, m, d] = s.split('-'); return +d + ' ' + MON[+m - 1] + ' ' + String(y).slice(2); };
        const monthStart = today.slice(0, 8) + '01';
        let draft = null;
        const rangeOf = k => k === 'Today' ? [today, today] : k === 'This Month' ? [monthStart, today] : [draft.from, draft.to];
        const cal = (which, year, month) => {
            const box = el('div', 'cal'), head = el('div', 'cal-head');
            const ms = el('select', 'ctl ctl-28'), ys = el('select', 'ctl ctl-28');
            MON.forEach((n, i) => { const o = el('option', '', n); o.value = i; o.selected = i === month; ms.append(o); });
            for (let y = +today.slice(0, 4) - 5; y <= +today.slice(0, 4) + 1; y++) { const o = el('option', '', y); o.value = y; o.selected = y === year; ys.append(o); }
            const redraw = () => { draft[which + 'View'] = [+ys.value, +ms.value]; render(); };
            ms.addEventListener('change', redraw); ys.addEventListener('change', redraw);
            head.append(ms, ys); box.append(head);
            const grid = el('div', 'cal-grid');
            ['M', 'T', 'W', 'T', 'F', 'S', 'S'].forEach(d => grid.append(el('span', 'cal-dow', d)));
            const lead = (new Date(year, month, 1).getDay() + 6) % 7, days = new Date(year, month + 1, 0).getDate();
            for (let i = 0; i < lead; i++) grid.append(el('span'));
            const [from, to] = rangeOf(draft.k);
            for (let d = 1; d <= days; d++) {
                const s = ymd(year, month, d), b = el('button', 'cal-day', d);
                b.type = 'button';
                if (s >= from && s <= to) b.classList.add('in');
                if (s === from || s === to) b.classList.add('end');
                if (s === today) b.classList.add('today');
                if (draft.k === 'Custom') b.addEventListener('click', () => { draft[which] = s; if (draft.from && draft.to && draft.from > draft.to) [draft.from, draft.to] = [draft.to, draft.from]; render(); });
                else b.disabled = true;
                grid.append(b);
            }
            box.append(grid);
            return box;
        };
        function render() {
            $$('[data-period-opt]', pop).forEach(b => b.classList.toggle('on', b.dataset.periodOpt === draft.k));
            const [ty, tm] = [+today.slice(0, 4), +today.slice(5, 7) - 1];
            const fv = draft.fromView || (draft.from ? [+draft.from.slice(0, 4), +draft.from.slice(5, 7) - 1] : [ty, tm]);
            const tv = draft.toView || (draft.to ? [+draft.to.slice(0, 4), +draft.to.slice(5, 7) - 1] : [ty, tm]);
            calsEl.replaceChildren(...(draft.k === 'Custom' ? [cal('from', ...fv), cal('to', ...tv)] : [cal('from', ty, tm)]));
            const [a, b] = rangeOf(draft.k);
            rangeEl.textContent = draft.k === 'Custom' && !(draft.from && draft.to) ? 'Pick a start and an end date' : fd(a) + (a === b ? '' : ' → ' + fd(b));
            $('[data-period-apply]', pop).disabled = draft.k === 'Custom' && !(draft.from && draft.to);
        }
        const close = () => { pop.hidden = true; btn.setAttribute('aria-expanded', 'false'); };
        btn.addEventListener('click', e => {
            e.stopPropagation();
            if (!pop.hidden) { close(); return; }
            const k = periodForm.elements.k.value;
            draft = { k: k === 'All time' ? 'This Month' : k, from: periodForm.elements.from.value, to: periodForm.elements.to.value };
            pop.hidden = false; btn.setAttribute('aria-expanded', 'true'); render();
        });
        pop.addEventListener('click', e => {
            e.stopPropagation();
            const o = e.target.closest('[data-period-opt]');
            if (o) { draft.k = o.dataset.periodOpt; render(); }
        });
        document.addEventListener('click', () => { if (!pop.hidden) close(); });
        document.addEventListener('keydown', e => { if (e.key === 'Escape' && !pop.hidden) close(); });
        $('[data-period-cancel]', pop).addEventListener('click', close);
        $('[data-period-apply]', pop).addEventListener('click', () => {
            periodForm.elements.k.value = draft.k;
            periodForm.elements.from.value = draft.k === 'Custom' ? draft.from : '';
            periodForm.elements.to.value = draft.k === 'Custom' ? draft.to : '';
            periodForm.submit();
        });
        $$('[data-period-clear]', periodForm).forEach(b => b.addEventListener('click', () => { periodForm.elements.k.value = 'All time'; periodForm.submit(); }));
    }

    // Search filters as you type (debounced). After reload the box has `autofocus` when it holds a query.
    const search = $('[data-search]');
    if (search) {
        let t;
        search.addEventListener('input', () => { clearTimeout(t); t = setTimeout(() => search.form.submit(), 450); });
    }

    /* ---------- filter panel (inline from 1100px, bottom sheet below) ---------- */
    const compact = matchMedia('(max-width: 1099.98px)');
    const filters = $('[data-filters]');
    if (filters) {
        const filterBtn = $('.bar [data-filter-toggle]'), fo = $('[data-fo]');
        const setFilters = open => {
            filters.hidden = !open;
            fo.value = open ? '1' : '';
            filterBtn.classList.toggle('btn-primary', open);
            filterBtn.setAttribute('aria-expanded', String(open));
        };
        $$('[data-filter-toggle]').forEach(b => b.addEventListener('click', () => setFilters(filters.hidden)));
        // As a sheet, tapping the scrim (outside the panel) or pressing Esc closes it.
        filters.addEventListener('click', e => { if (e.target === filters && compact.matches) setFilters(false); });
        document.addEventListener('keydown', e => { if (e.key === 'Escape' && !filters.hidden && compact.matches) setFilters(false); });
    }

    /* ---------- expandable rows ---------- */
    const seenForm = $('#seen-form');
    function setOpen(tr, open) {
        const detail = tr.nextElementSibling;
        tr.classList.toggle('open', open);
        tr.setAttribute('aria-expanded', String(open));
        if (detail && detail.classList.contains('detail')) detail.hidden = !open;
        const seen = open && $('[data-seen]', tr);
        if (seen) {
            // Payment Request / Approved Vendor Payable: clear the vendor's red dot once viewed.
            const body = new FormData(seenForm);
            body.set('area', seen.dataset.area);
            body.set('vendor', seen.dataset.seen);
            fetch(seenForm.action, { method: 'POST', body }).catch(() => { });
            const dot = $('.newdot', tr);
            if (dot) dot.remove();
            delete seen.dataset.seen;
        }
    }
    function syncExpandAll() {
        const btn = $('[data-expand-all]');
        if (!btn) return;
        const rows = $$('tr.top');
        btn.textContent = rows.length && rows.every(r => r.classList.contains('open')) ? 'Collapse All' : 'Expand All';
    }
    document.addEventListener('click', e => {
        const tr = e.target.closest('tr.top');
        if (!tr || e.target.closest('button, a, input, select, label')) return;
        setOpen(tr, !tr.classList.contains('open'));
        syncExpandAll();
    });
    document.addEventListener('keydown', e => {
        const tr = e.target.closest && e.target.closest('tr.top');
        if (tr && e.target === tr && (e.key === 'Enter' || e.key === ' ')) {
            e.preventDefault();
            setOpen(tr, !tr.classList.contains('open'));
            syncExpandAll();
        }
    });
    $$('[data-expand-all]').forEach(b => b.addEventListener('click', () => {
        const rows = $$('tr.top'), allOpen = rows.every(r => r.classList.contains('open'));
        rows.forEach(r => setOpen(r, !allOpen));
        syncExpandAll();
    }));

    /* ---------- the cart (Vendor Payable: Raise Bill · Approved Vendor Payable: Submit) ---------- */
    // Ticked services collect in the cart, grouped by vendor. The selection is kept in sessionStorage so it survives
    // filtering, paging and the Vendor Wise / Ticket Wise tabs; a successful Raise Bill / Submit removes what it used.
    const cart = $('[data-cart]'), pill = $('[data-cart-pill]'), main = $('[data-main]');
    const inr = n => '₹' + Math.round(n).toLocaleString('en-IN');
    const el = (tag, cls, text) => { const e = document.createElement(tag); if (cls) e.className = cls; if (text != null) e.textContent = text; return e; };
    const loadCart = group => { try { return JSON.parse(sessionStorage.getItem('fin.cart.' + group)) || []; } catch (e) { return []; } };
    const saveCart = (group, list) => { try { sessionStorage.setItem('fin.cart.' + group, JSON.stringify(list)); } catch (e) { /* storage unavailable */ } };

    if (cart) {
        const group = cart.dataset.group;
        let items = loadCart(group), hidden = false;
        const expanded = {};   // vendor index -> group row open
        const boxes = () => $$('.sel-chk[data-group="' + group + '"]');

        const itemOf = c => ({ id: c.value, vi: c.dataset.vi, vid: c.dataset.vid, legal: c.dataset.legal, vname: c.dataset.sub, tid: c.dataset.title, mbk: c.dataset.mbk, svc: c.dataset.svc, meta: c.dataset.meta, amt: +c.dataset.amt || 0 });
        function remove(ids) {
            const gone = new Set(ids);
            items = items.filter(i => !gone.has(i.id));
            boxes().forEach(c => { if (gone.has(c.value)) c.checked = false; });
            saveCart(group, items);
            render();
        }
        function render() {
            const n = items.length, vendors = [...new Set(items.map(i => i.vi))], tickets = new Set(items.map(i => i.tid)).size;
            const visible = n > 0 && !hidden;
            cart.classList.toggle('is-empty', n === 0);
            cart.classList.toggle('is-hidden', hidden);
            pill.classList.toggle('is-on', n > 0 && hidden);
            $('[data-cart-pill-n]', pill).textContent = n;
            // desktop: the rail steps aside while the cart is open; compact: leave room above the bottom sheet
            html.classList.toggle('rail-min', visible && desktop.matches);
            html.classList.toggle('cart-open', visible && !compact.matches);   // makes room for the full-height side panel
            main.style.paddingBottom = visible && compact.matches ? '300px' : '';
            if (n === 0) return;
            $('[data-cart-sub]', cart).textContent = n + ' service(s) · ' + tickets + ' ticket(s) · ' + vendors.length + ' vendor(s)';
            $('[data-cart-total-label]', cart).textContent = cart.dataset.totalLabel;
            $('[data-cart-total]', cart).textContent = inr(items.reduce((a, i) => a + i.amt, 0));
            $('[data-cart-go]', cart).textContent = cart.dataset.label + ' (' + n + ')';
            $('[data-cart-list]', cart).replaceChildren(...vendors.flatMap(vi => {
                const mine = items.filter(i => i.vi === vi), open = !!expanded[vi];
                const row = el('div', 'cart-grp'), name = el('div', 'cart-grp-name', mine[0].vname), x = el('button', 'cart-x', '✕');
                const main1 = el('div'), meta = el('div', 'cart-grp-meta', new Set(mine.map(i => i.tid)).size + ' ticket(s) · ' + mine.length + ' service(s)');
                name.append(el('span', 'cart-grp-id', mine[0].vid));
                main1.append(name, ...(mine[0].legal ? [el('div', 'cart-grp-meta', mine[0].legal)] : []), meta);
                x.type = 'button'; x.setAttribute('aria-label', 'Remove all of this vendor');
                x.addEventListener('click', e => { e.stopPropagation(); remove(mine.map(i => i.id)); });
                row.append(el('span', 'cart-chev', open ? '▼' : '▶'), main1, el('span', 'cart-grp-amt', inr(mine.reduce((a, i) => a + i.amt, 0))), x);
                row.addEventListener('click', () => { expanded[vi] = !open; render(); });
                return [row, ...(open ? mine.map(i => {
                    const it = el('div', 'cart-item'), t = el('div', 'cart-item-t', i.tid), rm = el('button', 'cart-x', '✕');
                    t.append(el('span', 'cart-item-mbk', i.mbk));
                    rm.type = 'button'; rm.setAttribute('aria-label', 'Remove ' + i.tid);
                    rm.addEventListener('click', () => remove([i.id]));
                    it.append(t, el('span', 'cart-item-amt', inr(i.amt)), rm, el('div', 'cart-item-svc', i.svc), el('div', 'cart-item-meta', i.meta));
                    return it;
                }) : [])];
            }));
        }
        function openSelection() {
            if (items.length) openPopup(cart.dataset.popType + '&ids=' + encodeURIComponent(items.map(i => i.id).join(',')));
        }

        document.addEventListener('change', e => {
            const c = e.target;
            if (!c.matches('.sel-chk') || c.dataset.group !== group) return;
            items = items.filter(i => i.id !== c.value);
            if (c.checked) items.push(itemOf(c));
            saveCart(group, items);
            render();
        });
        $('[data-cart-go]', cart).addEventListener('click', openSelection);
        $('[data-cart-clear]', cart).addEventListener('click', () => remove(items.map(i => i.id)));
        $('[data-cart-hide]', cart).addEventListener('click', () => { hidden = true; render(); });
        pill.addEventListener('click', () => { hidden = false; render(); });
        compact.addEventListener('change', render);
        desktop.addEventListener('change', render);

        // restore the ticks of what is already in the cart
        const ids = new Set(items.map(i => i.id));
        boxes().forEach(c => { if (ids.has(c.value)) c.checked = true; });
        render();
    }

    // Vendor Payable has no cart: ticking services shows a bulk "Raise Bill (n)" button in the toolbar.
    const selBtn = $('[data-sel-btn]');
    if (selBtn) {
        const ticked = () => [...new Set($$('.sel-chk[data-group="' + selBtn.dataset.group + '"]:checked').map(c => c.value))];
        const syncBulk = () => {
            const n = ticked().length;
            selBtn.hidden = n === 0;
            selBtn.textContent = selBtn.dataset.label + ' (' + n + ')';
        };
        document.addEventListener('change', e => { if (e.target.matches('.sel-chk')) syncBulk(); });
        selBtn.addEventListener('click', () => {
            const ids = ticked();
            if (ids.length) openPopup(selBtn.dataset.popType + '&ids=' + encodeURIComponent(ids.join(',')));
        });
        syncBulk();
    }

    // Advance Report: Bulk Payment. Start shows the tick column (advances without payment details); Proceed opens the pop-up for the ticked ones.
    const bulkStart = $('[data-bulk-start]');
    if (bulkStart) {
        const grid = $('[data-grid]'), go = $('[data-bulk-go]'), cancel = $('[data-bulk-cancel]');
        const group = bulkStart.dataset.group, boxes = () => $$('.sel-chk[data-group="' + group + '"]'), ticked = () => boxes().filter(c => c.checked).map(c => c.value);
        const sync = on => {
            grid.classList.toggle('is-bulk', on);
            $$('[data-bulk-off]').forEach(b => { b.hidden = on; });
            go.hidden = cancel.hidden = !on;
            go.textContent = 'Proceed (' + ticked().length + ')';
        };
        bulkStart.addEventListener('click', () => { sync(true); if (bulkStart.dataset.expand) $$('tr.top').forEach(r => setOpen(r, true)); syncExpandAll(); });
        cancel.addEventListener('click', () => { boxes().forEach(c => { c.checked = false; }); sync(false); });
        document.addEventListener('change', e => { if (e.target.matches('.sel-chk[data-group="' + group + '"]')) sync(true); });
        go.addEventListener('click', () => {
            const ids = ticked();
            if (!ids.length) { showToast('Select at least one entry to proceed.'); return; }
            openPopup(go.dataset.popType + '&sids=' + encodeURIComponent(ids.join(',')));
        });
    }

    /* ---------- module launcher (nine-dot button after the bell) ---------- */
    // Placeholder: choosing a module only selects it, and a quick action only shows a toast (no routes yet).
    const launcher = $('[data-launcher]');
    if (launcher) {
        const btn = $('[data-launch-btn]', launcher);
        const MODS = [
            { k: 'contracts', l: 'Contracts', t: 'blue', i: 'M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8zM14 3v5h5M9 13h6M9 17h4', a: [['New contract', 'New'], ['Go to dashboard', 'Dashboard'], ['Expiring soon', 'Expiring']] },
            { k: 'insights', l: 'Insights', t: 'purple', i: 'M4 20V10M10 20V4M16 20v-7M22 20H2', a: [['Go to dashboard', 'Dashboard'], ['Create report', 'Report'], ['Saved reports', 'Saved']] },
            { k: 'products', l: 'Products', t: 'orange', i: 'M21 8 12 3 3 8v8l9 5 9-5zM3 8l9 5 9-5M12 13v8', a: [['Add product', 'Add'], ['Go to dashboard', 'Dashboard'], ['Price list', 'Prices']] },
            { k: 'ems', l: 'EMS', t: 'green', i: 'M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z', a: [['Quick query', 'Query'], ['Go to dashboard', 'Dashboard'], ['Go to follow-ups', 'Follow-ups']] },
            { k: 'quotation', l: 'Quotation (MBK)', t: 'amber', i: 'M9 11l3 3 8-8M20 12v7a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h9', a: [['New quotation', 'New quote'], ['Go to dashboard', 'Dashboard'], ['Pending quotations', 'Pending']] },
            { k: 'sales', l: 'Sales', t: 'slate', i: 'M3 17l6-6 4 4 8-8M15 7h6v6', a: [['New sale', 'New sale'], ['Go to dashboard', 'Dashboard'], ['Open pipeline', 'Pipeline']] },
            { k: 'operations', l: 'Operations', t: 'blue', i: 'M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8zM12 2v3M12 19v3M2 12h3M19 12h3M5 5l2 2M17 17l2 2M19 5l-2 2M7 17l-2 2', a: [['Go to dashboard', 'Dashboard'], ["Today's schedule", 'Schedule'], ['Log an issue', 'Issue']] },
            { k: 'finance', l: 'Finance', t: 'green', i: 'M12 2v20M17 6H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6', a: [['Go to dashboard', 'Dashboard'], ['New invoice', 'Invoice'], ['Outstanding payments', 'Payments']] },
            { k: 'marketing', l: 'Marketing', t: 'orange', i: 'M3 11v2a2 2 0 0 0 2 2h2l5 4V5L7 9H5a2 2 0 0 0-2 2zM16 8a5 5 0 0 1 0 8', a: [['Go to dashboard', 'Dashboard'], ['New campaign', 'Campaign'], ['Campaign calendar', 'Calendar']] },
            { k: 'admin', l: 'Admin', t: 'slate', i: 'M12 3l8 3v6c0 5-3.5 8-8 9-4.5-1-8-4-8-9V6z', a: [['Manage users', 'Users'], ['System settings', 'Settings'], ['Audit log', 'Audit']] },
            { k: 'approvals', l: 'Approvals', t: 'green', i: 'M20 6 9 17l-5-5', a: [['Pending approvals', 'Pending'], ['Go to dashboard', 'Dashboard'], ['My requests', 'Requests']] },
            { k: 'access', l: 'Access mgmt.', t: 'purple', i: 'M15 7a4 4 0 1 1-3.5 6L3 21v-3h3v-3h3l2.5-2.5A4 4 0 0 1 15 7z', a: [['Manage roles', 'Roles'], ['Grant access', 'Grant'], ['Access log', 'Log']] },
        ];
        const svg = (d, s) => {
            const ns = 'http://www.w3.org/2000/svg', e = document.createElementNS(ns, 'svg'), p = document.createElementNS(ns, 'path');
            e.setAttribute('width', s); e.setAttribute('height', s); e.setAttribute('viewBox', '0 0 24 24'); e.setAttribute('fill', 'none'); e.setAttribute('stroke', 'currentColor');
            e.setAttribute('stroke-width', '2'); e.setAttribute('stroke-linecap', 'round'); e.setAttribute('stroke-linejoin', 'round'); e.setAttribute('aria-hidden', 'true');
            p.setAttribute('d', d); e.append(p);
            return e;
        };
        let sel = 'finance', panel = null, backdrop = null;
        const close = () => {
            if (panel) panel.remove();
            if (backdrop) backdrop.remove();
            panel = backdrop = null;
            btn.setAttribute('aria-expanded', 'false'); btn.classList.remove('open');
        };
        function fill() {
            const q = $('input', panel).value.trim().toLowerCase(), grid = $('.launch-grid', panel), empty = $('.launch-empty', panel), act = $('.launch-actions', panel);
            const shown = MODS.filter(m => m.l.toLowerCase().includes(q));
            grid.replaceChildren(...shown.map(m => {
                const b = el('button', 'launch-tile' + (m.k === sel ? ' on' : '')), ic = el('span', 'launch-ic'), lab = el('span', 'launch-lab', m.l);
                b.type = 'button'; b.setAttribute('aria-pressed', String(m.k === sel));
                ic.style.background = 'var(--tint-' + m.t + ')'; ic.style.color = 'var(--tint-' + m.t + '-ink)'; ic.append(svg(m.i, 18));
                b.append(ic, lab);
                b.addEventListener('click', () => { sel = m.k; fill(); });
                return b;
            }));
            empty.hidden = shown.length > 0;
            const m = MODS.find(x => x.k === sel);
            $('.launch-qh', panel).textContent = 'QUICK ACTIONS · ' + m.l;
            act.replaceChildren(...m.a.map(([full, short]) => {
                const b = el('button', 'launch-pill');
                b.type = 'button'; b.title = full;
                b.append(svg('M5 12h14M13 6l6 6-6 6', 16), el('span', '', short));
                b.addEventListener('click', () => showToast(full + ' — opens in the ' + m.l + ' module.'));
                return b;
            }));
        }
        function open() {
            backdrop = el('div', 'launch-backdrop'); panel = el('div', 'launch-panel');
            panel.setAttribute('role', 'dialog'); panel.setAttribute('aria-label', 'Module launcher');
            const sw = el('label', 'launch-search'), inp = el('input');
            inp.placeholder = 'Find a module'; inp.setAttribute('aria-label', 'Find a module');
            sw.append(svg('M11 4a7 7 0 1 0 0 14 7 7 0 0 0 0-14zM20 20l-3.5-3.5', 16), inp);
            inp.addEventListener('input', fill);
            const empty = el('div', 'launch-empty', 'No module matches that search.'); empty.hidden = true;
            panel.append(sw, el('div', 'launch-grid'), empty, el('div', 'launch-rule'), el('div', 'launch-qh'), el('div', 'launch-actions'));
            backdrop.addEventListener('click', close);
            launcher.append(backdrop, panel);
            btn.setAttribute('aria-expanded', 'true'); btn.classList.add('open');
            fill(); inp.focus();
        }
        btn.addEventListener('click', () => (panel ? close() : open()));
        document.addEventListener('keydown', e => { if (e.key === 'Escape' && panel) close(); });
    }

    /* ---------- toasts ---------- */
    const toast = $('#toast');
    let toastTimer;
    function showToast(msg) {
        if (!toast) return;
        if (msg) $('[data-toast-text]', toast).textContent = msg;
        toast.hidden = false;
        clearTimeout(toastTimer);
        toastTimer = setTimeout(() => { toast.hidden = true; }, 3000);
    }
    if (toast && !toast.hidden) showToast();

    /* ---------- tooltips ---------- */
    const tip = $('#tip');
    function showTip(el) {
        let lines;
        try { lines = JSON.parse(el.dataset.tip); } catch (e) { return; }
        tip.replaceChildren(...lines.map(l => {
            const d = document.createElement('div');
            d.className = 'tip-line' + (l.b ? ' b' : '') + (l.wide ? ' wide' : '') + (l.rule ? ' rule' : '') + (l.c ? ' c-' + l.c : '');
            const a = document.createElement('span'); a.textContent = l.l;
            d.append(a);
            if (l.v) { const b = document.createElement('span'); b.textContent = l.v; d.append(b); }
            return d;
        }));
        const r = el.getBoundingClientRect();
        tip.style.left = (r.left + r.width / 2) + 'px';
        const below = el.hasAttribute('data-tip-below');
        tip.classList.toggle('below', below);
        tip.style.top = below ? (r.bottom + 8) + 'px' : (r.top - 8) + 'px';
        tip.hidden = false;
    }
    const hideTip = () => { tip.hidden = true; };
    document.addEventListener('mouseover', e => { const el = e.target.closest('[data-tip]'); if (el) showTip(el); });
    document.addEventListener('mouseout', e => { const el = e.target.closest('[data-tip]'); if (el && !el.contains(e.relatedTarget)) hideTip(); });
    document.addEventListener('focusin', e => { if (e.target.matches('[data-tip]')) showTip(e.target); });
    document.addEventListener('focusout', e => { if (e.target.matches('[data-tip]')) hideTip(); });
    document.addEventListener('scroll', hideTip, true);

    /* ---------- popups ---------- */
    const root = $('#modal-root');
    let lastFocus = null;

    // Stacked tables (phone): each cell is labelled with its column heading.
    function labelStacked(scope) {
        $$('table.stack-tbl', scope).forEach(t => {
            const heads = [...(t.tHead ? t.tHead.rows[0].cells : [])].map(th => th.textContent.trim());
            [...t.tBodies].forEach(b => [...b.rows].forEach(r => [...r.cells].forEach((td, i) => { if (heads[i]) td.dataset.label = heads[i]; })));
        });
    }
    // Printable sheets keep their A4 width; on a narrow screen they shrink to fit instead of scrolling sideways.
    function fitDocs(scope) {
        $$('.doc', scope).forEach(d => {
            d.style.zoom = '';
            const frame = d.closest('.doc-frame, .sheet-scroll'), room = frame ? frame.clientWidth - 24 : 0;
            if (room > 0 && d.offsetWidth > room) d.style.zoom = (room / d.offsetWidth).toFixed(3);
        });
    }
    labelStacked(document);
    fitDocs(document);
    window.addEventListener('resize', () => fitDocs(document));
    window.addEventListener('beforeprint', () => $$('.doc').forEach(d => { d.dataset.z = d.style.zoom; d.style.zoom = ''; }));
    window.addEventListener('afterprint', () => $$('.doc').forEach(d => { d.style.zoom = d.dataset.z || ''; }));

    async function openPopup(url) {
        hideTip();
        lastFocus = document.activeElement;
        root.innerHTML = '<div class="dialog-loading">Loading…</div>';
        root.hidden = false;
        try {
            const res = await fetch(url, { headers: { 'X-Requested-With': 'fetch' } });
            // Session expired: log in again and come back to this page.
            if (res.status === 401) { location.href = '/Account/Login?expired=true&returnUrl=' + encodeURIComponent(location.pathname + location.search); return; }
            if (!res.ok) throw new Error(res.status);
            root.innerHTML = await res.text();
        } catch (e) {
            closePopup();
            showToast('Could not open this view. Please refresh and try again.');
            return;
        }
        labelStacked(root);
        fitDocs(root);
        $$('[data-return]', root).forEach(i => { i.value = returnUrl(); });
        wireForm($('form.js-validate', root));
        const first = $('input:not([type=hidden]), select, button:not([data-close])', root) || $('[data-close]', root);
        if (first) first.focus();
    }
    function closePopup() {
        root.hidden = true;
        root.innerHTML = '';
        if (lastFocus && lastFocus.focus) lastFocus.focus();
    }
    document.addEventListener('click', e => {
        const pop = e.target.closest('[data-pop]');
        if (pop && !pop.disabled) { e.stopPropagation(); openPopup(pop.dataset.pop); return; }
        const t = e.target.closest('[data-toast]');
        if (t) { showToast(t.dataset.toast); return; }
        if (e.target.closest('[data-close]') || e.target === root) closePopup();
    });
    document.addEventListener('keydown', e => { if (e.key === 'Escape' && !root.hidden) closePopup(); });

    function wireForm(form) {
        if (!form) return;
        const inr = n => '₹' + Math.round(n).toLocaleString('en-IN');
        // data-req: always required. data-req-mode / data-req-not-mode: required only when the
        // Payment Mode is (or is not) one of the listed values; the field's * follows suit.
        const isRequired = i => {
            const mode = form.elements.mode ? form.elements.mode.value : '';
            if (i.dataset.reqMode) return i.dataset.reqMode.split(',').includes(mode);
            if (i.dataset.reqNotMode) return !i.dataset.reqNotMode.split(',').includes(mode);
            return true;
        };
        const validate = () => {
            const fields = $$('[data-req], [data-req-mode], [data-req-not-mode]', form);
            fields.filter(i => i.dataset.reqMode || i.dataset.reqNotMode).forEach(i => {
                const req = isRequired(i), star = $('.req', i.closest('.fld'));
                if (star) star.hidden = !req;
                i.setAttribute('aria-required', String(req));
            });
            let ok = fields.filter(isRequired).every(i => i.type === 'file' ? i.files.length > 0 : i.value.trim() !== '');
            // Update UTR (Bulk Payment): at least one UTR must be entered; the button shows how many.
            const utrs = $$('[data-utr-in]', form);
            if (utrs.length) {
                const n = utrs.filter(i => i.value.trim() !== '').length;
                ok = ok && n > 0;
                $$('[data-needs-utr]', form).forEach(b => { b.textContent = 'Save UTR (' + n + ')'; });
            }
            $$('[data-needs-valid]', form).forEach(b => { b.disabled = !ok; });
            // Upload Bill: an invoice above the vendor payable shows the mismatch reason, blocks Submit (for a new upload) and allows a dispute.
            const blocks = $$('[data-svc-block]', form);
            if (blocks.length) {
                const st = blocks.map(b => {
                    const inv = parseFloat($('[data-inv]', b).value), over = inv > +b.dataset.payable, file = $('[data-main] input[type=file]', b).files.length > 0;
                    $('[data-mismatch]', b).hidden = !over;
                    return { over, file };
                });
                $$('[data-needs-valid]', form).forEach(b => { b.disabled = !ok || (b.dataset.blockOver === '1' && st.some(s => s.over)); });
                $$('[data-needs-dispute]', form).forEach(b => { b.disabled = !st.every(s => s.over && s.file); });
                $$('[data-needs-dispute-svc]', form).forEach(b => { const s = st[+b.dataset.needsDisputeSvc]; b.disabled = !(s.over && s.file); });
            }
        };
        // Upload opens the (hidden) file chooser of its own document row.
        form.addEventListener('click', e => {
            const up = e.target.closest('[data-upload-btn]');
            if (up) $('input[type=file]', up.parentElement).click();
        });
        // Scan: with no file chosen, fetch a scanned document from the scanner service on this PC (a simulated page when it cannot be reached);
        // with a file chosen, scan it. Scanning the main bill reads the amount (simulated: the vendor payable) into Amount as per Invoice.
        form.addEventListener('click', async e => {
            const btn = e.target.closest('[data-scan]');
            if (!btn) return;
            const row = btn.closest('[data-doc]') || btn.parentElement, input = $('input[type=file]', row), status = $('[data-scan-status]', row);
            let simulated = false;
            btn.disabled = true; btn.textContent = 'Scanning…';
            if (!input.files.length) {
                let blob = null;
                if (btn.dataset.scanUrl) {
                    try {
                        const res = await fetch(btn.dataset.scanUrl, { method: 'POST' });
                        if (!res.ok) throw new Error(res.status);
                        blob = await res.blob();
                    } catch (err) { blob = null; }
                }
                if (!blob) { blob = new Blob(['simulated scan'], { type: 'application/pdf' }); simulated = true; }
                const ext = blob.type.includes('pdf') ? 'pdf' : blob.type.includes('png') ? 'png' : 'jpg';
                const dt = new DataTransfer();
                dt.items.add(new File([blob], 'scan-' + Date.now() + '.' + ext, { type: blob.type }));
                input.files = dt.files;
                input.dispatchEvent(new Event('change', { bubbles: true }));
            }
            let msg = 'Scanned ✓';
            if (row.hasAttribute('data-main')) {
                const inv = $('[data-inv]', row.closest('[data-svc-block]'));
                if (inv) { inv.value = row.dataset.pay; inv.dispatchEvent(new Event('input', { bubbles: true })); }
                msg += ' · amount read ' + inr(+row.dataset.pay);
            }
            if (simulated) msg += ' (simulated — scanner not reachable)';
            row.dataset.scanned = '1';
            if (status) { status.textContent = msg; status.classList.add('ok'); }
            btn.disabled = false; btn.textContent = 'Scan';
        });
        form.addEventListener('input', validate);
        form.addEventListener('change', e => {
            const el = e.target;
            if (el.matches('[data-file]')) {
                // choosing (or clearing) a file resets its scan
                const doc = el.closest('[data-doc]'), status = $('[data-scan-status]', doc);
                doc.dataset.scanned = '';
                status.classList.remove('ok');
                status.textContent = el.files.length ? el.files[0].name + ' · Ready to scan' : 'No file chosen';
            }
            if (el.matches('[data-ctype]')) {
                const lbl = $('[data-no-label]', form);
                if (lbl) lbl.textContent = el.value === 'E-Cheque' ? 'Request No.' : 'Cheque No.';
            }
            validate();
        });
        const calc = $('[data-tds-calc]', form);
        if (calc) {
            const amt = +calc.dataset.amt, pct = $('[data-pct]', calc);
            pct.addEventListener('input', () => {
                const p = parseFloat(pct.value);
                const tds = isNaN(p) ? null : Math.round(amt * p / 100);
                $('[data-tds]', calc).textContent = tds == null ? '-' : inr(tds);
                $('[data-net]', calc).textContent = tds == null ? '-' : inr(amt - tds);
                validate();
            });
        }
        // Bulk Payment: each row's TDS and net follow its TDS %, and the strip on top follows the rows.
        const bulkRows = $$('[data-bulk-row]', form);
        if (bulkRows.length) {
            const recalc = () => {
                let adv = 0, tds = 0;
                bulkRows.forEach(r => {
                    const amt = +r.dataset.amt, p = parseFloat($('[data-pct-row]', r).value), t = isNaN(p) ? 0 : Math.round(amt * p / 100);
                    $('[data-row-tds]', r).textContent = isNaN(p) ? '-' : inr(t);
                    $('[data-row-net]', r).textContent = isNaN(p) ? '-' : inr(amt - t);
                    adv += amt; tds += t;
                });
                $('[data-tot-tds]', form).textContent = inr(tds);
                $('[data-tot-net]', form).textContent = inr(adv - tds);
            };
            form.addEventListener('input', e => { if (e.target.matches('[data-pct-row]')) recalc(); });
            recalc();
        }
        form.addEventListener('submit', () => {
            // A Raise Bill / Submit that came from the cart takes its services out of the stored cart.
            const m = /clear-cart-(\w+)/.exec(form.className);
            if (m) {
                const used = [...$$('input[name=ids]', form).flatMap(i => i.value.split(',')), ...$$('input[name$=".Sid"]', form).map(i => i.value)];
                saveCart(m[1], loadCart(m[1]).filter(i => !used.includes(i.id)));
            }
            $$('button[type=submit]', form).forEach(b => { setTimeout(() => { b.disabled = true; }, 0); });
        });
        validate();
    }

    syncExpandAll();

    // A pop-up the server asked for (e.g. "RTGS Advice generated" right after creating an advice).
    const auto = $('#auto-popup');
    if (auto) openPopup(auto.dataset.url);
})();
