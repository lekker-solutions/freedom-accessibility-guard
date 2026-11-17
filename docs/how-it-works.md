# How it works

An accessibility service watches which screen is in front. When it's a protected Settings screen, it covers the screen with a full-screen passcode pad. After the right passcode, Settings stays unlocked until you leave the Settings app or the screen turns off.

## What gets locked

| Screen | How it's detected |
| --- | --- |
| Settings → **Accessibility**, including search results that open it and Samsung's separate `com.samsung.accessibility` app | Activity class name, or the screen title read from the Settings app (so other languages work) |
| **App info** of the protected app (Freedom) and of Accessibility Guard itself, where Force stop and Uninstall are | The app's name and a "Force stop" button on the same screen. The Apps list and other apps' pages aren't locked. |
| **Device admin apps** list and the activate/deactivate screen, while Accessibility Guard is an active admin | Activity class contains `DeviceAdmin` (AOSP `DeviceAdminAdd`, Samsung `SecDeviceAdminAdd`), or the "Device admin apps" title |
| Samsung **General management → Reset** (*Reset accessibility settings*, *Reset all settings*, and the rest of that page, including Factory data reset) | A reset button's title and its description line both on screen, so a search-history entry with just the title doesn't trigger it |

The protected app defaults to `to.freedom.android2` and can be changed on the setup screen.

## The pad

It has its own number keys, so no keyboard is needed. **Cancel** and **Back** return to the previous screen. The pad stays up until the new screen is confirmed unlocked. If that doesn't leave the locked screen within a moment, Cancel goes Home.

The passcode is 4–8 digits, stored only as a salted PBKDF2 hash. After 5 wrong tries, a lockout starts that grows by 30 s with each further miss.

## Code

| File | Role |
| --- | --- |
| `GuardService.cs` | The accessibility service: screen detection, showing and hiding the pad, Cancel handling |
| `PinPadView.cs` | The passcode pad overlay |
| `PasscodeStore.cs` | Passcode hash and lockout |
| `GuardConfig.cs` | Which app's App-info page is protected |
| `GuardAdminReceiver.cs` | Device-admin receiver. As device owner, it also blocks its own uninstall. |
| `MainActivity.cs` | Setup screen: passcode, Accessibility settings shortcut, tamper protection on/off, protected app |

## Limitations

- **Detection is heuristic.** It relies on activity names, screen titles, and button labels in Settings. It's tuned for Samsung's One UI. Other OEM Settings apps may need extra names in `GuardService` (`_protectedTitles`, `_deviceAdminTitles`, `_resetItems`, `_forceStopLabel`). Two-pane tablet layouts may not be detected.
- **Brief flash.** A locked screen can be visible for a split second before the pad covers it.
- **No foreground service.** The accessibility framework rebinds the service after a reboot or a low-memory kill. Aggressive OEM battery managers could still kill it.
- **Safe Mode** disables third-party accessibility services. See [Tamper protection](tamper-protection.md) for the other remaining gaps.
- `QUERY_ALL_PACKAGES` is declared so a user-chosen protected app's name resolves. If you only ever protect Freedom, the `<queries>` entry is enough.