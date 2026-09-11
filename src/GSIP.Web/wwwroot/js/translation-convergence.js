(() => {
    'use strict';

    if (!document.documentElement.lang.toLowerCase().startsWith('ar')) return;

    const textMap = new Map([
        ['Request ID', 'معرّف الطلب'],
        ['Correlation ID', 'معرّف الارتباط'],
        ['Setup progress', 'تقدم الإعداد'],
        ['Welcome', 'مرحباً'],
        ['Preflight', 'الفحوصات الأولية'],
        ['Database', 'قاعدة البيانات'],
        ['Provision', 'تجهيز قاعدة البيانات'],
        ['Administrator', 'مدير النظام'],
        ['Branding', 'الهوية البصرية'],
        ['Security', 'الأمان'],
        ['Integrations', 'التكاملات'],
        ['Review', 'المراجعة'],
        ['Complete', 'مكتمل'],
        ['PASS', 'ناجح'],
        ['REQUIRED', 'مطلوب'],
        ['English name', 'الاسم الإنجليزي'],
        ['Windows Authentication — GSIP process identity', 'مصادقة Windows — هوية عملية GSIP'],
        ['SQL Authentication — username + password', 'مصادقة SQL — اسم المستخدم + كلمة المرور'],
        ['API Key Header', 'مفتاح API في الترويسة'],
        ['Static Bearer', 'Bearer ثابت'],
        ['Token Endpoint', 'نقطة إصدار الرمز'],
        ['API Key + Bearer', 'مفتاح API + Bearer'],
        ['Custom Headers', 'ترويسات مخصصة'],
        ['Base URL', 'عنوان URL الأساسي'],
        ['Service Path', 'مسار الخدمة'],
        ['Method', 'الطريقة'],
        ['Username', 'اسم المستخدم'],
        ['Password', 'كلمة المرور'],
        ['READY', 'جاهز'],
        ['MISSING', 'غير مُعد'],
        ['ON', 'مفعّل'],
        ['OFF', 'متوقف'],
        ['Status', 'الحالة'],
        ['Audit activity chart', 'مخطط نشاط التدقيق'],
        ['Audit pages', 'صفحات سجل التدقيق'],
        ['Audit event detail', 'تفاصيل حدث التدقيق'],
        ['Close', 'إغلاق'],
        ['Correlation', 'الارتباط'],
        ['System', 'النظام'],
        ['Healthy', 'سليم'],
        ['Degraded', 'متدهور جزئياً'],
        ['Unhealthy', 'غير سليم'],
        ['Not configured', 'غير مُعد'],
        ['Configured', 'مُعد'],
        ['Active', 'نشط'],
        ['Disabled', 'متوقف'],
        ['Success', 'ناجح'],
        ['Succeeded', 'ناجح'],
        ['Failed', 'فشل'],
        ['Failure', 'فشل'],
        ['Pending', 'قيد الانتظار'],
        ['Running', 'قيد التنفيذ'],
        ['Cancelled', 'ملغى'],
        ['TimedOut', 'انتهت المهلة'],
        ['Timeout', 'انتهت المهلة']
    ]);

    const attributeMap = new Map([
        ['Setup progress', 'تقدم الإعداد'],
        ['Audit activity chart', 'مخطط نشاط التدقيق'],
        ['Audit pages', 'صفحات سجل التدقيق'],
        ['Audit event detail', 'تفاصيل حدث التدقيق'],
        ['Close', 'إغلاق'],
        ['Request ID / service / outcome', 'معرّف الطلب / الخدمة / النتيجة']
    ]);

    const translateExactText = (node) => {
        if (node.nodeType !== Node.TEXT_NODE) return;
        const raw = node.nodeValue;
        const trimmed = raw.trim();
        if (!trimmed || !textMap.has(trimmed)) return;
        node.nodeValue = raw.replace(trimmed, textMap.get(trimmed));
    };

    const translateElement = (element) => {
        if (!(element instanceof Element)) return;
        for (const attr of ['aria-label', 'placeholder', 'title']) {
            const value = element.getAttribute(attr);
            if (value && attributeMap.has(value)) element.setAttribute(attr, attributeMap.get(value));
        }

        for (const child of element.childNodes) {
            if (child.nodeType === Node.TEXT_NODE) translateExactText(child);
        }

        if (element.matches('.setup-message[data-operation-code] span')) {
            const container = element.closest('.setup-message');
            element.textContent = container?.classList.contains('success')
                ? 'تمت العملية بنجاح.'
                : 'تعذر إكمال العملية. استخدم رمز العملية وسجل النظام للتشخيص.';
        }
    };

    const translateTree = (root) => {
        if (root instanceof Element) translateElement(root);
        root.querySelectorAll?.('*').forEach(translateElement);
    };

    translateTree(document.documentElement);

    const observer = new MutationObserver((mutations) => {
        for (const mutation of mutations) {
            mutation.addedNodes.forEach((node) => {
                if (node instanceof Element) translateTree(node);
                else if (node.nodeType === Node.TEXT_NODE) translateExactText(node);
            });
        }
    });
    observer.observe(document.body, { childList: true, subtree: true });
})();
