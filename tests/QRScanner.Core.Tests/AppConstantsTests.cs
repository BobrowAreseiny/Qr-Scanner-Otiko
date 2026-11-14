using QRScanner.Core;

namespace QRScanner.Core.Tests;

public class AppConstantsTests
{
    [Fact]
    public void AppName_IsNotEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(AppConstants.AppName));
    }

    [Fact]
    public void ApplicationId_MatchesLegacyPackage()
    {
        Assert.Equal("com.companyname.qrscanner", AppConstants.ApplicationId);
    }
}
