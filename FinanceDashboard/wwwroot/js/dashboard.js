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

    // Re-run count-up on hover over any card that has a stored target
    $(document).on('mouseenter', '.rich-card, .total-card', function () {
        $(this).find('[data-count-target]').each(function () {
            const target = parseFloat(this.dataset.countTarget);
            if (!isNaN(target)) countUpCompact(this, target, 600);
        });
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

            // 4 sector tiles — budget value (compact format)
            animateValue('#nonDevelopmentBudget', data.nonDevelopmentBudget, formatCompact);
            animateValue('#capitalAc1Budget',     data.capitalAc1Budget,     formatCompact);
            animateValue('#capitalAc2Budget',     data.capitalAc2Budget,     formatCompact);
            animateValue('#developmentBudget',    data.developmentBudget,    formatCompact);

            // Release & Expenditure per sector
            $('#nonDevelopmentRelease').text(formatCompact(data.nonDevelopmentRelease));
            $('#nonDevelopmentExpenditure').text(formatCompact(data.nonDevelopmentExpenditure));
            $('#capitalAc1Release').text(formatCompact(data.capitalAc1Release));
            $('#capitalAc1Expenditure').text(formatCompact(data.capitalAc1Expenditure));
            $('#capitalAc2Release').text(formatCompact(data.capitalAc2Release));
            $('#capitalAc2Expenditure').text(formatCompact(data.capitalAc2Expenditure));
            $('#developmentRelease').text(formatCompact(data.developmentRelease));
            $('#developmentExpenditure').text(formatCompact(data.developmentExpenditure));

            // Used % and progress bars
            updateUsed('#nonDevelopmentUsedPct', '#nonDevelopmentBar', data.nonDevelopmentExpenditure, data.nonDevelopmentBudget);
            updateUsed('#capitalAc1UsedPct',     '#capitalAc1Bar',     data.capitalAc1Expenditure,     data.capitalAc1Budget);
            updateUsed('#capitalAc2UsedPct',     '#capitalAc2Bar',     data.capitalAc2Expenditure,     data.capitalAc2Budget);
            updateUsed('#developmentUsedPct',    '#developmentBar',    data.developmentExpenditure,    data.developmentBudget);

            // Total row — budget & expenditure values
            animateValue('#totalFinalGrant',        data.totalFinalGrant,   formatCompact);
            animateValue('#totalActualExpenditure', data.totalExpenditure,  formatCompact);

            // Total release (sum across all sectors)
            const totalRelease = (data.nonDevelopmentRelease || 0) + (data.capitalAc1Release || 0)
                               + (data.capitalAc2Release || 0)    + (data.developmentRelease || 0);

            // Released against allocation %
            const releasedPct = data.totalFinalGrant > 0
                ? Math.min((totalRelease / data.totalFinalGrant) * 100, 100) : 0;
            const releasedLabel = releasedPct < 0.1 ? '<0.1%' : releasedPct.toFixed(1) + '%';
            $('#releasedAgainstAllocationPct').html(releasedLabel);
            setBarWidth('#releasedAgainstAllocationBar', releasedPct);
            $('#totalReleasedLabel').text('Released: ' + formatCompact(totalRelease));

            // Count sectors that have any budget allocated
            const sectorCount = [data.nonDevelopmentBudget, data.capitalAc1Budget,
                                  data.capitalAc2Budget, data.developmentBudget]
                                  .filter(v => v > 0).length;
            $('#sectorsFundedLabel').text(sectorCount + ' sector' + (sectorCount !== 1 ? 's' : '') + ' funded');

            // Spent against released %
            const spentAgainstReleased = totalRelease > 0
                ? Math.min((data.totalExpenditure / totalRelease) * 100, 100) : 0;
            const spentRelLabel = spentAgainstReleased < 0.1 ? '<0.1%' : spentAgainstReleased.toFixed(1) + '%';
            $('#spentAgainstReleasedPct').html(spentRelLabel);
            setBarWidth('#spentAgainstReleasedBar', spentAgainstReleased);

            // Spent against total allocation %
            const spentAgainstAlloc = data.totalFinalGrant > 0
                ? ((data.totalExpenditure / data.totalFinalGrant) * 100) : 0;
            const spentAllocLabel = spentAgainstAlloc < 0.1 ? '<0.1%' : spentAgainstAlloc.toFixed(1) + '%';
            $('#spentAgainstAllocationLabel').text('Spent against total allocation: ' + spentAllocLabel);

            // BER last updated — shown in page subtitle
            const d = data.berLastUpdated ? new Date(data.berLastUpdated) : null;
            if (d) {
                const months = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'];
                const fmt = String(d.getDate()).padStart(2,'0') + ' ' + months[d.getMonth()] + ' ' + d.getFullYear()
                          + '  ' + String(d.getHours()).padStart(2,'0') + ':' + String(d.getMinutes()).padStart(2,'0');
                $('#berLastUpdatedText').text(fmt);
            } else {
                $('#berLastUpdatedText').text('Not available');
            }


            if (typeof callback === 'function') callback();
        } else {
            showToast('error', 'Dashboard error: ' + (response.message || 'Unknown error. Check browser console.'));
            console.error('Dashboard summary failed:', response);
        }
    });
}

function updateUsed(pctSelector, barSelector, expenditure, budget) {
    const pct = (budget > 0) ? Math.min((expenditure / budget) * 100, 100) : 0;
    const label = pct < 0.1 ? '<0.1%' : pct.toFixed(1) + '%';
    $(pctSelector).html(label);
    setBarWidth(barSelector, pct);
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

/* ── Count-up animation ───────────────────────────────── */
function countUpCompact(element, targetValue, duration) {
    duration = duration || 900;
    const start = performance.now();
    const el    = typeof element === 'string' ? document.querySelector(element) : element;
    if (!el) return;
    el.dataset.countTarget = targetValue;   // store so hover replay can read it

    function step(now) {
        const elapsed = now - start;
        const progress = Math.min(elapsed / duration, 1);
        // ease-out cubic: 1 - (1-p)^3
        const eased = 1 - Math.pow(1 - progress, 3);
        el.textContent = formatCompact(targetValue * eased);
        if (progress < 1) requestAnimationFrame(step);
        else              el.textContent = formatCompact(targetValue);
    }
    requestAnimationFrame(step);
}

function animateValue(selector, value, formatter) {
    countUpCompact(selector, value, 900);
}

/* ── Progress bar ───────────────────────────────────── */
function setBarWidth(barSelector, pct) {
    const bar = document.querySelector(barSelector);
    if (!bar) return;
    const w = Math.max(0, Math.min(pct, 100)).toFixed(2) + '%';
    bar.dataset.width = w;   // store for potential re-trigger
    // Double rAF ensures the CSS transition fires even if bar was just reset to 0
    bar.style.width = '0%';
    requestAnimationFrame(function () {
        requestAnimationFrame(function () {
            bar.style.width = w;
        });
    });
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
