using HdmAutomation.Core;
using HdmAutomation.Engine;
using HdmAutomation.Windows.Desktop;

namespace HdmAutomation.App;

internal sealed partial class MainForm : Form
{
    private const string StopButtonText = "Stop (Ctrl+Alt+X)";
    private const int AutoStartSeconds = 10;

    private readonly IReadOnlyList<Routine> _routines;
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly RoutineRunner _runner;

    private CancellationTokenSource? _cancel;
    private AutoStartCountdown? _countdown;

    public MainForm()
    {
        Text = "HDM Automation";
        MinimumSize = new Size(640, 520);
        Size = new Size(760, 640);
        StartPosition = FormStartPosition.CenterScreen;

        _routines = RoutineCatalog.Discover(typeof(Routines.UrologySurgeryStats).Assembly);
        _runner = new RoutineRunner(
            Path.Combine(AppContext.BaseDirectory, "tessdata"),
            Path.Combine(AppSettings.DataFolder, "runs"),
            Log);

        BuildLayout();
        WireEvents();
        ApplySettings();
    }

    private Routine? Selected => _routineBox.SelectedIndex >= 0 ? _routines[_routineBox.SelectedIndex] : null;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!StopHotKey.Register(Handle))
        {
            Log("Could not register the Ctrl+Alt+X stop shortcut (another program uses it). Use the Stop button.");
        }
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        StopHotKey.Unregister(Handle);
        base.OnHandleDestroyed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (StopHotKey.IsPressed(m)) _cancel?.Cancel();
        base.WndProc(ref m);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        DetectRoutine();
        if (_autoRun.Checked && Selected is not null) BeginCountdown();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_cancel is not null)
        {
            e.Cancel = true;
            MessageBox.Show(this, "A routine is running. Stop it first.", Text);
            return;
        }
        SaveSettings();
        base.OnFormClosing(e);
    }

    private void WireEvents()
    {
        _routineBox.SelectedIndexChanged += (_, _) => RefreshSteps();
        _rangeBox.TextChanged += (_, _) => RefreshSteps();
        _runButton.Click += (_, _) => StartRun();
        _stopButton.Click += (_, _) => StopRequested();
    }

    private void ApplySettings()
    {
        foreach (var routine in _routines) _routineBox.Items.Add(routine.Name);

        _rangeBox.Text = DateRange.PreviousMonth(DateOnly.FromDateTime(DateTime.Today)).ToString();
        _outputBox.Text = _settings.OutputFolder ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "HDM Exports");
        _autoRun.Checked = _settings.RunOnLaunch;

        var lastIndex = _routines.ToList().FindIndex(routine => routine.Name == _settings.LastRoutine);
        _routineBox.SelectedIndex = _routines.Count == 0 ? -1 : Math.Max(lastIndex, 0);
    }

    private void SaveSettings()
    {
        _settings.OutputFolder = _outputBox.Text;
        _settings.LastRoutine = Selected?.Name;
        _settings.RunOnLaunch = _autoRun.Checked;
        _settings.Save();
    }

    private void DetectRoutine()
    {
        var matching = _routines
            .Where(routine => WindowFinder.FindByTitle(processName: null, routine.StartWindowTitle).Count == 1)
            .ToList();

        if (matching.Count == 1)
        {
            _routineBox.SelectedIndex = _routines.ToList().IndexOf(matching[0]);
            _detectedLabel.Text = $"Found the '{matching[0].StartWindowTitle}' window.";
        }
        else if (matching.Count > 1)
        {
            _detectedLabel.Text = $"{matching.Count} routines match the windows on screen; pick one.";
        }
        else
        {
            _detectedLabel.Text = "No routine's start window is on screen.";
        }
    }

    private void RefreshSteps()
    {
        _stepBox.Items.Clear();
        if (Selected is not { } routine) return;

        var steps = routine.GetSteps();
        for (var i = 0; i < steps.Count; i++) _stepBox.Items.Add($"{i + 1}. {steps[i].Name}");

        _stepBox.SelectedIndex = SuggestedStartStep(routine, steps.Count);
    }

    private int SuggestedStartStep(Routine routine, int stepCount)
    {
        if (!DateRange.TryParse(_rangeBox.Text, out var range, out _)) return 0;

        var state = _runner.LoadState(routine, range.ToString());
        if (state is null || state.Steps.Count != stepCount) return 0;
        return Math.Min(state.NextStepIndex(), stepCount - 1);
    }

    private void BeginCountdown()
    {
        _stopButton.Text = "Cancel";
        _stopButton.Enabled = true;
        _countdown = new AutoStartCountdown(
            AutoStartSeconds,
            seconds => _status.Text = $"Starting in {seconds} seconds. Press Cancel to stop.",
            () =>
            {
                EndCountdown();
                StartRun();
            });
        _countdown.Start();
    }

    private void EndCountdown()
    {
        _countdown?.Dispose();
        _countdown = null;
        _stopButton.Text = StopButtonText;
        _stopButton.Enabled = false;
    }

    private void StopRequested()
    {
        if (_countdown is not null)
        {
            EndCountdown();
            _status.Text = "Automatic start cancelled.";
            return;
        }
        _cancel?.Cancel();
    }

    private async void StartRun()
    {
        if (_cancel is not null || Selected is not { } routine) return;
        if (!TryReadParameters(out var parameters)) return;

        var startStep = Math.Max(_stepBox.SelectedIndex, 0);
        _cancel = new CancellationTokenSource();
        _runButton.Enabled = false;
        _stopButton.Enabled = true;
        _status.Text = $"Running '{routine.Name}'...  (Ctrl+Alt+X stops it)";
        Log($"Starting '{routine.Name}' for {parameters.DateRange} at step {startStep + 1}");
        WindowState = FormWindowState.Minimized;

        var result = await RunInBackground(routine, parameters, startStep);

        _cancel.Dispose();
        _cancel = null;
        _runButton.Enabled = true;
        _stopButton.Enabled = false;
        WindowState = FormWindowState.Normal;
        Activate();
        ShowResult(routine, parameters, result);
    }

    private bool TryReadParameters(out RunParameters parameters)
    {
        parameters = new RunParameters(string.Empty, string.Empty);

        if (!DateRange.TryParse(_rangeBox.Text, out var range, out var error))
        {
            MessageBox.Show(this, error, "Date range", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        var outputFolder = _outputBox.Text.Trim();
        if (outputFolder.Length == 0)
        {
            MessageBox.Show(this, "Choose a folder for the exported files.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        parameters = new RunParameters(range.ToString(), outputFolder);
        return true;
    }

    private async Task<RunResult> RunInBackground(Routine routine, RunParameters parameters, int startStep)
    {
        var token = _cancel!.Token;
        try
        {
            return await Task.Run(() => _runner.Run(routine, parameters, startStep, token));
        }
        catch (Exception ex)
        {
            return new RunResult(RunOutcome.Failed, ex.Message);
        }
    }

    private void ShowResult(Routine routine, RunParameters parameters, RunResult result)
    {
        _status.Text = result.Outcome switch
        {
            RunOutcome.Completed => $"Finished. Files are in {parameters.OutputFolder}",
            RunOutcome.Cancelled => "Stopped.",
            _ => "Failed. See the log.",
        };
        Log(result.Message);
        RefreshSteps();

        var icon = result.Outcome == RunOutcome.Completed ? MessageBoxIcon.Information : MessageBoxIcon.Warning;
        MessageBox.Show(this, result.Message, routine.Name, MessageBoxButtons.OK, icon);
    }

    private void Log(string message)
    {
        FileLog.Write(message);
        if (InvokeRequired) BeginInvoke(() => AppendToLog(message));
        else AppendToLog(message);
    }

    private void AppendToLog(string message) =>
        _log.AppendText($"{DateTime.Now:HH:mm:ss}  {message}{Environment.NewLine}");
}
