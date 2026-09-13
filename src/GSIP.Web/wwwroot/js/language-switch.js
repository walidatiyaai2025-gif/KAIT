(() => {
    'use strict';

    const cultureCookieName = '.AspNetCore.Culture';

    function persistCulture(culture) {
        const secure = window.location.protocol === 'https:' ? '; Secure' : '';
        document.cookie = `${cultureCookieName}=c=${culture}|uic=${culture}; Path=/; Max-Age=31536000; SameSite=Lax${secure}`;
    }

    document.querySelectorAll('[data-culture]').forEach((button) => {
        button.addEventListener('click', () => {
            const culture = button.dataset.culture;
            if (!culture) {
                return;
            }

            persistCulture(culture);

            const url = new URL(window.location.href);
            url.searchParams.set('culture', culture);
            url.searchParams.set('ui-culture', culture);
            window.location.assign(url.pathname + url.search + url.hash);
        });
    });
})();
