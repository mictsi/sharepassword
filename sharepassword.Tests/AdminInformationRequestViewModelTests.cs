using SharePassword.ViewModels;

namespace SharePassword.Tests;

public class AdminInformationRequestViewModelTests
{
    [Fact]
    public void StatusBadges_WhenSubmittedAndExpiringSoon_ReturnsBothBadges()
    {
        var item = new AdminInformationRequestListItemViewModel
        {
            IsExpiringSoon = true,
            LastSubmittedAtUtc = DateTime.UtcNow
        };

        var badges = item.StatusBadges;

        Assert.Collection(
            badges,
            badge =>
            {
                Assert.Equal("Response received", badge.Label);
                Assert.Equal("accessed", badge.Tone);
            },
            badge =>
            {
                Assert.Equal("Expiring soon", badge.Label);
                Assert.Equal("warning", badge.Tone);
            });
    }
}