using Android.Content;

namespace AccessibilityGuard;

/// <summary>Which app's App-info page the guard also protects (so it can't be force-stopped).</summary>
static class GuardConfig
{
    const string PrefsName = "guard_config";
    const string KeyPackage = "protectedPackage";

    public const string DefaultPackage = "to.freedom.android2"; // Freedom

    static ISharedPreferences P(Context c) =>
        c.GetSharedPreferences(PrefsName, FileCreationMode.Private)!;

    public static string ProtectedPackage(Context c) =>
        P(c).GetString(KeyPackage, DefaultPackage)!;

    public static void SetProtectedPackage(Context c, string pkg) =>
        P(c).Edit()!.PutString(KeyPackage, pkg.Trim())!.Commit();
}
