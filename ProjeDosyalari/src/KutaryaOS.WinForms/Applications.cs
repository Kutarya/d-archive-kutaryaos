using System.Globalization;
using KutaryaOS.Core;

namespace KutaryaOS.WinForms;

public sealed record DesktopContext(MachineConfig Machine, VirtualFileSystem Files, ResourceBudget Budget, ConfigurationStore Configuration, SecretStore Secrets, IAiProvider Ai, Action<string> ThemeChanged, LocalModelManager? LocalModel = null);

// Ortak sözleşme ve yaşam döngüsü: kalıtım ve çok biçimlilik.
public abstract class VirtualApplication(string id, string title, int ramCost)
{
    public string Id { get; } = id;
    public string Title { get; } = title;
    public int RamCost { get; } = ramCost;
    public abstract Form CreateWindow(DesktopContext context, string? argument = null);
    protected Form Window(Size size)
    {
        var form = new Form { Text = Title, ClientSize = size, MinimumSize = new(400, 300) }; Ui.Style(form); return form;
    }
}

public sealed class FileManagerApplication(bool trash = false) : VirtualApplication(trash ? "trash" : "files", trash ? "Çöp kutusu" : "Dosyalar", 96)
{
    public override Form CreateWindow(DesktopContext context, string? argument = null)
    {
        var form = Window(new(780, 470));
        var list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, BorderStyle = BorderStyle.None, Font = Ui.Font(10) };
        list.Columns.Add("Ad", 360); list.Columns.Add("Tür", 100); list.Columns.Add("Boyut", 100);
        var path = Ui.TextBox(argument ?? "Masaustu"); path.ReadOnly = true; path.Dock = DockStyle.Top;
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 52, Padding = new(4), WrapContents = false, AutoScroll = true };
        var status = Ui.Label("Dosyaları buraya sürükleyerek güvenle taşıyabilirsin.", 9); status.AutoSize = false; status.Dock = DockStyle.Bottom; status.Height = 35; status.Padding = new(12, 8, 0, 0);
        string current = argument ?? "Masaustu";
        void RefreshList()
        {
            if (form.IsDisposed) return;
            if (form.InvokeRequired) { form.BeginInvoke((Action)RefreshList); return; }
            list.Items.Clear();
            if (trash)
                foreach (var item in context.Files.ListTrash()) list.Items.Add(new ListViewItem([item.Original, "Silinmiş", "—"]) { Tag = item.Id });
            else
            {
                if (!Directory.Exists(context.Files.Resolve(current))) current = "";
                foreach (var item in context.Files.List(current)) list.Items.Add(new ListViewItem([item.Name, item.IsDirectory ? "Klasör" : Path.GetExtension(item.Name).TrimStart('.').ToUpperInvariant(), item.IsDirectory ? "—" : Ui.Bytes(item.Bytes)]) { Tag = item });
            }
            path.Text = trash ? "Sanal çöp kutusu • geri yüklenebilir" : "Kutarya / " + current;
            status.Text = $"{list.Items.Count} öğe • {Ui.Bytes(context.Files.UsedBytes)} / {context.Machine.StorageGb} GB";
        }
        VirtualEntry? Selected() => list.SelectedItems.Count > 0 ? list.SelectedItems[0].Tag as VirtualEntry : null;
        async Task OpenSelected()
        {
            var entry = Selected(); if (entry == null) return;
            if (entry.IsDirectory) { current = entry.Path; RefreshList(); }
            else if (new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif" }.Contains(Path.GetExtension(entry.Name).ToLowerInvariant()))
            {
                using var imageForm = new Form { Text = entry.Name, ClientSize = new(760, 500) }; Ui.Style(imageForm);
                using var source = Image.FromFile(context.Files.Resolve(entry.Path)); using var image = new Bitmap(source);
                imageForm.Controls.Add(new PictureBox { Dock = DockStyle.Fill, Image = image, SizeMode = PictureBoxSizeMode.Zoom }); imageForm.ShowDialog(form);
            }
            else if (new[] { ".exe", ".dll", ".msi", ".zip", ".pdf", ".docx" }.Contains(Path.GetExtension(entry.Name).ToLowerInvariant()))
                MessageBox.Show(form, "Bu dosya sanal depoda saklanır. Kutarya OS bu dosya türünü çalıştırmaz.", "Dosya bilgisi");
            else { using var editor = new NotesApplication().CreateWindow(context, entry.Path); editor.ShowDialog(form); }
            await Task.CompletedTask;
        }
        list.DoubleClick += async (_, _) => await Ui.GuardAsync(form, OpenSelected);
        if (trash) toolbar.Controls.Add(Ui.Button("Geri yükle", async (_, _) => await Ui.GuardAsync(form, async () => { if (list.SelectedItems.Count > 0) await context.Files.RestoreAsync((string)list.SelectedItems[0].Tag!); })));
        else
        {
            toolbar.Controls.Add(Ui.Button("↑ Üst klasör", (_, _) => { current = current.Contains('/') ? current[..current.LastIndexOf('/')] : ""; RefreshList(); }));
            toolbar.Controls.Add(Ui.Button("+ Klasör", async (_, _) => await Ui.GuardAsync(form, async () => { var name = Ui.Prompt(form, "Klasör adı"); if (name != null) await context.Files.CreateDirectoryAsync(Path.Combine(current, name)); })));
            toolbar.Controls.Add(Ui.Button("Yeniden adlandır", async (_, _) => await Ui.GuardAsync(form, async () => { var entry = Selected(); if (entry == null) return; var name = Ui.Prompt(form, "Yeni ad", entry.Name); if (name != null) await context.Files.MoveAsync(entry.Path, Path.Combine(current, name)); })));
            toolbar.Controls.Add(Ui.Button("Çöpe taşı", async (_, _) => await Ui.GuardAsync(form, async () => { var entry = Selected(); if (entry != null) await context.Files.TrashAsync(entry.Path); })));
            list.AllowDrop = true;
            list.DragEnter += (_, e) => e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Move : DragDropEffects.None;
            list.DragDrop += async (_, e) => await Ui.GuardAsync(form, async () =>
            {
                if (e.Data?.GetData(DataFormats.FileDrop) is not string[] sources) return;
                var results = await ImportFilesAsync(context.Files, sources, current); MessageBox.Show(form, results, "Dosya aktarımı");
            });
        }
        FlowLayoutPanel? transferBar = null;
        if (!trash)
        {
            transferBar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 52, Padding = new(4), WrapContents = false, AutoScroll = true };
            var export = Ui.Button("Ana PC’ye taşı", (_, _) =>
            {
                var entry = Selected(); if (entry == null) return;
                using var dialog = new HostExportForm(context.Files, entry); dialog.ShowDialog(form);
            });
            export.Name = "ExportToHostButton"; export.Enabled = false;
            export.Width = 180;
            list.SelectedIndexChanged += (_, _) => export.Enabled = Selected() != null;
            transferBar.Controls.Add(export);
            transferBar.Controls.Add(Ui.Label("Bir öğe seç → hedef klasörü belirle → taşı.", 9));
        }
        foreach (var button in toolbar.Controls.OfType<Button>()) button.Width = button.Text == "Yeniden adlandır" ? 190 : 140;
        EventHandler handler = (_, _) => RefreshList(); context.Files.Changed += handler; form.FormClosed += (_, _) => context.Files.Changed -= handler;
        form.Controls.Add(list); form.Controls.Add(path); form.Controls.Add(toolbar);
        if (transferBar != null) form.Controls.Add(transferBar);
        form.Controls.Add(status); RefreshList(); return form;
    }
    public static async Task<string> ImportFilesAsync(VirtualFileSystem files, IEnumerable<string> sources, string target)
    {
        var moved = 0; List<string> problems = [];
        foreach (var source in sources)
        {
            try
            {
                if (Directory.Exists(source)) { problems.Add(Path.GetFileName(source) + ": klasör yerine içindeki dosyaları seç."); continue; }
                var result = await files.ImportMoveAsync(source, target); if (result.Moved) moved++; else problems.Add(result.Message);
            }
            catch (Exception error) { problems.Add(Path.GetFileName(source) + ": " + error.Message); }
        }
        return $"{moved} dosya doğrulanarak taşındı." + (problems.Count > 0 ? "\n" + string.Join("\n", problems.Take(8)) : "");
    }
}

