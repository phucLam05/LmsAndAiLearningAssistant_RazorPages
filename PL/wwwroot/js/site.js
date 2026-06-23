/* ===========================================================
 * LMS AI — global client helpers.
 *
 * Public API (window.Lms):
 *   - Lms.confirm(form, message)   : intercept form submit, show SweetAlert
 *   - Lms.notify(message, kind)    : toast notification (success | error | info | warning)
 *   - Lms.copyText(id)             : copy an input/textarea value to clipboard
 * =========================================================== */
(function (global) {
    'use strict';

    function confirmForm(form, message) {
        if (!form) return true;
        if (typeof Swal === 'undefined') {
            return window.confirm(message);
        }
        // Cancel default submission, show Swal, then submit if confirmed.
        form.addEventListener('submit', function handler(e) {
            e.preventDefault();
            form.removeEventListener('submit', handler);
            Swal.fire({
                title: 'Xác nhận',
                text: message,
                icon: 'question',
                showCancelButton: true,
                confirmButtonText: 'Đồng ý',
                cancelButtonText: 'Hủy',
                confirmButtonColor: '#4f46e5',
            }).then((result) => {
                if (result.isConfirmed) {
                    // Re-submit bypassing the listener
                    form.submit();
                } else {
                    // Re-attach for next attempt
                    form.addEventListener('submit', handler);
                }
            });
        }, { once: false });
        return false;
    }

    function notify(message, kind) {
        if (typeof Swal === 'undefined') {
            window.alert(message);
            return;
        }
        const icons = { success: 'success', error: 'error', warning: 'warning', info: 'info' };
        const Toast = Swal.mixin({
            toast: true,
            position: 'top-end',
            showConfirmButton: false,
            timer: 3500,
            timerProgressBar: true,
        });
        Toast.fire({ icon: icons[kind] || 'info', title: message });
    }

    function copyText(id) {
        const el = document.getElementById(id);
        if (!el) return;
        el.select();
        try {
            navigator.clipboard.writeText(el.value);
            notify('Đã copy vào clipboard', 'success');
        } catch {
            document.execCommand('copy');
        }
    }

    // Auto-bind confirm-on-submit for any <form data-confirm="...">
    function bindConfirms() {
        document.querySelectorAll('form[data-confirm]').forEach(form => {
            const msg = form.dataset.confirm;
            form.addEventListener('submit', function (e) {
                e.preventDefault();
                Swal.fire({
                    title: 'Xác nhận',
                    text: msg,
                    icon: 'warning',
                    showCancelButton: true,
                    confirmButtonText: 'Đồng ý',
                    cancelButtonText: 'Hủy',
                    confirmButtonColor: '#ef4444',
                }).then(r => { if (r.isConfirmed) form.submit(); });
            });
        });
    }

    document.addEventListener('DOMContentLoaded', bindConfirms);

    global.Lms = { confirmForm, notify, copyText };
})(window);

// Show success/info banner from TempData if any
document.addEventListener('DOMContentLoaded', function () {
    const banner = document.querySelector('[data-auto-toast]');
    if (banner && window.Lms && window.Lms.notify) {
        window.Lms.notify(banner.dataset.autoToast, banner.dataset.kind || 'info');
    }
});
