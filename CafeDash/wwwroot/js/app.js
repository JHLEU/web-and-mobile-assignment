$(function () {
    // Cart modal, search, and home-page category filter wiring live here.
    var $filterCards = $('.filter-card[data-filter]');
    var $restaurantLinks = $('.detail-card-link[data-category]');
    var $detailWrapper = $('.detail-wrapper').first();
    var $searchInput = $('.search input[type="text"]').first();
    var $searchButton = $('.search-btn').first();
    var $homeCartTrigger = $('#openCartBtn');
    var $cartOverlay = $('#cartModalOverlay');
    var $cartItemsContainer = $('#cartItemsContainer');
    var $cartSubtotal = $('#cartSubtotal');
    var $cartSst = $('#cartSst');
    var $cartGrandTotal = $('#cartGrandTotal');
    var $goPaymentBtn = $('#goPaymentBtn');

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

    function renderCart(data) {
        var items = (data && data.items) ? data.items : [];
        var html = '';

        if (items.length === 0) {
            html = '<p class="cart-empty">Your cart is empty.</p>';
        } else {
            $.each(items, function (_, item) {
                var options = [];
                if (item.sugar) {
                    options.push('Sugar: ' + escapeHtml(item.sugar));
                }
                if (item.ice) {
                    options.push('Ice: ' + escapeHtml(item.ice));
                }
                if (item.remark) {
                    options.push('Remark: ' + escapeHtml(item.remark));
                }

                html += '<div class="cart-item-row">';
                html += '  <div class="cart-item-main">';
                html += '    <p class="cart-item-name">' + escapeHtml(item.itemName) + '</p>';
                html += '    <p class="cart-item-meta">Qty: ' + escapeHtml(item.quantity) + ' x ' + formatMoney(item.unitAmount) + '</p>';
                if (options.length > 0) {
                    html += '    <p class="cart-item-options">' + options.join(' | ') + '</p>';
                }
                html += '    <div class="cart-item-controls">';
                html += '      <button type="button" class="cart-mini-btn cart-qty-minus" data-cart-id="' + escapeHtml(item.cartId) + '" data-qty="' + escapeHtml(item.quantity) + '">-</button>';
                html += '      <span class="cart-qty-value">' + escapeHtml(item.quantity) + '</span>';
                html += '      <button type="button" class="cart-mini-btn cart-qty-plus" data-cart-id="' + escapeHtml(item.cartId) + '" data-qty="' + escapeHtml(item.quantity) + '">+</button>';
                html += '      <button type="button" class="cart-remove-btn" data-cart-id="' + escapeHtml(item.cartId) + '">Remove</button>';
                html += '    </div>';
                html += '  </div>';
                html += '  <div class="cart-item-total">' + formatMoney(item.lineTotal) + '</div>';
                html += '</div>';
            });
        }

        $cartItemsContainer.html(html);
        $cartSubtotal.text(formatMoney(data.subtotal));
        $cartSst.text(formatMoney(data.sst));
        $cartGrandTotal.text(formatMoney(data.grandTotal));

        // Only allow checkout when there is at least one cart item.
        if ($goPaymentBtn.length > 0) {
            if (items.length === 0) {
                $goPaymentBtn.hide().attr('aria-hidden', 'true');
            } else {
                $goPaymentBtn.show().removeAttr('aria-hidden');
            }
        }
    }

    function closeCartModal() {
        $cartOverlay.removeClass('open').attr('aria-hidden', 'true');
    }

    function openCartModal() {
        // Load the current cart before showing the modal.
        $.ajax({
            url: '/Cart/Api',
            method: 'GET',
            dataType: 'json',
            data: { action: 'get' }
        }).done(function (response) {
            if (!response || !response.success) {
                alert('Unable to load cart now.');
                return;
            }

            renderCart(response);
            $cartOverlay.addClass('open').attr('aria-hidden', 'false');
        }).fail(function () {
            alert('Unable to load cart now.');
        });
    }

    if ($homeCartTrigger.length > 0 && $cartOverlay.length > 0) {
        $homeCartTrigger.on('click', function (event) {
            event.preventDefault();
            openCartModal();
        });

        $('#closeCartBtn').on('click', function () {
            closeCartModal();
        });

        $cartItemsContainer.on('click', '.cart-remove-btn', function () {
            var cartId = String($(this).data('cart-id') || '');
            if (cartId === '') {
                return;
            }

            callCartApi('remove', { cart_id: cartId }).done(function (response) {
                if (!response || !response.success) {
                    alert('Failed to remove item.');
                    return;
                }

                renderCart(response);
            }).fail(function () {
                alert('Failed to remove item.');
            });
        });

        $cartItemsContainer.on('click', '.cart-qty-minus, .cart-qty-plus', function () {
            var $btn = $(this);
            var cartId = String($btn.data('cart-id') || '');
            var currentQty = Math.max(1, parseInt($btn.data('qty') || '1', 10));
            var nextQty = currentQty;

            if ($btn.hasClass('cart-qty-minus')) {
                nextQty = Math.max(1, currentQty - 1);
            } else {
                nextQty = currentQty + 1;
            }

            if (cartId === '' || nextQty === currentQty) {
                return;
            }

            callCartApi('update', {
                cart_id: cartId,
                quantity: nextQty
            }).done(function (response) {
                if (!response || !response.success) {
                    alert('Failed to update quantity.');
                    return;
                }

                renderCart(response);
            }).fail(function () {
                alert('Failed to update quantity.');
            });
        });

        $cartOverlay.on('click', function (event) {
            if (event.target === this) {
                closeCartModal();
            }
        });

        $(document).on('keydown', function (event) {
            if (event.key === 'Escape' && $cartOverlay.hasClass('open')) {
                closeCartModal();
            }
        });
    }
    
    function runCafeSearch() {
        // Search the restaurant list and jump to the matched cafe page.
        var keyword = $.trim($searchInput.val() || '');

        if (keyword === '') {
            alert('Please enter a cafe name.');
            $searchInput.trigger('focus');
            return;
        }

        $.ajax({
            url: '/Api/SearchCafe',
            method: 'GET',
            dataType: 'json',
            data: { q: keyword }
        }).done(function (response) {
            if (response && response.success && response.found && response.cafeId) {
                window.location.href = '/Sidebar/Cafe?id=' + encodeURIComponent(response.cafeId);
                return;
            }

            alert('No cafe found for "' + keyword + '".');
        }).fail(function () {
            alert('Unable to search cafe right now. Please try again.');
        });
    }

    if ($searchInput.length > 0 && $searchButton.length > 0) {
        $searchButton.on('click', function () {
            runCafeSearch();
        });

        $searchInput.on('keydown', function (event) {
            if (event.key === 'Enter') {
                event.preventDefault();
                runCafeSearch();
            }
        });
    }

    if ($filterCards.length === 0 || $restaurantLinks.length === 0 || $detailWrapper.length === 0) {
        return;
    }

    // Hide restaurants that do not match the selected category.
    var $emptyMessage = $('<p class="filter-empty" style="display:none;">No restaurants found for this category.</p>');
    $detailWrapper.after($emptyMessage);

    function applyFilter(filterValue) {
        var visibleCount = 0;

        $restaurantLinks.each(function () {
            var $item = $(this);
            var itemCategory = String($item.data('category') || '').toLowerCase();
            var shouldShow = filterValue === 'all' || itemCategory === filterValue;

            if (shouldShow) {
                $item.show();
                visibleCount += 1;
            } else {
                $item.hide();
            }
        });

        if (visibleCount === 0) {
            $emptyMessage.show();
        } else {
            $emptyMessage.hide();
        }
    }

    $filterCards.on('click', function () {
        var $card = $(this);
        var selectedFilter = String($card.data('filter') || 'all').toLowerCase();

        $filterCards.removeClass('active');
        $card.addClass('active');
        applyFilter(selectedFilter);
    });

    applyFilter('all');
});
