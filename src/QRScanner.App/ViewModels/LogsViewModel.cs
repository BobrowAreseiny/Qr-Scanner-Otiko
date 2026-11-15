using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using QRScanner.App.Services;

namespace QRScanner.App.ViewModels;

public sealed class LogFileItem
{
    public string Path { get; init; } = string.Empty;

    public string DateText { get; init; } = string.Empty;

    public string SizeText { get; init; } = string.Empty;

    public string FileName => System.IO.Path.GetFileName(Path);
}

public sealed partial class LogsViewModel : ObservableObject
{
    private readonly ILogService _logService;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;

    public LogsViewModel(
        ILogService logService,
        INavigationService navigation,
        IDialogService dialogs)
    {
        _logService = logService;
        _navigation = navigation;
        _dialogs = dialogs;

        ShareCommand = new AsyncRelayCommand(ShareAsync);
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        BackCommand = new AsyncRelayCommand(_navigation.GoBackAsync);
    }

    public ObservableCollection<LogFileItem> Files { get; } = new();

    public IAsyncRelayCommand ShareCommand { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand BackCommand { get; }

    [ObservableProperty]
    private string latestLogText = string.Empty;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    public async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var files = _logService.GetFiles();
            Files.Clear();

            foreach (var file in files)
            {
                Files.Add(new LogFileItem
                {
                    Path = file.Path,
                    DateText = file.Date.ToString("dd.MM.yyyy"),
                    SizeText = FormatSize(file.SizeBytes)
                });
            }

            LatestLogText = await _logService.ReadLatestAsync();
            StatusMessage = Files.Count == 0
                ? "Логи отсутствуют."
                : $"Файлов: {Files.Count}. Каталог: {_logService.DirectoryPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка чтения логов: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ShareAsync()
    {
        try
        {
            var archive = await _logService.CreateArchiveAsync();
            if (archive is null)
            {
                StatusMessage = "Логи отсутствуют.";
                return;
            }

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Логи QRScanner",
                File = new ShareFile(archive)
            });

            StatusMessage = $"Архив логов подготовлен: {System.IO.Path.GetFileName(archive)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка отправки логов: {ex.Message}";
            await _dialogs.ShowAlertAsync("Логи", StatusMessage);
        }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} Б";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024.0:0.#} КБ";
        }

        return $"{bytes / (1024.0 * 1024.0):0.#} МБ";
    }
}
