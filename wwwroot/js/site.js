function toggleSidebar() {
    const sidebar = $('#mainSidebar');
    const main = $('.ds-main');
    const topbar = $('.ds-topbar');
    const overlay = $('#sidebarOverlay');
    
    if ($(window).width() < 992) {
        // Mobile behavior
        sidebar.toggleClass('show');
        overlay.toggleClass('show');
    } else {
        // Desktop behavior
        sidebar.toggleClass('sidebar-closed');
        main.toggleClass('content-full');
        topbar.toggleClass('content-full');
    }
}

// Close sidebar on mobile when clicking overlay
$(document).ready(function() {
    $('#sidebarOverlay').on('click', function() {
        if ($(window).width() < 992) {
            $('#mainSidebar').removeClass('show');
            $(this).removeClass('show');
        }
    });

    // Real-time Table Filtering
    $('.ds-filter-input').on('keyup input', function() {
        const value = $(this).val().toLowerCase();
        const targetTable = $($(this).data('target'));
        
        targetTable.find('tbody tr').filter(function() {
            const text = $(this).text().toLowerCase();
            $(this).toggle(text.indexOf(value) > -1);
        });
        
        // Show "No results" if all rows are hidden
        const visibleRows = targetTable.find('tbody tr:visible').length;
        const noResultsId = $(this).data('no-results');
        if (noResultsId) {
            if (visibleRows === 0) {
                $(noResultsId).show();
            } else {
                $(noResultsId).hide();
            }
        }
    });

    // Auto-submit dropdown filters
    $('.ds-auto-submit').on('change', function() {
        $(this).closest('form').submit();
    });
});
