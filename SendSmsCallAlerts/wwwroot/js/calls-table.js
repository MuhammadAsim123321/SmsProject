/**
 * Calls Table Initialization
 * Initializes DataTables on call tables with proper pagination, search, and sorting.
 * 
 * Applied to:
 * - Outgoing calls page: Views/RingCentralCalls/Index.cshtml (table#tblCalls)
 * - Phone calls pane: Views/StaffActivity/Index.cshtml (table#tblCalls)
 * 
 * Requires jQuery and DataTables library to be loaded before this script.
 */

(function () {
    'use strict';

    // Wait for DOM to be ready and jQuery to be available
    document.addEventListener('DOMContentLoaded', function () {
        // Verify jQuery and DataTables are available
        if (typeof jQuery === 'undefined' || typeof jQuery.fn === 'undefined') {
            console.warn('[calls-table.js] jQuery not available, skipping DataTable initialization');
            return;
        }

        if (typeof jQuery.fn.DataTable === 'undefined') {
            console.warn('[calls-table.js] DataTables library not loaded, skipping DataTable initialization');
            return;
        }

        var $ = jQuery;

        // Find all call tables and initialize DataTables on each
        $('table.calls-table__table').each(function () {
            var $table = $(this);
            var tableId = $table.attr('id');

            // Skip if already initialized
            if ($.fn.DataTable.isDataTable(this)) {
                return;
            }

            // Initialize DataTables to match Technician Jobs page (Step 1: calls-align-techjobs 2026-11-19)
            // replaced by calls-align-techjobs 2026-11-19: removed explicit dom option to match Technician Jobs (which doesn't set it), kept pageLength 25
            $table.DataTable({
                paging: true,
                pageLength: 25,
                lengthMenu: [10, 25, 50, 100],
                searching: true,
                lengthChange: true,
                ordering: true,
                info: true,
                autoWidth: false,
                scrollX: false,
                scrollY: false,
                // replaced by calls-align-techjobs 2026-11-19: removed dom: 'lfrtip' to match Technician Jobs default
                // dom: 'lfrtip',
                order: [],
                language: {
                    search: '',
                    searchPlaceholder: 'Search calls...'
                }
            });
        });
    });

    // Handle tab switching: adjust table columns when Phone calls pane becomes visible
    // This is needed because hidden tables (display: none) don't calculate column widths correctly
    document.addEventListener('DOMContentLoaded', function () {
        var $ = jQuery;
        var tabButtons = document.querySelectorAll('.sa-tabs button[data-tab]');

        if (tabButtons.length === 0) {
            // Not on Staff Activity page, no tabs to handle
            return;
        }

        Array.prototype.forEach.call(tabButtons, function (button) {
            button.addEventListener('click', function () {
                var tabName = button.getAttribute('data-tab');

                // When Phone calls tab is shown, adjust the table columns
                if (tabName === 'calls') {
                    // Use a small timeout to allow the tab pane to be shown first
                    setTimeout(function () {
                        var $table = $('#tblCalls');
                        if ($table.length && $.fn.DataTable.isDataTable($table[0])) {
                            $table.DataTable().columns.adjust().draw(false);
                        }
                    }, 100);
                }
            });
        });
    });

})();
