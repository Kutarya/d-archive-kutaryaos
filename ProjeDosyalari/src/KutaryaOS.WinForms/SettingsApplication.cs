using KutaryaOS.Core;

namespace KutaryaOS.WinForms;

public sealed class SettingsApplication() : VirtualApplication("settings", "Ayarlar", 64)
{
    public override Form CreateWindow(DesktopContext context, string? argument = null)
    {
        var form = Window(new(720, 535));
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var personal = new TabPage("Görünüm") { BackColor = form.BackColor, AutoScroll = true };
        var ai = new TabPage("Kutarya Us") { BackColor = form.BackColor };
        var about = new TabPage("Cihaz") { BackColor = form.BackColor, Padding = new(22) };
        tabs.TabPages.AddRange([personal, ai, about]); form.Controls.Add(tabs);
        if (argument is "ai" or "settings-api" or "settings-local") tabs.SelectedTab = ai;
        var heading = Ui.Label("Duvar kâğıdı ve renkler", 20, true); heading.Location = new(22, 24); personal.Controls.Add(heading);
        var themes = new FlowLayoutPanel { Location = new(18, 85), Size = new(650, 180) };
        foreach (var palette in Palette.All)
        {
            var button = Ui.Button(palette.Name, (_, _) => { context.Configuration.SaveTheme(palette.Name); context.ThemeChanged(palette.Name); });
            button.Size = new(195, 150); button.BackgroundImage = Ui.Wallpaper(button.Size, palette, false); button.BackgroundImageLayout = ImageLayout.Stretch;
            button.ForeColor = Color.White; button.TextAlign = ContentAlignment.BottomCenter; button.Padding = new(0, 0, 0, 12); button.FlatAppearance.BorderSize = 0;
            button.Disposed += (_, _) => button.BackgroundImage?.Dispose(); themes.Controls.Add(button);
        }
        personal.Controls.Add(themes);
        var hint = Ui.Label("Seçtiğin görünüm sonraki açılışta da korunur.", 10); hint.Location = new(22, 280); hint.ForeColor = Ui.Muted; personal.Controls.Add(hint);
        var choices = new TabControl { Dock = DockStyle.Fill };
        var localPage = new TabPage("Yerel model indir") { BackColor = form.BackColor, AutoScroll = true };
        var ownPage = new TabPage("Kendi API’ni bağla") { BackColor = form.BackColor, AutoScroll = true };
        choices.TabPages.AddRange([localPage, ownPage]); ai.Controls.Add(choices);
        var settings = context.Secrets.Load();
        var ownProfile = new SecretStore(Path.Combine(context.Configuration.Root, "api-profili.dpapi"));
        var apiSettings = settings.Mode == AiMode.OwnApi ? settings : ownProfile.Load();
        if (settings.Mode == AiMode.OwnApi || argument == "settings-api") choices.SelectedTab = ownPage;
        if (argument == "settings-local") choices.SelectedTab = localPage;
        var localTitle = Ui.Label("Kutarya Us · çevrimdışı", 20, true); localTitle.Location = new(22, 25);
        var description = Ui.Label("Hafif modeli bir kez indir. Sohbet bu bilgisayarda çalışır.\nAI panelini açtığında model otomatik hazırlanır.", 11); description.Location = new(22, 78); description.ForeColor = Ui.Muted;
        var size = Ui.Label("İndirme: 640 MB  •  CPU ile çalışır", 10, true); size.Location = new(22, 145);
        var progressBar = new ProgressBar { Location = new(22, 195), Size = new(620, 20) };
        var localStatus = Ui.Label(context.LocalModel?.IsDownloaded == true ? "Model indirildi." : "Model henüz indirilmedi.", 10);
        localStatus.AutoSize = false; localStatus.SetBounds(22, 230, 620, 65);
        CancellationTokenSource? download = null;
        var action = Ui.Button(context.LocalModel?.IsDownloaded == true ? "Yerel modeli kullan" : "Modeli indir", async (_, _) =>
        {
            if (download != null || context.LocalModel == null) return;
            download = new CancellationTokenSource(); var token = download.Token;
            var last = DateTime.MinValue;
            var progress = new Progress<ModelProgress>(p =>
            {
                if (form.IsDisposed || DateTime.UtcNow - last < TimeSpan.FromMilliseconds(200) && p.Stage == "İndiriliyor") return;
                last = DateTime.UtcNow; progressBar.Value = (int)Math.Clamp(p.Received * 100 / Math.Max(1, p.Total), 0, 100);
                localStatus.Text = $"{p.Stage} · %{progressBar.Value}";
            });
            try
            {
                localStatus.Text = "Model hazırlanıyor…";
                await context.LocalModel.DownloadAsync(progress, token);
                var previous = context.Secrets.Load(); if (previous.Mode == AiMode.OwnApi) ownProfile.Save(previous);
                context.Secrets.Save(new("", "Kutarya Us", "", Mode: AiMode.LocalModel));
                if (!form.IsDisposed) localStatus.Text = "İndirme doğrulandı. AI panelini açarak konuşabilirsin.";
            }
            catch (OperationCanceledException) { if (!form.IsDisposed) localStatus.Text = "İndirme durduruldu. Tekrar başlatınca kaldığı yerden devam eder."; }
            catch (Exception error) { if (!form.IsDisposed) localStatus.Text = error.Message; }
            finally { download?.Dispose(); download = null; }
        }, true); action.SetBounds(22, 315, 210, 42); action.Name = "DownloadModelButton";
        var stop = Ui.Button("Durdur", (_, _) => download?.Cancel()); stop.SetBounds(245, 315, 120, 42);
        var localNote = Ui.Label("İlk indirme internet ister. Model setup’a dahil değildir.\nKısa sohbetler ve basit dosya işlemleri için uygundur.", 10); localNote.Location = new(22, 390); localNote.ForeColor = Ui.Muted;
        localPage.Controls.AddRange([localTitle, description, size, progressBar, localStatus, action, stop, localNote]);
        form.FormClosed += (_, _) => download?.Cancel();
        var url = Ui.TextBox(apiSettings.BaseUrl); url.PlaceholderText = "https://sağlayıcınız/v1";
        var model = Ui.TextBox(apiSettings.Model); model.PlaceholderText = "Sağlayıcının model kimliği";
        var key = Ui.TextBox(apiSettings.ApiKey); key.UseSystemPasswordChar = true;
        url.Name = "ApiUrl"; model.Name = "ApiModel"; key.Name = "ApiKey";
        var protocol = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = Ui.Font(10) };
        protocol.Items.AddRange(["Chat Completions", "Responses"]); protocol.SelectedIndex = (int)apiSettings.Protocol;
        void Row(string labelText, Control field, int y)
        {
            var label = Ui.Label(labelText, 10, true); label.Location = new(22, y); field.SetBounds(22, y + 24, 620, 30); ownPage.Controls.AddRange([label, field]);
        }
        Row("API adresi", url, 22); Row("Model kimliği", model, 95); Row("API anahtarı", key, 168); Row("Protokol", protocol, 241);
        var status = Ui.Label("Anahtar yalnız bu Windows hesabında şifreli saklanır.", 9); status.AutoSize = false; status.SetBounds(22, 395, 620, 65); status.ForeColor = Ui.Muted;
        AiSettings Current() => new(url.Text.Trim(), model.Text.Trim(), key.Text.Trim(), (AiProtocol)protocol.SelectedIndex, AiMode.OwnApi);
        var save = Ui.Button("API’yi bağla", (_, _) =>
        {
            try { if (model.Text.Trim().Length == 0) throw new ArgumentException("Model kimliğini yaz."); ownProfile.Save(Current()); context.Secrets.Save(Current()); status.Text = "API seçildi. AI panelinde konuşabilirsin."; }
            catch (Exception error) { status.Text = error.Message; }
        }, true); save.SetBounds(22, 330, 200, 40); save.Name = "SaveApiButton";
        var test = Ui.Button("Bağlantıyı dene", async (_, _) =>
        {
            try { status.Text = "Model listesi okunuyor…"; using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(20)); var models = await context.Ai.ModelsAsync(Current(), cancel.Token); if (!form.IsDisposed) status.Text = $"Servise ulaşıldı. {models.Count} model listelendi."; }
            catch (Exception error) { if (!form.IsDisposed) status.Text = error.Message; }
        }); test.SetBounds(235, 330, 200, 40);
        ownPage.Controls.AddRange([save, test, status]);
        var info = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = form.BackColor, Font = Ui.Font(11),
            Text = $"KUTARYA OS 0.2\n{context.Machine.Name}\n\nRAM bütçesi: {context.Machine.RamMb} MB\nVRAM bütçesi: {context.Machine.VramMb} MB\nGPU: {context.Machine.GpuName}\nDepolama: {context.Machine.StorageGb} GB\n\nDosyalar\n{context.Files.Root}\n\nRAM ve VRAM, sanal uygulamaların bütçesidir. Depolama kotası gerçek dosyaların kapladığı alanı sınırlar." };
        about.Controls.Add(info); return form;
    }
}

