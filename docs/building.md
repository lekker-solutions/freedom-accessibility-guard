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

```powershell
# Debug build, installed on the USB-connected phone
dotnet build -t:Run -f net10.0-android "-p:AndroidSdkDirectory=$env:LOCALAPPDATA\Android\Sdk" "-p:JavaSdkDirectory=$env:JAVA_HOME"

# Release APK (signing isn't set up yet)
dotnet publish -f net10.0-android -c Release
```

## Antivirus false positives

An accessibility service plus a screen overlay plus device admin is exactly what Android spyware detectors look for, so expect false positives:

- **Windows Defender** may quarantine `obj\...\classes.dex` mid-build, reported as `Android/Spy.Agent`. The build then fails with *Access to the path … classes.dex is denied*. Exclude this repo's `bin` and `obj` folders.
- **Play Protect** on the phone may warn about a harmful app when you install. Choose to install anyway.

## Reinstalling switches the guard off

Each reinstall or update turns off the accessibility service without telling you. Turn it back on in Accessibility settings, or over adb. This appends to the existing list, so other services such as Freedom stay on:

```powershell
$me = 'com.example.accessibilityguard/com.example.accessibilityguard.GuardService'
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
