// ========================================
// Finance Dashboard - Dashboard Page JS
// ========================================

let summaryChart = null;
let expenditureChart = null;
let pieChart = null;

$(document).ready(function () {
    // Only auto-init on the dashboard home page
    if ($('#nonDevelopmentBudget').length === 0) return;

    loadFinancialYears();
    // Load summary first, then charts sequentially to avoid DB overload
    loadDashboardSummary(null, function () {
        loadChartData();
        loadMonthlyExpenditure(new Date().getFullYear());
    });

    // Financial year change handler
    $('#financialYearFilter').on('change', function () {
        const year = $(this).val() ? parseInt($(this).val()) : null;
        loadDashboardSummary(year, function () {
            loadChartData(year);
            loadMonthlyExpenditure(year || new Date().getFullYear());
        });
    });
});

function loadFinancialYears() {
    ajaxGet('/Dashboard/GetFinancialYears', {}, function (response) {
        if (response.success) {
            const select = $('#financialYearFilter');
            select.empty();
            select.append('<option value="">-- All Years --</option>');
            response.data.forEach(function (item) {
                select.append('<option value="' + item.value + '">' + item.text + '</option>');
            });
        }
    });
}

function loadDashboardSummary(financialYear, callback) {
    const params = financialYear ? { financialYear: financialYear } : {};

    ajaxGet('/Dashboard/GetSummary', params, function (response) {
        if (response.success) {
            const data = response.data;

            // 4 sector tiles — compact format (no decimals)
            animateValue('#nonDevelopmentBudget', data.nonDevelopmentBudget, formatCompact);
            animateValue('#capitalAc1Budget',     data.capitalAc1Budget,     formatCompact);
            animateValue('#capitalAc2Budget',     data.capitalAc2Budget,     formatCompact);
            animateValue('#developmentBudget',    data.developmentBudget,    formatCompact);

            // Total row
            animateValue('#totalFinalGrant',     data.totalFinalGrant,      formatCompact);
            animateValue('#totalActualExpenditure', data.totalExpenditure, formatCompact);


            if (typeof callback === 'function') callback();
        } else {
            showToast('error', 'Dashboard error: ' + (response.message || 'Unknown error. Check browser console.'));
            console.error('Dashboard summary failed:', response);
        }
    });
}

function loadChartData(financialYear) {
    const params = financialYear ? { financialYear: financialYear } : {};

    ajaxGet('/Dashboard/GetChartData', params, function (response) {
        if (response.success && response.data.length > 0) {
            renderPieChart(response.data[0]);
        }
    });
}

function loadMonthlyExpenditure(financialYear, fundCode) {
    const params = { financialYear: financialYear };
    if (fundCode) params.fundCode = fundCode;

    ajaxGet('/Dashboard/GetMonthlyExpenditure', params, function (response) {
        if (response.success) {
            renderExpenditureChart(response.data);
        }
    });
}

function animateValue(selector, value, formatter) {
    const element = $(selector);
    if (element.length === 0) return;

    element.addClass('pulse');
    element.html(formatter(value));
    setTimeout(() => element.removeClass('pulse'), 500);
}

// Compact currency: no decimals, abbreviates billions/millions
function formatCompact(value) {
    if (value === null || value === undefined || isNaN(value)) return 'PKR 0';
    const n = parseFloat(value);
    if (n >= 1_000_000_000) return 'PKR ' + (n / 1_000_000_000).toFixed(2) + ' Bn';
    if (n >= 1_000_000)     return 'PKR ' + (n / 1_000_000).toFixed(2) + ' M';
    if (n >= 1_000)         return 'PKR ' + Math.round(n).toLocaleString('en-PK');
    return 'PKR ' + Math.round(n).toLocaleString('en-PK');
}

