var salesChartInstance = null;
var trendingChartInstance = null;

document.addEventListener('DOMContentLoaded', function () {
    loadRestaurantRanking();
});

function destroyCharts() {
    if (salesChartInstance) {
        try { salesChartInstance.destroy(); } catch (e) { /* ignore */ }
        salesChartInstance = null;
    }
    if (trendingChartInstance) {
        try { trendingChartInstance.destroy(); } catch (e) { /* ignore */ }
        trendingChartInstance = null;
    }
}

function getSalesColors(n) {
    var base = [
        'rgba(175, 96, 26, 0.85)',
        'rgba(212, 136, 43, 0.85)',
        'rgba(230, 162, 78, 0.85)',
        'rgba(193, 118, 17, 0.85)',
        'rgba(243, 156, 18, 0.85)',
        'rgba(217, 136, 70, 0.85)',
        'rgba(155, 89, 18, 0.85)',
        'rgba(249, 185, 95, 0.85)',
        'rgba(202, 125, 34, 0.85)',
        'rgba(179, 102, 15, 0.85)'
    ];
    var result = [];
    for (var i = 0; i < n; i++) { result.push(base[i % base.length]); }
    return result;
}

function getTrendingColors(n) {
    var base = [
        'rgba(22, 160, 133, 0.85)',
        'rgba(39, 174, 96, 0.85)',
        'rgba(46, 204, 113, 0.85)',
        'rgba(52, 152, 219, 0.85)',
        'rgba(41, 128, 185, 0.85)',
        'rgba(26, 188, 156, 0.85)',
        'rgba(30, 139, 195, 0.85)',
        'rgba(88, 214, 141, 0.85)',
        'rgba(24, 186, 146, 0.85)',
        'rgba(34, 197, 94, 0.85)'
    ];
    var result = [];
    for (var i = 0; i < n; i++) { result.push(base[i % base.length]); }
    return result;
}

function renderCharts(data) {
    if (typeof window.Chart === 'undefined') {
        console.warn('Chart.js not loaded; skipping chart rendering.');
        return;
    }

    destroyCharts();

    var ranking = (data && data.ranking) ? data.ranking : [];
    var labels = ranking.map(function (r) { return (r && r.restaurantName) ? r.restaurantName : 'Unknown'; });
    var totalSales = ranking.map(function (r) {
        var val = (r && typeof r.totalSales === 'number') ? r.totalSales : Number(r && r.totalSales ? r.totalSales : 0) || 0;
        return Number(Number(val).toFixed(2));
    });
    var transactionCounts = ranking.map(function (r) {
        return (r && typeof r.transactionCount === 'number') ? r.transactionCount : Number(r && r.transactionCount ? r.transactionCount : 0) || 0;
    });

    var salesCtx = document.getElementById('salesChart');
    var trendingCtx = document.getElementById('trendingChart');

    if (salesCtx) {
        var salesCtx2d = salesCtx.getContext('2d');
        var gradientSales = salesCtx2d.createLinearGradient(0, 0, 0, 350);
        gradientSales.addColorStop(0, 'rgba(175, 96, 26, 0.95)');
        gradientSales.addColorStop(1, 'rgba(243, 156, 18, 0.75)');

        salesChartInstance = new Chart(salesCtx2d, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [{
                    label: 'Total Sales (RM)',
                    data: totalSales,
                    backgroundColor: labels.length > 0 ? getSalesColors(labels.length) : gradientSales,
                    borderColor: 'rgba(139, 75, 18, 0.9)',
                    borderWidth: 1,
                    borderRadius: 6,
                    hoverBackgroundColor: 'rgba(193, 118, 17, 1)'
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: true, position: 'top', labels: { color: '#3d2b1f', font: { weight: '600' } } },
                    tooltip: {
                        callbacks: {
                            label: function (ctx) {
                                var val = Number(ctx.parsed.y || 0);
                                return ' RM ' + val.toLocaleString('en-MY', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        ticks: {
                            color: '#555',
                            maxRotation: 45,
                            minRotation: 0,
                            autoSkip: true,
                            font: { size: 11 }
                        },
                        grid: { display: false }
                    },
                    y: {
                        beginAtZero: true,
                        ticks: {
                            color: '#555',
                            callback: function (val) {
                                return 'RM ' + Number(val).toLocaleString('en-MY', { maximumFractionDigits: 0 });
                            }
                        },
                        grid: { color: 'rgba(0,0,0,0.06)' }
                    }
                }
            }
        });
    }

    if (trendingCtx) {
        var trendingCtx2d = trendingCtx.getContext('2d');
        trendingChartInstance = new Chart(trendingCtx2d, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [{
                    label: 'Successful Orders',
                    data: transactionCounts,
                    backgroundColor: labels.length > 0 ? getTrendingColors(labels.length) : 'rgba(22, 160, 133, 0.85)',
                    borderColor: 'rgba(19, 110, 91, 0.9)',
                    borderWidth: 1,
                    borderRadius: 6,
                    hoverBackgroundColor: 'rgba(39, 174, 96, 1)'
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: true, position: 'top', labels: { color: '#1d3b33', font: { weight: '600' } } },
                    tooltip: {
                        callbacks: {
                            label: function (ctx) {
                                var val = Number(ctx.parsed.y || 0);
                                return ' ' + val.toLocaleString() + ' order' + (val === 1 ? '' : 's');
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        ticks: {
                            color: '#555',
                            maxRotation: 45,
                            minRotation: 0,
                            autoSkip: true,
                            font: { size: 11 }
                        },
                        grid: { display: false }
                    },
                    y: {
                        beginAtZero: true,
                        ticks: {
                            color: '#555',
                            precision: 0,
                            callback: function (val) {
                                if (Math.floor(val) === val) { return val.toLocaleString(); }
                                return '';
                            }
                        },
                        grid: { color: 'rgba(0,0,0,0.06)' }
                    }
                }
            }
        });
    }
}

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

                renderCharts(data);
            } else {
                tbody.innerHTML = '<tr><td colspan="4" class="ranking-empty">No successful transactions found yet.</td></tr>';
                renderCharts({ ranking: [] });
            }
        })
        .catch(function (error) {
            console.error('Error:', error);
            tbody.innerHTML = ''
                + '<tr><td colspan="4" class="ranking-error">'
                + '<ion-icon name="alert-circle-outline"></ion-icon><br>'
                + 'Error loading data: ' + escapeHtml(error.message)
                + '</td></tr>';
            destroyCharts();
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
