using System.Runtime.InteropServices;
using HdmAutomation.Core;
using HdmAutomation.Engine;
using HdmAutomation.Windows;

namespace HdmAutomation.App;

internal sealed class MainForm : Form
{
    private const int StopHotKeyId = 1;
    private const int WmHotKey = 0x0312;
    private const uint ModControl = 0x2, ModAlt = 0x1, VkX = 0x58;

    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly IReadOnlyList<Routine> _routines;
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly RoutineRunner _runner;

    private readonly ComboBox _routineBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly Label _detectedLabel = new() { AutoSize = true, ForeColor = SystemColors.GrayText };
    private readonly TextBox _rangeBox = new() { Dock = DockStyle.Fill };
    private readonly TextBox _outputBox = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _stepBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly Button _runButton = new() { Text = "Run", AutoSize = true };
    private readonly Button _stopButton = new() { Text = "Stop (Ctrl+Alt+X)", AutoSize = true, Enabled = false };
    private readonly CheckBox _autoRun = new() { Text = "Run automatically when the program opens (after a 10 second countdown)", AutoSize = true };
    private readonly Label _status = new() { AutoSize = true, Text = "Ready." };
    private readonly TextBox _log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Font = new Font("Consolas", 9f) };

    private CancellationTokenSource? _cancel;
    private System.Windows.Forms.Timer? _countdown;

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

        foreach (var r in _routines) _routineBox.Items.Add(r.Name);
        _routineBox.SelectedIndexChanged += (_, _) => RefreshSteps();
        _rangeBox.TextChanged += (_, _) => RefreshSteps();
        _runButton.Click += (_, _) => StartRun();
        _stopButton.Click += (_, _) => _cancel?.Cancel();

        _rangeBox.Text = DateRange.PreviousMonth(DateOnly.FromDateTime(DateTime.Today)).ToString();
        _outputBox.Text = _settings.OutputFolder ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "HDM Exports");
        _autoRun.Checked = _settings.RunOnLaunch;

        var last = _routines.ToList().FindIndex(r => r.Name == _settings.LastRoutine);
        _routineBox.SelectedIndex = _routines.Count == 0 ? -1 : Math.Max(last, 0);
    }

    private void BuildLayout()
    {
        var grid = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, Padding = new Padding(12) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        void Row(string label, Control control, Control? extra = null)
        {
            grid.RowCount++;
            grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 8, 6) }, 0, grid.RowCount - 1);
            grid.Controls.Add(control, 1, grid.RowCount - 1);
            if (extra is not null) grid.Controls.Add(extra, 2, grid.RowCount - 1);
        }

        var detect = new Button { Text = "Detect window", AutoSize = true };
        detect.Click += (_, _) => DetectRoutine();
        var browse = new Button { Text = "Browse...", AutoSize = true };
        browse.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog { SelectedPath = _outputBox.Text };
            if (dialog.ShowDialog(this) == DialogResult.OK) _outputBox.Text = dialog.SelectedPath;
        };

        Row("Routine", _routineBox, detect);
        grid.Controls.Add(_detectedLabel, 1, grid.RowCount);
        grid.RowCount++;
        Row("Date range", _rangeBox);
        Row("Export folder", _outputBox, browse);
        Row("Start at step", _stepBox);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(12, 0, 12, 0) };
        buttons.Controls.AddRange(new Control[] { _runButton, _stopButton });

        var options = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(12, 6, 12, 0), FlowDirection = FlowDirection.TopDown };
        options.Controls.AddRange(new Control[] { _autoRun, _status });

        var logPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        logPanel.Controls.Add(_log);

        Controls.Add(logPanel);
        Controls.Add(options);
        Controls.Add(buttons);
        Controls.Add(grid);
    }

    // ---- life cycle -------------------------------------------------------------------------------------------

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!RegisterHotKey(Handle, StopHotKeyId, ModControl | ModAlt, VkX))
        {
            Log("Could not register the Ctrl+Alt+X stop shortcut (another program uses it). Use the Stop button.");
        }
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        UnregisterHotKey(Handle, StopHotKeyId);
        base.OnHandleDestroyed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotKey && m.WParam.ToInt32() == StopHotKeyId) _cancel?.Cancel();
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
        _settings.OutputFolder = _outputBox.Text;
        _settings.LastRoutine = Selected?.Name;
        _settings.RunOnLaunch = _autoRun.Checked;
        _settings.Save();
        base.OnFormClosing(e);
    }

    // ---- choosing what to run -----------------------------------------------------------------------------------

    private Routine? Selected => _routineBox.SelectedIndex >= 0 ? _routines[_routineBox.SelectedIndex] : null;

    /// <summary>Selects the routine whose start window is on screen right now.</summary>
    private void DetectRoutine()
    {
        var found = new List<Routine>();
        foreach (var r in _routines)
        {
            if (WindowFinder.FindByTitle("any", r.StartWindowTitle).Count == 1) found.Add(r);
        }

        if (found.Count == 1)
        {
            _routineBox.SelectedIndex = _routines.ToList().IndexOf(found[0]);
            _detectedLabel.Text = $"Found the '{found[0].StartWindowTitle}' window.";
        }
        else if (found.Count > 1)
        {
            _detectedLabel.Text = $"{found.Count} routines match the windows on screen; pick one.";
        }
        else
        {
            _detectedLabel.Text = "No routine's start window is on screen.";
        }
    }

    /// <summary>Lists the steps and starts at the first one not finished for this routine and date range.</summary>
    private void RefreshSteps()
    {
        _stepBox.Items.Clear();
        if (Selected is not { } routine) return;

        var steps = routine.GetSteps();
        for (var i = 0; i < steps.Count; i++) _stepBox.Items.Add($"{i + 1}. {steps[i].Name}");

        var next = 0;
        if (DateRange.TryParse(_rangeBox.Text, out var range, out _))
        {
            var state = _runner.LoadState(routine, range.ToString());
            if (state is not null && state.Steps.Count == steps.Count) next = Math.Min(state.NextStepIndex(), steps.Count - 1);
        }
        _stepBox.SelectedIndex = next;
    }

    // ---- running --------------------------------------------------------------------------------------------------

    private void BeginCountdown()
    {
        var left = 10;
        _countdown = new System.Windows.Forms.Timer { Interval = 1000 };
        _stopButton.Enabled = true;
        _stopButton.Text = "Cancel";
        _stopButton.Click += CancelCountdown;
        _status.Text = $"Starting in {left} seconds. Press Cancel to stop.";
        _countdown.Tick += (_, _) =>
        {
            left--;
            if (left > 0) { _status.Text = $"Starting in {left} seconds. Press Cancel to stop."; return; }
            EndCountdown();
            StartRun();
        };
        _countdown.Start();
    }

    private void CancelCountdown(object? sender, EventArgs e)
    {
        if (_countdown is null) return;
        EndCountdown();
        _status.Text = "Automatic start cancelled.";
    }

    private void EndCountdown()
    {
        _countdown?.Stop();
        _countdown?.Dispose();
        _countdown = null;
        _stopButton.Click -= CancelCountdown;
        _stopButton.Text = "Stop (Ctrl+Alt+X)";
        _stopButton.Enabled = false;
    }

    private async void StartRun()
    {
        if (_cancel is not null || Selected is not { } routine) return;

        if (!DateRange.TryParse(_rangeBox.Text, out var range, out var error))
        {
            MessageBox.Show(this, error, "Date range", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var outputFolder = _outputBox.Text.Trim();
        if (outputFolder.Length == 0)
        {
            MessageBox.Show(this, "Choose a folder for the exported files.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var startStep = Math.Max(_stepBox.SelectedIndex, 0);
        var parameters = new RunParameters(range.ToString(), outputFolder);

        _cancel = new CancellationTokenSource();
        _runButton.Enabled = false;
        _stopButton.Enabled = true;
        _status.Text = $"Running '{routine.Name}'...  (Ctrl+Alt+X stops it)";
        Log($"Starting '{routine.Name}' for {parameters.DateRange} at step {startStep + 1}");

        // Get out of the way: the routine reads and clicks the other program's windows.
        WindowState = FormWindowState.Minimized;

        RunResult result;
        try
        {
            var token = _cancel.Token;
            result = await Task.Run(() => _runner.Run(routine, parameters, startStep, token));
        }
        catch (Exception ex)
        {
            result = new RunResult(RunOutcome.Failed, ex.Message);
        }

        _cancel.Dispose();
        _cancel = null;
        _runButton.Enabled = true;
        _stopButton.Enabled = false;
        WindowState = FormWindowState.Normal;
        Activate();

        _status.Text = result.Outcome switch
        {
            RunOutcome.Completed => $"Finished. Files are in {outputFolder}",
            RunOutcome.Cancelled => "Stopped.",
            _ => "Failed. See the log.",
        };
        Log(result.Message);
        RefreshSteps();
        MessageBox.Show(this, result.Message, routine.Name, MessageBoxButtons.OK,
            result.Outcome == RunOutcome.Completed ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }

    private void Log(string message)
    {
        FileLog.Write(message);
        void Append() => _log.AppendText($"{DateTime.Now:HH:mm:ss}  {message}{Environment.NewLine}");
        if (InvokeRequired) BeginInvoke(Append);
        else Append();
    }
}
