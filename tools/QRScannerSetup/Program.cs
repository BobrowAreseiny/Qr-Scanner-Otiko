using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.DirectoryServices.AccountManagement;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
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
        private const string ServiceAccountDescription = "QRScanner SMB service account";
        private const string AccessFile = @"C:\QRScanner-access.txt";

        private static string _userName = UserName;
        private static bool _userNameExplicit;
        private static string _simulateTakenUser;

        private static bool _dryRun;
        private static bool _noPause;
        private static bool _noNotepad;
        private static bool _noQr;
        private static bool _qrOnly;
        private static bool _skipUser;
        private static bool _simulateWin7;
        private static bool _install;
        private static bool _uninstall;
        private static bool _yes;
        private static bool _keepData;
        private static bool _menu;
        private static bool _help;
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
            RegisterEmbeddedAssemblies();

            _dryRun = HasArg(args, "dry-run");
            _noPause = HasArg(args, "no-pause");
            _noNotepad = HasArg(args, "no-notepad");
            _noQr = HasArg(args, "no-qr");
            _qrOnly = HasArg(args, "qr-only");
            _skipUser = HasArg(args, "skip-user");
            _simulateWin7 = HasArg(args, "simulate-win7");
            _install = HasArg(args, "install");
            _uninstall = HasArg(args, "uninstall");
            _yes = HasArg(args, "yes");
            _keepData = HasArg(args, "keep-data");
            _menu = HasArg(args, "menu");
            _help = HasArg(args, "help") || HasArg(args, "h");

            string requestedUser = GetArgValue(args, "user");
            if (!string.IsNullOrWhiteSpace(requestedUser))
            {
                _userName = requestedUser.Trim();
                _userNameExplicit = true;
            }

            // Test-only hook: pretend a name is used by a human account.
            _simulateTakenUser = GetArgValue(args, "simulate-taken");

            Console.Title = "QRScanner Setup";

            if (_help)
            {
                ShowHelp();
                return 0;
            }

            bool noAction = !_install && !_uninstall && !_qrOnly;
            if (noAction &&
                (_menu || (args.Length == 0 && !Console.IsInputRedirected)))
            {
                int choice = ShowMenu();
                if (choice == 0)
                {
                    return 0;
                }

                if (choice == 1)
                {
                    _install = true;
                }
                else if (choice == 2)
                {
                    _uninstall = true;
                }
                else
                {
                    _qrOnly = true;
                }
            }

            if (_qrOnly)
            {
                ShowQrFromAccessFile();
                return 0;
            }

            if (_install && _uninstall)
            {
                Console.WriteLine("Choose either --install or --uninstall.");
                return 1;
            }

            if (!_dryRun && !IsAdministrator())
            {
                try
                {
                    string executable = Process.GetCurrentProcess().MainModule.FileName;
                    string arguments = BuildElevatedArguments();
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

            if (_uninstall)
            {
                try
                {
                    RunUninstall();
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

            Console.WriteLine("QRScanner SMB setup");
            Console.WriteLine("===================");
            Console.WriteLine();

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

        private static void ShowHelp()
        {
            Console.WriteLine("QRScanner Setup");
            Console.WriteLine("===============");
            Console.WriteLine();
            Console.WriteLine("Без параметров      показать меню");
            Console.WriteLine("--install           установить или обновить");
            Console.WriteLine("--uninstall         удалить всё (спросит YES)");
            Console.WriteLine("--uninstall --yes   удалить без вопросов");
            Console.WriteLine("--keep-data         при удалении сохранить C:\\QRScanner");
            Console.WriteLine("--qr-only           показать QR настроек");
            Console.WriteLine("--user NAME         задать имя служебного пользователя");
            Console.WriteLine("--skip-user         не создавать пользователя");
            Console.WriteLine("--no-qr             не показывать QR");
            Console.WriteLine("--no-pause          не ждать Enter");
            Console.WriteLine("--dry-run           только показать действия");
            Console.WriteLine("--help              эта справка");
        }

        private static int ShowMenu()
        {
            while (true)
            {
                Console.WriteLine("QRScanner Setup");
                Console.WriteLine("===============");
                Console.WriteLine("1. Установить или обновить");
                Console.WriteLine("2. Удалить всё");
                Console.WriteLine("3. Показать QR настроек");
                Console.WriteLine("0. Выход");
                Console.WriteLine();
                Console.Write("Выберите пункт [1]: ");

                string value = Console.ReadLine();
                if (value == null)
                {
                    return 0;
                }

                if (string.IsNullOrWhiteSpace(value))
                {
                    return 1;
                }

                switch (value.Trim())
                {
                    case "0":
                        return 0;
                    case "1":
                        return 1;
                    case "2":
                        return 2;
                    case "3":
                        return 3;
                }

                Console.WriteLine();
                Console.WriteLine("Введите 0, 1, 2 или 3.");
                Console.WriteLine();
            }
        }

        private static string BuildElevatedArguments()
        {
            List<string> parts = new List<string>();
            parts.Add(_uninstall ? "--uninstall" : "--install");

            if (_uninstall)
            {
                if (_yes)
                {
                    parts.Add("--yes");
                }

                if (_keepData)
                {
                    parts.Add("--keep-data");
                }
            }
            else
            {
                if (_skipUser)
                {
                    parts.Add("--skip-user");
                }

                if (_noQr)
                {
                    parts.Add("--no-qr");
                }

                if (_noNotepad)
                {
                    parts.Add("--no-notepad");
                }

                if (_simulateWin7)
                {
                    parts.Add("--simulate-win7");
                }
            }

            if (_userNameExplicit)
            {
                parts.Add("--user");
                parts.Add(Quote(_userName));
            }

            if (_noPause)
            {
                parts.Add("--no-pause");
            }

            return string.Join(" ", parts.ToArray());
        }

        private static void RegisterEmbeddedAssemblies()
        {
            AppDomain.CurrentDomain.AssemblyResolve +=
                delegate(object sender, ResolveEventArgs args)
                {
                    string name = new AssemblyName(args.Name).Name;
                    if (!string.Equals(
                        name,
                        "QRCoder",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return null;
                    }

                    using (Stream stream =
                        typeof(Program).Assembly.GetManifestResourceStream(
                            "QRCoder.dll"))
                    {
                        if (stream == null)
                        {
                            return null;
                        }

                        byte[] data = new byte[stream.Length];
                        int offset = 0;
                        while (offset < data.Length)
                        {
                            int read = stream.Read(
                                data,
                                offset,
                                data.Length - offset);
                            if (read <= 0)
                            {
                                break;
                            }

                            offset += read;
                        }

                        return Assembly.Load(data);
                    }
                };
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

            if (!_skipUser)
            {
                _userName = ResolveServiceUserName(_userName);
            }

            string account = Environment.MachineName + "\\" + _userName;

            Console.WriteLine("User:  " + account);
            Console.WriteLine("Name:  " + Environment.MachineName);
            Console.WriteLine("Share: " + ShareName);
            Console.WriteLine("Path:  " + Root);
            Console.WriteLine();

            if (_skipUser)
            {
                Console.WriteLine("[user] skipped (--skip-user)");
            }
            else if (_dryRun)
            {
                Console.WriteLine(
                    "[user] would create or update local user " + _userName);
            }
            else
            {
                EnsureUser(password);
            }

            EnsureSmb2();
            EnsureNetBios();

            Run("sc", "config lanmanserver start= auto", "server service");
            Run("net", "start lanmanserver", "start server", ignoreError: true);

            Run("net", "share " + ShareName + " /delete", "delete old share", ignoreError: true);
            Run(
                "net",
                "share " + ShareName + "=" + Root +
                " /GRANT:" + _userName + ",CHANGE /REMARK:\"QRScanner files\"",
                "create share");

            Run(
                "icacls",
                Root + " /grant " + _userName + ":(OI)(CI)M /T /C",
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
                Console.WriteLine(
                    "UNC path: \\\\" + Environment.MachineName +
                    "\\" + ShareName);
                Console.WriteLine();
                Console.WriteLine("Done.");

                if (!_noQr)
                {
                    try
                    {
                        ShowSettingsQr(
                            Environment.MachineName,
                            ShareName,
                            account,
                            password);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("QR: " + ex.Message);
                        Console.WriteLine(
                            "QR window could not be shown. " +
                            "Use " + AccessFile +
                            " or run QRScannerSetup.exe --qr-only.");
                    }
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

        private static void RunUninstall()
        {
            Console.WriteLine("QRScanner uninstall");
            Console.WriteLine("===================");
            Console.WriteLine();

            List<string> serviceUsers = FindServiceUsers();
            ShowUninstallPlan(serviceUsers);

            if (!_dryRun && !_yes)
            {
                Console.Write("Для продолжения введите YES: ");
                string answer = Console.ReadLine();
                if (!string.Equals(
                    answer,
                    "YES",
                    StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Отменено.");
                    return;
                }
            }

            Console.WriteLine();

            if (_dryRun)
            {
                Console.WriteLine(
                    "[share] would delete \\\\" + Environment.MachineName +
                    "\\" + ShareName);
            }
            else if (IsOurShare())
            {
                Run(
                    "net",
                    "share " + ShareName + " /delete",
                    "delete share",
                    ignoreError: true);
            }
            else
            {
                Console.WriteLine(
                    "[share] " + ShareName + " not found or not ours");
            }

            if (_keepData && serviceUsers.Count > 0 && Directory.Exists(Root))
            {
                foreach (string user in serviceUsers)
                {
                    Run(
                        "icacls",
                        Root + " /remove:g " + user + " /T /C",
                        "remove " + user + " from NTFS permissions",
                        ignoreError: true);
                }
            }

            foreach (string user in serviceUsers)
            {
                DeleteServiceUser(user);
            }

            Run(
                "netsh",
                "advfirewall firewall delete rule name=\"QRScanner SMB 445\"",
                "delete SMB firewall rule",
                ignoreError: true);
            Run(
                "netsh",
                "advfirewall firewall delete rule name=\"QRScanner NBNS 137\"",
                "delete NBNS firewall rule",
                ignoreError: true);
            Run(
                "netsh",
                "advfirewall firewall delete rule name=\"QRScanner LLMNR 5355\"",
                "delete LLMNR firewall rule",
                ignoreError: true);

            if (File.Exists(AccessFile))
            {
                if (_dryRun)
                {
                    Console.WriteLine("[file] would delete " + AccessFile);
                }
                else
                {
                    File.Delete(AccessFile);
                    Console.WriteLine("[file] deleted " + AccessFile);
                }
            }
            else
            {
                Console.WriteLine("[file] " + AccessFile + " not found");
            }

            string executable = Process.GetCurrentProcess().MainModule.FileName;
            bool exeInsideRoot = executable.StartsWith(
                Root + "\\",
                StringComparison.OrdinalIgnoreCase);

            if (_keepData)
            {
                Console.WriteLine("[folder] kept " + Root + " (--keep-data)");
            }
            else if (!Directory.Exists(Root))
            {
                Console.WriteLine("[folder] " + Root + " not found");
            }
            else if (exeInsideRoot)
            {
                Console.WriteLine(
                    "[folder] setup is running from " + Root +
                    "; folder kept. Move the EXE and run --uninstall again.");
            }
            else if ((File.GetAttributes(Root) & FileAttributes.ReparsePoint) != 0)
            {
                Console.WriteLine(
                    "[folder] " + Root +
                    " is a reparse point; remove it manually");
            }
            else if (_dryRun)
            {
                Console.WriteLine("[folder] would delete " + Root);
            }
            else
            {
                try
                {
                    DeleteDirectoryWithRetry(Root);
                    Console.WriteLine("[folder] deleted " + Root);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        "[folder] could not delete " + Root + ": " +
                        ex.Message);
                }
            }

            Console.WriteLine();
            Console.WriteLine(
                "[info] SMB2/NetBIOS are shared Windows settings and were not changed.");
            Console.WriteLine("Uninstall finished.");
        }

        private static void ShowUninstallPlan(List<string> serviceUsers)
        {
            Console.WriteLine("Будет удалено:");
            Console.WriteLine(
                "  - общая папка \\\\" + Environment.MachineName +
                "\\" + ShareName);

            if (serviceUsers.Count == 0)
            {
                Console.WriteLine("  - служебный пользователь: не найден");
            }
            else
            {
                Console.WriteLine(
                    "  - служебный пользователь: " +
                    string.Join(", ", serviceUsers.ToArray()));
            }

            Console.WriteLine(
                "  - правила брандмауэра QRScanner SMB 445, NBNS 137, LLMNR 5355");
            Console.WriteLine("  - файл " + AccessFile);

            if (_keepData)
            {
                Console.WriteLine(
                    "  - папка " + Root + " останется (--keep-data)");
            }
            else
            {
                Console.WriteLine(
                    "  - папка " + Root +
                    " со всеми заказами и QR-кодами");
            }

            Console.WriteLine();
        }

        private static List<string> FindServiceUsers()
        {
            List<string> result = new List<string>();
            List<string> candidates = new List<string>();

            string fromFile = ReadAccessUserName();
            if (!string.IsNullOrWhiteSpace(fromFile))
            {
                candidates.Add(fromFile);
            }

            if (_userNameExplicit)
            {
                candidates.Add(_userName);
            }

            for (int index = 1; index <= 99; index++)
            {
                candidates.Add(index == 1 ? UserName : UserName + index);
            }

            try
            {
                using (PrincipalContext context =
                    new PrincipalContext(ContextType.Machine))
                {
                    foreach (string candidate in candidates)
                    {
                        if (string.IsNullOrWhiteSpace(candidate) ||
                            result.Any(existing => string.Equals(
                                existing,
                                candidate,
                                StringComparison.OrdinalIgnoreCase)))
                        {
                            continue;
                        }

                        if (string.Equals(
                            candidate,
                            Environment.UserName,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine(
                                "[user] " + candidate +
                                " is the current Windows user; " +
                                "it will not be deleted");
                            continue;
                        }

                        UserPrincipal user =
                            UserPrincipal.FindByIdentity(context, candidate);
                        if (user == null)
                        {
                            continue;
                        }

                        bool ours = IsServiceAccount(user);
                        user.Dispose();

                        if (ours)
                        {
                            result.Add(candidate);
                        }
                        else if (string.Equals(
                                candidate,
                                fromFile,
                                StringComparison.OrdinalIgnoreCase) ||
                            (_userNameExplicit && string.Equals(
                                candidate,
                                _userName,
                                StringComparison.OrdinalIgnoreCase)))
                        {
                            Console.WriteLine(
                                "[user] " + candidate +
                                " is not a QRScanner service account; " +
                                "it will not be deleted");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "[user] could not list service accounts: " +
                    ex.Message);
            }

            return result;
        }

        private static string ReadAccessUserName()
        {
            string value = ReadAccessValue("User");
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            int slash = value.IndexOf('\\');
            if (slash >= 0)
            {
                value = value.Substring(slash + 1);
            }

            return value.Trim();
        }

        private static string ReadAccessValue(string key)
        {
            if (!File.Exists(AccessFile))
            {
                return null;
            }

            try
            {
                foreach (string line in File.ReadAllLines(AccessFile))
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith(
                        key + "=",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return trimmed.Substring(key.Length + 1).Trim();
                    }
                }
            }
            catch
            {
                // The access file is optional for uninstall.
            }

            return null;
        }

        private static void DeleteServiceUser(string name)
        {
            if (string.Equals(
                name,
                Environment.UserName,
                StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(
                    "[user] " + name +
                    " is the current Windows user; skipped");
                return;
            }

            try
            {
                using (PrincipalContext context =
                    new PrincipalContext(ContextType.Machine))
                {
                    UserPrincipal user =
                        UserPrincipal.FindByIdentity(context, name);

                    if (user == null)
                    {
                        Console.WriteLine("[user] " + name + " not found");
                        return;
                    }

                    if (!IsServiceAccount(user))
                    {
                        Console.WriteLine(
                            "[user] " + name +
                            " is not a QRScanner service account; skipped");
                        user.Dispose();
                        return;
                    }

                    if (_dryRun)
                    {
                        Console.WriteLine("[user] would delete " + name);
                        user.Dispose();
                        return;
                    }

                    user.Delete();
                    user.Dispose();
                    Console.WriteLine("[user] deleted " + name);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "[user] could not delete " + name + ": " +
                    ex.Message);
            }
        }

        private static bool IsOurShare()
        {
            CommandResult result = Run(
                "net",
                "share " + ShareName,
                "check share",
                ignoreError: true,
                capture: true);

            string output = result.Output ?? string.Empty;
            int index = output.IndexOf(
                Root,
                StringComparison.OrdinalIgnoreCase);

            while (index >= 0)
            {
                int end = index + Root.Length;
                if (end >= output.Length ||
                    char.IsWhiteSpace(output[end]))
                {
                    return true;
                }

                index = output.IndexOf(
                    Root,
                    index + 1,
                    StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private static void DeleteDirectoryWithRetry(string path)
        {
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    Directory.Delete(path, true);
                    return;
                }
                catch (Exception)
                {
                    if (attempt == 3)
                    {
                        throw;
                    }

                    System.Threading.Thread.Sleep(1000);
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

        private static string ResolveServiceUserName(string requested)
        {
            try
            {
                using (PrincipalContext context =
                    new PrincipalContext(ContextType.Machine))
                {
                    if (_userNameExplicit)
                    {
                        UserPrincipal explicitUser =
                            UserPrincipal.FindByIdentity(context, requested);

                        if (explicitUser != null)
                        {
                            bool ours = IsServiceAccount(explicitUser);
                            explicitUser.Dispose();

                            if (!ours)
                            {
                                Console.WriteLine(
                                    "[user] warning: " + requested +
                                    " already belongs to another account; " +
                                    "it will be reused because --user was given");
                            }
                        }

                        return requested;
                    }

                    string name = requested;
                    for (int suffix = 1; suffix <= 99; suffix++)
                    {
                        if (suffix > 1)
                        {
                            name = UserName + suffix;
                        }

                        if (IsSimulatedTaken(name))
                        {
                            Console.WriteLine(
                                "[user] " + name +
                                " is used by another account, trying next name");
                            continue;
                        }

                        UserPrincipal user =
                            UserPrincipal.FindByIdentity(context, name);

                        if (user == null)
                        {
                            if (suffix > 1)
                            {
                                Console.WriteLine(
                                    "[user] selected free name " + name);
                            }

                            return name;
                        }

                        bool ours = IsServiceAccount(user);
                        user.Dispose();

                        if (ours)
                        {
                            return name;
                        }

                        Console.WriteLine(
                            "[user] " + name +
                            " is used by another account, trying next name");
                    }

                    throw new InvalidOperationException(
                        "No free qrscanner account name found.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[user] name check failed: " + ex.Message);
                return requested;
            }
        }

        private static bool IsSimulatedTaken(string name)
        {
            return _dryRun &&
                !string.IsNullOrWhiteSpace(_simulateTakenUser) &&
                string.Equals(
                    name,
                    _simulateTakenUser.Trim(),
                    StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsServiceAccount(UserPrincipal user)
        {
            string description = user.Description ?? string.Empty;
            if (description.IndexOf(
                "QRScanner",
                StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            string displayName = user.DisplayName ?? string.Empty;
            return displayName.IndexOf(
                "QRScanner",
                StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void EnsureUser(string password)
        {
            using (PrincipalContext context =
                new PrincipalContext(ContextType.Machine))
            {
                UserPrincipal user = UserPrincipal.FindByIdentity(
                    context,
                    _userName);

                if (user == null)
                {
                    user = new UserPrincipal(context);
                    user.Name = _userName;
                    user.DisplayName = "QRScanner SMB";
                    user.Description = ServiceAccountDescription;
                    user.SetPassword(password);
                    user.PasswordNeverExpires = true;
                    user.UserCannotChangePassword = true;
                    user.Save();
                    Console.WriteLine(
                        "[user] created " + _userName +
                        " via local accounts API");
                }
                else
                {
                    user.SetPassword(password);
                    user.PasswordNeverExpires = true;
                    user.UserCannotChangePassword = true;
                    user.Description = ServiceAccountDescription;
                    user.Save();
                    Console.WriteLine(
                        "[user] updated " + _userName +
                        " via local accounts API");
                }

                user.Dispose();
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

            try
            {
                ShowSettingsQr(server, share, user, password);
            }
            catch (Exception ex)
            {
                Console.WriteLine("QR: " + ex.Message);
                Console.WriteLine(
                    "QR window could not be shown. " +
                    "Use " + AccessFile + " on the scanner.");
            }
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

        private static string GetArgValue(string[] args, string value)
        {
            string prefix = "--" + value + "=";
            for (int index = 0; index < args.Length; index++)
            {
                string arg = args[index];
                if (string.Equals(
                    arg,
                    "--" + value,
                    StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        arg,
                        "/" + value,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return index + 1 < args.Length
                        ? args[index + 1]
                        : null;
                }

                if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return arg.Substring(prefix.Length);
                }
            }

            return null;
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
