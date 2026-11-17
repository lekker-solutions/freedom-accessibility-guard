# Tamper protection

A force-stopped app can't restart itself, so the guard instead makes itself hard to stop. There are two levels.

## Device admin (recommended)

Works with your accounts signed in. Tap **Turn on tamper protection** in the app and activate the admin. While it's active:

- Settings greys out Accessibility Guard's **Force stop**.
- **Uninstall** needs the admin turned off first. The deactivate screen, the Device admin apps list, and the app's own App-info page are all behind the passcode.
- **Remove tamper protection** in the app turns it off after asking for the passcode.

Remaining gaps:
- Safe Mode disables third-party accessibility services.
- adb still works.
- While it's on, activating *other* apps' device admins also needs the passcode.

## Device owner (stronger)

As device owner, Android disables Force stop and Uninstall outright.

Provisioning works only when **no accounts at all** are on the device. That includes accounts added by third-party apps (WhatsApp, Signal, Microsoft Authenticator, OneDrive, and so on), not just Google and Samsung. Temporarily disabling those apps doesn't hide their accounts. In practice this means a freshly reset phone.

```bash
adb shell dpm set-device-owner com.example.accessibilityguard/.GuardAdminReceiver
```

## Removing it

- **In the app:** tap **Remove tamper protection** and enter the passcode. This works for both levels.
- **Over USB**, if the passcode is lost:
  ```bash
  adb shell dpm remove-active-admin com.example.accessibilityguard/.GuardAdminReceiver
  adb uninstall com.example.accessibilityguard
  ```

Keep the adb route as a backstop. The code never sets `DISALLOW_DEBUGGING_FEATURES`. Don't add it, or recovering would need a factory reset.
