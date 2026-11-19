using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Util;

namespace QRScanner.App.Services;

/// <summary>
/// Настраивает сканер ATOL Smart.Lite через сервис ru.atol.barcodeservice.
/// Нужен для старых ТСД, где QR-симвология и broadcast-режим не включены по умолчанию.
/// </summary>
internal sealed class AtolScannerSettings : Java.Lang.Object, IServiceConnection
{
    private const string Tag = "QRScanner";
    private const string AtolPackage = "ru.atol.barcodeservice";
    private const string AtolAction = "ru.atol.barcodeservice.SCANNER_SETTINGS_CONNECTION";
    private const string AtolDescriptor = "ru.atol.barcodeservice.IScannerSettingsApi";

    private const int TransactionSetIntProperty = 2;
    private const int TransactionSetStringProperty = 5;
    private const int TransactionSetBoolProperty = 9;
    private const int TransactionPropertySupported = 10;

    private readonly Context _context;
    private IBinder? _service;
    private bool _bound;
    private bool _packageMissing;

    public AtolScannerSettings(Context context)
    {
        _context = context;
    }

    public void Bind()
    {
        if (_service != null)
        {
            ApplySettings();
            return;
        }

        if (_bound || _packageMissing)
        {
            return;
        }

        if (!IsPackageInstalled())
        {
            _packageMissing = true;
            return;
        }

        try
        {
            var intent = new Intent(AtolAction);
            intent.SetPackage(AtolPackage);
            _bound = _context.BindService(
                intent,
                this,
                Android.Content.Bind.AutoCreate);

            if (!_bound)
            {
                Log.Warn(Tag, "ATOL scanner service is not available");
            }
        }
        catch (Exception ex)
        {
            Log.Warn(Tag, "ATOL scanner bind failed: " + ex.Message);
        }
    }

    public void OnServiceConnected(ComponentName? name, IBinder? service)
    {
        _service = service;
        ApplySettings();
    }

    public void OnServiceDisconnected(ComponentName? name)
    {
        _service = null;
        _bound = false;
    }

    private bool IsPackageInstalled()
    {
        try
        {
            _context.PackageManager?.GetPackageInfo(
                AtolPackage,
                (PackageInfoFlags)0);
            return true;
        }
        catch (PackageManager.NameNotFoundException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    private void ApplySettings()
    {
        if (_service == null)
        {
            return;
        }

        try
        {
            // BarcodeSendMode.Broadcast = 0
            SetInt("sendMode", 0);
            SetString(
                "sendIntent",
                "com.xcheng.scanner.action.BARCODE_DECODING_BROADCAST");
            SetString("sendExtraData", "EXTRA_BARCODE_DECODING_DATA");
            SetString("sendExtraSymbology", "EXTRA_BARCODE_DECODING_SYMBOLE");

            // WorkMode.SingleShot = 0
            SetInt("workMode", 0);

            SetBool("qrcodeEnable", true);
            SetBool("code128Enable", true);
            SetBool("datamatrixEnable", true);
            SetBool("gs1datamatrixEnable", true);

            Log.Info(
                Tag,
                "ATOL scanner: broadcast, QR and DataMatrix enabled");
        }
        catch (Exception ex)
        {
            Log.Warn(Tag, "ATOL scanner settings failed: " + ex.Message);
        }
    }

    private bool IsSupported(string name)
    {
        Parcel? data = null;
        Parcel? reply = null;

        try
        {
            data = Parcel.Obtain();
            reply = Parcel.Obtain();
            data.WriteInterfaceToken(AtolDescriptor);
            data.WriteString(name);

            if (_service?.Transact(
                    TransactionPropertySupported,
                    data,
                    reply,
                    0) != true)
            {
                return false;
            }

            reply.ReadException();
            return reply.ReadInt() != 0;
        }
        catch
        {
            return false;
        }
        finally
        {
            data?.Recycle();
            reply?.Recycle();
        }
    }

    private void SetBool(string name, bool value)
    {
        if (!IsSupported(name))
        {
            return;
        }

        Transact(TransactionSetBoolProperty, data =>
        {
            data.WriteString(name);
            data.WriteInt(value ? 1 : 0);
        });
    }

    private void SetInt(string name, int value)
    {
        if (!IsSupported(name))
        {
            return;
        }

        Transact(TransactionSetIntProperty, data =>
        {
            data.WriteString(name);
            data.WriteInt(value);
        });
    }

    private void SetString(string name, string value)
    {
        if (!IsSupported(name))
        {
            return;
        }

        Transact(TransactionSetStringProperty, data =>
        {
            data.WriteString(name);
            data.WriteString(value);
        });
    }

    private void Transact(int code, Action<Parcel> write)
    {
        Parcel? data = null;
        Parcel? reply = null;

        try
        {
            data = Parcel.Obtain();
            reply = Parcel.Obtain();
            data.WriteInterfaceToken(AtolDescriptor);
            write(data);

            if (_service?.Transact(code, data, reply, 0) == true)
            {
                reply.ReadException();
            }
        }
        catch (Exception ex)
        {
            Log.Warn(
                Tag,
                "ATOL transact " + code + " failed: " + ex.Message);
        }
        finally
        {
            data?.Recycle();
            reply?.Recycle();
        }
    }
}
