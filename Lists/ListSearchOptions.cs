namespace HdmAutomation.Lists;

public sealed record ListSearchOptions
{
    public bool ScrollToTopFirst { get; init; } = true;
    public bool JumpToFirstLetter { get; init; } = true;
    public int PagesAfterJump { get; init; } = 5;
    public int MaxPages { get; init; } = 40;
    public int NotchesPerPage { get; init; } = 3;
    public double MaxErrorRatio { get; init; } = 0.12;
}
