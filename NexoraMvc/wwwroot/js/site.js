// UI-only JavaScript. Business data is never stored here.
// The application uses MVC + EF Core + SQL Server as the source of truth.
document.querySelectorAll('.nav-link').forEach(link => {
    const href = link.getAttribute('href') || '';
    const path = new URL(href, window.location.origin).pathname.toLowerCase();
    if (path === window.location.pathname.toLowerCase()) {
        link.classList.add('active');
    }
});

// Keep long pages usable on smaller screens without creating an application data store.
document.querySelectorAll('form').forEach(form => {
    form.addEventListener('submit', () => {
        const button = form.querySelector('button[type="submit"], button:not([type])');
        if (button && !button.disabled) {
            button.dataset.originalText = button.textContent;
            button.disabled = true;
            button.textContent = 'Processing...';
        }
    });
});


// Automatically dismiss success/info/error notifications after 6 seconds.
document.querySelectorAll('.toast').forEach(toast => {
    window.setTimeout(() => {
        toast.style.opacity = '0';
        toast.style.transform = 'translateY(-10px)';
        window.setTimeout(() => toast.remove(), 250);
    }, 6000);
});
