document.addEventListener('DOMContentLoaded', function () {
    loadRestaurantRanking();
});

function loadRestaurantRanking() {
    var tbody = document.getElementById('rankingBody');
    if (!tbody) {
        return;
    }

    tbody.innerHTML = '<tr><td colspan="4" class="ranking-loading"><ion-icon name="sync-outline"></ion-icon><p>Fetching latest sales data...</p></td></tr>';

    fetch('/Admin/GetRanking?limit=10')
        .then(function (response) {
            if (!response.ok) {
                throw new Error('Could not fetch data. Check if you are logged in.');
            }
            return response.json();
        })
        .then(function (data) {
            if (data.success && Array.isArray(data.ranking) && data.ranking.length > 0) {
                tbody.innerHTML = '';
                data.ranking.forEach(function (item, index) {
                    var isFirst = index === 0 ? 'top-rank' : '';
                    var totalSales = typeof item.totalSales === 'number' ? item.totalSales : Number(item.totalSales) || 0;
                    var row = ''
                        + '<tr class="ranking-row">'
                        + '  <td>'
                        + '    <span class="rank-badge ' + isFirst + '">#' + (index + 1) + '</span>'
                        + '  </td>'
                        + '  <td class="text-restaurant">'
                        +        escapeHtml(item.restaurantName)
                        + '  </td>'
                        + '  <td>'
                        + '    <span class="transaction-pill">'
                        +        escapeHtml(item.transactionCount + ' orders')
                        + '    </span>'
                        + '  </td>'
                        + '  <td class="text-sales">'
                        +        totalSales.toLocaleString('en-MY', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
                        + '  </td>'
                        + '</tr>';
                    tbody.insertAdjacentHTML('beforeend', row);
                });
            } else {
                tbody.innerHTML = '<tr><td colspan="4" class="ranking-empty">No successful transactions found yet.</td></tr>';
            }
        })
        .catch(function (error) {
            console.error('Error:', error);
            tbody.innerHTML = ''
                + '<tr><td colspan="4" class="ranking-error">'
                + '<ion-icon name="alert-circle-outline"></ion-icon><br>'
                + 'Error loading data: ' + escapeHtml(error.message)
                + '</td></tr>';
        });
}

function escapeHtml(value) {
    return String(value == null ? '' : value)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}
