using System.Drawing.Imaging;
using System.Text.Json;
using KutaryaOS.Core;

namespace KutaryaOS.WinForms;

// Test uygulamanın kendi kontrol ağacını işletir; fiziksel fare/klavye otomasyonu değildir.
internal static class UiSmoke
{
    public static int Run(string root, string? modelRoot = null)
    {
        try
        {
            List<string> checks = [];
            void Check(string name, bool condition) { if (!condition) throw new Exception(name); checks.Add(name); }
            Directory.CreateDirectory(root);
            var uiRoot = Path.Combine(root, "UIKosular", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(uiRoot);
            var hardware = new WindowsHardwareProvider().Read(root);
            using (var setup = new SetupForm(hardware))
            {
                setup.Show(); Application.DoEvents(); Capture(setup, Path.Combine(root, "ilk-kurulum.png")); setup.Close();
                Check("İlk kurulum penceresi boyandı", File.Exists(Path.Combine(root, "ilk-kurulum.png")));
            }
            var machine = MachineConfig.Create("Okul-PC", 2048, 0, 2, hardware.Gpus[0], hardware);
            var files = new VirtualFileSystem(Path.Combine(uiRoot, "ui-cihaz"), 2 * HardwareSnapshot.GiB);
            using var client = new HttpClient();
            using var local = new LocalModelManager(modelRoot ?? Path.Combine(root, "YerelAI"));
            DesktopForm? desktop = null;
            var context = new DesktopContext(machine, files, new(machine.RamMb, 0), new(uiRoot), new(Path.Combine(uiRoot, "ui-ai.dpapi")), new RoutingAiProvider(new HttpAiProvider(client), local), name => desktop?.ApplyTheme(name), local);
            context.Secrets.Save(modelRoot == null ? AiSettings.Default : new("", "Kutarya Us", "", Mode: AiMode.LocalModel));
            using var form = new DesktopForm(context); desktop = form;
            form.Show(); Application.DoEvents(); Capture(form, Path.Combine(root, "masaustu.png"));
            Check("Masaüstü gerçekten gösterildi", form.Visible);
            Check("Uygulama simgelerinin tamamı ilk açılışta görünür", Walk(form).OfType<Button>().Count(b => b.Image != null && b.Visible) >= 6);
            FindButton(form, "StartButton").PerformClick(); Application.DoEvents(); Capture(form, Path.Combine(root, "baslangic-menusu.png")); form.ToggleStart();
            form.Launch("files"); form.Launch("calculator"); form.Launch("monitor"); form.Launch("settings"); Application.DoEvents();
            Check("Dört gerçek sanal uygulama penceresi açıldı", form.OpenWindowCount == 4);
            Check("Pencerelerin RAM bütçesi 240 MB", context.Budget.UsedRam == 240);
            var fileWindow = FindForms(form).Single(child => child.Text == "Dosyalar");
            var exportButton = Walk(fileWindow).OfType<Button>().Single(button => button.Name == "ExportToHostButton");
            var fileList = Walk(fileWindow).OfType<ListView>().Single();
            Check("Ana PC'ye taşıma düğmesi görünür ve seçim bekliyor", exportButton.Visible && !exportButton.Enabled &&
                !fileList.RectangleToScreen(fileList.ClientRectangle).IntersectsWith(exportButton.RectangleToScreen(exportButton.ClientRectangle)));
            var transferSource = files.Resolve("Masaustu/AktarımDenemesi"); Directory.CreateDirectory(transferSource);
            File.WriteAllText(Path.Combine(transferSource, "not.txt"), "UI gerçek aktarım");
            var transferTarget = Path.Combine(uiRoot, "AnaPC"); Directory.CreateDirectory(transferTarget);
            using (var transferWindow = new HostExportForm(files, new("AktarımDenemesi", "Masaustu/AktarımDenemesi", true, 0, DateTime.Now), _ => transferTarget))
            {
                transferWindow.Show(fileWindow); Application.DoEvents();
                var begin = Walk(transferWindow).OfType<Button>().Single(button => button.Name == "BeginHostMove");
                Check("Hedef seçilmeden taşıma başlayamaz", !begin.Enabled);
                Walk(transferWindow).OfType<Button>().Single(button => button.Name == "ChooseHostDirectory").PerformClick(); Application.DoEvents();
                Check("Kullanıcı hedef seçince taşıma düğmesi etkinleşir", begin.Enabled);
                begin.PerformClick();
                var transferLimit = DateTime.UtcNow.AddSeconds(15);
                while (transferWindow.IsTransferring && DateTime.UtcNow < transferLimit) { Application.DoEvents(); Thread.Sleep(15); }
                // Sonuçtan sonra kuyrukta kalan Progress mesajları başarı metnini ezmemeli.
                Application.DoEvents(); Thread.Sleep(30); Application.DoEvents();
                Check("WinForms düğmesinden gerçek klasör ana PC'ye taşındı", !transferWindow.IsTransferring && !Directory.Exists(transferSource) &&
                    File.ReadAllText(Path.Combine(transferTarget, "AktarımDenemesi", "not.txt")) == "UI gerçek aktarım" && !begin.Enabled &&
                    Walk(transferWindow).OfType<TextBox>().Single(box => box.Name == "TransferResult").Text.StartsWith("Dosyalar doğrulandı ve ana PC’ye taşındı."));
                Capture(transferWindow, Path.Combine(root, "ana-pc-aktarim.png")); transferWindow.Close();
            }
            foreach (var child in FindForms(form).ToArray()) Capture(child, Path.Combine(root, "uygulama-" + child.Text.Replace(' ', '-') + ".png"));
            Capture(form, Path.Combine(root, "uygulamalar.png"));
            foreach (var child in FindForms(form).ToArray()) child.Close();
            Check("Pencere kapatınca bütçe geri döndü", context.Budget.UsedRam == 0 && form.OpenWindowCount == 0);
            FindButton(form, "AiButton").PerformClick();
            var limit = DateTime.UtcNow.AddSeconds(2);
            while (DateTime.UtcNow < limit) { Application.DoEvents(); Thread.Sleep(10); }
            Check("AI düğmesi sağ paneli açtı", form.AiIsOpen);
            Check("AI bağlantı seçenekleri görünür", Walk(form).OfType<Button>().Count(b => b.Visible && b.Text is "Kendi API’ni bağla" or "Yerel model indir") == 2);
            Capture(form, Path.Combine(root, "ai-paneli.png"));
            if (modelRoot != null)
            {
                var readyLimit = DateTime.UtcNow.AddSeconds(40);
                while (!local.IsReady && DateTime.UtcNow < readyLimit) { Application.DoEvents(); Thread.Sleep(15); }
                Application.DoEvents();
                Check("AI sekmesi yerel modeli otomatik yükledi", local.IsReady);
                var input = Walk(form).OfType<TextBox>().Single(t => t.PlaceholderText.StartsWith("Bir klasör"));
                input.Text = "Masaustu içinde Deneme adında bir klasör oluştur.";
                Walk(form).OfType<Button>().Single(b => b.Text.StartsWith("Gönder")).PerformClick();
                var actionLimit = DateTime.UtcNow.AddSeconds(60);
                while (!Directory.Exists(files.Resolve("Masaustu/Deneme")) && DateTime.UtcNow < actionLimit) { Application.DoEvents(); Thread.Sleep(15); }
                Capture(form, Path.Combine(root, "yerel-ai-sohbet.png"));
                ConfigurationStore.AtomicWrite(Path.Combine(root, "yerel-ai-panel-metni.txt"), string.Join("\n", Walk(form).OfType<RichTextBox>().Select(box => box.Text)));
                Check("Paneldeki gerçek model dosya aracını çalıştırdı", Directory.Exists(files.Resolve("Masaustu/Deneme")));
                var aiPanel = Walk(form).OfType<AiPanel>().Single();
                while ((aiPanel.IsBusy || !aiPanel.ConnectionStatus.StartsWith("Çevrimdışı hazır")) && DateTime.UtcNow < actionLimit) { Application.DoEvents(); Thread.Sleep(15); }
                Check("AI yanıtı tamamlanınca panel hazır", !aiPanel.IsBusy && aiPanel.ConnectionStatus.StartsWith("Çevrimdışı hazır"));
                Application.DoEvents(); form.Refresh();
                Capture(form, Path.Combine(root, "yerel-ai-sohbet.png"));
                ConfigurationStore.AtomicWrite(Path.Combine(root, "yerel-ai-panel-metni.txt"), string.Join("\n", Walk(form).OfType<RichTextBox>().Select(box => box.Text)));
            }
            form.Launch("settings-local"); Application.DoEvents();
            using (var settingsWindow = FindForms(form).Single())
            {
                var pageNames = Walk(settingsWindow).OfType<TabControl>().SelectMany(t => t.TabPages.Cast<TabPage>()).Select(t => t.Text).ToArray();
                Check("Ayarlar iki AI yöntemini sunuyor", pageNames.Contains("Yerel model indir") && pageNames.Contains("Kendi API’ni bağla"));
                Capture(settingsWindow, Path.Combine(root, "ai-secimleri.png"));
                var methods = Walk(settingsWindow).OfType<TabControl>().Single(t => t.TabPages.Cast<TabPage>().Any(p => p.Text == "Kendi API’ni bağla"));
                methods.SelectedTab = methods.TabPages.Cast<TabPage>().Single(p => p.Text == "Kendi API’ni bağla");
                Walk(settingsWindow).OfType<TextBox>().Single(t => t.Name == "ApiUrl").Text = "https://example.invalid/v1";
                Walk(settingsWindow).OfType<TextBox>().Single(t => t.Name == "ApiModel").Text = "controlled-fixture-model";
                Walk(settingsWindow).OfType<TextBox>().Single(t => t.Name == "ApiKey").Text = "TEST-KEY-NOT-REAL";
                Walk(settingsWindow).OfType<Button>().Single(b => b.Name == "SaveApiButton").PerformClick();
                Check("API yöntemi UI üzerinden şifreli kaydedildi", context.Secrets.Load().Mode == AiMode.OwnApi && context.Secrets.Load().Model == "controlled-fixture-model");
                if (modelRoot != null)
                {
                    methods.SelectedIndex = 0;
                    Walk(settingsWindow).OfType<Button>().Single(b => b.Name == "DownloadModelButton").PerformClick();
                    var modeLimit = DateTime.UtcNow.AddSeconds(15);
                    while (context.Secrets.Load().Mode != AiMode.LocalModel && DateTime.UtcNow < modeLimit) { Application.DoEvents(); Thread.Sleep(15); }
                    Check("Yerel modele geçiş API profilini korudu", context.Secrets.Load().Mode == AiMode.LocalModel && new SecretStore(Path.Combine(uiRoot, "api-profili.dpapi")).Load().Model == "controlled-fixture-model");
                }
                settingsWindow.Close();
            }
            context.Configuration.SaveTheme("Gün Doğumu"); form.ApplyTheme("Gün Doğumu"); Application.DoEvents(); Capture(form, Path.Combine(root, "tema-gun-dogumu.png"));
            Check("Tema kalıcı kaydedildi", context.Configuration.LoadTheme() == "Gün Doğumu");
            form.Size = new(960, 640); Application.DoEvents(); Capture(form, Path.Combine(root, "kucuk-ekran.png"));
            form.Close(); Application.DoEvents();
            Check("Kapanışta kaynak sızıntısı yok", context.Budget.UsedRam == 0);
            ConfigurationStore.AtomicWrite(Path.Combine(root, "ui-kabul.json"), JsonSerializer.Serialize(new { passed = true, count = checks.Count, checks, evidence = "Gerçek WinForms pencereleri Show/PerformClick/DrawToBitmap ile test edildi; fiziksel sürükleme yapılmadı.", hardware }, ConfigurationStore.JsonOptions)); return 0;
        }
        catch (Exception error) { ConfigurationStore.AtomicWrite(Path.Combine(root, "ui-kabul.json"), JsonSerializer.Serialize(new { passed = false, error = error.ToString() }, ConfigurationStore.JsonOptions)); return 1; }
    }
    private static IEnumerable<Control> Walk(Control root) { foreach (Control child in root.Controls) { yield return child; foreach (var nested in Walk(child)) yield return nested; } }
    private static Button FindButton(Form root, string name) => Walk(root).OfType<Button>().Single(b => b.Name == name);
    private static IEnumerable<Form> FindForms(Form root) => Walk(root).OfType<Form>();
    private static void Capture(Form form, string path) { using var bitmap = new Bitmap(form.Width, form.Height); form.DrawToBitmap(bitmap, new(Point.Empty, bitmap.Size)); bitmap.Save(path, ImageFormat.Png); }
}
