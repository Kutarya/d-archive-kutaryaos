using System.Diagnostics;
using System.Text.Json;
using KutaryaOS.Core;

namespace KutaryaOS.WinForms;

internal static class LocalAiDiagnostics
{
    public static async Task<int> RunAsync(string root, bool download)
    {
        try
        {
            using var local = new LocalModelManager(Path.Combine(root, "YerelAI"));
            var timer = Stopwatch.StartNew();
            if (download)
            {
                await local.DownloadAsync(null, CancellationToken.None);
                ConfigurationStore.AtomicWrite(Path.Combine(root, "model-indirme.json"), JsonSerializer.Serialize(new { passed = true, seconds = timer.Elapsed.TotalSeconds, manifest = ModelDownload.School }, ConfigurationStore.JsonOptions));
                return 0;
            }
            await local.EnsureReadyAsync(CancellationToken.None); var loadSeconds = timer.Elapsed.TotalSeconds;
            var hw = new WindowsHardwareProvider().Read(root);
            var machine = MachineConfig.Create("Okul-PC", 2048, 0, 2, hw.Gpus[0], hw);
            var files = new VirtualFileSystem(Path.Combine(root, "yerel-ai-kosular", Guid.NewGuid().ToString("N")), 2 * HardwareSnapshot.GiB);
            var provider = new RoutingAiProvider(new HttpAiProvider(new HttpClient()), local);
            var session = new AiSession(provider, new VirtualToolExecutor(files, _ => { }), machine);
            var settings = new AiSettings("", "Kutarya Us", "", Mode: AiMode.LocalModel);
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            var answer = await session.ChatAsync(settings, "Merhaba. Bana tek cümleyle kendini tanıt.", timeout.Token);
            var folder = await session.ChatAsync(settings, "Masaustu içinde Okul adında bir klasör oluştur.", timeout.Token);
            var created = Directory.Exists(files.Resolve("Masaustu/Okul"));
            var note = await session.ChatAsync(settings, "Masaustu/merhaba.txt dosyasını oluştur. Dosya içeriği: Merhaba Kutarya.", timeout.Token);
            var written = File.Exists(files.Resolve("Masaustu/merhaba.txt"));
            var text = written ? await files.ReadTextAsync("Masaustu/merhaba.txt") : "";
            var passed = !string.IsNullOrWhiteSpace(answer) && answer != "Model metin döndürmedi." && created && written && text == "Merhaba Kutarya.";
            ConfigurationStore.AtomicWrite(Path.Combine(root, "yerel-ai-kabul.json"), JsonSerializer.Serialize(new { passed, realModel = true, externalServer = false,
                date = DateTimeOffset.Now, executable = Environment.ProcessPath, application = AppContext.BaseDirectory,
                loadSeconds, totalSeconds = timer.Elapsed.TotalSeconds, answer, folder, note, created, written, text, workingSetMb = Process.GetCurrentProcess().WorkingSet64 / HardwareSnapshot.MiB }, ConfigurationStore.JsonOptions));
            return passed ? 0 : 1;
        }
        catch (Exception error)
        {
            ConfigurationStore.AtomicWrite(Path.Combine(root, "yerel-ai-hata.txt"), error.ToString()); return 1;
        }
    }
}
