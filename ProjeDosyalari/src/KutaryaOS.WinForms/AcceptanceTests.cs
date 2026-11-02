using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using KutaryaOS.Core;

namespace KutaryaOS.WinForms;

// Dış test çatısı gerektirmeyen C# kabul çalıştırıcısı; yalnız kendi geçici verisiyle çalışır.
internal static class AcceptanceTests
{
    public static async Task<int> RunAsync(string reportRoot)
    {
        var testRoot = Path.Combine(reportRoot, "Kosular", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(testRoot);
        List<object> results = []; var failures = 0;
        async Task Test(string name, Func<Task> run)
        {
            try { await run(); results.Add(new { name, passed = true, error = "" }); }
            catch (Exception error) { failures++; results.Add(new { name, passed = false, error = error.ToString() }); }
        }
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        static async Task Reject(Func<Task> run)
        {
            try { await run(); } catch (Exception error) when (error is ArgumentException or IOException or InvalidOperationException or OperationCanceledException) { return; }
            throw new Exception("Reddedilmesi gereken işlem kabul edildi.");
        }
        var hardware = new WindowsHardwareProvider().Read(testRoot);
        var gpu = hardware.Gpus[0];
        var machine = MachineConfig.Create("Okul-PC", 2048, (int)Math.Min(512, gpu.DedicatedBytes / HardwareSnapshot.MiB), 2, gpu, hardware);
        var files = new VirtualFileSystem(Path.Combine(testRoot, "Cihaz"), 16384);
        await Test("Gerçek Windows RAM/disk ve DXGI GPU ölçümü", () => { Check(hardware.TotalRamBytes > 0 && hardware.FreeDiskBytes > 0 && hardware.Gpus.Count > 0, "Donanım okunamadı."); return Task.CompletedTask; });
        await Test("Cihaz ayarlarının kalıcı JSON dönüşü", () => { var store = new ConfigurationStore(Path.Combine(testRoot, "Ayar")); store.SaveMachine(machine); Check(store.LoadMachine()?.Name == machine.Name, "Ayar değişti."); return Task.CompletedTask; });
        await Test("Gerçek RAM sınırı", () => Reject(() => { MachineConfig.Create("Okul", hardware.MaximumRamMb + 1, 0, 1, gpu, hardware); return Task.CompletedTask; }));
        await Test("Gerçek VRAM sınırı", () => Reject(() => { MachineConfig.Create("Okul", 512, checked((int)(gpu.DedicatedBytes / HardwareSnapshot.MiB) + 1), 1, gpu, hardware); return Task.CompletedTask; }));
        await Test("Gerçek disk sınırı", () => Reject(() => { MachineConfig.Create("Okul", 512, 0, hardware.MaximumStorageGb + 1, gpu, hardware); return Task.CompletedTask; }));
        await Test("Geçersiz cihaz adı", () => Reject(() => { MachineConfig.Create(" ", 512, 0, 1, gpu, hardware); return Task.CompletedTask; }));
        await Test("Sanal kaynak bütçesi ve IDisposable iadesi", () => { var budget = new ResourceBudget(256, 0); using (budget.Acquire("Not", 64)) Check(budget.UsedRam == 64, "Bütçe ayrılmadı."); Check(budget.UsedRam == 0, "Bütçe bırakılmadı."); return Task.CompletedTask; });
        await Test("RAM bütçesi aşımı", () => Reject(() => { new ResourceBudget(256, 0).Acquire("Büyük uygulama", 512); return Task.CompletedTask; }));
        await Test("Negatif bütçe reddi", () => Reject(() => { new ResourceBudget(256, 0).Acquire("Hatalı", -1); return Task.CompletedTask; }));
        await Test("Metin yazma/okuma ve UTF-8", async () => { await files.WriteTextAsync("Masaustu/ornek.txt", "Öğrenci: ı İ ş ğ ç"); Check(await files.ReadTextAsync("Masaustu/ornek.txt") == "Öğrenci: ı İ ş ğ ç", "UTF-8 bozuldu."); });
        await Test("Mevcut dosyayı AI oluşturması ezemez", () => Reject(() => files.WriteTextAsync("Masaustu/ornek.txt", "ez")));
        await Test("Depolama kotası gerçekten uygulanır", () => Reject(() => files.WriteTextAsync("Masaustu/buyuk.txt", new string('a', 20000))));
        await Test("Kota hatasında dosya sağlam kalır", async () => { Check(!File.Exists(files.Resolve("Masaustu/buyuk.txt")) && (await files.ReadTextAsync("Masaustu/ornek.txt")).StartsWith("Öğrenci"), "Veri kaybı."); });
        foreach (var invalid in new[] { "../kacis.txt", "Masaustu/../../kacis.txt", "D:/kacis.txt", "Masaustu/a.txt:stream", "Masaustu/CON.txt", "Masaustu/a. " })
            await Test("Yol/isim sınırı: " + invalid, () => Reject(() => files.WriteTextAsync(invalid, "x")));
        await Test("Olay ile dosya değişimi bildirimi", async () => { var raised = false; EventHandler handler = (_, _) => raised = true; files.Changed += handler; await files.CreateDirectoryAsync("Projeler/OOP"); files.Changed -= handler; Check(raised, "Olay yok."); });
        await Test("Gerçek dosya hash doğrulamalı TAŞIMA", async () =>
        {
            var source = Path.Combine(testRoot, "gercek.txt"); await File.WriteAllTextAsync(source, "Gerçekten taşınacak okul test dosyası");
            var result = await files.ImportMoveAsync(source, "Masaustu");
            Check(result.Moved && !File.Exists(source) && File.Exists(files.Resolve(result.Destination)), "Kaynak silinmedi veya hedef yok.");
            Check((await files.ReadTextAsync(result.Destination)).Contains("okul"), "Taşınan içerik bozuldu.");
        });
        await Test("İsim çakışmasında dosya ezilmez", async () =>
        {
            var source = Path.Combine(testRoot, "gercek.txt"); await File.WriteAllTextAsync(source, "İkinci içerik"); var result = await files.ImportMoveAsync(source, "Masaustu");
            Check(result.Destination.EndsWith("gercek (2).txt") && (await files.ReadTextAsync("Masaustu/gercek.txt")).Contains("okul"), "Çakışma yanlış.");
        });
        await Test("Kota dolunca gerçek kaynak KORUNUR", async () =>
        {
            var source = Path.Combine(testRoot, "buyuk.txt"); await File.WriteAllTextAsync(source, new string('x', 24000)); await Reject(() => files.ImportMoveAsync(source, "Masaustu")); Check(File.Exists(source), "Kaynak kayboldu.");
        });
        await Test("İptal edilen taşıma kaynağı korur", async () =>
        {
            var source = Path.Combine(testRoot, "iptal.txt"); await File.WriteAllTextAsync(source, "koru"); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            await Reject(() => files.ImportMoveAsync(source, "Masaustu", cancellation.Token)); Check(File.Exists(source), "İptalde kayboldu.");
        });
        await Test("Kilitli kaynakta veri korunur", async () =>
        {
            var source = Path.Combine(testRoot, "kilit.txt"); await File.WriteAllTextAsync(source, "kilit"); using var held = new FileStream(source, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            await Reject(() => files.ImportMoveAsync(source, "Masaustu")); Check(File.Exists(source), "Kilitli dosya kayboldu.");
        });
        await Test("Kaynak silinemeyince doğrulanmış kopya ve kaynak birlikte korunur", async () =>
        {
            var source = Path.Combine(testRoot, "salt-okunur.txt"); await File.WriteAllTextAsync(source, "korunacak"); File.SetAttributes(source, FileAttributes.ReadOnly);
            try { var result = await files.ImportMoveAsync(source, "Masaustu"); Check(!result.Moved && File.Exists(source) && File.Exists(files.Resolve(result.Destination)), "Silme hatası yanlış bildirildi."); }
            finally { File.SetAttributes(source, FileAttributes.Normal); }
        });
        await Test("Çöp kutusu ve çakışmasız geri yükleme", async () =>
        {
            await files.TrashAsync("Masaustu/ornek.txt"); Check(!File.Exists(files.Resolve("Masaustu/ornek.txt")), "Çöpe gitmedi.");
            var item = files.ListTrash().Single(); await files.WriteTextAsync("Masaustu/ornek.txt", "Yeni içerik"); await files.RestoreAsync(item.Id);
            Check(File.Exists(files.Resolve("Masaustu/ornek (2).txt")) && await files.ReadTextAsync("Masaustu/ornek.txt") == "Yeni içerik", "Geri yükleme ezdi.");
        });
        await Test("Klasör ve dosya yeniden adlandırma", async () => { await files.MoveAsync("Projeler/OOP", "Projeler/Ders"); Check(Directory.Exists(files.Resolve("Projeler/Ders")), "Klasör taşınmadı."); });
        await Test("DPAPI gizli ayarlar round-trip", () => { var store = new SecretStore(Path.Combine(testRoot, "ai.dpapi")); store.Save(new("http://127.0.0.1:8912/v1", "test-model", "TEST-KEY-NOT-A-REAL-SECRET")); Check(store.Load().ApiKey == "TEST-KEY-NOT-A-REAL-SECRET" && !Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(testRoot, "ai.dpapi"))).Contains("TEST-KEY"), "DPAPI şifrelemedi."); return Task.CompletedTask; });
        await Test("Uzak bağlantıda düz HTTP reddi", () => Reject(() => { new AiSettings("http://example.com/v1", "", "").Endpoint("models"); return Task.CompletedTask; }));
        await Test("AI sanal disk dışına dosya yazamaz", async () => { var executor = new VirtualToolExecutor(files, _ => { }); var result = await executor.ExecuteAsync(new("id", "virtual_files", """{"operation":"write","path":"../zarar.txt","content":"x"}"""), CancellationToken.None); Check(result.Contains("error") && !File.Exists(Path.Combine(testRoot, "zarar.txt")), "AI yol sınırı yok."); });
        await Test("AI keyfi komut çalıştıramaz", async () => { var executor = new VirtualToolExecutor(files, _ => throw new Exception("Açılmamalı")); var result = await executor.ExecuteAsync(new("id", "shell", """{"command":"anything"}"""), CancellationToken.None); Check(result.Contains("error"), "Bilinmeyen araç kabul edildi."); });
        foreach (var protocol in Enum.GetValues<AiProtocol>())
            await Test("Gerçek loopback HTTP, model listesi ve AI araç döngüsü: " + protocol, async () =>
            {
                using var server = new ProtocolTestServer(protocol);
                using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(10) };
                var provider = new HttpAiProvider(client); var settings = new AiSettings(server.BaseUrl, "controlled-test-model", "", protocol);
                Check((await provider.ModelsAsync(settings, CancellationToken.None)).Single() == "controlled-test-model", "Model protokolü hatalı.");
                var executor = new VirtualToolExecutor(files, _ => { }); var session = new AiSession(provider, executor, machine);
                var result = await session.ChatAsync(settings, "Test dosyası oluştur", CancellationToken.None);
                Check(result == "Kontrollü protokol testi tamamlandı." && await files.ReadTextAsync("Masaustu/http-" + protocol + ".txt") == "Kontrollü servis içeriği", "HTTP araç işlemi başarısız.");
                Check(server.ToolResultReceived, "Araç sonucu modele iletilmedi.");
            });
        var report = new { date = DateTimeOffset.Now, total = results.Count, passed = results.Count - failures, failures, hardware, tests = results, realAiTested = false, note = "HTTP test sunucusu kontrollüdür; gerçek model değildir." };
        ConfigurationStore.AtomicWrite(Path.Combine(reportRoot, "kabul.json"), JsonSerializer.Serialize(report, ConfigurationStore.JsonOptions));
        return failures == 0 ? 0 : 1;
    }

    private sealed class ProtocolTestServer : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _loop;
        private readonly AiProtocol _protocol;
        private int _round;
        public bool ToolResultReceived { get; private set; }
        public string BaseUrl { get; }
        public ProtocolTestServer(AiProtocol protocol)
        {
            _protocol = protocol; _listener.Start(); BaseUrl = "http://127.0.0.1:" + ((IPEndPoint)_listener.LocalEndpoint).Port + "/v1"; _loop = RunAsync();
        }
        private async Task RunAsync()
        {
            try
            {
                while (!_stop.IsCancellationRequested)
                {
                    using var socket = await _listener.AcceptTcpClientAsync(_stop.Token); using var stream = socket.GetStream();
                    List<byte> bytes = []; var buffer = new byte[8192]; var end = -1; var length = 0;
                    while (true)
                    {
                        var count = await stream.ReadAsync(buffer, _stop.Token); if (count == 0) break; bytes.AddRange(buffer.Take(count));
                        var raw = Encoding.UTF8.GetString(bytes.ToArray()); end = raw.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                        if (end < 0) continue;
                        var contentLength = raw[..end].Split("\r\n").FirstOrDefault(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
                        length = contentLength == null ? 0 : int.Parse(contentLength.Split(':')[1].Trim());
                        if (bytes.Count >= end + 4 + length) break;
                    }
                    var request = Encoding.UTF8.GetString(bytes.ToArray()); string response;
                    if (request.StartsWith("GET ")) response = """{"data":[{"id":"controlled-test-model"}]}""";
                    else
                    {
                        var payload = JsonNode.Parse(request[(end + 4)..])!.AsObject();
                        if (_round++ == 0)
                        {
                            var arguments = JsonSerializer.Serialize(new { operation = "write", path = "Masaustu/http-" + _protocol + ".txt", content = "Kontrollü servis içeriği" });
                            response = _protocol == AiProtocol.ChatCompletions
                                ? new JsonObject { ["choices"] = new JsonArray(new JsonObject { ["message"] = new JsonObject { ["role"] = "assistant", ["content"] = null, ["tool_calls"] = new JsonArray(new JsonObject { ["id"] = "call_1", ["type"] = "function", ["function"] = new JsonObject { ["name"] = "virtual_files", ["arguments"] = arguments } }) } }) }.ToJsonString()
                                : new JsonObject { ["output"] = new JsonArray(new JsonObject { ["id"] = "fc_1", ["type"] = "function_call", ["call_id"] = "call_1", ["name"] = "virtual_files", ["arguments"] = arguments }) }.ToJsonString();
                        }
                        else
                        {
                            ToolResultReceived = payload.ToJsonString().Contains("Dosya olu", StringComparison.Ordinal) || payload.ToJsonString().Contains("olu\\u015F", StringComparison.Ordinal);
                            response = _protocol == AiProtocol.ChatCompletions
                                ? """{"choices":[{"message":{"role":"assistant","content":"Kontrollü protokol testi tamamlandı."}}]}"""
                                : """{"output":[{"id":"msg_1","type":"message","role":"assistant","status":"completed","content":[{"type":"output_text","text":"Kontrollü protokol testi tamamlandı."}]}]}""";
                        }
                    }
                    var body = Encoding.UTF8.GetBytes(response); var header = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(header, _stop.Token); await stream.WriteAsync(body, _stop.Token);
                }
            }
            catch (OperationCanceledException) { } catch (ObjectDisposedException) { }
        }
        public void Dispose() { _stop.Cancel(); _listener.Stop(); try { _loop.GetAwaiter().GetResult(); } catch (SocketException) { } _stop.Dispose(); }
    }
}
