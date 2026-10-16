namespace HdmAutomation.App;

internal sealed class AutoStartCountdown : IDisposable
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 };
    private readonly Action<int> _onTick;
    private readonly Action _onElapsed;
    private int _secondsLeft;

    public AutoStartCountdown(int seconds, Action<int> onTick, Action onElapsed)
    {
        _secondsLeft = seconds;
        _onTick = onTick;
        _onElapsed = onElapsed;
        _timer.Tick += (_, _) => Advance();
    }

    public void Start()
    {
        _onTick(_secondsLeft);
        _timer.Start();
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
    }

    private void Advance()
    {
        _secondsLeft--;
        if (_secondsLeft > 0)
        {
            _onTick(_secondsLeft);
            return;
        }
        _timer.Stop();
        _onElapsed();
    }
}
