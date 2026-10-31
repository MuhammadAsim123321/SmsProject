(function (window, document) {
    var HIDDEN_INPUT_TYPE = 'password';
    var closedEyeIcon = '<svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true" focusable="false"><path d="M2.5 12s3.5-6 9.5-6 9.5 6 9.5 6-3.5 6-9.5 6-9.5-6-9.5-6Z" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"></path><path d="M4 4 20 20" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round"></path></svg>';
    var openEyeIcon = '<svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true" focusable="false"><path d="M2.5 12s3.5-6 9.5-6 9.5 6 9.5 6-3.5 6-9.5 6-9.5-6-9.5-6Z" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"></path><circle cx="12" cy="12" r="2.5" fill="none" stroke="currentColor" stroke-width="1.7"></circle></svg>';

    function updateButton(input, button) {
        var hidden = input.type === HIDDEN_INPUT_TYPE;
        button.setAttribute('aria-label', hidden ? 'Show password' : 'Hide password');
        button.setAttribute('aria-pressed', String(!hidden));
        button.innerHTML = hidden ? closedEyeIcon : openEyeIcon;
    }

    function toggle(input, button) {
        var start = input.selectionStart;
        var end = input.selectionEnd;
        input.type = input.type === HIDDEN_INPUT_TYPE ? 'text' : HIDDEN_INPUT_TYPE;
        updateButton(input, button);
        if (start !== null && end !== null) {
            try { input.setSelectionRange(start, end); } catch (e) { }
        }
        input.focus();
    }

    function enhance(root) {
        var scope = root || document;
        scope.querySelectorAll('input[type="password"]:not([data-no-toggle]), input[type="text"][data-password-toggle]').forEach(function (input) {
            if (input.parentElement && input.parentElement.classList.contains('password-toggle-wrap')) {
                updateButton(input, input.parentElement.querySelector('.password-toggle-button'));
                return;
            }
            var wrapper = document.createElement('span');
            wrapper.className = 'password-toggle-wrap';
            input.parentNode.insertBefore(wrapper, input);
            wrapper.appendChild(input);
            var button = document.createElement('button');
            button.type = 'button';
            button.className = 'password-toggle-button';
            button.addEventListener('click', function () { toggle(input, button); });
            wrapper.appendChild(button);
            updateButton(input, button);
        });
    }

    window.initPasswordToggles = enhance;
    document.addEventListener('DOMContentLoaded', function () { enhance(document); });
})(window, document);