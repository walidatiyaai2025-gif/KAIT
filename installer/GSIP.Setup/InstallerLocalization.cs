using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace GSIP.Setup;

internal static partial class InstallerLocalization
{
    private static bool _translating;

    private static readonly (string English, string Arabic)[] Replacements =
    [
        ("Government Services Integration Portal Setup", "إعداد بوابة تكامل الخدمات الحكومية"),
        ("Welcome to GSIP Setup", "مرحباً بك في إعداد GSIP"),
        ("Prerequisites", "المتطلبات المسبقة"),
        ("Installation and IIS application pool", "التثبيت ومجموعة تطبيق IIS"),
        ("IIS binding and HTTPS certificate", "ربط IIS وشهادة HTTPS"),
        ("Ready to install", "جاهز للتثبيت"),
        ("Installation complete", "اكتمل التثبيت"),
        ("Refresh certificates", "تحديث الشهادات"),
        ("Launch First-Run Setup when I click Finish", "تشغيل إعداد المرة الأولى عند الضغط على إنهاء"),
        ("Install path", "مسار التثبيت"),
        ("IIS site name", "اسم موقع IIS"),
        ("Application pool", "مجموعة التطبيقات"),
        ("App Pool identity", "هوية مجموعة التطبيقات"),
        ("Protocol", "البروتوكول"),
        ("Port", "المنفذ"),
        ("Host name (optional)", "اسم المضيف (اختياري)"),
        ("HTTPS certificate", "شهادة HTTPS"),
        ("No certificate selected", "لم يتم اختيار شهادة"),
        ("Not required for HTTP", "غير مطلوب عند استخدام HTTP"),
        ("Back", "السابق"),
        ("Next", "التالي"),
        ("Cancel", "إلغاء"),
        ("Install", "تثبيت"),
        ("Finish", "إنهاء"),
        ("OK", "موافق"),
        ("Retry", "إعادة المحاولة"),
        ("Close", "إغلاق"),
        ("Prerequisite check passed.", "تم اجتياز فحص المتطلبات المسبقة."),
        ("Prerequisite check found blocking items:", "كشف فحص المتطلبات المسبقة عن عناصر مانعة:"),
        ("IIS Web Server / appcmd.exe detected", "تم اكتشاف IIS Web Server / appcmd.exe"),
        ("ASP.NET Core Module V2 detected", "تم اكتشاف ASP.NET Core Module V2"),
        ("Administrator elevation active", "صلاحيات المسؤول مفعلة"),
        ("ApplicationPoolIdentity is the supported least-privilege identity.", "ApplicationPoolIdentity هي هوية أقل الصلاحيات المدعومة."),
        ("The installer grants this pool Modify access only to app/App_Data.", "يمنح المثبت مجموعة التطبيقات صلاحية التعديل على app/App_Data فقط."),
        ("HTTPS certificates are read from Local Computer / Personal and must be currently valid with an accessible private key.", "تُقرأ شهادات HTTPS من Local Computer / Personal ويجب أن تكون سارية وبها مفتاح خاص متاح."),
        ("No private key or certificate file is copied into the installer or log.", "لا يتم نسخ أي مفتاح خاص أو ملف شهادة إلى المثبت أو السجل."),
        ("Protected runtime state under App_Data is preserved across install/upgrade/repair/default uninstall.", "يتم الحفاظ على حالة التشغيل المحمية داخل App_Data أثناء التثبيت والترقية والإصلاح وإلغاء التثبيت الافتراضي."),
        ("The application package does not contain a development database or runtime secrets.", "لا تحتوي حزمة التطبيق على قاعدة بيانات تطوير أو أسرار تشغيل."),
        ("Click Install to deploy files, configure the application pool, ACL and IIS binding, then verify the installation.", "اضغط تثبيت لنشر الملفات وإعداد مجموعة التطبيقات وACL وربط IIS ثم التحقق من التثبيت."),
        ("GSIP application files, IIS application pool, binding and mutable-state ACL were configured and verified.", "تم إعداد ملفات تطبيق GSIP ومجموعة تطبيق IIS والربط وصلاحيات ACL للحالة القابلة للتغيير والتحقق منها."),
        ("First-Run Setup configures SQL Server, the initial administrator, security baseline and service integration settings.", "يقوم إعداد المرة الأولى بضبط SQL Server ومدير النظام الأول وإعدادات الأمان وتكامل الخدمات."),
        ("This wizard installs Government Services Integration Portal", "يقوم هذا المعالج بتثبيت بوابة تكامل الخدمات الحكومية"),
        ("Database and government-service credentials are not collected by this installer.", "لا يجمع هذا المثبت بيانات اعتماد قاعدة البيانات أو الخدمات الحكومية."),
        ("After deployment, GSIP opens its protected First-Run Setup at /setup for SQL Server, administrator, security and integration configuration.", "بعد النشر، يفتح GSIP إعداد المرة الأولى المحمي على /setup لإعداد SQL Server ومدير النظام والأمان والتكامل."),
        ("A sanitized installer log is written to:", "يتم حفظ سجل تثبيت منقح في:"),
        ("Install path:", "مسار التثبيت:"),
        ("IIS site:", "موقع IIS:"),
        ("Application pool:", "مجموعة التطبيقات:"),
        ("Binding:", "الربط:"),
        ("Certificate:", "الشهادة:"),
        ("First-Run Setup:", "إعداد المرة الأولى:"),
        ("Sanitized installer log:", "سجل التثبيت المنقح:"),
        ("Install path is required.", "مسار التثبيت مطلوب."),
        ("IIS site name is required.", "اسم موقع IIS مطلوب."),
        ("Application pool name is required.", "اسم مجموعة التطبيقات مطلوب."),
        ("Select a valid Local Computer HTTPS certificate before continuing.", "اختر شهادة HTTPS صالحة من Local Computer قبل المتابعة."),
        ("Application deployment failed. Review the sanitized installer log and Windows event logs.", "فشل نشر التطبيق. راجع سجل التثبيت المنقح وسجلات أحداث Windows."),
        ("Installation verification failed: GSIP.Web.dll is missing.", "فشل التحقق من التثبيت: الملف GSIP.Web.dll غير موجود."),
        ("Installation verification failed: requested IIS binding was not found.", "فشل التحقق من التثبيت: لم يتم العثور على ربط IIS المطلوب.")
    ];

