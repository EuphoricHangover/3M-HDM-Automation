namespace HdmAutomation.App;

internal sealed partial class MainForm
{
    private const int FormPadding = 12;

    private readonly ComboBox _routineBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly Label _detectedLabel = new() { AutoSize = true, ForeColor = SystemColors.GrayText };
    private readonly TextBox _rangeBox = new() { Dock = DockStyle.Fill };
    private readonly TextBox _outputBox = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _stepBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly Button _runButton = new() { Text = "Run", AutoSize = true };
    private readonly Button _stopButton = new() { Text = StopButtonText, AutoSize = true, Enabled = false };
    private readonly CheckBox _autoRun = new()
    {
        Text = "Run automatically when the program opens (after a 10 second countdown)",
        AutoSize = true,
    };
    private readonly Label _status = new() { AutoSize = true, Text = "Ready." };
    private readonly TextBox _log = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill,
        Font = new Font("Consolas", 9f),
    };

    private void BuildLayout()
    {
        Controls.Add(CreateLogPanel());
        Controls.Add(CreateOptionsPanel());
        Controls.Add(CreateButtonPanel());
        Controls.Add(CreateSettingsGrid());
    }

    private TableLayoutPanel CreateSettingsGrid()
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            Padding = new Padding(FormPadding),
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var detect = new Button { Text = "Detect window", AutoSize = true };
        detect.Click += (_, _) => DetectRoutine();

        var browse = new Button { Text = "Browse...", AutoSize = true };
        browse.Click += (_, _) => BrowseForOutputFolder();

        AddRow(grid, "Routine", _routineBox, detect);
        grid.Controls.Add(_detectedLabel, 1, grid.RowCount);
        grid.RowCount++;
        AddRow(grid, "Date range", _rangeBox);
        AddRow(grid, "Files folder", _outputBox, browse);
        AddRow(grid, "Start at step", _stepBox);
        return grid;
    }

    private static void AddRow(TableLayoutPanel grid, string labelText, Control control, Control? extra = null)
    {
        var label = new Label
        {
            Text = labelText,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 8, 6),
        };

        grid.RowCount++;
        var row = grid.RowCount - 1;
        grid.Controls.Add(label, 0, row);
        grid.Controls.Add(control, 1, row);
        if (extra is not null) grid.Controls.Add(extra, 2, row);
    }

    private FlowLayoutPanel CreateButtonPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(FormPadding, 0, FormPadding, 0),
        };
        panel.Controls.AddRange(new Control[] { _runButton, _stopButton });
        return panel;
    }

    private FlowLayoutPanel CreateOptionsPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(FormPadding, 6, FormPadding, 0),
            FlowDirection = FlowDirection.TopDown,
        };
        panel.Controls.AddRange(new Control[] { _autoRun, _status });
        return panel;
    }

    private Panel CreateLogPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(FormPadding) };
        panel.Controls.Add(_log);
        return panel;
    }

    private void BrowseForOutputFolder()
    {
        using var dialog = new FolderBrowserDialog { SelectedPath = _outputBox.Text };
        if (dialog.ShowDialog(this) == DialogResult.OK) _outputBox.Text = dialog.SelectedPath;
    }
}
