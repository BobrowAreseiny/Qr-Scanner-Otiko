$csc = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
& $csc /nologo /target:exe /out:QRScannerSetup.exe /win32manifest:QRScannerSetup.exe.manifest /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:QRCoder.dll Program.cs
