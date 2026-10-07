// Southern Travels Finance — progressive enhancement for the server-rendered reports.
(function () {
    'use strict';

    const $ = (sel, root) => (root || document).querySelector(sel);
    const $$ = (sel, root) => Array.from((root || document).querySelectorAll(sel));
    const returnUrl = () => location.pathname + location.search;

    /* ---------- sidebar ---------- */
    $$('[data-side-toggle]').forEach(b => b.addEventListener('click', () => {
        const closed = document.documentElement.classList.toggle('side-closed');
        try { localStorage.setItem('fin.side', closed ? 'closed' : 'open'); } catch (e) { /* storage unavailable */ }
    }));

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

    /* ---------- filter panel ---------- */
    $$('[data-filter-toggle]').forEach(b => b.addEventListener('click', () => {
        const panel = $('[data-filters]'), fo = $('[data-fo]');
        const open = panel.hidden;
        panel.hidden = !open;
        fo.value = open ? '1' : '';
        b.classList.toggle('btn-primary', open);
        b.setAttribute('aria-expanded', String(open));
    }));

    /* ---------- expandable rows ---------- */
    const seenForm = $('#seen-form');
    function setOpen(tr, open) {
        const detail = tr.nextElementSibling;
        tr.classList.toggle('open', open);
        tr.setAttribute('aria-expanded', String(open));
        if (detail && detail.classList.contains('detail')) detail.hidden = !open;
        const seen = open && $('[data-seen]', tr);
        if (seen) {
            // Payment Request: clear the "new tickets" dot once viewed.
            const body = new FormData(seenForm);
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

    /* ---------- bulk selection (Raise Bill / Submit) ---------- */
    const selBtn = $('[data-sel-btn]');
    function selected() {
        return selBtn ? $$('.sel-chk[data-group="' + selBtn.dataset.group + '"]:checked').map(c => c.value) : [];
    }
    function syncSel() {
        if (!selBtn) return;
        const ids = [...new Set(selected())];
        selBtn.hidden = ids.length === 0;
        selBtn.textContent = selBtn.dataset.label + ' (' + ids.length + ')';
    }
    document.addEventListener('change', e => { if (e.target.matches('.sel-chk')) syncSel(); });
    if (selBtn) selBtn.addEventListener('click', () => {
        const ids = [...new Set(selected())];
        if (ids.length) openPopup('/Finance/Popup?t=' + selBtn.dataset.popType + '&ids=' + encodeURIComponent(ids.join(',')));
    });

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
            const ok = fields.filter(isRequired).every(i => i.type === 'file' ? i.files.length > 0 : i.value.trim() !== '');
            $$('[data-needs-valid]', form).forEach(b => { b.disabled = !ok; });
        };
        form.addEventListener('input', validate);
        form.addEventListener('change', e => {
            const el = e.target;
            if (el.matches('[data-file]')) {
                const fld = el.closest('.fld'), stamp = fld && fld.nextElementSibling && $('[data-file-stamp]', fld.nextElementSibling);
                $('[data-file-text]', fld).textContent = el.files.length ? el.files[0].name + ' selected' : '';
                if (stamp) stamp.textContent = el.files.length ? 'Will be set on submit' : '-';
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
        form.addEventListener('submit', () => { $$('button[type=submit]', form).forEach(b => { setTimeout(() => { b.disabled = true; }, 0); }); });
        validate();
    }

    syncExpandAll();
    syncSel();
})();
