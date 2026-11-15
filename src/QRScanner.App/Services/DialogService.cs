namespace QRScanner.App.Services;

public sealed class DialogService : IDialogService
{
    public Task ShowAlertAsync(string title, string message, string cancel = "OK")
    {
        return Shell.Current.DisplayAlertAsync(title, message, cancel);
    }

    public Task<bool> ShowConfirmationAsync(
        string title,
        string message,
        string accept = "Да",
        string cancel = "Нет")
    {
        return Shell.Current.DisplayAlertAsync(title, message, accept, cancel);
    }

    public Task<string?> ShowActionSheetAsync(string title, string cancel, params string[] buttons)
    {
        return Shell.Current.DisplayActionSheetAsync(title, cancel, null, buttons);
    }

    public Task<string?> ShowPromptAsync(
        string title,
        string message,
        string placeholder,
        string? initialValue = null)
    {
        return Shell.Current.DisplayPromptAsync(
            title,
            message,
            "OK",
            "Отмена",
            placeholder,
            initialValue: initialValue,
            keyboard: Keyboard.Text);
    }
}
