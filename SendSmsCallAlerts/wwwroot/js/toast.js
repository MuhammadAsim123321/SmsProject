/* ── Toast notifications ── */
(function () {
    function getContainer() {
        var c = document.querySelector('.toast-container');
        if (!c) {
            c = document.createElement('div');
            c.className = 'toast-container';
            document.body.appendChild(c);
        }
        return c;
    }

    function createToast(text, type) {
        var container = getContainer();
        var toast = document.createElement('div');
        toast.className = 'toast toast--' + type;
        toast.setAttribute('role', 'alert');
        toast.setAttribute('aria-live', 'polite');

        var iconSvg = type === 'success'
            ? '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path><polyline points="22 4 12 14.01 9 11.01"></polyline></svg>'
            : '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="8" x2="12" y2="12"></line><line x1="12" y1="16" x2="12.01" y2="16"></line></svg>';

        var closeSvg = '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M18 6 6 18"></path><path d="m6 6 12 12"></path></svg>';

        var progressHtml = type === 'success' ? '<div class="toast__progress" aria-hidden="true"></div>' : '';

        toast.innerHTML =
            '<div class="toast__icon">' + iconSvg + '</div>' +
            '<span class="toast__text">' + text + '</span>' +
            '<button class="toast__close" type="button" aria-label="Close">' + closeSvg + '</button>' +
            progressHtml;

        container.appendChild(toast);

        var closeBtn = toast.querySelector('.toast__close');
        function removeToast() {
            toast.classList.add('toast--exiting');
            setTimeout(function () {
                if (toast.parentNode) toast.parentNode.removeChild(toast);
            }, 300);
        }
        closeBtn.addEventListener('click', function (e) {
            e.stopPropagation();
            removeToast();
        });

        if (type === 'success') {
            setTimeout(removeToast, 10000);
        }
    }

    // Expose globally for inline JS callers (e.g. AJAX CRUD responses)
    window.showToast = function (text, type) {
        createToast(text, type || 'success');
    };

    // Global action confirmation: replaces native confirm() with on-screen modal
    var _pendingActionForm = null;
    window.confirmAction = function (event, title, message, form) {
        event.preventDefault();
        _pendingActionForm = form;
        var titleEl = document.getElementById('actionConfirmTitle');
        var msgEl = document.getElementById('actionConfirmMessage');
        if (titleEl) titleEl.textContent = title;
        if (msgEl) msgEl.textContent = message;
        if (window.jQuery) {
            jQuery('#actionConfirmModal').modal('show');
        }
        return false;
    };

    window.executeAction = function () {
        if (window.jQuery) {
            jQuery('#actionConfirmModal').modal('hide');
        }
        if (_pendingActionForm) {
            _pendingActionForm.submit();
            _pendingActionForm = null;
        }
    };

    // Backwards-compatible alias
    window.confirmDelete = function (event, message, form) {
        return confirmAction(event, 'Confirm Delete', message, form);
    };

    // Initialize server-rendered toasts from TempData
    var container = document.querySelector('.toast-container');
    if (container) {
        var toasts = container.querySelectorAll('.toast');
        toasts.forEach(function (toast) {
            var isSuccess = toast.classList.contains('toast--success');
            var closeBtn = toast.querySelector('.toast__close');

            function removeToast() {
                toast.classList.add('toast--exiting');
                setTimeout(function () {
                    if (toast.parentNode) toast.parentNode.removeChild(toast);
                }, 300);
            }

            if (closeBtn) {
                closeBtn.addEventListener('click', function (e) {
                    e.stopPropagation();
                    removeToast();
                });
            }

            if (isSuccess) {
                setTimeout(removeToast, 10000);
            }
        });
    }
})();
