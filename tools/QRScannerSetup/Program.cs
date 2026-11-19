using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;
using QRCoder;

namespace QRScannerSetup
{
    internal static class Program
    {
        private const string Root = @"C:\QRScanner";
        private const string OrdersFolder = @"C:\QRScanner\Заказы";
        private const string QrFolder = @"C:\QRScanner\QR-коды";
        private const string UpdatesFolder = @"C:\QRScanner\Обновления";
        private const string ShareName = "QRScanner";
        private const string UserName = "qrscanner";
        private const string AccessFile = @"C:\QRScanner-access.txt";

        private static bool _dryRun;
        private static bool _noPause;
        private static bool _noNotepad;
        private static bool _noQr;
        private static bool _qrOnly;
        private static bool _skipUser;
        private static bool _simulateWin7;
        private const int CommandTimeoutMilliseconds = 30000;
        private static string _osName = string.Empty;
        private static Version _osVersion = new Version(0, 0);
        private static bool _needsSmb2Registry;
        private static bool _unsupportedOs;
        private static bool _smb2Changed;
        private static bool _netbiosChanged;

        [STAThread]
        private static int Main(string[] args)
        {
            _dryRun = HasArg(args, "dry-run");
            _noPause = HasArg(args, "no-pause");
            _noNotepad = HasArg(args, "no-notepad");
            _noQr = HasArg(args, "no-qr");
            _qrOnly = HasArg(args, "qr-only");
            _skipUser = HasArg(args, "skip-user");
            _simulateWin7 = HasArg(args, "simulate-win7");

            Console.Title = "QRScanner Setup";
            Console.WriteLine("QRScanner SMB setup");
            Console.WriteLine("===================");
            Console.WriteLine();

            if (_qrOnly)
            {
                ShowQrFromAccessFile();
                return 0;
            }

            if (!_dryRun && !IsAdministrator())
            {
                try
                {
                    string executable = Process.GetCurrentProcess().MainModule.FileName;
                    string arguments = string.Join(" ", args.Select(Quote));
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = executable,
                        Arguments = arguments,
                        Verb = "runas",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Run as administrator: " + ex.Message);
                    Pause();
                }

                return 0;
            }

            try
            {
                Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: " + ex.Message);
                Pause();
                return 1;
            }

            Pause();
            return 0;
        }