public sealed class NotesApplication() : VirtualApplication("notes", "Not defteri", 64)
{
    public override Form CreateWindow(DesktopContext context, string? argument = null)
    {
        var form = Window(new(700, 460));
        var text = new TextBox { Multiline = true, AcceptsTab = true, AcceptsReturn = true, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, Font = new("Consolas", 12), MaxLength = 2 * 1024 * 1024 };
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 50, Padding = new(5) };
        var status = Ui.Label(argument ?? "Yeni belge • kaydetmek için Ctrl+S"); status.AutoSize = false; status.Dock = DockStyle.Bottom; status.Height = 30; status.Padding = new(12, 5, 0, 0);
        var dirty = false; var loading = false; var previouslySaved = argument != null; string? file = argument;
        async Task Save()
        {
            var candidate = file;
            if (candidate == null) { var name = Ui.Prompt(form, "Dosya adı", "notlar.txt"); if (name == null) return; candidate = "Belgeler/" + name; }
            await context.Files.WriteTextAsync(candidate, text.Text, previouslySaved); file = candidate; previouslySaved = true; dirty = false; status.Text = file + " • kaydedildi"; form.Text = "Not defteri • " + Path.GetFileName(file);
        }
        toolbar.Controls.Add(Ui.Button("Kaydet  Ctrl+S", async (_, _) => await Ui.GuardAsync(form, Save), true));
        toolbar.Controls.Add(Ui.Button("Farklı kaydet", async (_, _) => await Ui.GuardAsync(form, async () =>
        {
            var name = Ui.Prompt(form, "Yeni dosya adı", file == null ? "notlar.txt" : Path.GetFileName(file)); if (name == null) return;
            var candidate = "Belgeler/" + name; await context.Files.WriteTextAsync(candidate, text.Text, false);
            file = candidate; previouslySaved = true; dirty = false; status.Text = file + " • kaydedildi"; form.Text = "Not defteri • " + name;
        })));
        text.TextChanged += (_, _) => { if (!loading) { dirty = true; status.Text = (file ?? "Yeni belge") + " • kaydedilmedi"; } };
        form.KeyPreview = true; form.KeyDown += async (_, e) => { if (e.Control && e.KeyCode == Keys.S) { e.SuppressKeyPress = true; await Ui.GuardAsync(form, Save); } };
        form.Shown += async (_, _) => { if (argument != null) await Ui.GuardAsync(form, async () => { loading = true; try { text.Text = await context.Files.ReadTextAsync(argument); dirty = false; } finally { loading = false; } }); };
        form.FormClosing += (_, e) => { if (dirty && MessageBox.Show(form, "Kaydedilmemiş değişiklikler var. Kaydetmeden kapatılsın mı?", "Not defteri", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) e.Cancel = true; };
        form.Controls.Add(text); form.Controls.Add(toolbar); form.Controls.Add(status); return form;
    }
}

