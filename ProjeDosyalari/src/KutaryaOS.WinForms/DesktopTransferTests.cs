using System.Text.Json;
using System.Text.Json.Nodes;
using KutaryaOS.Core;

namespace KutaryaOS.WinForms;

internal static class DesktopTransferTests
{
    public static async Task<int> RunAsync(string reportRoot)
    {
        var root = Path.Combine(reportRoot, "Kosular", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        List<object> results = []; var failures = 0; var index = 0;
        async Task Test(string name, Func<VirtualFileSystem, string, Task> run)
        {
            var path = Path.Combine(root, (++index).ToString()); Directory.CreateDirectory(path);
            var files = new VirtualFileSystem(Path.Combine(path, "cihaz"), 32 * HardwareSnapshot.MiB);
            var host = Path.Combine(path, "AnaPC"); Directory.CreateDirectory(host);
            try { await run(files, host); results.Add(new { name, passed = true, error = "" }); }
            catch (Exception error) { failures++; results.Add(new { name, passed = false, error = error.ToString() }); }
        }
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        static async Task Reject(Func<Task> run)
        {
            try { await run(); }
            catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException or OperationCanceledException) { return; }
            throw new Exception("Reddedilmesi gereken işlem kabul edildi.");
        }
        static Task<string> Tool(VirtualFileSystem files, string operation, string path, string content = "", string target = "") =>
            new VirtualToolExecutor(files, _ => { }).ExecuteAsync(new("test", "virtual_files", JsonSerializer.Serialize(new { operation, path, content, target })), CancellationToken.None);

        await Test("AI Masaüstünde klasör ve dosya oluşturur", async (files, _) =>
        {
            Check(!(await Tool(files, "mkdir", "Masaustu/Okul")).Contains("error"), "Klasör oluşturulmadı.");
            await Tool(files, "write", "Masaustu/Okul/not.txt", "gerçek içerik");
            Check(await files.ReadTextAsync("Masaustu/Okul/not.txt") == "gerçek içerik", "İçerik farklı.");
        });
        await Test("AI yalın göreli yolu yalnız Masaüstüne yazar", async (files, _) =>
        { await Tool(files, "write", "not.txt", "x"); Check(File.Exists(files.Resolve("Masaustu/not.txt")) && !File.Exists(files.Resolve("not.txt")), "Yalın yol yanlış konumda."); });
        foreach (var path in new[] { "Belgeler/x.txt", "Projeler/x.txt", "Resimler/x.txt", "../x.txt", "Masaustu/../x.txt", @"Masaustu\..\Belgeler\x.txt", @"C:\x.txt", @"\\localhost\share\x.txt", "/" })
            await Test("AI dış yol reddi: " + path, async (files, _) =>
            { Check((await Tool(files, "write", path, "x")).Contains("error"), "Dış yol kabul edildi."); Check(files.UsedBytes == 0, "Reddedilen işlem dosya oluşturdu."); });
        await Test("AI Masaüstü dışını listeleyemez veya okuyamaz", async (files, _) =>
        {
            await files.WriteTextAsync("Belgeler/ozel.txt", "koru");
            Check((await Tool(files, "list", "Belgeler")).Contains("error") && (await Tool(files, "read", "Belgeler/ozel.txt")).Contains("error"), "Dış içerik okundu.");
        });
        await Test("AI yeniden adlandırmada kaynak ve hedefi sınırlar", async (files, _) =>
        {
            await files.WriteTextAsync("Masaustu/koru.txt", "koru");
            Check((await Tool(files, "rename", "Masaustu/koru.txt", target: "Belgeler/cikis.txt")).Contains("error"), "Dış hedef kabul edildi.");
            Check(File.Exists(files.Resolve("Masaustu/koru.txt")), "Kaynak kayboldu.");
            await Tool(files, "rename", "Masaustu/koru.txt", target: "Masaustu/yeni.txt");
            Check(await files.ReadTextAsync("Masaustu/yeni.txt") == "koru", "İzinli yeniden adlandırma bozuldu.");
        });
        await Test("AI Masaüstü kökünü taşıyamaz", async (files, _) =>
        { Check((await Tool(files, "rename", "Masaustu", target: "Masaustu/yeni")).Contains("error") && Directory.Exists(files.Resolve("Masaustu")), "Masaüstü kökü değişti."); });
        await Test("AI için ana PC aktarım aracı bulunmaz", async (files, host) =>
        {
            var executor = new VirtualToolExecutor(files, _ => throw new Exception("Uygulama açılmamalı."));
            var result = await executor.ExecuteAsync(new("test", "export_move", JsonSerializer.Serialize(new { path = "Masaustu", target = host })), CancellationToken.None);
            Check(result.Contains("error") && !Directory.EnumerateFileSystemEntries(host).Any(), "AI dışa aktarım yaptı.");
        });
        foreach (var mode in new[] { AiMode.LocalModel, AiMode.OwnApi })
            foreach (var protocol in Enum.GetValues<AiProtocol>())
                await Test($"AI oturumunda dış yol reddi: {mode}/{protocol}", async (files, host) =>
                {
                    var hardware = new WindowsHardwareProvider().Read(host);
                    var machine = MachineConfig.Create("Test-PC", 2048, 0, 1, hardware.Gpus[0], hardware);
                    var provider = new EscapeProvider(protocol);
                    var session = new AiSession(provider, new VirtualToolExecutor(files, _ => { }), machine);
                    var answer = await session.ChatAsync(new("https://example.invalid/v1", "test", "", protocol, mode), "Dışarı yaz", CancellationToken.None);
                    Check(files.UsedBytes == 0 && (answer.Contains("error") || provider.SawError), "Oturum sınırı aşılabildi.");
                });
        await Test("İç içe klasör taşınır, içerik ve boş klasör korunur", async (files, host) =>
        {
            await files.WriteTextAsync("Masaustu/Okul/Alt/ödev.cs", "sınıf içeriği"); await files.WriteTextAsync("Masaustu/Okul/readme.txt", "okul");
            await files.CreateDirectoryAsync("Masaustu/Okul/Boş");
            var changed = false; files.Changed += (_, _) => changed = true;
            var result = await files.ExportMoveAsync("Masaustu/Okul", host);
            Check(result.Moved && result.FileCount == 2 && !Directory.Exists(files.Resolve("Masaustu/Okul")), "Klasör kaldırılmadı.");
            Check(File.ReadAllText(Path.Combine(result.Destination, "Alt", "ödev.cs")) == "sınıf içeriği" && Directory.Exists(Path.Combine(result.Destination, "Boş")), "Hedef içerik eksik.");
            Check(files.UsedBytes == 0 && changed && !Directory.EnumerateDirectories(host, ".kutarya-aktarim-*").Any(), "Kota/olay/geçici klasör yanlış.");
        });
        await Test("Aynı isimde klasör ezilmez", async (files, host) =>
        {
            Directory.CreateDirectory(Path.Combine(host, "Okul")); File.WriteAllText(Path.Combine(host, "Okul", "eski.txt"), "eski");
            await files.WriteTextAsync("Masaustu/Okul/yeni.txt", "yeni"); var result = await files.ExportMoveAsync("Masaustu/Okul", host);
            Check(result.Moved && Path.GetFileName(result.Destination) == "Okul (2)" && File.ReadAllText(Path.Combine(host, "Okul", "eski.txt")) == "eski", "Var olan klasör ezildi.");
        });
        await Test("Tek dosya taşınır ve aynı isimde hedef korunur", async (files, host) =>
        {
            File.WriteAllText(Path.Combine(host, "not.txt"), "eski"); await files.WriteTextAsync("Masaustu/not.txt", "yeni");
            var result = await files.ExportMoveAsync("Masaustu/not.txt", host);
            Check(result.Moved && Path.GetFileName(result.Destination) == "not (2).txt" && File.ReadAllText(result.Destination) == "yeni" && File.ReadAllText(Path.Combine(host, "not.txt")) == "eski", "Dosya çakışması yanlış.");
        });
        await Test("Boş klasör taşınır", async (files, host) =>
        { await files.CreateDirectoryAsync("Masaustu/Boş"); var result = await files.ExportMoveAsync("Masaustu/Boş", host); Check(result.Moved && result.FileCount == 0 && Directory.Exists(result.Destination), "Boş klasör taşınmadı."); });
        await Test("Önceden iptal edilen aktarım kaynağı korur", async (files, host) =>
        {
            await files.WriteTextAsync("Masaustu/iptal.txt", "koru"); using var stop = new CancellationTokenSource(); stop.Cancel();
            await Reject(() => files.ExportMoveAsync("Masaustu/iptal.txt", host, cancellation: stop.Token));
            Check(File.Exists(files.Resolve("Masaustu/iptal.txt")) && !Directory.EnumerateFileSystemEntries(host).Any(), "İptalde kaynak/hedef değişti.");
        });
        await Test("Kopyalama ortasında iptal tüm sanal kaynakları korur", async (files, host) =>
        {
            await files.WriteTextAsync("Masaustu/İptal/a.txt", "a"); await files.WriteTextAsync("Masaustu/İptal/b.txt", "b");
            using var stop = new CancellationTokenSource();
            await Reject(() => files.ExportMoveAsync("Masaustu/İptal", host, new CallbackProgress(_ => stop.Cancel()), stop.Token));
            Check(await files.ReadTextAsync("Masaustu/İptal/a.txt") == "a" && await files.ReadTextAsync("Masaustu/İptal/b.txt") == "b" && !Directory.EnumerateFileSystemEntries(host).Any(), "İptalde veri kaybı/geçici artık oluştu.");
        });
        await Test("Kilitli dosya varsa klasördeki hiçbir kaynak kaldırılmaz", async (files, host) =>
        {
            await files.WriteTextAsync("Masaustu/Kilit/a.txt", "a"); await files.WriteTextAsync("Masaustu/Kilit/b.txt", "b");
            using var held = new FileStream(files.Resolve("Masaustu/Kilit/b.txt"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            await Reject(() => files.ExportMoveAsync("Masaustu/Kilit", host));
            Check(File.Exists(files.Resolve("Masaustu/Kilit/a.txt")) && File.Exists(files.Resolve("Masaustu/Kilit/b.txt")) && !Directory.EnumerateFileSystemEntries(host).Any(), "Kilit nedeniyle kısmi kayıp oluştu.");
        });
        await Test("Salt okunur kaynak kaldırılmaz", async (files, host) =>
        {
            await files.WriteTextAsync("Masaustu/koru.txt", "koru"); var path = files.Resolve("Masaustu/koru.txt"); File.SetAttributes(path, FileAttributes.ReadOnly);
            try { await Reject(() => files.ExportMoveAsync("Masaustu/koru.txt", host)); Check(File.Exists(path) && !Directory.EnumerateFileSystemEntries(host).Any(), "Salt okunur kaynak değişti."); }
            finally { File.SetAttributes(path, FileAttributes.Normal); }
        });
        await Test("Sanal veri klasörü hedef seçilemez", async (files, _) =>
        { await files.WriteTextAsync("Masaustu/a.txt", "a"); await Reject(() => files.ExportMoveAsync("Masaustu/a.txt", files.Resolve("Belgeler"))); Check(File.Exists(files.Resolve("Masaustu/a.txt")), "Sanal veri taşındı."); });
        await Test("Sanal ana klasörlerin kendisi taşınamaz", async (files, host) =>
        { foreach (var path in new[] { "", "Masaustu", "Belgeler", "Projeler", "Resimler" }) await Reject(() => files.ExportMoveAsync(path, host)); });
        await Test("Göreli, olmayan veya ağ hedefi reddedilir", async (files, host) =>
        { await files.WriteTextAsync("Masaustu/a.txt", "a"); foreach (var target in new[] { "goreli", Path.Combine(host, "yok"), @"\\localhost\share" }) await Reject(() => files.ExportMoveAsync("Masaustu/a.txt", target)); });
        await Test("Kopyalama sırasında eklenen kaynak algılanır", async (files, host) =>
        {
            await files.WriteTextAsync("Masaustu/Değişim/a.txt", "a");
            await Reject(() => files.ExportMoveAsync("Masaustu/Değişim", host, new CallbackProgress(_ => File.WriteAllText(files.Resolve("Masaustu/Değişim/sonra.txt"), "sonra"))));
            Check(File.Exists(files.Resolve("Masaustu/Değişim/a.txt")) && File.Exists(files.Resolve("Masaustu/Değişim/sonra.txt")) && !Directory.EnumerateFileSystemEntries(host).Any(), "Değişen kaynak koruması başarısız.");
        });
        await Test("Hedefte junction engellenir", async (files, host) =>
        {
            var junction = Path.Combine(host, "baglanti"); CreateJunction(junction, files.Resolve("Belgeler"));
            try { await files.WriteTextAsync("Masaustu/a.txt", "a"); await Reject(() => files.ExportMoveAsync("Masaustu/a.txt", junction)); Check(File.Exists(files.Resolve("Masaustu/a.txt")), "Junction hedefi kabul edildi."); }
            finally { Directory.Delete(junction); }
        });
        await Test("Kaynak altındaki junction engellenir", async (files, host) =>
        {
            await files.CreateDirectoryAsync("Masaustu/Bağlantı"); var junction = Path.Combine(files.Resolve("Masaustu/Bağlantı"), "link"); CreateJunction(junction, host);
            try { await Reject(() => files.ExportMoveAsync("Masaustu/Bağlantı", host)); Check(!Directory.EnumerateFileSystemEntries(host).Any(), "Junction kaynak işlendi."); }
            finally { Directory.Delete(junction); }
        });
        await Test("D sürücüsünden C sürücüsüne gerçek klasör aktarımı", async (files, _) =>
        {
            var host = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp", "KutaryaOS-AktarimTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(host);
            try
            {
                Check(Path.GetPathRoot(files.Root) != Path.GetPathRoot(host), "İki farklı sürücü test edilmedi.");
                await files.WriteTextAsync("Masaustu/FarklıDisk/not.txt", "iki disk gerçek aktarım");
                var result = await files.ExportMoveAsync("Masaustu/FarklıDisk", host);
                Check(result.Moved && File.ReadAllText(Path.Combine(result.Destination, "not.txt")) == "iki disk gerçek aktarım", "Farklı disk aktarımı başarısız.");
            }
            finally { Directory.Delete(host, true); }
        });
        ConfigurationStore.AtomicWrite(Path.Combine(reportRoot, "aktarim-kabul.json"), JsonSerializer.Serialize(new { passed = failures == 0, count = results.Count, failures, tests = results,
            note = "Gerçek dosya sistemi; kontrollü AI sağlayıcısı saldırı argümanları sınır testi içindir, gerçek model olarak sayılmaz." }, ConfigurationStore.JsonOptions));
        return failures == 0 ? 0 : 1;
    }
    private sealed class CallbackProgress(Action<ExportProgress> callback) : IProgress<ExportProgress> { public void Report(ExportProgress value) => callback(value); }
    private static void CreateJunction(string junction, string target)
    {
        // Yalnız test fixture'ı; kaynak/çıktı bu koşunun geçici kökleri içindedir.
        var start = new System.Diagnostics.ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("/c"); start.ArgumentList.Add("mklink"); start.ArgumentList.Add("/J"); start.ArgumentList.Add(junction); start.ArgumentList.Add(target);
        using var process = System.Diagnostics.Process.Start(start)!; process.WaitForExit(); if (process.ExitCode != 0) throw new IOException("Test junction oluşturulamadı: " + process.StandardError.ReadToEnd());
    }
    private sealed class EscapeProvider(AiProtocol protocol) : IAiProvider
    {
        private int _round;
        public bool SawError { get; private set; }
        public Task<IReadOnlyList<string>> ModelsAsync(AiSettings settings, CancellationToken cancellation) => Task.FromResult<IReadOnlyList<string>>(["test"]);
        public Task<AiReply> CompleteAsync(AiSettings settings, JsonArray history, JsonArray tools, CancellationToken cancellation)
        {
            SawError |= history.ToJsonString().Contains("error");
            var first = _round++ == 0;
            var call = new AiToolCall("escape", "virtual_files", """{"operation":"write","path":"Belgeler/izinsiz.txt","content":"x"}""");
            JsonObject raw = protocol == AiProtocol.Responses
                ? new() { ["output"] = new JsonArray(first ? new JsonObject { ["type"] = "function_call", ["call_id"] = call.Id, ["name"] = call.Name, ["arguments"] = call.Arguments } : new JsonObject { ["type"] = "message", ["role"] = "assistant", ["content"] = new JsonArray() }) }
                : new() { ["choices"] = new JsonArray(new JsonObject { ["message"] = new JsonObject { ["role"] = "assistant", ["content"] = "Kontrollü sınır testi", ["tool_calls"] = first ? new JsonArray(new JsonObject { ["id"] = call.Id, ["type"] = "function", ["function"] = new JsonObject { ["name"] = call.Name, ["arguments"] = call.Arguments } }) : null } }) };
            return Task.FromResult(new AiReply("Kontrollü sınır testi", first ? [call] : [], raw));
        }
    }
}
