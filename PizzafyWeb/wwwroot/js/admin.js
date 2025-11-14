// Admin Panel Mobile Responsive JavaScript

document.addEventListener('DOMContentLoaded', function() {
    // Create mobile sidebar toggle button if it doesn't exist
    if (!document.querySelector('.sidebar-toggle')) {
        const toggleBtn = document.createElement('button');
        toggleBtn.className = 'sidebar-toggle';
        toggleBtn.innerHTML = '<i class="fas fa-bars"></i>';
        toggleBtn.setAttribute('aria-label', 'Toggle sidebar');
        document.body.appendChild(toggleBtn);
    }
    
    // Create sidebar overlay if it doesn't exist
    if (!document.querySelector('.sidebar-overlay')) {
        const overlay = document.createElement('div');
        overlay.className = 'sidebar-overlay';
        document.body.appendChild(overlay);
    }
    
    const sidebar = document.querySelector('.admin-sidebar');
    const toggleBtn = document.querySelector('.sidebar-toggle');
    const overlay = document.querySelector('.sidebar-overlay');
    
    if (!sidebar || !toggleBtn || !overlay) return;
    
    // Toggle sidebar on button click
    toggleBtn.addEventListener('click', function() {
        sidebar.classList.toggle('open');
        overlay.classList.toggle('active');
        document.body.style.overflow = sidebar.classList.contains('open') ? 'hidden' : '';
    });
    
    // Close sidebar when overlay is clicked
    overlay.addEventListener('click', function() {
        sidebar.classList.remove('open');
        overlay.classList.remove('active');
        document.body.style.overflow = '';
    });
    
    // Close sidebar when clicking a nav link on mobile
    const navLinks = sidebar.querySelectorAll('.nav-link');
    navLinks.forEach(link => {
        link.addEventListener('click', function() {
            if (window.innerWidth <= 768) {
                sidebar.classList.remove('open');
                overlay.classList.remove('active');
                document.body.style.overflow = '';
            }
        });
    });
    
    // Handle window resize
    let resizeTimer;
    window.addEventListener('resize', function() {
        clearTimeout(resizeTimer);
        resizeTimer = setTimeout(function() {
            if (window.innerWidth > 768) {
                sidebar.classList.remove('open');
                overlay.classList.remove('active');
                document.body.style.overflow = '';
            }
        }, 250);
    });
    
    // Add data-label attributes to table cells for mobile view
    const tables = document.querySelectorAll('.products-table-container');
    tables.forEach(table => {
        const headers = table.querySelectorAll('.table-header-cell');
        const rows = table.querySelectorAll('.table-row');
        
        rows.forEach(row => {
            const cells = row.querySelectorAll('.table-cell');
            cells.forEach((cell, index) => {
                if (headers[index]) {
                    cell.setAttribute('data-label', headers[index].textContent.trim());
                }
            });
        });
    });
    
    // Handle touch-friendly interactions
    if ('ontouchstart' in window) {
        document.body.classList.add('touch-device');
    }
    
    // Improve form accessibility on mobile
    const formControls = document.querySelectorAll('.form-control, .form-select');
    formControls.forEach(control => {
        control.addEventListener('focus', function() {
            this.scrollIntoView({ behavior: 'smooth', block: 'center' });
        });
    });
    
    // Handle image loading states
    const productImages = document.querySelectorAll('.product-image img');
    productImages.forEach(img => {
        img.addEventListener('load', function() {
            this.removeAttribute('data-loading');
        });
        
        img.addEventListener('error', function() {
            this.setAttribute('data-error', 'true');
            this.src = '/images/placeholder.png'; // Fallback image
        });
    });
    
    // Optimize table scrolling on mobile
    const tableContainers = document.querySelectorAll('.admin-content-card');
    tableContainers.forEach(container => {
        let isScrolling = false;
        
        container.addEventListener('touchstart', function() {
            isScrolling = true;
        });
        
        container.addEventListener('touchend', function() {
            setTimeout(() => {
                isScrolling = false;
            }, 100);
        });
    });
    
    // Add swipe gesture to close sidebar on mobile
    let touchStartX = 0;
    let touchEndX = 0;
    
    sidebar.addEventListener('touchstart', function(e) {
        touchStartX = e.changedTouches[0].screenX;
    });
    
    sidebar.addEventListener('touchend', function(e) {
        touchEndX = e.changedTouches[0].screenX;
        handleSwipe();
    });
    
    function handleSwipe() {
        if (touchEndX < touchStartX - 50) {
            // Swiped left
            sidebar.classList.remove('open');
            overlay.classList.remove('active');
            document.body.style.overflow = '';
        }
    }
    
    // Lazy loading for product images
    if ('IntersectionObserver' in window) {
        const imageObserver = new IntersectionObserver((entries, observer) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    const img = entry.target;
                    if (img.dataset.src) {
                        img.src = img.dataset.src;
                        img.removeAttribute('data-src');
                        observer.unobserve(img);
                    }
                }
            });
        });
        
        const lazyImages = document.querySelectorAll('img[data-src]');
        lazyImages.forEach(img => imageObserver.observe(img));
    }
    
    // Enhance dropdown accessibility on touch devices
    const dropdowns = document.querySelectorAll('.dropdown-toggle');
    dropdowns.forEach(dropdown => {
        dropdown.addEventListener('touchstart', function(e) {
            e.preventDefault();
            this.click();
        });
    });
    
    // Prevent zoom on input focus for iOS
    if (/iPad|iPhone|iPod/.test(navigator.userAgent)) {
        const inputs = document.querySelectorAll('input, select, textarea');
        inputs.forEach(input => {
            const currentSize = parseFloat(window.getComputedStyle(input).fontSize);
            if (currentSize < 16) {
                input.style.fontSize = '16px';
            }
        });
    }
});

// Utility function to debounce events
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

// Export for use in other scripts
window.adminUtils = {
    debounce: debounce
};
