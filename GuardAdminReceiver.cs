using Android.App;
using Android.App.Admin;
using Android.Content;

namespace AccessibilityGuard;

/// <summary>
/// Device-admin / device-owner receiver. When the app is provisioned as device
/// owner over adb, Android disables its Force stop and Uninstall buttons in
/// Settings, so the only way to remove it is adb (or a factory reset).
/// </summary>
[BroadcastReceiver(Name = "com.lekkersolutions.accessibilityguard.GuardAdminReceiver",
    Permission = "android.permission.BIND_DEVICE_ADMIN", Exported = true)]
[MetaData("android.app.device_admin", Resource = "@xml/device_admin")]
[IntentFilter(new[] { "android.app.action.DEVICE_ADMIN_ENABLED" })]
public class GuardAdminReceiver : DeviceAdminReceiver
{
    public static ComponentName Component(Context c) =>
        new(c, Java.Lang.Class.FromType(typeof(GuardAdminReceiver)));

    public override void OnEnabled(Context context, Intent intent)
    {
        // As device owner, block our own uninstall so removal needs adb.
        var dpm = (DevicePolicyManager)context.GetSystemService(Context.DevicePolicyService)!;
        if (dpm.IsDeviceOwnerApp(context.PackageName))
            dpm.SetUninstallBlocked(Component(context), context.PackageName!, true);
    }

    public override Java.Lang.ICharSequence? OnDisableRequestedFormatted(Context context, Intent intent) =>
        new Java.Lang.String("Removing Accessibility Guard turns off the accessibility lock.");
}
