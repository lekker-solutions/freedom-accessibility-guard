using Android.AccessibilityServices;
using Android.App;
using Android.App.Admin;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Views.Accessibility;
using Android.Views.InputMethods;

namespace AccessibilityGuard;

/// <summary>
/// Watches which screen is in front. When the Settings app shows its Accessibility
/// section (or this app's own accessibility page, which is where it could be
/// switched off), it covers the screen with a passcode pad.
///
/// After a correct passcode, Settings stays unlocked until you leave the Settings
/// app or the screen turns off.
/// </summary>
[Service(Name = "com.example.accessibilityguard.GuardService",
         Label = "@string/service_label",
         Permission = "android.permission.BIND_ACCESSIBILITY_SERVICE",
         Exported = true)]
[IntentFilter(new[] { "android.accessibilityservice.AccessibilityService" })]
[MetaData("android.accessibilityservice", Resource = "@xml/accessibility_service_config")]
public class GuardService : AccessibilityService
{
    const string AospSettings = "com.android.settings";
    const string SamsungAccessibility = "com.samsung.accessibility"; // Samsung moves this section into its own app
    static readonly HashSet<string> SettingsPackages = new() { AospSettings, SamsungAccessibility };

    // Screen titles that count as "the Accessibility section".
    readonly HashSet<string> _protectedTitles = new(StringComparer.OrdinalIgnoreCase);
    // Titles of the "Device admin apps" screen, locked only while this app is an active admin.
    readonly HashSet<string> _deviceAdminTitles = new(StringComparer.OrdinalIgnoreCase);
    // Buttons that switch every accessibility service off in one tap (Samsung's "Reset options" page),
    // as (title, summary). Both must be on screen, so a search-history entry with just the title
    // doesn't count.
    readonly List<(string Title, string Summary)> _resetItems = new()
    {
        ("Reset accessibility settings", "Reset accessibility settings to their defaults."),
        ("Reset all settings", "Reset all settings to their defaults."),
    };
    DevicePolicyManager? _dpm;
    // Windows from these packages (status bar, keyboards, this app) don't count as leaving Settings.
    readonly HashSet<string> _ignoredPackages = new();

    IWindowManager? _windowManager;
    readonly Handler _handler = new(Looper.MainLooper!);
    PinPadView? _overlay;
    ScreenOffReceiver? _screenOffReceiver;
    bool _unlocked;
    bool _onProtectedScreen;
    long _lastContentCheck;

    // The app whose App-info page is also locked (where Force stop lives).
    string? _protectedPkg;
    string? _protectedLabel;
    string _forceStopLabel = "Force stop";

    protected override void OnServiceConnected()
    {
        base.OnServiceConnected();
        _windowManager = GetSystemService(WindowService).JavaCast<IWindowManager>();
        _dpm = (DevicePolicyManager)GetSystemService(DevicePolicyService)!;
        LoadProtectedTitles();
        LoadIgnoredPackages();
        LoadForceStopLabel();
        EnsureProtectedApp();

        _screenOffReceiver = new ScreenOffReceiver(OnScreenOff);
        RegisterReceiver(_screenOffReceiver, new IntentFilter(Intent.ActionScreenOff));
    }

    public override void OnAccessibilityEvent(AccessibilityEvent? e)
    {
        if (e is null || !PasscodeStore.HasPasscode(this)) return;
        EnsureProtectedApp();

        var pkg = e.PackageName;
        if (string.IsNullOrEmpty(pkg) || _ignoredPackages.Contains(pkg)) return;
        bool isSettings = SettingsPackages.Contains(pkg);

        if (e.EventType == EventTypes.WindowStateChanged)
        {
            if (!isSettings)
            {
                // The user went somewhere else (home, recents, another app): lock again.
                _unlocked = false;
                _onProtectedScreen = false;
                HideOverlay();
                return;
            }
            _onProtectedScreen = IsProtectedScreen(pkg, e, includeEventText: true);
            UpdateOverlay();
        }
        else if (e.EventType == EventTypes.WindowContentChanged && isSettings && !_onProtectedScreen)
        {
            // Some Settings versions set the screen title a moment after the window opens.
            long now = SystemClock.UptimeMillis();
            if (now - _lastContentCheck < 250) return;
            _lastContentCheck = now;

            if (IsProtectedScreen(pkg, e, includeEventText: false))
            {
                _onProtectedScreen = true;
                UpdateOverlay();
            }
        }
    }

    public override void OnInterrupt() { }

    public override void OnDestroy()
    {
        HideOverlay();
        if (_screenOffReceiver is not null) UnregisterReceiver(_screenOffReceiver);
        base.OnDestroy();
    }

