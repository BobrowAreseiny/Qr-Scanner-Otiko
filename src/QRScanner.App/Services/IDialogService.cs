namespace QRScanner.App.Services;

public interface IDialogService
{
    Task ShowAlertAsync(string title, string message, string cancel = "OK");

    Task<bool> ShowConfirmationAsync(
        string title,
        string message,
        string accept = "Да",
        string cancel = "Нет");

    Task<string?> ShowActionSheetAsync(string title, string cancel, params string[] buttons);

    Task<string?> ShowPromptAsync(
        string title,
        string message,
        string placeholder,
        string? initialValue = null);
}
