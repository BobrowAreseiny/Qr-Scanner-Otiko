using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;
using Microsoft.Extensions.DependencyInjection;
using QRScanner.App.Services;

namespace QRScanner.App;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private const string BarcodeAction = "com.xcheng.scanner.action.BARCODE_DECODING_BROADCAST";
    private const string BarcodeDataExtra = "EXTRA_BARCODE_DECODING_DATA";
    private const string BarcodeSymbologyExtra = "EXTRA_BARCODE_DECODING_SYMBOLE";

    private ScannerBroadcastReceiver? _scannerReceiver;
    private bool _receiverRegistered;
    private FileSaveService? _fileSaveService;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        _fileSaveService = IPlatformApplication.Current?.Services
            .GetService<FileSaveService>();

        Window?.AddFlags(WindowManagerFlags.DrawsSystemBarBackgrounds);
        Window?.SetStatusBarColor(Android.Graphics.Color.ParseColor("#172554"));
        Window?.SetNavigationBarColor(Android.Graphics.Color.Black);

        if ((int)Build.VERSION.SdkInt >= 35 && Window is { } window)
        {
            var controller = WindowCompat.GetInsetsController(window, window.DecorView);
            if (controller != null)
            {
                controller.AppearanceLightStatusBars = true;
            }
        }
    }

    protected override void OnResume()
    {
        base.OnResume();

        GetScannerService()?.EnableBroadcastMode();

        if (!_receiverRegistered)
        {
            _scannerReceiver = new ScannerBroadcastReceiver();
            var filter = new IntentFilter(BarcodeAction);

            if ((int)Build.VERSION.SdkInt >= 33)
            {
                RegisterReceiver(_scannerReceiver, filter, ReceiverFlags.Exported);
            }
            else
            {
                RegisterReceiver(_scannerReceiver, filter);
            }

            _receiverRegistered = true;
        }
    }

    protected override void OnPause()
    {
        if (_receiverRegistered && _scannerReceiver != null)
        {
            UnregisterReceiver(_scannerReceiver);
            _receiverRegistered = false;
        }

        base.OnPause();
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        HandleBarcodeIntent(intent);
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        _fileSaveService?.HandleActivityResult(requestCode, resultCode, data);
    }

    public override bool OnKeyDown(Keycode keyCode, KeyEvent? e)
    {
        if (CurrentFocus is not Android.Widget.EditText)
        {
            var digit = MapDigit((int)keyCode);
            if (digit is not null)
            {
                if (e?.RepeatCount > 0)
                {
                    return true;
                }

                var shortcuts = IPlatformApplication.Current?.Services
                    .GetService<IKeyboardShortcutService>();

                if (shortcuts != null)
                {
                    shortcuts.Publish(digit.Value);
                    return true;
                }
            }
        }

        return base.OnKeyDown(keyCode, e);
    }

    private static void HandleBarcodeIntent(Intent? intent)
    {
        if (intent?.Extras == null)
        {
            return;
        }

        var data = intent.GetStringExtra(BarcodeDataExtra);
        var symbology = intent.GetStringExtra(BarcodeSymbologyExtra);

        GetScannerService()?.Publish(data, symbology);
    }

    private static IScannerService? GetScannerService()
    {
        return IPlatformApplication.Current?.Services.GetService<IScannerService>();
    }

    private static int? MapDigit(int keyCode)
    {
        return keyCode switch
        {
            7 => 0,
            8 => 1,
            9 => 2,
            10 => 3,
            11 => 4,
            12 => 5,
            13 => 6,
            14 => 7,
            15 => 8,
            16 => 9,
            144 => 0,
            145 => 1,
            146 => 2,
            147 => 3,
            148 => 4,
            149 => 5,
            150 => 6,
            151 => 7,
            152 => 8,
            153 => 9,
            _ => null
        };
    }

    [BroadcastReceiver(Enabled = true, Exported = true)]
    public sealed class ScannerBroadcastReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context? context, Intent? intent)
        {
            HandleBarcodeIntent(intent);
        }
    }
}
