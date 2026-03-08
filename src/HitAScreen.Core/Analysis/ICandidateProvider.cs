using HitAScreen.Platform.Abstractions;

namespace HitAScreen.Core;

public interface ICandidateProvider
{
    string Name { get; }
    IReadOnlyList<UiCandidate> GetCandidates(AnalysisContext context);
}