public sealed class CalculatorApplication() : VirtualApplication("calculator", "Hesap makinesi", 32)
{
    public override Form CreateWindow(DesktopContext context, string? argument = null)
    {
        var form = Window(new(410, 420)); var display = new TextBox { Dock = DockStyle.Top, Height = 65, Font = Ui.Font(26), TextAlign = HorizontalAlignment.Right, ReadOnly = true, Text = "0" };
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 5, Padding = new(15) };
        for (var i = 0; i < 4; i++) grid.ColumnStyles.Add(new(SizeType.Percent, 25)); for (var i = 0; i < 5; i++) grid.RowStyles.Add(new(SizeType.Percent, 20));
        decimal accumulator = 0; string? operation = null; var fresh = true;
        foreach (var key in new[] { "C", "±", "%", "÷", "7", "8", "9", "×", "4", "5", "6", "−", "1", "2", "3", "+", "0", ".", "⌫", "=" })
        {
            var button = Ui.Button(key, (_, _) =>
            {
                try
                {
                    var value = decimal.Parse(display.Text, CultureInfo.InvariantCulture);
                    if (key == "C") { accumulator = 0; operation = null; display.Text = "0"; fresh = true; }
                    else if (key == "±") display.Text = (-value).ToString(CultureInfo.InvariantCulture);
                    else if (key == "%") display.Text = (value / 100).ToString(CultureInfo.InvariantCulture);
                    else if (key == "⌫") display.Text = display.Text.Length > 1 ? display.Text[..^1] : "0";
                    else if (key is "+" or "−" or "×" or "÷" or "=")
                    {
                        if (operation != null && !fresh) accumulator = operation switch { "+" => accumulator + value, "−" => accumulator - value, "×" => accumulator * value, "÷" => accumulator / value, _ => value }; else accumulator = value;
                        display.Text = accumulator.ToString(CultureInfo.InvariantCulture); operation = key == "=" ? null : key; fresh = true;
                    }
                    else { if (fresh) { display.Text = "0"; fresh = false; } if (key == ".") { if (!display.Text.Contains('.')) display.Text += "."; } else if (display.Text.Length < 25) display.Text = display.Text == "0" ? key : display.Text + key; }
                }
                catch (ArithmeticException) { display.Text = "0"; operation = null; fresh = true; MessageBox.Show(form, "Bu işlem hesaplanamadı.", "Hesap makinesi"); }
            }, key == "="); button.Dock = DockStyle.Fill; grid.Controls.Add(button);
        }
        form.Controls.Add(grid); form.Controls.Add(display); return form;
    }
}

