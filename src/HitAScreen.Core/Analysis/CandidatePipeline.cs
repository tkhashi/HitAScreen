using HitAScreen.Platform.Abstractions;

namespace HitAScreen.Core;

public sealed class CandidatePipeline
{
    private readonly IReadOnlyList<ICandidateProvider> _providers;

    public CandidatePipeline(IEnumerable<ICandidateProvider> providers)
    {
        _providers = providers?.ToArray() ?? throw new ArgumentNullException(nameof(providers));
    }

    public IReadOnlyList<UiCandidate> GetActionableElements(AnalysisContext context)
    {
        if (_providers.Count == 0)
        {
            return Array.Empty<UiCandidate>();
        }

        var merged = new List<UiCandidate>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var provider in _providers)
        {
            var candidates = provider.GetCandidates(context);
            foreach (var candidate in candidates)
            {
                var key = string.IsNullOrWhiteSpace(candidate.CandidateId)
                    ? $"{candidate.Source}:{candidate.Role}:{candidate.Bounds.X:0.##}:{candidate.Bounds.Y:0.##}:{candidate.Bounds.Width:0.##}:{candidate.Bounds.Height:0.##}"
                    : candidate.CandidateId;
                if (!seen.Add(key))
                {
                    continue;
                }

                merged.Add(candidate);
            }
        }

        return merged;
    }
}
