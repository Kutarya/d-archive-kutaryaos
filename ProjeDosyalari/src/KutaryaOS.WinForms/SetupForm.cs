using KutaryaOS.Core;

namespace KutaryaOS.WinForms;

public sealed class SetupForm : Form
{
    private readonly TextBox _name = Ui.TextBox(Environment.UserName + "-PC");
    private readonly NumericUpDown _ram = new() { Increment = 256, Minimum = 256, ThousandsSeparator = true };
    private readonly NumericUpDown _vram = new() { Increment = 128, Minimum = 0, ThousandsSeparator = true };
    private readonly NumericUpDown _storage = new() { Minimum = 1, ThousandsSeparator = true };
    private readonly ComboBox _gpu = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    public MachineConfig? Configuration { get; private set; }
    public SetupForm(HardwareSnapshot hardware)
    {
        Ui.Style(this); Text = "Kutarya OS • İlk kurulum";
        var screenHeight = Screen.PrimaryScreen?.WorkingArea.Height ?? 900;
        ClientSize = new(1000, Math.Clamp(screenHeight - 55, 580, 705)); MinimumSize = new(1016, 620); MaximizeBox = false;
        var brand = new WallpaperPanel { Dock = DockStyle.Left, Width = 315, BackColor = Palette.All[0].Top };
        brand.BackgroundImage = Ui.Wallpaper(new(315, 705), Palette.All[0], false); brand.BackgroundImageLayout = ImageLayout.Stretch;
        var logo = Ui.Label("kutarya\nOS", 36, true); logo.ForeColor = Color.White; logo.Location = new(30, 40);
        var tagline = Ui.Label("Çalışma alanını\noluştur.", 17); tagline.ForeColor = Color.White; tagline.Location = new(32, 220);
        var note = Ui.Label("Kutarya OS 0.2", 10); note.ForeColor = Color.FromArgb(198, 221, 231); note.Location = new(32, 550);
        brand.Controls.AddRange([logo, tagline, note]); Controls.Add(brand);
        var main = new Panel { Dock = DockStyle.Fill }; Controls.Add(main); main.BringToFront();
        var content = new Panel { Dock = DockStyle.Fill, Padding = new(35, 30, 35, 25), AutoScroll = true, AutoScrollMinSize = new(640, 615) };
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 74 };
        main.Controls.Add(content); main.Controls.Add(footer);
        var heading = Ui.Label("Kendi cihazını oluştur", 24, true); heading.Location = new(35, 28);
        var subtitle = Ui.Label("Kaynak sınırlarını seç, Kutarya OS masaüstünü başlat.", 11); subtitle.ForeColor = Ui.Muted; subtitle.Location = new(35, 76);
        content.Controls.AddRange([heading, subtitle]);
        _ram.Maximum = hardware.MaximumRamMb; _ram.Value = Math.Min(2048, hardware.MaximumRamMb);
        _storage.Maximum = hardware.MaximumStorageGb; _storage.Value = Math.Min(8, hardware.MaximumStorageGb);
        _gpu.Items.AddRange(hardware.Gpus.Cast<object>().ToArray()); _gpu.DisplayMember = nameof(GpuInfo.Name);
        _gpu.SelectedIndexChanged += (_, _) => { _vram.Maximum = ((GpuInfo)_gpu.SelectedItem!).DedicatedBytes / HardwareSnapshot.MiB; _vram.Value = Math.Min(512, _vram.Maximum); };
        _gpu.SelectedIndex = 0;
        AddRow(content, "Cihaz adı", _name, 120, "Masaüstünde ve AI karşılamasında bu isim görünür.");
        AddRow(content, "RAM bütçesi (MB)", _ram, 207, $"Gerçek RAM: {Ui.Bytes(hardware.TotalRamBytes)} • şu an kullanılabilir: {Ui.Bytes(hardware.AvailableRamBytes)}");
        AddRow(content, "Grafik adaptörü", _gpu, 294, "Bu bilgisayarda bulunan grafik adaptörlerinden seç.");
        AddRow(content, "VRAM bütçesi (MB)", _vram, 381, "Paylaşımlı adaptörde ayrılmış VRAM yoksa 0 seçilir.");
        AddRow(content, "Depolama kotası (GB)", _storage, 468, $"{hardware.Drive} sürücüsünde {Ui.Bytes(hardware.FreeDiskBytes)} boş alan • dosyalar bu kotaya uyar.");
        var explanation = Ui.Label("RAM/VRAM sanal uygulama bütçesidir; donanım ayrılmaz.\nDepolama gerçekten kullanılan dosya alanını sınırlar.", 9); explanation.ForeColor = Ui.Muted; explanation.Location = new(35, 555);
        var create = Ui.Button("Masaüstünü oluştur →", (_, _) =>
        {
            try { Configuration = MachineConfig.Create(_name.Text, (int)_ram.Value, (int)_vram.Value, (int)_storage.Value, (GpuInfo)_gpu.SelectedItem!, hardware); DialogResult = DialogResult.OK; }
            catch (Exception error) { Ui.Error(this, error); }
        }, true); create.Size = new(300, 42); footer.Controls.Add(create);
        footer.Layout += (_, _) => create.Location = new(Math.Max(20, footer.ClientSize.Width - 335), 16);
        content.Controls.Add(explanation); AcceptButton = create;
        brand.Resize += (_, _) => note.Top = Math.Max(430, brand.ClientSize.Height - 110);
        FormClosed += (_, _) => brand.BackgroundImage?.Dispose();
    }
    private static void AddRow(Control parent, string title, Control field, int y, string hint)
    {
        var label = Ui.Label(title, 10, true); label.Location = new(35, y); field.SetBounds(35, y + 25, 570, 30); field.Font = Ui.Font(11);
        var helper = Ui.Label(hint, 9); helper.ForeColor = Ui.Muted; helper.Location = new(35, y + 60); parent.Controls.AddRange([label, field, helper]);
    }
}
