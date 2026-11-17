# Testing

Run through this on the phone after a change. `adb logcat -s AccessibilityGuard` shows why each screen locked.

- [ ] Settings → Accessibility: the pad appears. A wrong code is rejected, and the right code opens the page.
- [ ] Home, then reopen Accessibility: locked again. Screen off and on: locked again.
- [ ] Search Settings for "Accessibility Guard" and open the result: locked.
- [ ] Open Settings search with old "accessibility" entries in the history: **not** locked.
- [ ] Apps → Freedom and Apps → Accessibility Guard: locked.
- [ ] The Apps list and any other app's page: **not** locked.
- [ ] Accessibility Guard's Force stop is greyed out. Uninstall can't be reached without the passcode.
- [ ] Device admin apps: locked.
- [ ] General management → Reset: locked.
- [ ] Cancel on each locked screen returns to the screen before it.
- [ ] Change the device language: the locks still work.
