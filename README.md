# Accessibility Guard

A commitment device for Android. It locks the Settings screens that could switch off your blocker app ([Freedom](https://freedom.to) by default) behind a passcode. Give the passcode to someone you trust, and you can't turn your blocker off in a weak moment.

## Install it on your phone

1. On your phone, open the [latest release](https://github.com/lekker-solutions/freedom-accessibility-guard/releases/latest) and download `AccessibilityGuard-<version>.apk`.
2. Open the downloaded file. If Android asks, allow your browser or Files app to install unknown apps.
3. If Play Protect warns about a harmful app, choose to install anyway. It flags any app that uses an accessibility service and device admin; see [Building](docs/building.md#antivirus-false-positives).

Updates install over the top in the same way, and your passcode is kept. To build it yourself instead, see [Building](docs/building.md).

## Set it up

1. Open **Accessibility Guard** and choose a 4–8 digit passcode.
2. Tap **Open Accessibility settings** and turn on *Accessibility Guard*. If the switch is greyed out ("Restricted setting"), first go to *Settings → Apps → Accessibility Guard → ⋮ → Allow restricted settings*.
3. Tap **Turn on tamper protection** and activate the device admin. This stops the app being force-stopped or uninstalled without the passcode.
4. Give the passcode to your accountability partner.

To check it works, open Settings → Accessibility. The passcode pad should cover it.

> **After every reinstall or update**, Android turns the guard's accessibility service off. Turn it back on in Accessibility settings, or see [Building](docs/building.md#reinstalling-switches-the-guard-off).

## What it locks

- Settings → **Accessibility**
- **App info** for Freedom and for Accessibility Guard itself (where Force stop and Uninstall are)
- **Device admin apps** (where tamper protection could be turned off)
- Samsung's **Reset** page (*Reset accessibility settings* would switch every accessibility service off)

Everything else in Settings works normally. **Cancel** on the pad takes you back to the previous screen.

## More

- [How it works](docs/how-it-works.md): detection rules, code layout, limitations
- [Tamper protection](docs/tamper-protection.md): device admin vs. device owner, and how to remove either
- [Building](docs/building.md): toolchain setup, antivirus, release builds
- [Testing](docs/testing.md): on-device test checklist

Tested on a Samsung Galaxy S23+ (One UI, Android 17).

## License

Copyright (C) 2026 Lekker Solutions. Licensed under the GNU General Public License v3.0 or later. See [LICENSE](LICENSE).
