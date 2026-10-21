
namespace HdmAutomation.Lists;

public sealed record ListSearchResult(MatchResult Match, int PagesRead, IReadOnlyList<string> Warnings);
