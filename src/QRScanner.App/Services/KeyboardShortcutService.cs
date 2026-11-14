namespace QRScanner.App.Services;

public sealed class KeyboardShortcutService : IKeyboardShortcutService
{
    public event Action<int>? ShortcutPressed;

    public void Publish(int digit)
    {
        ShortcutPressed?.Invoke(digit);
    }
}
