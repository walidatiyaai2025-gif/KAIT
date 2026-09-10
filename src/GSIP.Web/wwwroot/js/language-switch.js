(() => {
    'use strict';

    document.querySelectorAll('[data-culture]').forEach((button) => {
        button.addEventListener('click', () => {
            const culture = button.dataset.culture;
            if (!culture) {
                return;
            }

            const url = new URL(window.location.href);
            url.searchParams.set('culture', culture);
            url.searchParams.set('ui-culture', culture);
            window.location.assign(url.pathname + url.search + url.hash);
        });
    });
})();
