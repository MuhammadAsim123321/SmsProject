(function ($) {
    var page = $('.users-page');
    var urls = {
        create: page.data('create-url'),
        edit: page.data('edit-url'),
        remove: page.data('delete-url'),
        list: page.data('list-url')
    };

    function setLoader(settings, on) {
        $(settings.nTableWrapper).find('.dataTables_processing').each(function () {
            this.style.setProperty('display', on ? 'block' : 'none', 'important');
        });
    }

    $('#tblRPM').on('processing.dt', function (e, settings, processing) { setLoader(settings, processing); });
    $('#tblRPM').on('xhr.dt error.dt', function (e, settings) { setLoader(settings, false); });

    var table = $('#tblRPM').length ? $('#tblRPM').DataTable({
        autoWidth: false,
        sAjaxSource: urls.list,
        sServerMethod: 'POST',
        bServerSide: false,
        bProcessing: true,
        scrollX: true,
        pageLength: 100,
        order: [],
        dom: '<"users-toolbar"<"users-search"f><"users-length"l>>rt<"users-footer"ip>',
        language: {
            emptyTable: 'No record found.',
            processing: '<i class="fa fa-spinner fa-spin fa-3x fa-fw"></i><span class="sr-only">Loading...</span>',
            loadingRecords: ' ',
            search: '',
            searchPlaceholder: 'Search users...',
            lengthMenu: '_MENU_'
        },
        columns: [
            { data: 'name', searchable: true },
            { data: 'username', searchable: true },
            { data: 'email', searchable: true },
            { data: 'phone', searchable: true },
            { data: 'role', searchable: true, render: function (data, type) { if (type !== 'display') return data; var admin = (data || '').toUpperCase() === 'ADMIN'; return '<span class="user-role-badge ' + (admin ? 'user-role-admin' : 'user-role-staff') + '">' + (admin ? 'Admin' : 'Staff') + '</span>'; } },
            { data: 'id', orderable: false, searchable: false, render: function (data, type, row) { return '<button type="button" class="btn btn-sm btn-primary" onclick="EditRecord(' + data + ')"><i class="fa fa-edit"></i> Edit</button> <button type="button" class="btn btn-sm btn-danger" onclick="DeleteRecord(' + data + ', this.dataset.username, this.dataset.name)" data-username="' + $('<div>').text(row.username || '').html() + '" data-name="' + $('<div>').text(row.name || '').html() + '"><i class="fa fa-trash"></i> Delete</button>'; } }
        ],
        initComplete: function (settings) { this.api().columns.adjust(); setLoader(settings, false); },
        drawCallback: function (settings) { setLoader(settings, false); }
    }) : null;

    function showForm(url, title) {
        $('#user-form-title').text(title);
        setSaveButton(false, title);
        $('#user-form-body').html('<div class="users-modal-loader"><i class="fa fa-spinner fa-spin fa-3x fa-fw" aria-hidden="true"></i><span class="sr-only">Loading...</span></div>');
        $('#user-form-modal').modal('show');
        $.get(url).done(function (html) {
            $('#user-form-body').html(html);
            window.initPasswordToggles($('#user-form-body')[0]);
            touchedFields = {};
            $('#phone-duplicate-hint').hide();
            setSaveButton(false, title);
        }).fail(showAjaxError);
    }

    function setSaveButton(busy, title) {
        var label = title && title.indexOf('Edit') === 0 ? 'Update' : 'Save';
        $('#save-user-button').prop('disabled', busy).html(busy
            ? '<i class="fa fa-spinner fa-spin fa-3x fa-fw users-button-spinner" aria-hidden="true"></i><span class="sr-only">Loading...</span>'
            : '<i class="fa fa-save" aria-hidden="true"></i> ' + label);
    }

    function showAjaxError(xhr) {
        var message = xhr.responseJSON && xhr.responseJSON.message ? xhr.responseJSON.message : 'Unable to complete the request.';
        $('#user-form-body').html('<div class="alert alert-danger" role="alert">' + $('<div>').text(message).html() + '</div>');
        setSaveButton(false, $('#user-form-title').text());
    }

    $('#create-user-button').on('click', function (e) { e.preventDefault(); showForm(urls.create, 'Create user'); });
    window.EditRecord = function (id) { showForm(urls.edit + '?id=' + encodeURIComponent(id), 'Edit user'); };
    window.DeleteRecord = function (id, username, name) {
        $('#confirm-delete-user').data('id', id);
        $('#delete-user-name').text((username || '') + (name ? ' - ' + name : ''));
        $('#delete-user-error').hide().empty();
        $('#delete-user-modal').modal('show');
    };
    $('#confirm-delete-user').on('click', function () {
        var button = $(this);
        button.prop('disabled', true).html('<i class="fa fa-spinner fa-spin fa-3x fa-fw users-button-spinner" aria-hidden="true"></i><span class="sr-only">Loading...</span>');
        $.post(urls.remove, {
            id: button.data('id'),
            __RequestVerificationToken: $('#user-form-body input[name="__RequestVerificationToken"]').val() || $('input[name="__RequestVerificationToken"]').first().val()
        }).done(function (response) {
            if (!response.success) { $('#delete-user-error').text(response.message).show(); return; }
            $('#delete-user-modal').modal('hide');
            showToast(response.message, 'success');
            table.ajax.reload(null, false);
        }).fail(function (xhr) {
            var message = xhr.responseJSON && xhr.responseJSON.message ? xhr.responseJSON.message : 'Unable to complete the request.';
            $('#delete-user-error').text(message).show();
        }).always(function () {
            button.prop('disabled', false).html('<i class="fa fa-trash" aria-hidden="true"></i> Delete');
        });
    });

    /* replaced by users-fix 2026-11-17: the prior delegated AJAX submit caused duplicate requests. */
    /*
    $(document).on('submit', '.user-modal-form', function (e) { e.preventDefault(); });
    */
    var userRequestInFlight = false;
    var touchedFields = {};

    function fieldError(name, message) {
        var input = $('.user-modal-form [name="' + name + '"]');
        input.toggleClass('is-invalid', !!message);
        input.closest('.form-group').find('[data-user-error="' + name + '"]').remove();
        if (message) input.closest('.form-group').append('<span class="text-danger" data-user-error="' + name + '">' + $('<div>').text(message).html() + '</span>');
    }

    function normalizePhone(phone) {
        return phone ? phone.replace(/[\s\-()]/g, '') : '';
    }

    function updatePhoneDuplicateHint(form) {
        var phone = normalizePhone($.trim(form.find('[name="Phone"]').val() || ''));
        var hint = $('#phone-duplicate-hint');
        if (!/^\+[1-9]\d{7,14}$/.test(phone)) { hint.hide(); return; }
        if (!table) { hint.hide(); return; }
        var normalizedRows = table.rows().data().toArray();
        var exists = normalizedRows.some(function (row) {
            return normalizePhone(row.phone || '') === phone;
        });
        hint.toggle(exists);
    }

    function validateUserForm(form, showAll) {
        if (!form || !form.length) return { Form: 'The form could not be loaded.' };
        var errors = {}, username = $.trim(form.find('[name="Username"]').val() || ''), name = $.trim(form.find('[name="Name"]').val() || ''), email = $.trim(form.find('[name="Email"]').val() || ''), role = (form.find('[name="Role"]').val() || '').toUpperCase(), phone = $.trim(form.find('[name="Phone"]').val() || ''), password = form.find('[name="Password"]').val() || '';
        if (!/^[A-Za-z0-9_.]{3,30}$/.test(username)) errors.Username = 'Username must be 3 to 30 characters and contain only letters, digits, underscores, and dots.';
        if (name.length < 2 || name.length > 100) errors.Name = 'Name must be between 2 and 100 characters.';
        else if (/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(name)) errors.Name = "Enter the person's name, not an email address.";
        if (email && !/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(email)) errors.Email = 'Enter a valid email address.';
        if (role !== 'STAFF' && role !== 'ADMIN') errors.Role = 'Role must be Staff or Admin.';
        if (form.data('user-operation') === 'create' && (!password || password.length < 8 || !/[A-Za-z]/.test(password) || !/\d/.test(password))) errors.Password = 'Password must be at least 8 characters and contain a letter and a digit.';
        if (role === 'STAFF' && !phone) errors.Phone = 'Twilio number is required for Staff users.';
        if (phone && !/^\+[1-9]\d{7,14}$/.test(normalizePhone(phone))) errors.Phone = 'Enter a valid phone number in international format, such as +923456543210.';
        ['Username', 'Name', 'Email', 'Role', 'Phone', 'Password'].forEach(function (key) {
            if (showAll || touchedFields[key]) fieldError(key, errors[key] || '');
            else {
                var input = $('.user-modal-form [name="' + key + '"]');
                input.removeClass('is-invalid');
                input.closest('.form-group').find('[data-user-error="' + key + '"]').remove();
            }
        });
        updatePhoneDuplicateHint(form);
        return errors;
    }

    /* replaced by remove-confirm-modal 2026-11-18: confirmation and executeAction are not used by Users. */
    /*
    function setUserConfirmButton(disabled) { $('#actionConfirmModal .btn-danger').prop('disabled', disabled); }
    */
    $(document).on('submit', '.user-modal-form', function (e) {
        e.preventDefault();
        var form = $(this);
        if (userRequestInFlight) return false;
        ['Username', 'Name', 'Email', 'Role', 'Phone', 'Password'].forEach(function (key) { touchedFields[key] = true; });
        var errors = validateUserForm(form, true);
        if (Object.keys(errors).length) {
            setSaveButton(false, $('#user-form-title').text());
            var firstName = Object.keys(errors)[0];
            var firstInput = $('.user-modal-form [name="' + firstName + '"]');
            if (firstInput.length) firstInput.trigger('focus');
            return false;
        }
        userRequestInFlight = true;
        var title = $('#user-form-title').text();
        setSaveButton(true, title);
        $.ajax({
            url: form.attr('action'),
            method: 'POST',
            data: form.serialize(),
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        }).done(function (response) {
            if (response.success) {
                $('#user-form-modal').modal('hide');
                showToast(response.message, 'success');
                window.setTimeout(function () {
                    window.location.href = response.redirectUrl || '/Users';
                }, 250);
            } else {
                var serverErrors = response.errors || {};
                Object.keys(serverErrors).forEach(function (key) {
                    touchedFields[key] = true;
                    fieldError(key, serverErrors[key]);
                });
                showToast(response.message || 'Unable to save the user.', 'error');
            }
        }).fail(function (xhr) {
            var payload = xhr.responseJSON || {}, serverErrors = payload.errors || {};
            Object.keys(serverErrors).forEach(function (key) {
                touchedFields[key] = true;
                fieldError(key, serverErrors[key]);
            });
            showToast(payload.message || 'Unable to complete the request. Please try again.', 'error');
        }).always(function () {
            userRequestInFlight = false;
            setSaveButton(false, $('#user-form-title').text());
        });
        return false;
    });
    $(document).on('blur', '.user-modal-form input, .user-modal-form select', function () {
        touchedFields[$(this).attr('name')] = true;
        validateUserForm($(this).closest('form'), false);
    });
    $(document).on('input change', '.user-modal-form input, .user-modal-form select', function () {
        var name = $(this).attr('name');
        if (touchedFields[name]) validateUserForm($(this).closest('form'), false);
        else updatePhoneDuplicateHint($(this).closest('form'));
    });
})(jQuery);
