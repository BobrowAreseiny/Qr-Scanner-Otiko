using QRScanner.App.Views;

namespace QRScanner.App;

public partial class AppShell : Shell
{
    public AppShell(HomePage homePage)
    {
        InitializeComponent();

        HomeContent.Content = homePage;

        Routing.RegisterRoute(nameof(AggregationPage), typeof(AggregationPage));
        Routing.RegisterRoute(nameof(ShipmentPage), typeof(ShipmentPage));
        Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
        Routing.RegisterRoute(nameof(OneCSettingsPage), typeof(OneCSettingsPage));
        Routing.RegisterRoute(nameof(LogsPage), typeof(LogsPage));
        Routing.RegisterRoute(nameof(HelpPage), typeof(HelpPage));
        Routing.RegisterRoute(nameof(SectionPage), typeof(SectionPage));
        Routing.RegisterRoute(nameof(CorobCreatePage), typeof(CorobCreatePage));
        Routing.RegisterRoute(nameof(CorobListPage), typeof(CorobListPage));
        Routing.RegisterRoute(nameof(CorobDetailPage), typeof(CorobDetailPage));
        Routing.RegisterRoute(nameof(OrderPage), typeof(OrderPage));
        Routing.RegisterRoute(nameof(OrderHistoryPage), typeof(OrderHistoryPage));
        Routing.RegisterRoute(nameof(OrderHistoryDetailPage), typeof(OrderHistoryDetailPage));
        Routing.RegisterRoute(nameof(GtinCheckPage), typeof(GtinCheckPage));
        Routing.RegisterRoute(nameof(CorobInfoPage), typeof(CorobInfoPage));
    }
}
