using System.Security.Cryptography;
using System.Text;
using Android.Content;

namespace AccessibilityGuard;

/// <summary>
/// Stores the passcode as a salted PBKDF2 hash (never in plain text) and
/// slows down guessing with a growing lockout after repeated wrong attempts.
/// </summary>
static class PasscodeStore
{
    public const int MinLength = 4;
    public const int MaxLength = 8;

    const int Iterations = 50_000;
    const int FreeAttempts = 5;
    static readonly TimeSpan LockoutStep = TimeSpan.FromSeconds(30);

    const string KeySalt = "salt";
    const string KeyHash = "hash";
    const string KeyFailed = "failed";
    const string KeyLockedUntil = "lockedUntil";

    static ISharedPreferences Prefs(Context c) =>
        c.GetSharedPreferences("passcode", FileCreationMode.Private)!;

    static long NowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public static bool HasPasscode(Context c) => Prefs(c).Contains(KeyHash);

    public static bool IsValidFormat(string pin) =>
        pin.Length >= MinLength && pin.Length <= MaxLength && pin.All(char.IsAsciiDigit);

    public static void Set(Context c, string pin)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        Prefs(c).Edit()!
            .PutString(KeySalt, Convert.ToBase64String(salt))!
            .PutString(KeyHash, Convert.ToBase64String(Hash(pin, salt)))!
            .PutInt(KeyFailed, 0)!
            .PutLong(KeyLockedUntil, 0)!
            .Commit();
    }

    public static TimeSpan LockoutRemaining(Context c)
    {
        long until = Prefs(c).GetLong(KeyLockedUntil, 0);
        long now = NowMs;
        return until > now ? TimeSpan.FromMilliseconds(until - now) : TimeSpan.Zero;
    }

    /// <summary>Checks the passcode. Always false while locked out.</summary>
    public static bool Verify(Context c, string pin)
    {
        if (LockoutRemaining(c) > TimeSpan.Zero) return false;

        var prefs = Prefs(c);
        var saltB64 = prefs.GetString(KeySalt, null);
        var hashB64 = prefs.GetString(KeyHash, null);
        if (saltB64 is null || hashB64 is null) return false;

        bool ok = CryptographicOperations.FixedTimeEquals(
            Hash(pin, Convert.FromBase64String(saltB64)),
            Convert.FromBase64String(hashB64));

        var edit = prefs.Edit()!;
        if (ok)
        {
            edit.PutInt(KeyFailed, 0);
        }
        else
        {
            int failed = prefs.GetInt(KeyFailed, 0) + 1;
            edit.PutInt(KeyFailed, failed);
            if (failed >= FreeAttempts)
            {
                // 30 s after the 5th miss, 60 s after the 6th, 90 s after the 7th...
                var wait = LockoutStep * (failed - FreeAttempts + 1);
                edit.PutLong(KeyLockedUntil, NowMs + (long)wait.TotalMilliseconds);
            }
        }
        edit.Commit();
        return ok;
    }

    static byte[] Hash(string pin, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(pin), salt, Iterations, HashAlgorithmName.SHA256, 32);
}
