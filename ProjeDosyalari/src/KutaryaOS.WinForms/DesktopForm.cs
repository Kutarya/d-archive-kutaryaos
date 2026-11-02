using KutaryaOS.Core;

namespace KutaryaOS.WinForms;

public sealed class DesktopForm : Form
{
    private readonly DesktopContext _context;
    private readonly WallpaperPanel _workspace = new() { Dock = DockStyle.Fill };
    private readonly FlowLayoutPanel _icons = new() { Location = new(20, 28), Width = 128, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = Color.Transparent };
    private readonly FlowLayoutPanel _tasks = new() { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true };
    private readonly Dictionary<string, VirtualApplication> _applications;
    private readonly List<Form> _windows = [];
    private readonly AiPanel _ai;
    private readonly System.Windows.Forms.Timer _slide = new() { Interval = 15 };
    private readonly System.Windows.Forms.Timer _clockTimer = new() { Interval = 1000 };
    private readonly Label _clock = new() { AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
    private readonly Label _resources = Ui.Label("", 10);
    private Palette _palette;
    private bool _aiOpen;
    private Panel? _startMenu;
    public int OpenWindowCount => _windows.Count;
    public bool AiIsOpen => _aiOpen;
    public DesktopForm(DesktopContext context)
    {
        _context = context; _palette = Palette.FromName(context.Configuration.LoadTheme());
        _applications = new VirtualApplication[] { new FileManagerApplication(), new NotesApplication(), new CalculatorApplication(), new MonitorApplication(), new SettingsApplication(), new FileManagerApplication(true) }.ToDictionary(a => a.Id);
        Ui.Style(this); Text = "Kutarya OS • " + context.Machine.Name; ClientSize = new(1280, 800); MinimumSize = new(960, 640); KeyPreview = true;
        _ai = new(context, Launch, ToggleAi) { Dock = DockStyle.Right, Width = 0 };
        var taskbar = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 66, BackColor = Color.FromArgb(243, 247, 251), ColumnCount = 4, Padding = new(10, 8, 10, 8) };
        taskbar.ColumnStyles.Add(new(SizeType.Absolute, 160)); taskbar.ColumnStyles.Add(new(SizeType.Percent, 100)); taskbar.ColumnStyles.Add(new(SizeType.Absolute, 160)); taskbar.ColumnStyles.Add(new(SizeType.Absolute, 135));
        taskbar.RowStyles.Add(new(SizeType.Percent, 100));
        var start = Ui.Button("❖  kutarya", (_, _) => ToggleStart(), true); start.Name = "StartButton"; start.Dock = DockStyle.Fill; start.BackColor = _palette.Accent; start.FlatAppearance.BorderColor = start.BackColor;
        var aiButton = Ui.Button("✦ Kutarya Us", (_, _) => ToggleAi()); aiButton.Name = "AiButton"; aiButton.Dock = DockStyle.Fill; aiButton.BackColor = Color.FromArgb(222, 235, 242);
        taskbar.Controls.Add(start, 0, 0); taskbar.Controls.Add(_tasks, 1, 0); taskbar.Controls.Add(aiButton, 2, 0); taskbar.Controls.Add(_clock, 3, 0);
        Controls.Add(_workspace); Controls.Add(_ai); Controls.Add(taskbar);
        _workspace.Controls.Add(_icons);
        var device = new Panel { Size = new(300, 58), BackColor = Color.FromArgb(232, 240, 245), Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
        var deviceHeading = Ui.Label(context.Machine.Name, 10, true); deviceHeading.Location = new(14, 8);
        _resources.SetBounds(14, 32, 280, 20); _resources.Font = Ui.Font(9); _resources.AutoSize = false;
        device.Controls.AddRange([deviceHeading, _resources]); _workspace.Controls.Add(device);
        void FitCards()
        {
            _icons.Height = Math.Max(100, _workspace.Height - 50);
            device.Location = new(Math.Max(145, _workspace.Width - 320), Math.Max(0, _workspace.Height - 80));
        }
        _workspace.Layout += (_, _) => FitCards();
        _workspace.AllowDrop = true; _icons.AllowDrop = true;
        foreach (var target in new Control[] { _workspace, _icons })
        {
            target.DragEnter += (_, e) => e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Move : DragDropEffects.None;
            target.DragDrop += async (_, e) => await Ui.GuardAsync(this, async () =>
            {
                if (e.Data?.GetData(DataFormats.FileDrop) is string[] sources) MessageBox.Show(this, await FileManagerApplication.ImportFilesAsync(context.Files, sources, "Masaustu"), "Dosya aktarımı");
            });
        }
        _slide.Tick += (_, _) =>
        {
            var target = _aiOpen ? Math.Min(390, ClientSize.Width - 500) : 0;
            var difference = target - _ai.Width;
            if (Math.Abs(difference) < 5) { _ai.Width = target; _slide.Stop(); FitWindows(); }
            else _ai.Width += Math.Sign(difference) * Math.Max(5, Math.Abs(difference) / 3);
        };
        _clockTimer.Tick += (_, _) => _clock.Text = DateTime.Now.ToString("HH:mm\n dd MMM ddd"); _clockTimer.Start(); _clock.Text = DateTime.Now.ToString("HH:mm\n dd MMM ddd");
        _workspace.Resize += (_, _) => { RefreshWallpaper(); _icons.Height = Math.Max(100, _workspace.Height - 50); FitWindows(); };
        context.Files.Changed += OnFilesChanged; context.Budget.Changed += OnBudgetChanged;
        FormClosing += (_, e) =>
        {
            foreach (var window in _windows.ToArray()) { window.Close(); if (!window.IsDisposed) { e.Cancel = true; break; } }
        };
        FormClosed += (_, _) => { _slide.Dispose(); _clockTimer.Dispose(); context.Files.Changed -= OnFilesChanged; context.Budget.Changed -= OnBudgetChanged; _workspace.BackgroundImage?.Dispose(); };
        KeyDown += (_, e) => { if (e.Control && e.KeyCode == Keys.Space) { ToggleAi(); e.SuppressKeyPress = true; } if (e.KeyCode == Keys.Escape) _startMenu?.Hide(); };
        Shown += (_, _) => FitCards();
        RefreshIcons(); RefreshBudget(); RefreshWallpaper();
    }
    public void ApplyTheme(string name) { _palette = Palette.FromName(name); RefreshWallpaper(); }
    private void RefreshWallpaper()
    {
        if (_workspace.Width <= 0 || _workspace.Height <= 0) return;
        var old = _workspace.BackgroundImage; _workspace.BackgroundImage = Ui.Wallpaper(_workspace.Size, _palette); old?.Dispose();
    }
    private void OnFilesChanged(object? sender, EventArgs e) { if (IsDisposed) return; if (InvokeRequired) BeginInvoke((Action)RefreshIcons); else RefreshIcons(); }
    private void OnBudgetChanged(object? sender, EventArgs e) => RefreshBudget();
    private void RefreshBudget() => _resources.Text = $"{_context.Machine.RamMb} MB RAM  ·  {_context.Machine.StorageGb} GB disk";
    private void RefreshIcons()
    {
        foreach (Control old in _icons.Controls.Cast<Control>().ToArray()) { _icons.Controls.Remove(old); if (old is Button button) button.Image?.Dispose(); old.Dispose(); }
        foreach (var id in new[] { "files", "notes", "calculator", "monitor", "settings", "trash" }) AddIcon(_applications[id].Title, id, () => Launch(id));
        foreach (var entry in _context.Files.List("Masaustu")) AddIcon(entry.Name, entry.IsDirectory ? "folder" : "notes", () => { if (entry.IsDirectory) Launch("files", entry.Path); else Launch("notes", entry.Path); });
    }
    private void AddIcon(string title, string kind, Action action)
    {
        var button = new Button { Text = title, Image = Ui.Icon(kind, 50), TextImageRelation = TextImageRelation.ImageAboveText, Size = new(105, 91), FlatStyle = FlatStyle.Flat, Font = Ui.Font(9), ForeColor = Color.White, BackColor = Color.Transparent, Cursor = Cursors.Hand, Margin = new(0, 0, 0, 8) };
        button.FlatAppearance.BorderSize = 0; button.FlatAppearance.MouseOverBackColor = Color.FromArgb(55, 103, 126); button.Click += (_, _) => action(); _icons.Controls.Add(button);
    }
    public void Launch(string id) => Launch(id, null);
    public void Launch(string id, string? argument)
    {
        if (id is "settings-api" or "settings-local") { argument = id; id = "settings"; }
        try
        {
            if (!_applications.TryGetValue(id, out var application)) throw new ArgumentException("Uygulama bulunamadı.");
            _startMenu?.Hide();
            var lease = _context.Budget.Acquire(application.Title, application.RamCost);
            Form window;
            try { window = application.CreateWindow(_context, argument); }
            catch { lease.Dispose(); throw; }
            window.TopLevel = false; window.StartPosition = FormStartPosition.Manual;
            window.Location = new(155 + (_windows.Count * 24) % 110, 30 + (_windows.Count * 28) % 130);
            window.Size = new(Math.Min(window.Width, Math.Max(400, _workspace.Width - window.Left - 10)), Math.Min(window.Height, Math.Max(300, _workspace.Height - 80)));
            var task = Ui.Button(application.Title, (_, _) => { window.WindowState = FormWindowState.Normal; window.Show(); window.BringToFront(); }); task.Width = 145;
            _windows.Add(window); _workspace.Controls.Add(window); _tasks.Controls.Add(task);
            window.FormClosed += (_, _) => { _windows.Remove(window); _tasks.Controls.Remove(task); task.Dispose(); lease.Dispose(); };
            window.Resize += (_, _) => { if (window.WindowState == FormWindowState.Minimized) window.Hide(); };
            window.Show(); window.BringToFront();
        }
        catch (Exception error) { Ui.Error(this, error); }
    }
    private void FitWindows()
    {
        foreach (var window in _windows)
        {
            if (window.WindowState == FormWindowState.Maximized) continue;
            window.Size = new(Math.Min(window.Width, Math.Max(400, _workspace.Width - 20)), Math.Min(window.Height, Math.Max(300, _workspace.Height - 20)));
            window.Location = new(Math.Max(0, Math.Min(window.Left, _workspace.Width - window.Width)), Math.Max(0, Math.Min(window.Top, _workspace.Height - window.Height)));
        }
    }
    public void ToggleAi() { _aiOpen = !_aiOpen; _slide.Start(); if (_aiOpen) _ = _ai.OnOpenedAsync(); }
    public void ToggleStart()
    {
        if (_startMenu != null) { _startMenu.Visible = !_startMenu.Visible; _startMenu.BringToFront(); return; }
        _startMenu = new Panel { Size = new(340, 415), BackColor = Color.FromArgb(247, 250, 253), BorderStyle = BorderStyle.FixedSingle };
        _startMenu.Location = new(10, Math.Max(0, _workspace.Height - 425)); _startMenu.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
        var title = Ui.Label(_context.Machine.Name, 17, true); title.Location = new(22, 19); _startMenu.Controls.Add(title);
        var y = 66;
        foreach (var application in _applications.Values)
        {
            var button = Ui.Button(application.Title, (_, _) => Launch(application.Id)); button.SetBounds(16, y, 302, 39); button.Image = Ui.Icon(application.Id, 25); button.ImageAlign = ContentAlignment.MiddleLeft; _startMenu.Controls.Add(button); y += 46;
        }
        var power = Ui.Button("⏻  Kutarya OS’yi kapat", (_, _) => Close()); power.SetBounds(16, 352, 302, 40); _startMenu.Controls.Add(power);
        _workspace.Controls.Add(_startMenu); _startMenu.BringToFront();
    }
}
