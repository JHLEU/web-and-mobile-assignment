$(function () {
    var $form = $('#contactForm');
    var $messageArea = $('#formStatusNotification');

    function escapeHtml(value) {
        return $('<div>').text(value).html();
    }

    function buildNotification(errors) {
        var listItems = errors.map(function (message) {
            return '<li>' + escapeHtml(message) + '</li>';
        });

        return '<div class="status-notification status-notification-error" role="alert" aria-live="assertive">' +
            '<div class="status-notification-content">' +
            '<strong>Validation Error</strong>' +
            '<ul>' + listItems.join('') + '</ul>' +
            '</div>' +
            '</div>';
    }

    function showNotification(html) {
        $messageArea.html(html);
        $messageArea.addClass('status-notification-wrapper-visible');

        window.clearTimeout($messageArea.data('hideTimer'));
        $messageArea.data('hideTimer', window.setTimeout(function () {
            $messageArea.empty();
            $messageArea.removeClass('status-notification-wrapper-visible');
        }, 6000));
    }

    function validateInput(value) {
        return $.trim(value) !== '';
    }

    function validateEmail(value) {
        return /^[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}$/i.test($.trim(value));
    }

    $form.on('submit', function (event) {
        var errors = [];
        var fields = [
            { name: 'name', validator: validateInput, message: 'Please enter your name.' },
            { name: 'phone', validator: validateInput, message: 'Please enter your phone number.' },
            { name: 'email', validator: validateEmail, message: 'Please enter a valid email address.' },
            { name: 'message', validator: validateInput, message: 'Please enter your message.' }
        ];

        $.each(fields, function (_, field) {
            var value = $form.find('[name="' + field.name + '"]').val();
            if (!field.validator(value)) {
                errors.push(field.message);
            }
        });

        if (errors.length > 0) {
            event.preventDefault();
            showNotification(buildNotification(errors));
        }
    });
});
