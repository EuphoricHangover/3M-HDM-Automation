<#
.SYNOPSIS
    Shows what an automation tool can "see" inside another program's windows.

.DESCRIPTION
    Read-only by default. For each matching window it lists:
      * every child control (Win32 class name, control id, position)
      * every UI Automation element (type, name, automation id, supported actions)
      * the item names inside combo boxes and list boxes, read two ways
        (UI Automation and classic Win32 messages)
    and ends with a verdict per list: readable by name, or OCR needed.

    Text typed into edit boxes and the contents of data grids are NOT printed,
    to keep patient data out of the output. Item names inside combo boxes and
    list boxes ARE printed, so check the output file before sharing it.

.PARAMETER Title
    Part of the window title to inspect (case-insensitive), e.g. "Report".

.PARAMETER Process
    Process name without .exe, e.g. "HDM". Inspects all of its visible windows.

.PARAMETER ExpandCombos
    Opens each combo box drop-down briefly so its items load, then closes it.
    Changes nothing, but you will see the drop-downs flicker.

.PARAMETER Select
    Exact item text to try selecting, e.g. "Listing Report". This DOES change the
    selection in the program. Tries UI Automation first, then Win32 messages, and
    reports which one worked.

.PARAMETER MaxItems
    Most items to print per list (default 200).

.PARAMETER MaxDepth
    How deep to walk the UI Automation tree (default 25).

.PARAMETER OutFile
    Where to save the report. Defaults to inspect-<time>.txt in the current folder.

.EXAMPLE
    .\Inspect-Windows.ps1
    Lists all visible top-level windows so you can find the program's titles.

.EXAMPLE
    .\Inspect-Windows.ps1 -Process HDM -ExpandCombos
    Dumps every window of HDM.exe, including drop-down items.

.EXAMPLE
    .\Inspect-Windows.ps1 -Title "Report Selection" -Select "Listing Report"
    Tries to pick "Listing Report" in a list on that window.

.NOTES
    Works in Windows PowerShell 5.1 (built into Windows 11) and PowerShell 7.
    If the program runs as administrator, run this as administrator too.
    If scripts are blocked:  powershell -ExecutionPolicy Bypass -File .\Inspect-Windows.ps1 ...
#>
[CmdletBinding()]
param(
    [string]$Title,
    [string]$Process,
    [switch]$ExpandCombos,
    [string]$Select,
    [int]$MaxItems = 200,
    [int]$MaxDepth = 25,
    [string]$OutFile
)

Set-StrictMode -Version 2
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

if (-not ('InspectW32' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public static class InspectW32
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    private delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent, EnumProc cb, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hWnd, StringBuilder sb, int max);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] private static extern int GetDlgCtrlID(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern IntPtr GetParent(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int index);
    [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam,
        uint flags, uint timeoutMs, out IntPtr result);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, StringBuilder lParam,
        uint flags, uint timeoutMs, out IntPtr result);

    private const uint SMTO_ABORTIFHUNG = 0x2;
    private const uint TIMEOUT_MS = 2000;

    // Returned by Send() when the target window did not answer in time.
    public const long NoAnswer = long.MinValue;

    public static void MakeDpiAware() { SetProcessDPIAware(); }

    public static List<IntPtr> TopWindows()
    {
        var list = new List<IntPtr>();
        EnumWindows(delegate (IntPtr h, IntPtr l) { list.Add(h); return true; }, IntPtr.Zero);
        return list;
    }

    // All descendants, depth-first, in Z order.
    public static List<IntPtr> ChildWindows(IntPtr parent)
    {
        var list = new List<IntPtr>();
        EnumChildWindows(parent, delegate (IntPtr h, IntPtr l) { list.Add(h); return true; }, IntPtr.Zero);
        return list;
    }

    public static string ClassOf(IntPtr h)
    {
        var sb = new StringBuilder(256);
        GetClassName(h, sb, sb.Capacity);
        return sb.ToString();
    }

    public static bool Visible(IntPtr h) { return IsWindowVisible(h); }
    public static int CtrlId(IntPtr h) { return GetDlgCtrlID(h); }
    public static IntPtr ParentOf(IntPtr h) { return GetParent(h); }
    public static int Style(IntPtr h) { return GetWindowLong(h, -16); }

    public static uint Pid(IntPtr h)
    {
        uint pid;
        GetWindowThreadProcessId(h, out pid);
        return pid;
    }

    public static RECT Rect(IntPtr h)
    {
        RECT r;
        GetWindowRect(h, out r);
        return r;
    }

    public static long Send(IntPtr h, uint msg, long wParam, long lParam)
    {
        IntPtr result;
        IntPtr ok = SendMessageTimeout(h, msg, new IntPtr(wParam), new IntPtr(lParam),
            SMTO_ABORTIFHUNG, TIMEOUT_MS, out result);
        if (ok == IntPtr.Zero) return NoAnswer;
        return result.ToInt64();
    }

    // For messages that fill a caller-supplied string buffer (WM_GETTEXT, CB_GETLBTEXT, LB_GETTEXT).
    // Windows copies the string across processes for these standard messages.
    public static string SendForText(IntPtr h, uint msg, long wParam, int length)
    {
        var sb = new StringBuilder(Math.Max(length, 0) + 1);
        IntPtr result;
        IntPtr ok = SendMessageTimeout(h, msg, new IntPtr(wParam), sb,
            SMTO_ABORTIFHUNG, TIMEOUT_MS, out result);
        if (ok == IntPtr.Zero) return null;
        return sb.ToString();
    }

    public static string WindowText(IntPtr h)
    {
        long len = Send(h, 0x000E /* WM_GETTEXTLENGTH */, 0, 0);
        if (len <= 0 || len == NoAnswer) return "";
        if (len > 4096) len = 4096;
        return SendForText(h, 0x000D /* WM_GETTEXT */, len + 1, (int)len) ?? "";
    }
}
'@
}

