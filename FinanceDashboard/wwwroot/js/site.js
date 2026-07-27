// ========================================
// Finance Dashboard - Site JavaScript
// ========================================

$(document).ready(function () {
    // Ensure sidebar starts expanded on desktop
    var sidebar = document.getElementById('sidebar');
    if (sidebar && !localStorage.getItem('sidebar-collapsed')) {
        sidebar.classList.remove('collapsed');
        document.getElementById('content')?.classList.remove('expanded');
    }

    // Initialize Select2
    $('.select2').select2({
        theme: 'bootstrap-5',
        width: '100%'
    });

    // Initialize DataTables with defaults
    $.extend(true, $.fn.dataTable.defaults, {
        responsive: true,
        pageLength: 50,
        lengthMenu: [[25, 50, 100, 200], [25, 50, 100, 200]],
        language: {
            processing: '<i class="bi bi-hourglass-split"></i> Loading...',
            emptyTable: 'No data available',
            zeroRecords: 'No matching records found',
            info: 'Showing _START_ to _END_ of _TOTAL_ entries',
            infoEmpty: 'Showing 0 to 0 of 0 entries',
            infoFiltered: '(filtered from _MAX_ total entries)',
            paginate: {
                first: '<i class="bi bi-chevron-double-left"></i>',
                previous: '<i class="bi bi-chevron-left"></i>',
                next: '<i class="bi bi-chevron-right"></i>',
                last: '<i class="bi bi-chevron-double-right"></i>'
            }
        },
        dom: '<"row"<"col-sm-12 col-md-6"l><"col-sm-12 col-md-6"f>>' +
             '<"row"<"col-sm-12"tr>>' +
             '<"row"<"col-sm-12 col-md-5"i><"col-sm-12 col-md-7"p>>'
    });

    // Mobile sidebar toggle
    $('#sidebarCollapse').on('click', function () {
        if ($(window).width() <= 768) {
            $('#sidebar').toggleClass('mobile-show');
        }
    });

    // Close sidebar when clicking outside on mobile
    $(document).on('click', function (e) {
        if ($(window).width() <= 768) {
            if (!$(e.target).closest('#sidebar').length && !$(e.target).closest('#sidebarCollapse').length) {
                $('#sidebar').removeClass('mobile-show');
            }
        }
    });

    // Auto-hide alerts after 5 seconds
    setTimeout(function () {
        $('.alert-dismissible').alert('close');
    }, 5000);

    // Confirm dialog helper
    window.confirmAction = function (message, callback) {
        if (confirm(message)) {
            callback();
        }
    };

    // AJAX setup with anti-forgery token
    $.ajaxSetup({
        headers: {
            'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
        }
    });

    // Global AJAX error handler
    $(document).ajaxError(function (event, jqXHR, settings, error) {
        if (jqXHR.status === 401) {
            window.location.href = '/Account/Login';
        } else if (jqXHR.status === 403) {
            showToast('error', 'You do not have permission to perform this action.');
        } else if (jqXHR.status === 500) {
            showToast('error', 'An internal server error occurred.');
        }
    });
});

// Utility Functions
function formatCurrency(value) {
    if (value === null || value === undefined) return 'PKR 0.00';
    return new Intl.NumberFormat('en-PK', {
        style: 'currency',
        currency: 'PKR',
        minimumFractionDigits: 2
    }).format(value);
}

function formatNumber(value) {
    if (value === null || value === undefined) return '0';
    return new Intl.NumberFormat('en-PK').format(value);
}

function formatPercentage(value) {
    if (value === null || value === undefined) return '0.00%';
    return parseFloat(value).toFixed(2) + '%';
}

function showToast(type, message) {
    const toastEl = document.getElementById('toast-' + type);
    if (toastEl) {
        document.getElementById('toast-' + type + '-msg').textContent = message;
        const toast = new bootstrap.Toast(toastEl, { delay: 5000 });
        toast.show();
    }
}

function showLoading() {
    document.getElementById('loading-overlay').classList.remove('d-none');
}

function hideLoading() {
    document.getElementById('loading-overlay').classList.add('d-none');
}

// AJAX helper for common operations
function ajaxGet(url, data, successCallback, errorCallback) {
    showLoading();
    $.ajax({
        url: url,
        type: 'GET',
        data: data,
        success: function (response) {
            hideLoading();
            if (successCallback) successCallback(response);
        },
        error: function (xhr) {
            hideLoading();
            if (errorCallback) {
                errorCallback(xhr);
            } else {
                showToast('error', 'An error occurred while fetching data.');
            }
        }
    });
}

function ajaxPost(url, data, successCallback, errorCallback) {
    showLoading();
    $.ajax({
        url: url,
        type: 'POST',
        data: data,
        success: function (response) {
            hideLoading();
            if (successCallback) successCallback(response);
        },
        error: function (xhr) {
            hideLoading();
            if (errorCallback) {
                errorCallback(xhr);
            } else {
                showToast('error', 'An error occurred while processing your request.');
            }
        }
    });
}

function ajaxPostJson(url, data, successCallback, errorCallback) {
    showLoading();
    $.ajax({
        url: url,
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(data),
        headers: {
            'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
        },
        success: function (response) {
            hideLoading();
            if (successCallback) successCallback(response);
        },
        error: function (xhr) {
            hideLoading();
            if (errorCallback) {
                errorCallback(xhr);
            } else {
                showToast('error', 'An error occurred while processing your request.');
            }
        }
    });
}

// Chart color palette
const chartColors = {
    primary: '#1a3a5c',
    primaryLight: '#2c5282',
    success: '#198754',
    info: '#0dcaf0',
    warning: '#ffc107',
    danger: '#dc3545',
    secondary: '#6c757d',
    palette: [
        '#1a3a5c', '#2c5282', '#198754', '#0dcaf0', '#ffc107',
        '#dc3545', '#6f42c1', '#d63384', '#fd7e14', '#20c997',
        '#0d6efd', '#6610f2', '#adb5bd', '#28a745', '#17a2b8'
    ]
};

// Destroy chart if exists
function destroyChart(chartInstance) {
    if (chartInstance) {
        chartInstance.destroy();
    }
}

// Debounce function
function debounce(func, wait) {
    let timeout;
    return function executedFunction(...args) {
        const later = () => {
            clearTimeout(timeout);
            func(...args);
        };
        clearTimeout(timeout);
        timeout = setTimeout(later, wait);
    };
}

// Export to print
function printPage() {
    window.print();
}

// Export table to CSV
function exportTableToCSV(tableId, filename) {
    const table = document.getElementById(tableId);
    if (!table) return;

    let csv = [];
    const rows = table.querySelectorAll('tr');

    for (let i = 0; i < rows.length; i++) {
        const row = [], cols = rows[i].querySelectorAll('td, th');
        for (let j = 0; j < cols.length; j++) {
            row.push(cols[j].innerText.replace(/,/g, ''));
        }
        csv.push(row.join(','));
    }

    downloadCSV(csv.join('\n'), filename);
}

function downloadCSV(csv, filename) {
    const csvFile = new Blob([csv], { type: 'text/csv' });
    const downloadLink = document.createElement('a');
    downloadLink.download = filename;
    downloadLink.href = window.URL.createObjectURL(csvFile);
    downloadLink.style.display = 'none';
    document.body.appendChild(downloadLink);
    downloadLink.click();
    document.body.removeChild(downloadLink);
}
