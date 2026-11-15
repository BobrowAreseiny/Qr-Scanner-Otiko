using Microsoft.Extensions.Logging;
using QRScanner.App.Data;
using QRScanner.App.Services;
using QRScanner.App.ViewModels;
using QRScanner.App.Views;

namespace QRScanner.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddSingleton<INavigationService, NavigationService>();
		builder.Services.AddSingleton<IDialogService, DialogService>();
		builder.Services.AddSingleton<IKeyboardShortcutService, KeyboardShortcutService>();
		builder.Services.AddSingleton<IScannerService, ScannerService>();
		builder.Services.AddSingleton<IApiSettingsService, SecureStorageApiSettingsService>();
		builder.Services.AddSingleton<IDatamarkApiClient, DatamarkApiClient>();
		builder.Services.AddSingleton<IOneCSettingsService, SecureStorageOneCSettingsService>();
		builder.Services.AddSingleton<IOneCApiClient, OneCApiClient>();

		builder.Services.AddSingleton<IDbKeyProvider, SecureStorageDbKeyProvider>();
		builder.Services.AddSingleton<LegacyDatabaseImporter>();
		builder.Services.AddSingleton<IDatabaseService, DatabaseService>();

		builder.Services.AddSingleton<HomeViewModel>();
		builder.Services.AddSingleton<HomePage>();

		builder.Services.AddTransient<AggregationViewModel>();
		builder.Services.AddTransient<AggregationPage>();

		builder.Services.AddTransient<CorobCreateViewModel>();
		builder.Services.AddTransient<CorobCreatePage>();

		builder.Services.AddTransient<CorobListViewModel>();
		builder.Services.AddTransient<CorobListPage>();

		builder.Services.AddTransient<CorobDetailViewModel>();
		builder.Services.AddTransient<CorobDetailPage>();

		builder.Services.AddTransient<OrderViewModel>();
		builder.Services.AddTransient<OrderPage>();

		builder.Services.AddTransient<OrderHistoryViewModel>();
		builder.Services.AddTransient<OrderHistoryPage>();

		builder.Services.AddTransient<OrderHistoryDetailViewModel>();
		builder.Services.AddTransient<OrderHistoryDetailPage>();

		builder.Services.AddTransient<ShipmentViewModel>();
		builder.Services.AddTransient<ShipmentPage>();

		builder.Services.AddTransient<SettingsViewModel>();
		builder.Services.AddTransient<SettingsPage>();

		builder.Services.AddTransient<OneCSettingsViewModel>();
		builder.Services.AddTransient<OneCSettingsPage>();

		builder.Services.AddTransient<SectionViewModel>();
		builder.Services.AddTransient<SectionPage>();

		builder.Services.AddSingleton<AppShell>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