[InspectW32]::MakeDpiAware()

# ---------------------------------------------------------------------------
# Output
# ---------------------------------------------------------------------------

$script:report = New-Object System.Collections.Generic.List[string]
function Say([string]$text = '') {
    Write-Host $text
    $script:report.Add($text)
}

# One row per list found, for the verdict table at the end.
$script:lists = New-Object System.Collections.Generic.List[object]

# Edit boxes can hold patient data, so their text is never printed.
$EditClassPattern = 'Edit|RichEdit|TextBox|TMemo|Thunder.*Text'

# ---------------------------------------------------------------------------
# Win32 list reading (combo boxes and list boxes)
# ---------------------------------------------------------------------------

function Get-ListKind([string]$class) {
    if ($class -match 'ComboLBox') { return 'ListBox' }      # the drop-down part of a combo box
    if ($class -match 'ComboBoxEx') { return $null }          # wrapper; its child ComboBox is read instead
    if ($class -match 'ComboBox|COMBOBOX') { return 'ComboBox' }
    if ($class -match 'ListBox|LISTBOX') { return 'ListBox' }
    return $null
}

function Read-Win32List([IntPtr]$hwnd, [string]$kind) {
    if ($kind -eq 'ComboBox') {
        $GETCOUNT = 0x146; $GETTEXTLEN = 0x149; $GETTEXT = 0x148; $GETCURSEL = 0x147
        $HASSTRINGS = 0x200; $OWNERDRAW = 0x30
    } else {
        $GETCOUNT = 0x18B; $GETTEXTLEN = 0x18A; $GETTEXT = 0x189; $GETCURSEL = 0x188
        $HASSTRINGS = 0x40; $OWNERDRAW = 0x30
    }

    $style = [InspectW32]::Style($hwnd)
    $ownerDrawn = ($style -band $OWNERDRAW) -ne 0
    $hasStrings = ($style -band $HASSTRINGS) -ne 0

    $result = [pscustomobject]@{
        Count      = [InspectW32]::Send($hwnd, $GETCOUNT, 0, 0)
        Selected   = [InspectW32]::Send($hwnd, $GETCURSEL, 0, 0)
        OwnerDrawn = $ownerDrawn
        HasStrings = $hasStrings
        Items      = New-Object System.Collections.Generic.List[string]
        Note       = ''
    }

    if ($result.Count -eq [InspectW32]::NoAnswer) { $result.Note = 'program did not answer'; return $result }
    if ($result.Count -lt 0) { $result.Note = 'count failed'; return $result }
    if ($ownerDrawn -and -not $hasStrings) {
        # Owner-drawn without HASSTRINGS: the program paints the text itself and
        # only stores a number per item, so there is no text to read.
        $result.Note = 'owner-drawn without stored text'
        return $result
    }

    $n = [Math]::Min($result.Count, $MaxItems)
    for ($i = 0; $i -lt $n; $i++) {
        $len = [InspectW32]::Send($hwnd, $GETTEXTLEN, $i, 0)
        if ($len -lt 0) { $result.Items.Add('<unreadable>'); continue }
        $text = [InspectW32]::SendForText($hwnd, $GETTEXT, $i, [int]$len)
        if ($null -eq $text) { $text = '<no answer>' }
        $result.Items.Add($text)
    }
    return $result
}

