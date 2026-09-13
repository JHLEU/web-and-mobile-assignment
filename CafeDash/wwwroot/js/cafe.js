$(function () {
    // Menu modal state for the currently selected item.
    var $overlay = $('#menuModalOverlay');
    var $form = $('#menuModalForm');
    var $modalTitle = $('#modalItemTitle');
    var $qtyInput = $('#itemQty');
    var $drinkOptions = $('#drinkOptions');
    var $remarkInput = $('#itemRemark');
    var $toast = null;
    var toastTimer = null;
    var currentItem = null;

    function escapeHtml(value) {
        return $('<div>').text(value == null ? '' : String(value)).html();
    }

    function formatMoney(value) {
        var amount = Number(value) || 0;
        return 'RM ' + amount.toFixed(2);
    }

    function callCartApi(action, payload) {
        return $.ajax({
            url: '/Cart/Api',
            method: 'POST',
            dataType: 'json',
            data: $.extend({ action: action }, payload || {})
        });
    }

    function ensureToast() {
        if ($toast && $toast.length > 0) {
            return $toast;
        }

        $toast = $('<div class="top-toast" role="status" aria-live="polite"></div>');
        $('body').append($toast);
        return $toast;
    }

    function showToast(message, type) {
        var $el = ensureToast();
        var toneClass = type === 'error' ? 'top-toast-error' : 'top-toast-success';

        if (toastTimer) {
            clearTimeout(toastTimer);
            toastTimer = null;
        }

        $el.removeClass('top-toast-success top-toast-error top-toast-show')
            .addClass(toneClass)
            .text(message);

        // Trigger animation frame so repeated messages still animate.
        window.requestAnimationFrame(function () {
            $el.addClass('top-toast-show');
        });

        toastTimer = setTimeout(function () {
            $el.removeClass('top-toast-show');
        }, 2200);
    }

    function openModal(foodId, restaurantId, restaurantName, itemName, itemType, isDrink, itemAmount) {
        // Keep source ids in modal state so checkout can persist accurate history data.
        currentItem = {
            foodId: Number(foodId) || 0,
            restaurantId: Number(restaurantId) || 0,
            restaurantName: String(restaurantName || ''),
            name: itemName,
            type: itemType,
            isDrink: isDrink,
            amount: Number(itemAmount) || 0
        };

        $modalTitle.text(itemName);
        $qtyInput.val(1);
        $remarkInput.val('');
        $form.find('input[name="sugar"][value="Normal"]').prop('checked', true);
        $form.find('input[name="ice"][value="Normal"]').prop('checked', true);

        if (isDrink) {
            $drinkOptions.removeClass('hidden');
        } else {
            $drinkOptions.addClass('hidden');
        }

        $overlay.addClass('open').attr('aria-hidden', 'false');
    }

    function closeModal() {
        $overlay.removeClass('open').attr('aria-hidden', 'true');
        currentItem = null;
    }

    // Open the modal from any menu item button on the page.
    $(document).on('click', '.menu-item-trigger', function () {
        var $btn = $(this);
        openModal(
            $btn.data('food-id') || 0,
            $btn.data('restaurant-id') || 0,
            $btn.data('restaurant-name') || '',
            $btn.data('item-name') || 'Item',
            $btn.data('item-type') || 'Food / Beverage',
            String($btn.data('is-drink')) === '1',
            $btn.data('item-amount') || 0
        );
    });

    $('#qtyMinus').on('click', function () {
        var current = Math.max(1, parseInt($qtyInput.val() || '1', 10));
        $qtyInput.val(Math.max(1, current - 1));
    });

    $('#qtyPlus').on('click', function () {
        var current = Math.max(1, parseInt($qtyInput.val() || '1', 10));
        $qtyInput.val(current + 1);
    });

    $qtyInput.on('change', function () {
        var current = parseInt($qtyInput.val() || '1', 10);
        $qtyInput.val(isNaN(current) || current < 1 ? 1 : current);
    });

    $('#closeModalBtn').on('click', function () {
        closeModal();
    });


    $form.on('submit', function (event) {
        event.preventDefault();
        if (!currentItem) {
            return;
        }

        // Send the selected item and options to the shared cart API.
        var quantity = Math.max(1, parseInt($qtyInput.val() || '1', 10));
        var remark = $.trim($remarkInput.val());
        callCartApi('add', {
            // Send both display and relational identifiers for downstream payment persistence.
            food_id: currentItem.foodId,
            restaurant_id: currentItem.restaurantId,
            restaurant_name: currentItem.restaurantName,
            item_name: currentItem.name,
            item_type: currentItem.type,
            quantity: quantity,
            unit_amount: currentItem.amount,
            sugar: currentItem.isDrink ? ($form.find('input[name="sugar"]:checked').val() || 'Normal') : '',
            ice: currentItem.isDrink ? ($form.find('input[name="ice"]:checked').val() || 'Normal') : '',
            remark: remark
        }).done(function (response) {
            if (!response || !response.success) {
                showToast('Failed to add item to cart. Please try again.', 'error');
                return;
            }

            showToast('Added ' + currentItem.name + ' x' + quantity, 'success');
            closeModal();
        }).fail(function () {
            showToast('Failed to add item to cart. Please try again.', 'error');
        });
    });
});
