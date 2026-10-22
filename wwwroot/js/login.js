/* Login page: Send OTP (AJAX, like STPLCostSheet's sendOTP), 60-second resend timer and OTP check before submitting. */
(function () {
    const $ = s => document.getElementById(s);
    const form = $('login-form'), email = $('email'), send = $('send-otp'), otp = $('otp');
    const otpSection = $('otp-section'), timer = $('otp-timer'), countdown = $('countdown');
    const errorBox = $('login-error'), infoBox = $('login-info'), verify = $('verify');
    let interval = null;

    function show(box, msg) {
        errorBox.hidden = infoBox.hidden = true;
        if (!msg) return;
        box.textContent = msg;
        box.hidden = false;
    }

    function startCountdown() {
        clearInterval(interval);
        let left = 60;
        send.disabled = true;
        timer.hidden = false;
        countdown.textContent = left;
        interval = setInterval(() => {
            countdown.textContent = --left;
            if (left <= 0) {
                clearInterval(interval);
                timer.hidden = true;
                send.disabled = false;
                send.textContent = 'Resend OTP';
            }
        }, 1000);
    }

    send.addEventListener('click', async () => {
        const value = email.value.trim();
        if (!value || !email.checkValidity()) { show(errorBox, 'Please enter a valid email address.'); email.focus(); return; }
        send.disabled = true;
        send.textContent = 'Sending…';
        try {
            const body = new FormData();
            body.append('EmailId', value);
            body.append('__RequestVerificationToken', form.querySelector('input[name="__RequestVerificationToken"]').value);
            const res = await fetch(send.dataset.sendUrl, { method: 'POST', body, headers: { 'X-Requested-With': 'fetch' } });
            if (res.status === 429) throw new Error('Too many attempts. Please wait a minute and try again.');
            if (!res.ok) throw new Error('Error sending OTP. Please try again.');
            const r = await res.json();
            if (!r.success) throw new Error(r.message || 'Failed to send OTP.');
            show(infoBox, r.message || 'OTP sent to your email.');
            email.readOnly = true;
            otpSection.hidden = false;
            send.textContent = 'Resend OTP';
            otp.focus();
            startCountdown();
        } catch (e) {
            show(errorBox, e.message);
            send.disabled = false;
            send.textContent = 'Send OTP';
        }
    });

    // Enter in the email box sends the OTP instead of submitting the form.
    email.addEventListener('keydown', e => { if (e.key === 'Enter' && otpSection.hidden) { e.preventDefault(); send.click(); } });
    otp.addEventListener('input', () => { otp.value = otp.value.replace(/\D/g, '').slice(0, 6); });

    form.addEventListener('submit', e => {
        if (!/^\d{6}$/.test(otp.value)) {
            e.preventDefault();
            show(errorBox, 'Please enter the 6-digit OTP.');
            otp.focus();
            return;
        }
        verify.disabled = true;
        verify.textContent = 'Verifying…';
    });
})();