function Select-Win32ListItem([IntPtr]$hwnd, [string]$kind, [int]$index) {
    if ($kind -eq 'ComboBox') { $SETCURSEL = 0x14E; $GETCURSEL = 0x147 }
    else { $SETCURSEL = 0x186; $GETCURSEL = 0x188 }

    [void][InspectW32]::Send($hwnd, $SETCURSEL, $index, 0)

    # SETCURSEL does not tell the program anything changed, so send the same
    # "selection changed" notice a real click would (CBN_SELCHANGE / LBN_SELCHANGE = 1).
    $parent = [InspectW32]::ParentOf($hwnd)
    $id = [InspectW32]::CtrlId($hwnd) -band 0xFFFF
    $wParam = (1 -shl 16) -bor $id
    [void][InspectW32]::Send($parent, 0x0111 <# WM_COMMAND #>, $wParam, $hwnd.ToInt64())

    return ([InspectW32]::Send($hwnd, $GETCURSEL, 0, 0) -eq $index)
}

# ---------------------------------------------------------------------------
# Window discovery
# ---------------------------------------------------------------------------

$procNames = @{}
function Get-ProcName([uint32]$procId) {
    if (-not $procNames.ContainsKey($procId)) {
        $p = Get-Process -Id $procId -ErrorAction SilentlyContinue
        $procNames[$procId] = if ($p) { $p.ProcessName } else { '?' }
    }
    return $procNames[$procId]
}

function Format-Rect($r) {
    return ('x={0} y={1} w={2} h={3}' -f $r.Left, $r.Top, ($r.Right - $r.Left), ($r.Bottom - $r.Top))
}

function Get-TargetWindows {
    foreach ($h in [InspectW32]::TopWindows()) {
        if (-not [InspectW32]::Visible($h)) { continue }
        $text = [InspectW32]::WindowText($h)
        $proc = Get-ProcName ([InspectW32]::Pid($h))
        $match = $true
        if ($Title -and $text -notlike "*$Title*") { $match = $false }
        if ($Process -and $proc -ne $Process) { $match = $false }
        if ($match) {
            [pscustomobject]@{ Handle = $h; Title = $text; Class = [InspectW32]::ClassOf($h); Process = $proc }
        }
    }
}

# ---------------------------------------------------------------------------
# Win32 dump
# ---------------------------------------------------------------------------

function Show-Win32Controls($win) {
    Say '  -- Win32 controls --'
    $children = [InspectW32]::ChildWindows($win.Handle)
    if ($children.Count -eq 0) { Say '  (none: the window draws everything itself, so expect to need OCR)' }

    foreach ($h in $children) {
        $class = [InspectW32]::ClassOf($h)
        $vis = if ([InspectW32]::Visible($h)) { '' } else { ' [hidden]' }
        $text = if ($class -match $EditClassPattern) { '<edit text not shown>' } else { [InspectW32]::WindowText($h) }
        Say ('  0x{0:X8} {1,-28} id={2,-6} "{3}" {4}{5}' -f $h.ToInt64(), $class, [InspectW32]::CtrlId($h),
            $text, (Format-Rect ([InspectW32]::Rect($h))), $vis)

        $kind = Get-ListKind $class
        if (-not $kind) { continue }

        $list = Read-Win32List $h $kind
        Say ('      {0}: {1} items, selected index {2}, owner-drawn={3}, has-strings={4} {5}' -f
            $kind, $list.Count, $list.Selected, $list.OwnerDrawn, $list.HasStrings, $list.Note)
        for ($i = 0; $i -lt $list.Items.Count; $i++) { Say ('        [{0}] {1}' -f $i, $list.Items[$i]) }
        if ($list.Count -gt $list.Items.Count -and $list.Items.Count -gt 0) {
            Say ('        ... {0} more not shown' -f ($list.Count - $list.Items.Count))
        }

        $readable = @($list.Items | Where-Object { $_ -and $_ -notmatch '^<' }).Count
        $script:lists.Add([pscustomobject]@{
            Window = $win.Title; Where = ('Win32 {0} id={1}' -f $class, [InspectW32]::CtrlId($h))
            Items = $list.Count; Named = $readable; Handle = $h; Kind = $kind; Texts = $list.Items
        })
    }
}

# ---------------------------------------------------------------------------
# UI Automation dump
# ---------------------------------------------------------------------------

$walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
$ActionPatterns = 'Invoke|SelectionItem|Toggle|ExpandCollapse|Value'

function Get-PatternNames($el) {
    $names = foreach ($p in $el.GetSupportedPatterns()) {
        $p.ProgrammaticName -replace 'PatternIdentifiers\.Pattern$', ''
    }
    return ($names | Sort-Object) -join ','
}

function Show-UiaElement($el, [int]$depth) {
    if ($depth -gt $MaxDepth) { return }
    try { $c = $el.Current } catch { return }

    $type = $c.ControlType.ProgrammaticName -replace '^ControlType\.', ''
    $patterns = Get-PatternNames $el
    $r = $c.BoundingRectangle
    $pos = if ($r.IsEmpty) { 'offscreen' } else { 'x={0} y={1} w={2} h={3}' -f [int]$r.X, [int]$r.Y, [int]$r.Width, [int]$r.Height }
    $name = $c.Name
    if ($type -eq 'Edit' -or $type -eq 'Document') { $name = "$name <value not shown>" }
    $clickable = if ($name -and $patterns -match $ActionPatterns) { '  <- usable by name' } else { '' }

    Say ('{0}{1} "{2}" id="{3}" class="{4}" [{5}] {6}{7}' -f ('  ' * ($depth + 1)), $type, $name,
        $c.AutomationId, $c.ClassName, $patterns, $pos, $clickable)

    # Report grids can hold patient rows: count them, don't print them.
    if ($type -in 'DataGrid', 'Table') {
        $rows = 0
        $child = $walker.GetFirstChild($el)
        while ($null -ne $child) { $rows++; $child = $walker.GetNextSibling($child) }
        Say ('{0}({1} rows/children, contents not shown)' -f ('  ' * ($depth + 2)), $rows)
        return
    }

    $expanded = $false
    if ($type -eq 'ComboBox' -and $ExpandCombos) {
        try {
            $ec = $el.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
            $ec.Expand(); Start-Sleep -Milliseconds 400; $expanded = $true
        } catch { }
    }

    $count = 0; $named = 0
    $texts = New-Object System.Collections.Generic.List[string]
    $child = $walker.GetFirstChild($el)
    while ($null -ne $child) {
        $count++
        if ($count -le $MaxItems) { Show-UiaElement $child ($depth + 1) }
        try {
            $cc = $child.Current
            if ($cc.ControlType -eq [System.Windows.Automation.ControlType]::ListItem -and $cc.Name) { $named++; $texts.Add($cc.Name) }
            # Combo boxes often nest their items one level down inside a List.
            if ($cc.ControlType -eq [System.Windows.Automation.ControlType]::List) {
                $g = $walker.GetFirstChild($child)
                while ($null -ne $g) {
                    if ($g.Current.Name) { $named++; $texts.Add($g.Current.Name) }
                    $g = $walker.GetNextSibling($g)
                }
            }
        } catch { }
        $child = $walker.GetNextSibling($child)
    }
    if ($count -gt $MaxItems) { Say ('{0}... {1} more not shown' -f ('  ' * ($depth + 2)), ($count - $MaxItems)) }

    if ($expanded) { try { $ec.Collapse() } catch { } }

    if ($type -in 'ComboBox', 'List') {
        $script:lists.Add([pscustomobject]@{
            Window = $script:currentWindow; Where = ('UIA {0} "{1}" id="{2}"' -f $type, $c.Name, $c.AutomationId)
            Items = $count; Named = $named; Handle = $null; Kind = 'UIA'; Texts = $texts
        })
    }
}

function Show-UiaTree($win) {
    Say '  -- UI Automation tree --'
    try {
        $root = [System.Windows.Automation.AutomationElement]::FromHandle($win.Handle)
        Show-UiaElement $root 0
    } catch {
        Say "  (UI Automation could not read this window: $($_.Exception.Message))"
    }
}

# ---------------------------------------------------------------------------
# Selecting an item by name
# ---------------------------------------------------------------------------

function Invoke-SelectTest($windows) {
    Say ''
    Say "== Trying to select ""$Select"" =="

    foreach ($win in $windows) {
        $root = [System.Windows.Automation.AutomationElement]::FromHandle($win.Handle)
        $cond = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::NameProperty, $Select)
        foreach ($el in $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)) {
            try {
                $sel = $el.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
                $sel.Select()
                Start-Sleep -Milliseconds 300
                if ($sel.Current.IsSelected) {
                    Say "  OK via UI Automation in ""$($win.Title)"". The bot can select this item by name."
                    return
                }
                Say '  UI Automation Select() ran but the item does not report as selected.'
            } catch {
                Say "  Found ""$Select"" via UI Automation, but it can't be selected that way."
            }
        }
    }

    foreach ($l in $script:lists) {
        if (-not $l.Handle) { continue }
        $index = $l.Texts.IndexOf($Select)
        if ($index -lt 0) { continue }
        if (Select-Win32ListItem $l.Handle $l.Kind $index) {
            Say "  OK via Win32 messages ($($l.Where)) in ""$($l.Window)""."
            Say '  Check the program: if the screen updated as if you clicked it, the bot can use this.'
            Say '  If nothing else changed on screen, the bot should click/type instead.'
            return
        }
        Say "  Win32 found ""$Select"" at index $index in $($l.Where) but the selection didn't stick."
    }

    Say "  Could not select ""$Select"" by name. Expect to use OCR plus a click for this list."
}

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