    public static bool IsArabic => CultureInfo.CurrentUICulture.Name.StartsWith("ar", StringComparison.OrdinalIgnoreCase)
        || CultureInfo.CurrentCulture.Name.StartsWith("ar", StringComparison.OrdinalIgnoreCase);

    public static string T(string english, string arabic) => IsArabic ? arabic : english;

    public static void Attach(Form form)
    {
        if (!IsArabic) return;

        InstallerDialogLocalizationHook.EnsureInstalled();
        form.RightToLeft = RightToLeft.Yes;
        form.RightToLeftLayout = true;
        AttachRecursive(form);
    }

    internal static string TranslateForUser(string text)
    {
        if (!IsArabic || string.IsNullOrWhiteSpace(text)) return text;
        var result = StepRegex().Replace(text, match => $"الخطوة {match.Groups[1].Value} من 6");
        foreach (var (english, arabic) in Replacements)
            result = result.Replace(english, arabic, StringComparison.Ordinal);
        return result;
    }

    private static void AttachRecursive(Control control)
    {
        TranslateControl(control);
        control.ControlAdded -= HandleControlAdded;
        control.ControlAdded += HandleControlAdded;
        control.TextChanged -= HandleTextChanged;
        control.TextChanged += HandleTextChanged;
        foreach (Control child in control.Controls)
            AttachRecursive(child);
    }

    private static void HandleControlAdded(object? sender, ControlEventArgs args)
    {
        if (args.Control is { } control)
            AttachRecursive(control);
    }

    private static void HandleTextChanged(object? sender, EventArgs args)
    {
        if (sender is Control control) TranslateControl(control);
    }

    private static void TranslateControl(Control control)
    {
        if (_translating || !IsArabic || string.IsNullOrWhiteSpace(control.Text)) return;
        var translated = TranslateForUser(control.Text);
        if (string.Equals(translated, control.Text, StringComparison.Ordinal)) return;

        try
        {
            _translating = true;
            control.Text = translated;
        }
        finally
        {
            _translating = false;
        }
    }

    [GeneratedRegex("^Step ([1-6]) of 6$", RegexOptions.CultureInvariant)]
    private static partial Regex StepRegex();
}
