namespace QRScanner.App.Services;

public interface IKeyboardShortcutService
{
    event Action<int>? ShortcutPressed;

    void Publish(int digit);
}