    bool IsProtectedScreen(string pkg, AccessibilityEvent? e, bool includeEventText)
    {
        if (pkg == SamsungAccessibility) return true;

        // While we're an active device admin, deactivating us (directly, or via
        // "Deactivate & uninstall") goes through the Device admin screens, so lock those too.
        bool guardAdmin = _dpm?.IsAdminActive(GuardAdminReceiver.Component(this)) == true;

        if (includeEventText && e is not null)
        {
            // e.g. com.android.settings.Settings$AccessibilitySettingsActivity
            string cls = e.ClassName ?? "";
            if (cls.Contains("Accessibility", StringComparison.OrdinalIgnoreCase)) return Hit("activity class");
            // Settings$DeviceAdminSettingsActivity, DeviceAdminAdd, Samsung's SecDeviceAdminAdd
            if (guardAdmin && cls.Contains("DeviceAdmin", StringComparison.OrdinalIgnoreCase)) return Hit("device admin class");

            // For a newly opened screen, the event text is the screen's title. Popups (e.g. the
            // recent-searches list in Settings search) also send this event, with their items as
            // text, so only trust it when the event comes from an activity.
            if (IsActivity(pkg, cls))
                foreach (var text in e.Text ?? [])
                    if (IsProtectedTitle(text?.ToString(), guardAdmin)) return Hit($"event title '{text}'");
        }

        foreach (var window in Windows ?? [])
        {
            if (window.Type == AccessibilityWindowType.Application && window.IsActive
                && IsProtectedTitle(window.Title, guardAdmin))
                return Hit($"window title '{window.Title}'");
        }

        // The protected app's App-info page (where Force stop / Uninstall live).
        if (IsProtectedAppInfoScreen()) return Hit("protected app info");

        // Any screen offering a reset of accessibility (or all) settings.
        if (IsResetScreen()) return Hit("reset button");

        return false;

        bool Hit(string why)
        {
            Android.Util.Log.Debug("AccessibilityGuard", $"Locking: {why} (pkg={pkg}, class={e?.ClassName})");
            return true;
        }
    }

    readonly Dictionary<string, bool> _isActivity = new();

    bool IsActivity(string pkg, string cls)
    {
        if (cls.Length == 0) return false;
        string key = pkg + "/" + cls;
        if (_isActivity.TryGetValue(key, out bool known)) return known;
        try
        {
            PackageManager!.GetActivityInfo(new ComponentName(pkg, cls), 0);
            known = true;
        }
        catch (Android.Content.PM.PackageManager.NameNotFoundException)
        {
            known = false;
        }
        return _isActivity[key] = known;
    }

    bool IsResetScreen()
    {
        var root = RootInActiveWindow;
        if (root is null) return false;
        try
        {
            foreach (var (title, summary) in _resetItems)
                if (NodeHasText(root, t => string.Equals(t, title, StringComparison.OrdinalIgnoreCase))
                    && NodeHasText(root, t => string.Equals(t, summary, StringComparison.OrdinalIgnoreCase)))
                    return true;
            return false;
        }
        finally
        {
            root.Recycle();
        }
    }