if (-not $OutFile) { $OutFile = Join-Path (Get-Location) ('inspect-{0:yyyyMMdd-HHmmss}.txt' -f (Get-Date)) }

Say "Window inspector  $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')  PowerShell $($PSVersionTable.PSVersion)"
Say 'Read-only unless -Select is used. Check this output for patient data before sharing it.'
Say ''

if (-not $Title -and -not $Process) {
    Say 'Visible top-level windows (rerun with -Title "<part of title>" or -Process <name>):'
    Say ''
    foreach ($w in Get-TargetWindows) {
        if (-not $w.Title) { continue }
        Say ('  {0,-24} {1,-30} "{2}"' -f $w.Process, $w.Class, $w.Title)
    }
    $script:report | Set-Content -Path $OutFile -Encoding UTF8
    Write-Host ''
    Write-Host "Saved to $OutFile"
    return
}

$windows = @(Get-TargetWindows)
if ($windows.Count -eq 0) {
    Say 'No visible window matched. Run with no arguments to list window titles and process names.'
    return
}

foreach ($win in $windows) {
    $script:currentWindow = $win.Title
    Say ('== Window "{0}"  process={1}  class={2}  {3} ==' -f $win.Title, $win.Process, $win.Class,
        (Format-Rect ([InspectW32]::Rect($win.Handle))))
    Show-Win32Controls $win
    Show-UiaTree $win
    Say ''
}

Say '== Lists found =='
if ($script:lists.Count -eq 0) {
    Say '  No combo boxes or lists were exposed. Lists here are likely custom-drawn: plan on OCR.'
}
foreach ($l in $script:lists) {
    $verdict = if ($l.Items -gt 0 -and $l.Named -ge $l.Items) { 'READABLE BY NAME' }
               elseif ($l.Named -gt 0) { 'PARTLY READABLE' }
               elseif ($l.Items -eq 0) { 'EMPTY (try -ExpandCombos, or open the list first)' }
               else { 'NOT READABLE -> OCR' }
    Say ('  {0,-48} {1,4} items, {2,4} named   {3}   [{4}]' -f $l.Where, $l.Items, $l.Named, $verdict, $l.Window)
}

if ($Select) { Invoke-SelectTest $windows }

$script:report | Set-Content -Path $OutFile -Encoding UTF8
Write-Host ''
Write-Host "Saved to $OutFile"
