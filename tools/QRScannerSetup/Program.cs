using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text;

namespace QRScannerSetup
{
    internal static class Program
    {
        private const string Root = @"C:\QRScanner";
        private const string OrdersFolder = @"C:\QRScanner\Заказы";
        private const string QrFolder = @"C:\QRScanner\QR-коды";
        private const string ShareName = "QRScanner";
        private const string UserName = "qrscanner";
        private const string AccessFile = @"C:\QRScanner-access.txt";

        private static bool _dryRun;
        private static bool _noPause;
        private static bool _noNotepad;

        private static int Main(string[] args)
        {
            _dryRun = HasArg(args, "dry-run");
            _noPause = HasArg(args, "no-pause");
            _noNotepad = HasArg(args, "no-notepad");

            Console.Title = "QRScanner Setup";
            Console.WriteLine("QRScanner SMB setup");
            Console.WriteLine("===================");
            Console.WriteLine();

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

            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(OrdersFolder);
            Directory.CreateDirectory(QrFolder);

            string password = LoadOrCreatePassword();
            string account = Environment.MachineName + "\\" + UserName;

            Console.WriteLine("User:  " + account);
            Console.WriteLine("Share: " + ShareName);
            Console.WriteLine("Path:  " + Root);
            Console.WriteLine();

            if (UserExists())
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

            string ip = GetPrimaryIPv4();
            string[] lines =
            {
                "QRScanner SMB settings",
                "Server=" + ip,
                "Share=" + ShareName,
                "User=" + account,
                "Password=" + password,
                "Orders=\\\\" + ip + "\\" + ShareName + "\\Заказы",
                "QR=\\\\" + ip + "\\" + ShareName + "\\QR-коды",
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
                Console.WriteLine("Server:   " + ip);
                Console.WriteLine("Share:    " + ShareName);
                Console.WriteLine("User:     " + account);
                Console.WriteLine("Password: " + password);
                Console.WriteLine();
                Console.WriteLine("Folders:");
                Console.WriteLine("  " + OrdersFolder);
                Console.WriteLine("  " + QrFolder);
                Console.WriteLine();
                Console.WriteLine("UNC path: \\\\" + ip + "\\" + ShareName);
                Console.WriteLine();
                Console.WriteLine("Done.");

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
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

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
