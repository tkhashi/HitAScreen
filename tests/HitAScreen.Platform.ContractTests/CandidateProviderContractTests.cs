using HitAScreen.Platform.Abstractions;
using HitAScreen.Platform.MacOS;
using Xunit;

namespace HitAScreen.Platform.ContractTests;

public sealed class CandidateProviderContractTests
{
    [Fact]
    public void AccessibilityProvider_ShouldImplementInterface()
    {
        var provider = new MacAccessibilityCandidateProvider();
        Assert.IsAssignableFrom<IAccessibilityElementProvider>(provider);
    }
}
