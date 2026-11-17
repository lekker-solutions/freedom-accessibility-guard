# Building

## One-time setup

Requirements: .NET 10 SDK, a JDK 17, and the Android SDK. The commands below fetch the Android SDK for you.

```powershell
dotnet workload install android
dotnet build -t:InstallAndroidDependencies -f net10.0-android "-p:AndroidSdkDirectory=$env:LOCALAPPDATA\Android\Sdk" -p:AcceptAndroidSDKLicenses=True
```

If you set `ANDROID_HOME` (to the SDK folder) and `JAVA_HOME`, you can drop the `-p:` flags from every command.

To target .NET 9 instead, change `<TargetFramework>` in the `.csproj` to `net9.0-android`.

## Build and install

Turn on USB debugging on the phone (*Settings → About phone → Software information*, tap *Build number* 7 times, then *Developer options → USB debugging*), connect it, and accept the prompt. Then:

```powershell
dotnet build -t:Run -f net10.0-android "-p:AndroidSdkDirectory=$env:LOCALAPPDATA\Android\Sdk" "-p:JavaSdkDirectory=$env:JAVA_HOME"
```

A debug build is signed with your local debug key, so it can't install over a release from GitHub (or the other way round). Uninstall one before installing the other. Turn off tamper protection first, in the app, with the passcode.

## Releases

GitHub Actions builds every push and pull request (`.github/workflows/ci.yml`). Pushing a `v*` tag builds a signed APK and publishes it as a GitHub Release (`.github/workflows/release.yml`):

```powershell
git tag v1.1
git push origin v1.1
```

The tag sets the version name (`v1.1` gives `1.1`), and the workflow run number sets the version code. A release can also be started by hand from the Actions tab.

Signing uses three repository secrets: `ANDROID_KEYSTORE_BASE64` (the keystore file, base64), `ANDROID_KEYSTORE_PASSWORD`, and `ANDROID_KEY_ALIAS`. Every release must be signed with the same key, or updates won't install over earlier versions. Keep a backup of the keystore and its password outside the repo; if they're lost, users have to uninstall and reinstall.

To build a signed release locally:

```powershell
dotnet publish -f net10.0-android -c Release -p:AndroidKeyStore=true "-p:AndroidSigningKeyStore=<path>.keystore" -p:AndroidSigningKeyAlias=accessibilityguard "-p:AndroidSigningKeyPass=env:KEYSTORE_PASSWORD" "-p:AndroidSigningStorePass=env:KEYSTORE_PASSWORD"
```

## Antivirus false positives

An accessibility service plus a screen overlay plus device admin is exactly what Android spyware detectors look for, so expect false positives:

- **Windows Defender** may quarantine `obj\...\classes.dex` mid-build, reported as `Android/Spy.Agent`. The build then fails with *Access to the path … classes.dex is denied*. Exclude this repo's `bin` and `obj` folders.
- **Play Protect** on the phone may warn about a harmful app when you install. Choose to install anyway.

## Reinstalling switches the guard off

Each reinstall or update turns off the accessibility service without telling you. Turn it back on in Accessibility settings, or over adb. This appends to the existing list, so other services such as Freedom stay on:

```powershell
$me = 'com.lekkersolutions.accessibilityguard/com.lekkersolutions.accessibilityguard.GuardService'
$cur = "$(adb shell settings get secure enabled_accessibility_services)".Trim()
adb shell settings put secure enabled_accessibility_services "'$cur`:$me'"
adb shell settings put secure accessibility_enabled 1
```

Device admin stays active across reinstalls.

## Debugging

Each time the guard locks a screen, it logs the reason:

```powershell
adb logcat -s AccessibilityGuard
```