    /// <summary>
    /// True when the screen in front is the App-info page for the protected app.
    /// We require the app's own label AND a "Force stop" control to be present, so
    /// the Apps *list* (label, but no Force stop button) doesn't trigger a lock.
    /// </summary>
    bool IsProtectedAppInfoScreen()
    {
        var root = RootInActiveWindow;
        if (root is null) return false;
        try
        {
            // The protected app's page, and our own (where Uninstall / deactivating the admin start).
            string ownLabel = ApplicationInfo!.LoadLabel(PackageManager!);
            return NodeHasText(root, t => string.Equals(t, _protectedLabel, StringComparison.OrdinalIgnoreCase)
                                          || string.Equals(t, ownLabel, StringComparison.OrdinalIgnoreCase))
                && NodeHasText(root, t => string.Equals(t, _forceStopLabel, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            root.Recycle();
        }
    }

    static bool NodeHasText(AccessibilityNodeInfo? node, Func<string, bool> isMatch, int depth = 0)
    {
        if (node is null || depth > 30) return false;

        if (Matches(node.Text?.ToString()) || Matches(node.ContentDescription))
            return true;

        for (int i = 0; i < node.ChildCount; i++)
        {
            var child = node.GetChild(i);
            bool found = NodeHasText(child, isMatch, depth + 1);
            child?.Recycle();
            if (found) return true;
        }
        return false;

        bool Matches(string? value) => value is not null && isMatch(value.Trim());
    }

    void EnsureProtectedApp()
    {
        var pkg = GuardConfig.ProtectedPackage(this);
        if (pkg == _protectedPkg) return; // unchanged since last check

        _protectedPkg = pkg;
        _protectedLabel = null;
        try
        {
            var info = PackageManager!.GetApplicationInfo(pkg, 0);
            _protectedLabel = PackageManager!.GetApplicationLabel(info);
        }
        catch (Exception)
        {
            // App not installed or not visible; app-info locking is simply off until it is.
        }
    }

    void LoadForceStopLabel()
    {
        try
        {
            var res = PackageManager!.GetResourcesForApplication(AospSettings);
            int id = res.GetIdentifier("force_stop", "string", AospSettings);
            if (id != 0) _forceStopLabel = res.GetString(id);
        }
        catch (Exception)
        {
            // Keep the English default.
        }
    }

    bool IsProtectedTitle(string? title, bool guardAdmin) =>
        !string.IsNullOrWhiteSpace(title)
        && (_protectedTitles.Contains(title.Trim()) || (guardAdmin && _deviceAdminTitles.Contains(title.Trim())));

    void UpdateOverlay()
    {
        if (_onProtectedScreen && !_unlocked) ShowOverlay();
        else HideOverlay();
    }

    void ShowOverlay()
    {
        if (_overlay is not null || _windowManager is null) return;

        _overlay = new PinPadView(this, onUnlocked: Unlock, onCancel: Cancel);
        var lp = new WindowManagerLayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.MatchParent,
            WindowManagerTypes.AccessibilityOverlay,
            WindowManagerFlags.LayoutInScreen | WindowManagerFlags.NotFocusable,
            Format.Opaque);
        _windowManager.AddView(_overlay, lp);
    }

    void HideOverlay()
    {
        if (_overlay is null) return;
        _windowManager?.RemoveView(_overlay);
        _overlay = null;
    }

    void Unlock()
    {
        _unlocked = true;
        HideOverlay();
    }

    void Cancel()
    {
        // Go back to the previous screen. The pad stays up meanwhile: the next screen's event hides
        // it if that screen isn't protected. If Back didn't get us off a protected screen, go home.
        PerformGlobalAction(GlobalAction.Back);
        _handler.PostDelayed(() =>
        {
            if (_overlay is null) return;
            var root = RootInActiveWindow;
            string? pkg = root?.PackageName;
            root?.Recycle();
            if (pkg is not null && SettingsPackages.Contains(pkg) && !IsProtectedScreen(pkg, null, includeEventText: false))
            {
                _onProtectedScreen = false;
                UpdateOverlay();
                return;
            }
            HideOverlay();
            PerformGlobalAction(GlobalAction.Home);
        }, 800);
    }

    void OnScreenOff()
    {
        _unlocked = false;
        UpdateOverlay(); // if Settings is still on the Accessibility screen, it's covered again on wake
    }

    void LoadProtectedTitles()
    {
        _protectedTitles.Add("Accessibility");
        _protectedTitles.Add(GetString(Resource.String.service_label));
        _deviceAdminTitles.Add("Device admin apps");

        // The Settings app's own labels for these sections, so this works in any language.
        try
        {
            var res = PackageManager!.GetResourcesForApplication(AospSettings);
            int id = res.GetIdentifier("accessibility_settings", "string", AospSettings);
            if (id != 0) _protectedTitles.Add(res.GetString(id));
            id = res.GetIdentifier("manage_device_admin", "string", AospSettings);
            if (id != 0) _deviceAdminTitles.Add(res.GetString(id));
            foreach (var (titleName, summaryName) in new[]
                     {
                         ("sec_reset_accessibility_title", "sec_reset_accessibility_summary"),
                         ("sec_settings_reset_title", "sec_settings_reset_list_summary"),
                     })
            {
                int titleId = res.GetIdentifier(titleName, "string", AospSettings);
                int summaryId = res.GetIdentifier(summaryName, "string", AospSettings);
                if (titleId != 0 && summaryId != 0)
                    _resetItems.Add((res.GetString(titleId), res.GetString(summaryId)));
            }
        }
        catch (Exception)
        {
            // Settings resources not readable on this device; fall back to the English title.
        }
    }

    void LoadIgnoredPackages()
    {
        _ignoredPackages.Add(PackageName!);
        _ignoredPackages.Add("com.android.systemui");

        var imm = GetSystemService(InputMethodService).JavaCast<InputMethodManager>();
        if (imm?.EnabledInputMethodList is { } keyboards)
            foreach (var keyboard in keyboards)
                if (keyboard.PackageName is { } name) _ignoredPackages.Add(name);
    }

    class ScreenOffReceiver : BroadcastReceiver
    {
        readonly System.Action _onScreenOff;
        public ScreenOffReceiver(System.Action onScreenOff) => _onScreenOff = onScreenOff;
        public override void OnReceive(Context? context, Intent? intent) => _onScreenOff();
    }
}
