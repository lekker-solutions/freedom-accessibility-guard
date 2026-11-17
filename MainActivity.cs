using Android.App;
using Android.App.Admin;
using Android.Content;
using Android.OS;
using Android.Text;
using Android.Views;
using Android.Widget;

namespace AccessibilityGuard;

/// <summary>Set or change the passcode, and see whether protection is switched on.</summary>
[Activity(Label = "@string/app_name", MainLauncher = true, Exported = true)]
public class MainActivity : Activity
{
    TextView _status = null!;
    EditText _current = null!;
    EditText _new = null!;
    EditText _confirm = null!;
    Button _save = null!;
    Button _openSettings = null!;
    Button _addAdmin = null!;
    Button _removeOwner = null!;
    EditText _protectedApp = null!;
    Button _saveApp = null!;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
        int pad = (int)(24 * Resources!.DisplayMetrics!.Density);
        root.SetPadding(pad, pad, pad, pad);

        _status = new TextView(this) { TextSize = 17 };
        _status.SetPadding(0, 0, 0, pad);
        _current = PinField("Current passcode");
        _new = PinField($"New passcode ({PasscodeStore.MinLength}–{PasscodeStore.MaxLength} digits)");
        _confirm = PinField("Confirm new passcode");

        _save = new Button(this);
        _save.Click += (_, _) => SavePasscode();

        _openSettings = new Button(this) { Text = "Open Accessibility settings" };
        _openSettings.Click += (_, _) =>
            StartActivity(new Intent(Android.Provider.Settings.ActionAccessibilitySettings));

        _addAdmin = new Button(this) { Text = "Turn on tamper protection" };
        _addAdmin.Click += (_, _) =>
        {
            var intent = new Intent(DevicePolicyManager.ActionAddDeviceAdmin);
            intent.PutExtra(DevicePolicyManager.ExtraDeviceAdmin, GuardAdminReceiver.Component(this));
            intent.PutExtra(DevicePolicyManager.ExtraAddExplanation,
                "Stops Accessibility Guard from being force-stopped, and makes uninstalling it need your passcode.");
            StartActivity(intent);
        };

        _removeOwner = new Button(this) { Text = "Remove tamper protection" };
        _removeOwner.Click += (_, _) => ConfirmRemoveOwner();

        _protectedApp = new EditText(this)
        {
            Hint = "App to also protect (package name)",
            InputType = InputTypes.ClassText
        };
        _protectedApp.Text = GuardConfig.ProtectedPackage(this);
        _saveApp = new Button(this) { Text = "Save protected app" };
        _saveApp.Click += (_, _) => SaveProtectedApp();

        foreach (var view in new View[]
                 { _status, _current, _new, _confirm, _save, _openSettings, _addAdmin, _removeOwner, _protectedApp, _saveApp })
            root.AddView(view);