        private static void Run()
        {
            if (_dryRun)
            {
                Console.WriteLine("DRY RUN: commands are printed, nothing is changed.");
                Console.WriteLine();
            }

            DetectOperatingSystem();

            Console.WriteLine(
                "OS: " + _osName +
                " (" + _osVersion + "), " +
                (Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit"));
            Console.WriteLine(
                "SMB2 registry step: " + (_needsSmb2Registry ? "required" : "not required"));
            Console.WriteLine();

            if (_unsupportedOs)
            {
                Console.WriteLine("This Windows version is not supported for SMB2.");
                Console.WriteLine("Use Windows 7 or newer.");
                return;
            }

            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(OrdersFolder);
            Directory.CreateDirectory(QrFolder);
            Directory.CreateDirectory(UpdatesFolder);

            string password = LoadOrCreatePassword();
            string account = Environment.MachineName + "\\" + UserName;

            Console.WriteLine("User:  " + account);
            Console.WriteLine("Name:  " + Environment.MachineName);
            Console.WriteLine("Share: " + ShareName);
            Console.WriteLine("Path:  " + Root);
            Console.WriteLine();

            if (_skipUser)
            {
                Console.WriteLine("[user] skipped (--skip-user)");
            }
            else if (UserExists())
            {
                Run("net", "user " + UserName + " \"" + password + "\"", "set password");
                Run("net", "user " + UserName + " /expires:never /passwordchg:no", "user flags");
            }
            else
            {
                Run(
                    "net",
                    "user " + UserName + " \"" + password + "\" /add /expires:never /passwordchg:no",
                    "create user");
            }

            EnsureSmb2();
            EnsureNetBios();

            Run("sc", "config lanmanserver start= auto", "server service");
            Run("net", "start lanmanserver", "start server", ignoreError: true);

            Run("net", "share " + ShareName + " /delete", "delete old share", ignoreError: true);
            Run(
                "net",
                "share " + ShareName + "=" + Root +
                " /GRANT:" + UserName + ",CHANGE /REMARK:\"QRScanner files\"",
                "create share");

            Run(
                "icacls",
                Root + " /grant " + UserName + ":(OI)(CI)M /T /C",
                "NTFS permissions");

            Run(
                "netsh",
                "advfirewall firewall delete rule name=\"QRScanner SMB 445\"",
                "delete old firewall rule",
                ignoreError: true);
            Run(
                "netsh",
                "advfirewall firewall add rule name=\"QRScanner SMB 445\" " +
                "dir=in action=allow protocol=TCP localport=445 remoteip=localsubnet",
                "firewall rule");

            Run(
                "netsh",
                "advfirewall firewall delete rule name=\"QRScanner NBNS 137\"",
                "delete old NBNS rule",
                ignoreError: true);
            Run(
                "netsh",
                "advfirewall firewall add rule name=\"QRScanner NBNS 137\" " +
                "dir=in action=allow protocol=UDP localport=137 remoteip=localsubnet",
                "NBNS firewall rule");

            Run(
                "netsh",
                "advfirewall firewall delete rule name=\"QRScanner LLMNR 5355\"",
                "delete old LLMNR rule",
                ignoreError: true);
            Run(
                "netsh",
                "advfirewall firewall add rule name=\"QRScanner LLMNR 5355\" " +
                "dir=in action=allow protocol=UDP localport=5355 remoteip=localsubnet",
                "LLMNR firewall rule");

            ReportPort445();

            string ip = GetPrimaryIPv4();
            string[] lines =
            {
                "QRScanner SMB settings",
                "OS=" + _osName + " (" + _osVersion + ")",
                "SMB2=" + (_needsSmb2Registry ? "enabled" : "native"),
                "Server=" + Environment.MachineName,
                "IP=" + ip,
                "Share=" + ShareName,
                "User=" + account,
                "Password=" + password,
                "Orders=\\\\" + Environment.MachineName + "\\" + ShareName + "\\Заказы",
                "QR=\\\\" + Environment.MachineName + "\\" + ShareName + "\\QR-коды",
                "Updates=\\\\" + Environment.MachineName + "\\" + ShareName + "\\Обновления",
                "",
                "Enter these values in QRScanner -> Settings -> Computer (SMB)."
            };

            if (_dryRun)
            {
                Console.WriteLine();
                Console.WriteLine("Access file would be: " + AccessFile);
                foreach (string line in lines)
                {
                    Console.WriteLine(line);
                }
            }
            else
            {
                File.WriteAllLines(AccessFile, lines, new UTF8Encoding(false));

                Console.WriteLine();
                Console.WriteLine("Access file: " + AccessFile);
                Console.WriteLine();
                Console.WriteLine("Server:   " + Environment.MachineName + " (" + ip + ")");
                Console.WriteLine("Share:    " + ShareName);
                Console.WriteLine("User:     " + account);
                Console.WriteLine("Password: " + password);
                Console.WriteLine();
                Console.WriteLine("Folders:");
                Console.WriteLine("  " + OrdersFolder);
                Console.WriteLine("  " + QrFolder);
                Console.WriteLine("  " + UpdatesFolder);
                Console.WriteLine();
                Console.WriteLine("UNC path: \\\\" + ip + "\\" + ShareName);
                Console.WriteLine();
                Console.WriteLine("Done.");

                if (!_noQr)
                {
                    ShowSettingsQr(
                        Environment.MachineName,
                        ShareName,
                        account,
                        password);
                }

                if (!_noNotepad)
                {
                    try
                    {
                        Process.Start("notepad.exe", AccessFile);
                    }
                    catch
                    {
                        // Notepad is optional.
                    }
                }
            }
        }

        private static void DetectOperatingSystem()
        {
            _osVersion = Environment.OSVersion.Version;

            if (_simulateWin7)
            {
                _osName = "Windows 7 (simulated)";
                _osVersion = new Version(6, 1, 7601);
                _needsSmb2Registry = true;
                _unsupportedOs = false;
                return;
            }

            if (_osVersion.Major == 10)
            {
                _osName = _osVersion.Build >= 22000 ? "Windows 11" : "Windows 10";
                _needsSmb2Registry = false;
            }
            else if (_osVersion.Major == 6 && _osVersion.Minor == 3)
            {
                _osName = "Windows 8.1 / Server 2012 R2";
                _needsSmb2Registry = false;
            }
            else if (_osVersion.Major == 6 && _osVersion.Minor == 2)
            {
                _osName = "Windows 8 / Server 2012";
                _needsSmb2Registry = false;
            }
            else if (_osVersion.Major == 6 && _osVersion.Minor == 1)
            {
                _osName = "Windows 7 / Server 2008 R2";
                _needsSmb2Registry = true;
            }
            else if (_osVersion.Major == 6 && _osVersion.Minor == 0)
            {
                _osName = "Windows Vista / Server 2008";
                _needsSmb2Registry = true;
            }
            else
            {
                _osName = "Unsupported Windows " + _osVersion;
                _unsupportedOs = true;
                _needsSmb2Registry = false;
            }
        }

        private static void EnsureSmb2()
        {
            if (!_needsSmb2Registry)
            {
                Console.WriteLine("[SMB2] not required on this Windows version");
                return;
            }

            const string path = @"SYSTEM\CurrentControlSet\Services\LanmanServer\Parameters";

            try
            {
                RegistryKey baseKey = Environment.Is64BitOperatingSystem
                    ? RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                    : Registry.LocalMachine;

                using (RegistryKey key = baseKey.OpenSubKey(path, writable: !_dryRun))
                {
                    if (key == null)
                    {
                        Console.WriteLine("[SMB2] registry key not found; skipping");
                        return;
                    }

                    object value = key.GetValue("SMB2");
                    bool enabled = value != null && Convert.ToInt32(value) == 1;
                    if (enabled)
                    {
                        Console.WriteLine("[SMB2] already enabled");
                        return;
                    }

                    if (_dryRun)
                    {
                        Console.WriteLine("[SMB2] would set registry value SMB2=1");
                        return;
                    }

                    key.SetValue("SMB2", 1, RegistryValueKind.DWord);
                    _smb2Changed = true;
                    Console.WriteLine("[SMB2] enabled in registry");
                }

                if (_smb2Changed)
                {
                    Run("net", "stop lanmanserver /y", "stop server for SMB2", ignoreError: true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[SMB2] error: " + ex.Message);
            }
        }

        private static void EnsureNetBios()
        {
            const string path = @"SYSTEM\CurrentControlSet\Services\NetBT\Parameters\Interfaces";

            try
            {
                RegistryKey baseKey = Environment.Is64BitOperatingSystem
                    ? RegistryKey.OpenBaseKey(
                        RegistryHive.LocalMachine,
                        RegistryView.Registry64)
                    : Registry.LocalMachine;

                using (RegistryKey interfaces =
                    baseKey.OpenSubKey(path, writable: !_dryRun))
                {
                    if (interfaces == null)
                    {
                        Console.WriteLine("[NetBIOS] interfaces key not found");
                        return;
                    }

                    foreach (string name in interfaces.GetSubKeyNames())
                    {
                        if (!name.StartsWith(
                            "Tcpip_",
                            StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        using (RegistryKey iface =
                            interfaces.OpenSubKey(name, writable: !_dryRun))
                        {
                            if (iface == null)
                            {
                                continue;
                            }

                            object value = iface.GetValue("NetbiosOptions");
                            int options = value == null
                                ? 0
                                : Convert.ToInt32(value);

                            if (options == 1)
                            {
                                continue;
                            }

                            if (_dryRun)
                            {
                                Console.WriteLine(
                                    "[NetBIOS] would enable for " + name);
                                continue;
                            }

                            iface.SetValue(
                                "NetbiosOptions",
                                1,
                                RegistryValueKind.DWord);
                            Console.WriteLine("[NetBIOS] enabled for " + name);
                            _netbiosChanged = true;
                        }
                    }
                }

                if (_netbiosChanged)
                {
                    Console.WriteLine(
                        "[NetBIOS] reboot Windows to apply the change");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[NetBIOS] error: " + ex.Message);
            }
        }

        private static void ShowQrFromAccessFile()
        {
            if (!File.Exists(AccessFile))
            {
                Console.WriteLine("Нет файла " + AccessFile);
                return;
            }

            string server = null;
            string share = null;
            string user = null;
            string password = null;

            foreach (string line in File.ReadAllLines(AccessFile))
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith(
                    "Server=",
                    StringComparison.OrdinalIgnoreCase))
                {
                    server = trimmed.Substring(7).Trim();
                }
                else if (trimmed.StartsWith(
                    "Share=",
                    StringComparison.OrdinalIgnoreCase))
                {
                    share = trimmed.Substring(6).Trim();
                }
                else if (trimmed.StartsWith(
                    "User=",
                    StringComparison.OrdinalIgnoreCase))
                {
                    user = trimmed.Substring(5).Trim();
                }
                else if (trimmed.StartsWith(
                    "Password=",
                    StringComparison.OrdinalIgnoreCase))
                {
                    password = trimmed.Substring(9).Trim();
                }
            }

            if (string.IsNullOrWhiteSpace(server) ||
                string.IsNullOrWhiteSpace(share) ||
                string.IsNullOrWhiteSpace(user) ||
                string.IsNullOrWhiteSpace(password))
            {
                Console.WriteLine(
                    "В файле нет Server, Share, User и Password.");
                return;
            }

            ShowSettingsQr(server, share, user, password);
        }

        private static void ShowSettingsQr(
            string server,
            string share,
            string user,
            string password)
        {
            try
            {
                string raw = server + "|" + share + "|" + user + "|" + password;
                string payload = "QRSCANNER:SMB:" +
                    Convert.ToBase64String(
                        Encoding.UTF8.GetBytes(raw));

                using (QRCodeGenerator generator = new QRCodeGenerator())
                using (QRCodeData data = generator.CreateQrCode(
                    payload,
                    QRCodeGenerator.ECCLevel.M))
                {
                    PngByteQRCode qr = new PngByteQRCode(data);
                    byte[] png = qr.GetGraphic(12);

                    using (MemoryStream stream = new MemoryStream(png))
                    using (Image image = Image.FromStream(stream))
                    using (Bitmap bitmap = new Bitmap(image))
                    using (Form form = new Form())
                    {
                        form.Text = "QRScanner — настройки SMB";
                        form.StartPosition = FormStartPosition.CenterScreen;
                        form.Width = 640;
                        form.Height = 780;
                        form.TopMost = true;
                        form.MinimizeBox = false;
                        form.MaximizeBox = false;

                        Label label = new Label();
                        label.Text =
                            "На ТСД: Настройки → Компьютер → " +
                            "Сканировать QR настроек.\n" +
                            "Наведите сканер на этот QR-код.";
                        label.Dock = DockStyle.Top;
                        label.Height = 80;
                        label.TextAlign = ContentAlignment.MiddleCenter;
                        label.Font = new Font("Segoe UI", 11f);

                        PictureBox picture = new PictureBox();
                        picture.Image = bitmap;
                        picture.SizeMode = PictureBoxSizeMode.Zoom;
                        picture.Dock = DockStyle.Fill;

                        Button button = new Button();
                        button.Text = "Готово";
                        button.Dock = DockStyle.Bottom;
                        button.Height = 52;
                        button.Font = new Font("Segoe UI", 11f);
                        button.Click += delegate { form.Close(); };

                        form.Controls.Add(picture);
                        form.Controls.Add(label);
                        form.Controls.Add(button);

                        try
                        {
                            Application.EnableVisualStyles();
                        }
                        catch
                        {
                            // Visual styles are optional.
                        }

                        form.ShowDialog();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("QR: " + ex.Message);
            }
        }

        private static void ReportPort445()
        {
            try
            {
                bool listening = IPGlobalProperties.GetIPGlobalProperties()
                    .GetActiveTcpListeners()
                    .Any(endpoint => endpoint.Port == 445);
                Console.WriteLine("[TCP 445] " + (listening ? "listening" : "not listening"));
            }
            catch (Exception ex)
            {
                Console.WriteLine("[TCP 445] check failed: " + ex.Message);
            }
        }

        private static bool UserExists()
        {
            CommandResult result = Run(
                "net",
                "user " + UserName,
                "check user",
                ignoreError: true,
                capture: true);
            return result.ExitCode == 0;
        }

        private static string LoadOrCreatePassword()
        {
            if (File.Exists(AccessFile))
            {
                foreach (string line in File.ReadAllLines(AccessFile))
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("Password=", StringComparison.OrdinalIgnoreCase) ||
                        trimmed.StartsWith("Password:", StringComparison.OrdinalIgnoreCase))
                    {
                        int separator = trimmed.IndexOf('=');
                        if (separator < 0)
                        {
                            separator = trimmed.IndexOf(':');
                        }

                        string value = trimmed.Substring(separator + 1).Trim();
                        if (value.Length >= 8)
                        {
                            return value;
                        }
                    }
                }
            }

            const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string lower = "abcdefghijkmnopqrstuvwxyz";
            const string digits = "23456789";
            const string all = upper + lower + digits;
            Random random = new Random();
            StringBuilder builder = new StringBuilder();
            builder.Append("Qr");
            builder.Append(digits[random.Next(digits.Length)]);
            for (int index = 0; index < 13; index++)
            {
                builder.Append(all[random.Next(all.Length)]);
            }

            return builder.ToString();
        }

        private static string GetPrimaryIPv4()
        {
            List<string> candidates = new List<string>();
            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    networkInterface.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                string name = (networkInterface.Name ?? string.Empty).ToLowerInvariant();
                if (name.Contains("vethernet") ||
                    name.Contains("virtual") ||
                    name.Contains("wsl") ||
                    name.Contains("hyper-v") ||
                    name.Contains("vpn") ||
                    name.Contains("tap") ||
                    name.Contains("wireguard"))
                {
                    continue;
                }

                foreach (UnicastIPAddressInformation address in
                    networkInterface.GetIPProperties().UnicastAddresses)
                {
                    if (address.Address.AddressFamily != AddressFamily.InterNetwork)
                    {
                        continue;
                    }

                    string value = address.Address.ToString();
                    if (!value.StartsWith("169.254."))
                    {
                        candidates.Add(value);
                    }
                }
            }

            return candidates.FirstOrDefault(value => value.StartsWith("192.168.0."))
                ?? candidates.FirstOrDefault(value => value.StartsWith("192.168."))
                ?? candidates.FirstOrDefault(value => value.StartsWith("10."))
                ?? candidates.FirstOrDefault(value => value.StartsWith("172."))
                ?? candidates.FirstOrDefault()
                ?? "127.0.0.1";
        }

        private static CommandResult Run(
            string file,
            string arguments,
            string title,
            bool ignoreError = false,
            bool capture = false)
        {
            Console.WriteLine("[" + title + "] " + file + " " + arguments);

            if (_dryRun)
            {
                return new CommandResult(0, string.Empty, string.Empty);
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, file + ".exe"),
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(startInfo))
            {
                bool exited = process.WaitForExit(
                    CommandTimeoutMilliseconds);
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                if (!exited)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                        // The process may have just exited.
                    }

                    try
                    {
                        process.WaitForExit();
                    }
                    catch
                    {
                        // Ignore cleanup errors.
                    }

                    Console.WriteLine(
                        "[" + title + "] TIMEOUT after " +
                        (CommandTimeoutMilliseconds / 1000) + " seconds");

                    if (ignoreError)
                    {
                        return new CommandResult(-1, output, error);
                    }

                    throw new InvalidOperationException(
                        title + " timed out");
                }

                if (!capture)
                {
                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        Console.WriteLine(output.Trim());
                    }

                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        Console.WriteLine(error.Trim());
                    }
                }

                if (process.ExitCode != 0 && !ignoreError)
                {
                    throw new InvalidOperationException(
                        title + " failed with exit code " + process.ExitCode);
                }

                return new CommandResult(process.ExitCode, output, error);
            }
        }

        private static bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        private static bool HasArg(string[] args, string value)
        {
            return args.Any(arg =>
                string.Equals(arg, "--" + value, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "/" + value, StringComparison.OrdinalIgnoreCase));
        }

        private static string Quote(string value)
        {
            return value.Contains(" ") ? "\"" + value + "\"" : value;
        }

        private static void Pause()
        {
            if (_noPause)
            {
                return;
            }

            Console.WriteLine();
            Console.WriteLine("Press Enter to exit.");
            Console.ReadLine();
        }

        private sealed class CommandResult
        {
            public CommandResult(int exitCode, string output, string error)
            {
                ExitCode = exitCode;
                Output = output;
                Error = error;
            }

            public int ExitCode { get; private set; }

            public string Output { get; private set; }

            public string Error { get; private set; }
        }
    }
}
