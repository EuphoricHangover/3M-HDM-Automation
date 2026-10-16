using HdmAutomation.Core;

namespace HdmAutomation.Windows.Lists;

public sealed record ListSearchResult(MatchResult Match, int PagesRead, IReadOnlyList<string> Warnings);