        var scroll = new ScrollView(this);
        scroll.AddView(root);
        SetContentView(scroll);
    }

    protected override void OnResume()
    {
        base.OnResume();
        Refresh();
    }

    void Refresh()
    {
        bool hasPin = PasscodeStore.HasPasscode(this);
        bool enabled = IsServiceEnabled();

        _status.Text = (hasPin, enabled) switch
        {
            (false, _) => "Step 1: choose a passcode.",
            (true, false) => "Protection is OFF.\n\nTap “Open Accessibility settings”, find “Accessibility Guard” " +
                             "(it may be under “Installed apps” or “Downloaded apps”) and turn it on.",
            (true, true) => "Protection is ON.\n\nThe Accessibility section of Settings now asks for your passcode."
        };
        var dpm = (DevicePolicyManager)GetSystemService(DevicePolicyService)!;
        bool owner = dpm.IsDeviceOwnerApp(PackageName);
        bool admin = dpm.IsAdminActive(GuardAdminReceiver.Component(this));
        _status.Text += (owner, admin) switch
        {
            (true, _) => "\n\nTamper protection: ON (device owner). Force stop and uninstall are disabled; removal needs adb.",
            (false, true) => "\n\nTamper protection: ON (device admin). Force stop is disabled, and uninstalling first " +
                             "needs the admin turned off, which asks for your passcode.",
            _ => "\n\nTamper protection: OFF. The app can still be force-stopped or uninstalled."
        };

        _current.Visibility = hasPin ? ViewStates.Visible : ViewStates.Gone;
        _save.Text = hasPin ? "Change passcode" : "Save passcode";
        _openSettings.Enabled = hasPin; // don't let protection start without a passcode
        _addAdmin.Visibility = hasPin && !admin ? ViewStates.Visible : ViewStates.Gone;
        _removeOwner.Visibility = admin ? ViewStates.Visible : ViewStates.Gone;
    }

    void ConfirmRemoveOwner()
    {
        var wait = PasscodeStore.LockoutRemaining(this);
        if (wait > TimeSpan.Zero)
        {
            Toast($"Too many wrong attempts. Try again in {Math.Ceiling(wait.TotalSeconds)} s.");
            return;
        }

        var input = new EditText(this)
        {
            Hint = "Passcode",
            InputType = InputTypes.ClassNumber | InputTypes.NumberVariationPassword
        };
        new AlertDialog.Builder(this)!
            .SetTitle("Remove tamper protection?")!
            .SetMessage("Enter your passcode. This turns off device admin, so the app can be force-stopped and uninstalled again.")!
            .SetView(input)!
            .SetPositiveButton("Remove", (_, _) =>
            {
                if (PasscodeStore.Verify(this, input.Text ?? "")) RemoveOwner();
                else Toast("Wrong passcode.");
            })!
            .SetNegativeButton("Cancel", (_, _) => { })!
            .Show();
    }

    void SaveProtectedApp()
    {
        string pkg = (_protectedApp.Text ?? "").Trim();
        if (pkg.Length == 0)
        {
            Toast("Enter the app's package name, e.g. to.freedom.android2.");
            return;
        }
        GuardConfig.SetProtectedPackage(this, pkg);

        // Confirm it's installed, and show its name so you know you've got the right one.
        try
        {
            var info = PackageManager!.GetApplicationInfo(pkg, 0);
            Toast($"Now also protecting: {PackageManager!.GetApplicationLabel(info)}");
        }
        catch (Exception)
        {
            Toast("Saved, but that app isn't installed yet.");
        }
    }

    void RemoveOwner()
    {
        var dpm = (DevicePolicyManager)GetSystemService(DevicePolicyService)!;
        try
        {
            if (dpm.IsDeviceOwnerApp(PackageName))
            {
                // Lift the uninstall block, then drop device-owner. Clearing owner also
                // wipes its policies, but we unblock first so nothing is left dangling.
                dpm.SetUninstallBlocked(GuardAdminReceiver.Component(this), PackageName!, false);
                dpm.ClearDeviceOwnerApp(PackageName!);
            }
            else
            {
                dpm.RemoveActiveAdmin(GuardAdminReceiver.Component(this));
            }
            Toast("Tamper protection removed.");
        }
        catch (Java.Lang.Exception ex)
        {
            Toast("Couldn't remove: " + ex.Message);
        }
        Refresh();
    }

    void SavePasscode()
    {
        string current = _current.Text ?? "";
        string pin = _new.Text ?? "";
        string confirm = _confirm.Text ?? "";

        if (PasscodeStore.HasPasscode(this))
        {
            var wait = PasscodeStore.LockoutRemaining(this);
            if (wait > TimeSpan.Zero)
            {
                Toast($"Too many wrong attempts. Try again in {Math.Ceiling(wait.TotalSeconds)} s.");
                return;
            }
            if (!PasscodeStore.Verify(this, current))
            {
                Toast("Current passcode is wrong.");
                return;
            }
        }
        if (!PasscodeStore.IsValidFormat(pin))
        {
            Toast($"Use {PasscodeStore.MinLength}–{PasscodeStore.MaxLength} digits.");
            return;
        }
        if (pin != confirm)
        {
            Toast("The new passcodes don't match.");
            return;
        }

        PasscodeStore.Set(this, pin);
        _current.Text = _new.Text = _confirm.Text = "";
        Toast("Passcode saved.");
        Refresh();
    }

    bool IsServiceEnabled()
    {
        var enabled = Android.Provider.Settings.Secure.GetString(
            ContentResolver, Android.Provider.Settings.Secure.EnabledAccessibilityServices) ?? "";
        var me = new ComponentName(this, Java.Lang.Class.FromType(typeof(GuardService)));
        var names = new[] { me.FlattenToString(), me.FlattenToShortString() };
        return enabled.Split(':').Any(s => names.Contains(s, StringComparer.OrdinalIgnoreCase));
    }

    EditText PinField(string hint)
    {
        var field = new EditText(this)
        {
            Hint = hint,
            InputType = InputTypes.ClassNumber | InputTypes.NumberVariationPassword
        };
        field.SetFilters(new IInputFilter[] { new InputFilterLengthFilter(PasscodeStore.MaxLength) });
        return field;
    }

    void Toast(string message) =>
        Android.Widget.Toast.MakeText(this, message, ToastLength.Short)!.Show();
}