function updateVarianceIndicator(selector, percentage) {
    const element = $(selector);
    if (element.length === 0) return;

    const isPositive = percentage >= 0;
    const icon = isPositive ? 'bi-arrow-up-circle' : 'bi-arrow-down-circle';
    const colorClass = isPositive ? 'text-danger' : 'text-success';

    element.html('<i class="bi ' + icon + ' ' + colorClass + '"></i> ' + formatPercentage(percentage));
}

function renderPieChart(chartData) {
    const ctx = document.getElementById('summaryPieChart');
    if (!ctx) return;

    destroyChart(pieChart);

    pieChart = new Chart(ctx, {
        type: 'doughnut',
        data: {
            labels: chartData.labels,
            datasets: [{
                data: chartData.data,
                backgroundColor: chartData.backgroundColors || chartColors.palette,
                borderWidth: 2,
                borderColor: '#fff'
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    position: 'right',
                    labels: {
                        boxWidth: 12,
                        padding: 10,
                        font: { size: 11 }
                    }
                },
                tooltip: {
                    callbacks: {
                        label: function (context) {
                            const label = context.label || '';
                            const value = context.parsed || 0;
                            const total = context.dataset.data.reduce((a, b) => a + b, 0);
                            const percentage = total > 0 ? ((value / total) * 100).toFixed(1) : 0;
                            return label + ': ' + formatCurrency(value) + ' (' + percentage + '%)';
                        }
                    }
                }
            }
        }
    });
}

function renderExpenditureChart(data) {
    const ctx = document.getElementById('expenditureChart');
    if (!ctx) return;

    destroyChart(expenditureChart);

    const labels = data.map(d => d.month);
    const values = data.map(d => d.amount);

    expenditureChart = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: labels,
            datasets: [{
                label: 'Monthly Expenditure',
                data: values,
                backgroundColor: chartColors.primary + '80',
                borderColor: chartColors.primary,
                borderWidth: 1,
                borderRadius: 4
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: { display: false },
                tooltip: {
                    callbacks: {
                        label: function (context) {
                            return 'Expenditure: ' + formatCurrency(context.parsed.y);
                        }
                    }
                }
            },
            scales: {
                y: {
                    beginAtZero: true,
                    ticks: {
                        callback: function (value) {
                            return formatCurrency(value).replace('PKR', '');
                        },
                        font: { size: 10 }
                    }
                },
                x: {
                    ticks: { font: { size: 10 } }
                }
            }
        }
    });
}

// Initialize DataTable for budget data
function initBudgetDataTable(tableId, ajaxUrl, columns, additionalData) {
    return $(tableId).DataTable({
        processing: true,
        serverSide: true,
        ajax: {
            url: ajaxUrl,
            type: 'POST',
            contentType: 'application/json',
            data: function (d) {
                const requestData = {
                    draw: d.draw,
                    start: d.start,
                    length: d.length,
                    searchValue: d.search?.value || '',
                    sortColumn: d.columns?.[d.order?.[0]?.column]?.data || 'Id',
                    sortDirection: d.order?.[0]?.dir || 'asc'
                };

                if (additionalData) {
                    Object.assign(requestData, additionalData());
                }

                return JSON.stringify(requestData);
            },
            dataSrc: 'data',
            beforeSend: function () { showLoading(); },
            complete: function () { hideLoading(); },
            error: function () { hideLoading(); showToast('error', 'Failed to load data.'); }
        },
        columns: columns,
        order: [[0, 'asc']],
        pageLength: 50,
        language: {
            emptyTable: 'No records found',
            zeroRecords: 'No matching records found'
        }
    });
}

// Filter and reload DataTable
function reloadDataTable(table, filters) {
    table.ajax.reload();
}

// Export handlers
function exportToExcel(exportUrl, filters) {
    const params = $.param(filters || {});
    window.location.href = exportUrl + (params ? '?' + params : '');
}

function exportToPdf(exportUrl, filters) {
    const params = $.param(filters || {});
    window.location.href = exportUrl + (params ? '?' + params : '');
}
