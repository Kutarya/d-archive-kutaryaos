using KutaryaOS.Core;

namespace KutaryaOS.WinForms;

// AI bu pencereyi açamaz veya hedef seçemez; başlatma düğmesi kullanıcı işlemidir.
internal sealed class HostExportForm : Form
{
    internal bool IsTransferring { get; private set; }
    public HostExportForm(VirtualFileSystem files, VirtualEntry entry, Func<IWin32Window, string?>? selectDestination = null)
    {
        Text = "Ana PC’ye taşı"; ClientSize = new(580, 300); MinimumSize = new(520, 300);
        StartPosition = FormStartPosition.CenterParent; Ui.Style(this);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(20), ColumnCount = 1, RowCount = 5 };
        layout.RowStyles.Add(new(SizeType.Absolute, 70)); layout.RowStyles.Add(new(SizeType.Absolute, 44));
        layout.RowStyles.Add(new(SizeType.Absolute, 50)); layout.RowStyles.Add(new(SizeType.Percent, 100)); layout.RowStyles.Add(new(SizeType.Absolute, 46));
        var info = Ui.Label($"Seçilen öğe: {entry.Name}\nHedef doğrulanınca sanal cihazdaki öğe kaldırılır. Mevcut dosyalar ezilmez.", 10);
        info.Dock = DockStyle.Fill; info.AutoSize = false;
        string? destinationPath = null;
        var target = Ui.TextBox("Hedef klasör seçilmedi"); target.ReadOnly = true; target.Dock = DockStyle.Fill;
        string? Browse(IWin32Window owner)
        {
            using var dialog = new FolderBrowserDialog { Description = "Seçilen sanal öğenin taşınacağı ana PC klasörü", UseDescriptionForTitle = true, ShowNewFolderButton = true };
            return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.SelectedPath : null;
        }
        var choose = Ui.Button("Hedef klasör seç", (_, _) =>
        {
            var selected = (selectDestination ?? Browse)(this);
            if (selected != null) { destinationPath = selected; target.Text = selected; }
        });
        choose.Name = "ChooseHostDirectory"; choose.Width = 190;
        var state = new TextBox { Name = "TransferResult", Text = "Öğe ana PC’ye yalnız bu pencereden taşınır.", ReadOnly = true, Multiline = true, WordWrap = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Font = Ui.Font(9), BorderStyle = BorderStyle.None, BackColor = BackColor };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        CancellationTokenSource? transfer = null; var busy = false;
        var close = Ui.Button("Kapat", (_, _) => { if (busy) transfer?.Cancel(); else Close(); });
        var move = Ui.Button("Taşımayı başlat", async (_, _) =>
        {
            if (busy || destinationPath == null) return;
            busy = true; IsTransferring = true; choose.Enabled = false; close.Text = "İptal";
            actions.Controls.OfType<Button>().Single(button => button.Name == "BeginHostMove").Enabled = false;
            transfer = new CancellationTokenSource();
            var progress = new Progress<ExportProgress>(value => { if (!IsDisposed && IsTransferring) state.Text = $"Doğrulanıyor: {value.CompletedFiles}/{value.TotalFiles} dosya • {value.Name}"; });
            try
            {
                var destination = destinationPath;
                var result = await Task.Run(() => files.ExportMoveAsync(entry.Path, destination, progress, transfer.Token));
                state.Text = result.Message + Environment.NewLine + result.Destination;
                // Tamamlanan bir öğeye ikinci kez taşıma uygulanmaz.
                choose.Enabled = false;
            }
            catch (OperationCanceledException) { state.Text = "Aktarım iptal edildi. Sanal kaynak korundu."; choose.Enabled = true; }
            catch (Exception error) { state.Text = error.Message; choose.Enabled = true; }
            finally
            {
                busy = false; IsTransferring = false; close.Text = "Kapat"; transfer.Dispose(); transfer = null;
                actions.Controls.OfType<Button>().Single(button => button.Name == "BeginHostMove").Enabled = choose.Enabled && destinationPath != null;
            }
        }, true);
        move.Name = "BeginHostMove"; move.Enabled = false; move.Width = 190;
        target.TextChanged += (_, _) => move.Enabled = !busy && destinationPath != null;
        FormClosing += (_, e) => { if (busy) { transfer?.Cancel(); e.Cancel = true; state.Text = "İptal isteği alındı; güvenli durma bekleniyor…"; } };
        actions.Controls.Add(close); actions.Controls.Add(move);
        layout.Controls.Add(info); layout.Controls.Add(target); layout.Controls.Add(choose); layout.Controls.Add(state); layout.Controls.Add(actions); Controls.Add(layout);
    }
}