public sealed class MonitorApplication() : VirtualApplication("monitor", "Kaynak izleyici", 48)
{
    public override Form CreateWindow(DesktopContext context, string? argument = null)
    {
        var form = Window(new(660, 420)); var body = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = form.BackColor, Font = Ui.Font(12) };
        var timer = new System.Windows.Forms.Timer { Interval = 1500 };
        void RefreshStats()
        {
            var host = new WindowsHardwareProvider().Read(context.Configuration.Root);
            body.Text = $"{context.Machine.Name}\n\nSANAL KAYNAK BÜTÇESİ\nRAM: {context.Budget.UsedRam} / {context.Budget.RamLimit} MB\nVRAM: {context.Budget.UsedVram} / {context.Budget.VramLimit} MB\nDisk: {Ui.Bytes(context.Files.UsedBytes)} / {context.Machine.StorageGb} GB\n\nGERÇEK UYGULAMA\nİşlem belleği: {Ui.Bytes(Environment.WorkingSet)}\nHost kullanılabilir RAM: {Ui.Bytes(host.AvailableRamBytes)}\n\nAÇIK SANAL UYGULAMALAR\n" + string.Join("\n", context.Budget.Applications.Select(a => $"• {a.Name} — {a.Ram} MB bütçe")) + "\n\nSanal bütçe fiziksel bellek tahsisi değildir.";
        }
        timer.Tick += (_, _) => { try { RefreshStats(); } catch (IOException) { } }; form.FormClosed += (_, _) => timer.Dispose(); form.Controls.Add(body); RefreshStats(); timer.Start(); return form;
    }
}
