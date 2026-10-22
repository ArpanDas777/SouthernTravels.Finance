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

    /* ---------- auto-submitting controls ---------- */
    document.addEventListener('change', e => {
        const el = e.target;
        if (el.matches('[data-autosubmit]') && el.form) el.form.requestSubmit ? el.form.requestSubmit() : el.form.submit();
    });

    $$('[data-period-clear]').forEach(b => b.addEventListener('click', () => {
        const f = $('#period-form');
        f.elements.k.value = 'All time';
        f.submit();
    }));

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

        const itemOf = c => ({ id: c.value, vi: c.dataset.vi, vid: c.dataset.vid, vname: c.dataset.sub, tid: c.dataset.title, mbk: c.dataset.mbk, svc: c.dataset.svc, meta: c.dataset.meta, amt: +c.dataset.amt || 0 });
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
                main1.append(name, meta);
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
        tip.style.top = (r.top - 8) + 'px';
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
            // Create RTGS Advice: at least one ticket must be ticked; the button shows how many.
            const ticks = $$('[data-rtgs-check]', form);
            if (ticks.length) {
                const n = ticks.filter(c => c.checked).length;
                ok = ok && n > 0;
                $$('[data-needs-checked]', form).forEach(b => { b.textContent = 'Submit (' + n + ')'; });
                const nEl = $('[data-rtgs-n]', form), sumEl = $('[data-rtgs-sum]', form);
                if (nEl) nEl.textContent = n;
                if (sumEl) sumEl.textContent = inr(ticks.filter(c => c.checked).reduce((a, c) => a + (+c.dataset.amt || 0), 0));
            }
            $$('[data-needs-valid]', form).forEach(b => { b.disabled = !ok; });
            // Raise Bill: a per-service button only needs that service's own fields.
            $$('[data-needs-svc]', form).forEach(b => {
                b.disabled = !fields.filter(i => i.dataset.svc === b.dataset.needsSvc && isRequired(i))
                    .every(i => i.type === 'file' ? i.files.length > 0 : i.value.trim() !== '');
            });
        };
        // Scan: fetch a scanned document from the scanner service on this PC and drop it into the file field next to the button.
        form.addEventListener('click', async e => {
            const btn = e.target.closest('[data-scan]');
            if (!btn) return;
            const input = $('input[type=file]', btn.parentElement);
            if (!btn.dataset.scanUrl) { showToast('No scanner is configured (Scanner:Url in appsettings.json).'); return; }
            btn.disabled = true; btn.textContent = 'Scanning…';
            try {
                const res = await fetch(btn.dataset.scanUrl, { method: 'POST' });
                if (!res.ok) throw new Error(res.status);
                const blob = await res.blob(), ext = blob.type.includes('pdf') ? 'pdf' : blob.type.includes('png') ? 'png' : 'jpg';
                const dt = new DataTransfer();
                dt.items.add(new File([blob], 'scan-' + Date.now() + '.' + ext, { type: blob.type }));
                input.files = dt.files;
                input.dispatchEvent(new Event('change', { bubbles: true }));
            } catch (err) { showToast('Scanner not reachable — check the scanner service is running.'); }
            btn.disabled = false; btn.textContent = 'Scan';
        });
        form.addEventListener('input', validate);
        form.addEventListener('change', e => {
            const el = e.target;
            if (el.matches('[data-check-all]')) $$('[data-rtgs-check]', form).forEach(c => { c.checked = el.checked; });
            // the select-all box mirrors the rows
            if (el.matches('[data-rtgs-check]')) $('[data-check-all]', form).checked = $$('[data-rtgs-check]', form).every(c => c.checked);
            if (el.matches('[data-file]')) {
const fld = el.closest('.fld'), grp = el.closest('.fields'), stamp = grp && $('[data-file-stamp]', grp);
                $('[data-file-text]', fld).textContent = el.files.length ? el.files[0].name + ' selected' : '';
                if (stamp) stamp.textContent = $$('[data-file]', grp).some(f => f.files.length) ? 'Will be set on submit' : '-';
            }
            if (el.matches('[data-ctype]')) {
                const lbl = $('[data-no-label]', form);
                if (lbl) lbl.textContent = el.value === 'Online' ? 'UTR / Transaction No.' : 'Cheque No.';
            }
            validate();
        });
        const calc = $('[data-tds-calc]', form);
        if (calc) {
            const amt = +calc.dataset.amt, pct = $('[data-pct]', calc);
            // Amount Paid follows the net payable until the user types their own figure.
            const paid = $('[data-amount-auto]', form);
            if (paid) paid.addEventListener('input', () => { paid.dataset.edited = paid.value.trim() !== '' ? '1' : ''; });
            pct.addEventListener('input', () => {
                const p = parseFloat(pct.value);
                const tds = isNaN(p) ? null : Math.round(amt * p / 100);
                $('[data-tds]', calc).textContent = tds == null ? '-' : inr(tds);
                $('[data-net]', calc).textContent = tds == null ? '-' : inr(amt - tds);
                if (paid && !paid.dataset.edited) paid.value = tds == null ? '' : String(amt - tds);
                validate();
            });
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
