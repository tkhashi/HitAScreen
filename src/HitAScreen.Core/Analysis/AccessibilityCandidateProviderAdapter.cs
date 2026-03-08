using HitAScreen.Platform.Abstractions;

namespace HitAScreen.Core;

internal sealed class AccessibilityCandidateProviderAdapter(IAccessibilityElementProvider provider) : ICandidateProvider
{
    public string Name => "Accessibility";

    public IReadOnlyList<UiCandidate> GetCandidates(AnalysisContext context)
    {
        return provider.GetActionableElements(context);
    }
}
