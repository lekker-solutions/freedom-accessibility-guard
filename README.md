# Accessibility Guard (.NET for Android)

A personal commitment device. It locks the parts of Android Settings that could switch off a blocker app (by default [Freedom](https://freedom.to), `to.freedom.android2`) behind a passcode. Give the passcode to someone you trust, and you can't disable your blocker in a weak moment. The rest of Settings works normally.

Tested on a Samsung Galaxy S23+ (One UI, Android 17), using device-admin tamper protection.

## What gets locked

| Screen | How it's detected |
| --- | --- |
| Settings → **Accessibility**, including search results that open it and Samsung's separate `com.samsung.accessibility` app | Activity class name, or the screen title read from the Settings app (so other languages work) |
| **App info** of the protected app (Freedom) and of Accessibility Guard itself, where Force stop and Uninstall are | The app's name and a "Force stop" button on the same screen. The Apps list and other apps' pages aren't locked. |
| **Device admin apps** list and the activate/deactivate screen, while Accessibility Guard is an active admin | Activity class contains `DeviceAdmin` (AOSP `DeviceAdminAdd`, Samsung `SecDeviceAdminAdd`), or the "Device admin apps" title |
| Samsung **General management → Reset** (*Reset accessibility settings*, *Reset all settings*, and the rest of that page, including Factory data reset) | A reset button's title and its description line both on screen, so a search-history entry with just the title doesn't trigger it |

The lock is a full-screen pad with its own number keys. **Cancel** and **Back** return to the previous screen. If that doesn't leave the locked screen within a moment, Cancel goes Home. After the right passcode, Settings stays unlocked until you leave the Settings app or the screen turns off.

## Code

- `GuardService.cs`: the accessibility service. It watches the foreground screen and shows or hides the pad. Each lock logs its reason under the logcat tag `AccessibilityGuard` (`adb logcat -s AccessibilityGuard`).
- `PinPadView.cs`: the passcode pad overlay.
- `PasscodeStore.cs`: a salted PBKDF2 hash of the 4–8 digit passcode. After 5 wrong tries, a lockout starts that grows by 30 s with each further miss.
- `GuardConfig.cs`: which app's App-info page is protected (default `to.freedom.android2`).
- `GuardAdminReceiver.cs`: the device-admin receiver. As device owner, it also blocks its own uninstall.
- `MainActivity.cs`: setup screen. Set or change the passcode, open Accessibility settings, turn tamper protection on, remove it (needs the passcode), and choose the protected app.

## Build and install

Requirements: .NET 10 SDK, the Android workload, a JDK 17, and the Android SDK.

```powershell
dotnet workload install android
# One-time: fetch the Android SDK into a folder of your choice
dotnet build -t:InstallAndroidDependencies -f net10.0-android "-p:AndroidSdkDirectory=$env:LOCALAPPDATA\Android\Sdk" -p:AcceptAndroidSDKLicenses=True

# Build and install on the USB-connected phone (or set ANDROID_HOME and JAVA_HOME and drop the -p flags)
dotnet build -t:Run -f net10.0-android "-p:AndroidSdkDirectory=$env:LOCALAPPDATA\Android\Sdk" "-p:JavaSdkDirectory=$env:JAVA_HOME"

# Release APK (signing isn't set up yet)
dotnet publish -f net10.0-android -c Release
```

**Antivirus false positive.** An accessibility service plus a screen overlay plus device admin is exactly what Android spyware detectors look for. On Windows, Defender may quarantine `obj\...\classes.dex` mid-build (reported as `Android/Spy.Agent`, with the build failing with *Access to the path … classes.dex is denied*). Exclude this repo's `bin` and `obj` folders. On the phone, Play Protect may warn when you install. Choose to install anyway.

**Reinstalling switches the guard off.** Each reinstall or update turns off the accessibility service without telling you. Turn it back on in Accessibility settings, or over adb. This appends to the existing list, so other services such as Freedom stay on:

```powershell
$me = 'com.example.accessibilityguard/com.example.accessibilityguard.GuardService'
$cur = "$(adb shell settings get secure enabled_accessibility_services)".Trim()
adb shell settings put secure enabled_accessibility_services "'$cur`:$me'"
adb shell settings put secure accessibility_enabled 1
```

## First run on the phone

1. Open **Accessibility Guard** and set a passcode.
2. Tap **Open Accessibility settings**, find *Accessibility Guard* (often under "Installed apps"), and turn it on. If the switch is greyed out ("Restricted setting", Android 13+ for sideloaded apps), go to *Settings → Apps → Accessibility Guard → ⋮ → Allow restricted settings* first.
3. Tap **Turn on tamper protection** and activate the device admin.
4. Give the passcode to your accountability partner.

## Test checklist

- Settings → Accessibility: the pad appears. A wrong code is rejected, and the right code opens the page.
- Home, then reopen Accessibility: locked again. Screen off and on: locked again.
- Search Settings for "Accessibility Guard" and open the result: locked. Opening search with old "accessibility" entries in the history: **not** locked.
- Apps → Freedom and Apps → Accessibility Guard: locked. The Apps list and any other app's page: **not** locked.
- Accessibility Guard's Force stop is greyed out. Uninstall can't be reached without the passcode.
- Device admin apps: locked. General management → Reset: locked.
- Cancel on each locked screen returns to the screen before it.
- Change the device language: the locks still work.

## Tamper protection

A force-stopped app can't restart itself, so the guard instead makes itself hard to stop.

### Device admin (recommended, works with accounts signed in)

Tap **Turn on tamper protection** in the app. While it's active:

- Settings greys out Accessibility Guard's **Force stop**.
- **Uninstall** needs the admin turned off first. The deactivate screen, the Device admin apps list, and the app's own App-info page are all behind the passcode.
- **Remove tamper protection** in the app turns it off after asking for the passcode.

Remaining gaps: Safe Mode disables third-party accessibility services, and adb (`adb shell dpm remove-active-admin com.example.accessibilityguard/.GuardAdminReceiver`) still works. While it's on, activating *other* apps' device admins also needs the passcode.

### Device owner (stronger, needs a phone with zero accounts)

As device owner, Android disables Force stop and Uninstall outright. Provisioning works only when **no accounts at all** are on the device. That includes accounts added by third-party apps (WhatsApp, Signal, Microsoft Authenticator, OneDrive, and so on), not just Google and Samsung. Temporarily disabling those apps doesn't hide their accounts. In practice this means a freshly reset phone.

```bash
adb shell dpm set-device-owner com.example.accessibilityguard/.GuardAdminReceiver
```

Remove it with **Remove tamper protection** in the app (passcode), or over USB:

```bash
adb shell dpm remove-active-admin com.example.accessibilityguard/.GuardAdminReceiver
adb uninstall com.example.accessibilityguard
```

Keep the adb route as a backstop in case you forget the passcode. The code never sets `DISALLOW_DEBUGGING_FEATURES`. Don't add it, or recovering would need a factory reset.

## Limitations

- **Detection is heuristic.** It relies on activity names, screen titles, and button labels in Settings. Other OEM Settings apps (non-Samsung) may need extra names in `GuardService` (`_protectedTitles`, `_deviceAdminTitles`, `_resetItems`, `_forceStopLabel`). Two-pane tablet layouts may not be detected.
- **Brief flash.** A locked screen can be visible for a split second before the pad covers it.
- **No foreground service.** The accessibility framework rebinds the service after a reboot or a low-memory kill. Aggressive OEM battery managers could still kill it.
- `QUERY_ALL_PACKAGES` is declared so a user-chosen protected app's name resolves. If you only ever protect Freedom, the `<queries>` entry is enough.
- `applicationId` is still `com.example.accessibilityguard`, and release signing isn't set up.

## License

Copyright (C) 2026 Lekker Solutions

This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. See [LICENSE](LICENSE).
