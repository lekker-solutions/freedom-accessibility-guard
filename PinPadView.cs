using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Widget;

namespace AccessibilityGuard;

/// <summary>
/// Full-screen, opaque passcode pad drawn as an accessibility overlay on top of Settings.
/// It has its own number keys, so it never needs the on-screen keyboard.
/// </summary>
class PinPadView : LinearLayout
{
    readonly Action _onUnlocked;
    readonly Action _onCancel;
    readonly TextView _dots;
    readonly TextView _message;
    string _entry = "";

    public PinPadView(Context context, Action onUnlocked, Action onCancel) : base(context)
    {
        _onUnlocked = onUnlocked;
        _onCancel = onCancel;

        Orientation = Orientation.Vertical;
        SetGravity(GravityFlags.Center);
        SetBackgroundColor(Color.ParseColor("#121212"));
        int pad = Dp(24);
        SetPadding(pad, pad, pad, pad);
        Clickable = true; // swallow every touch so nothing reaches Settings underneath

        AddView(Label("Accessibility settings are locked", 22, Color.White));
        AddView(Label("Enter your passcode to continue", 15, Color.LightGray));

        _dots = Label("", 30, Color.White);
        _dots.SetMinHeight(Dp(56));
        AddView(_dots);

        _message = Label("", 14, Color.ParseColor("#FF8A80"));
        _message.SetMinHeight(Dp(24));
        AddView(_message);

        var grid = new GridLayout(context) { ColumnCount = 3 };
        foreach (var key in new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "⌫", "0", "OK" })
            grid.AddView(KeyButton(key));
        AddView(grid, new LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent));

        var cancel = new Button(context) { Text = "Cancel" };
        cancel.Click += (_, _) => _onCancel();
        var cancelParams = new LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent)
        {
            TopMargin = Dp(16)
        };
        AddView(cancel, cancelParams);
    }

    Button KeyButton(string key)
    {
        var button = new Button(Context) { Text = key, TextSize = 22 };
        button.Click += (_, _) => OnKey(key);
        var lp = new GridLayout.LayoutParams { Width = Dp(80), Height = Dp(72) };
        lp.SetMargins(Dp(6), Dp(6), Dp(6), Dp(6));
        button.LayoutParameters = lp;
        return button;
    }

    void OnKey(string key)
    {
        switch (key)
        {
            case "⌫":
                if (_entry.Length > 0) _entry = _entry[..^1];
                break;
            case "OK":
                Submit();
                return;
            default:
                if (_entry.Length < PasscodeStore.MaxLength) _entry += key;
                _message.Text = "";
                break;
        }
        _dots.Text = new string('●', _entry.Length);
    }

    void Submit()
    {
        var ctx = Context!;
        var wait = PasscodeStore.LockoutRemaining(ctx);
        if (wait > TimeSpan.Zero)
        {
            _message.Text = $"Too many wrong attempts. Try again in {Math.Ceiling(wait.TotalSeconds)} s.";
        }
        else if (PasscodeStore.Verify(ctx, _entry))
        {
            _onUnlocked();
        }
        else
        {
            wait = PasscodeStore.LockoutRemaining(ctx);
            _message.Text = wait > TimeSpan.Zero
                ? $"Wrong passcode. Locked for {Math.Ceiling(wait.TotalSeconds)} s."
                : "Wrong passcode.";
        }
        _entry = "";
        _dots.Text = "";
    }

    TextView Label(string text, float sizeSp, Color color)
    {
        var tv = new TextView(Context) { Text = text, TextSize = sizeSp, Gravity = GravityFlags.Center };
        tv.SetTextColor(color);
        tv.SetPadding(0, Dp(4), 0, Dp(4));
        return tv;
    }

    int Dp(int dp) => (int)(dp * Context!.Resources!.DisplayMetrics!.Density + 0.5f);
}
