using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KutaryaOS.Core;

namespace KutaryaOS.WinForms;

internal static class ModelDownloadTests
{
    public static async Task<int> RunAsync(string root)
    {
        List<string> checks = [];
        void Check(bool value, string title) { if (!value) throw new Exception(title); checks.Add(title); }
        var test = Path.Combine(root, "ModelKosular", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(test);
        try
        {
            using var server = new DownloadServer();
            var manifest = new ModelDownload(server.Url, "test.gguf", server.Bytes.Length, Convert.ToHexString(SHA256.HashData(server.Bytes)));
            using (var model = new LocalModelManager(Path.Combine(test, "normal"), manifest))
            {
                await model.DownloadAsync(null, CancellationToken.None);
                Check(model.IsDownloaded && !File.Exists(model.ModelPath + ".part"), "Doğrulanmış indirme atomik tamamlandı");
                File.WriteAllBytes(model.ModelPath, new byte[server.Bytes.Length]);
                await model.DownloadAsync(null, CancellationToken.None);
                Check(SHA256.HashData(File.ReadAllBytes(model.ModelPath)).SequenceEqual(SHA256.HashData(server.Bytes)), "Bozuk final model yeniden indirildi");
            }
            using (var model = new LocalModelManager(Path.Combine(test, "resume"), manifest))
            {
                File.WriteAllBytes(model.ModelPath + ".part", server.Bytes[..32768]);
                await model.DownloadAsync(null, CancellationToken.None);
                Check(server.LastRange == 32768 && model.IsDownloaded, "HTTP Range ile kesilen indirme sürdü");
            }
            server.IgnoreRange = true;
            using (var model = new LocalModelManager(Path.Combine(test, "reset"), manifest))
            {
                File.WriteAllBytes(model.ModelPath + ".part", server.Bytes[..1000]);
                await model.DownloadAsync(null, CancellationToken.None);
                Check(model.IsDownloaded, "Range yok sayılınca dosya baştan yazıldı");
            }
            server.IgnoreRange = false;
            using (var model = new LocalModelManager(Path.Combine(test, "bad"), manifest with { Sha256 = new string('0', 64) }))
            {
                var rejected = false; try { await model.DownloadAsync(null, CancellationToken.None); } catch (InvalidDataException) { rejected = true; }
                Check(rejected && !File.Exists(model.ModelPath) && !File.Exists(model.ModelPath + ".part"), "Yanlış SHA final dosyaya dönüşmedi");
            }
            server.Slow = true;
            using (var model = new LocalModelManager(Path.Combine(test, "cancel"), manifest))
            {
                using var stop = new CancellationTokenSource();
                var canceled = false;
                try { await model.DownloadAsync(new InlineProgress(p => { if (p.Received >= 16384) stop.Cancel(); }), stop.Token); } catch (OperationCanceledException) { canceled = true; }
                Check(canceled && !File.Exists(model.ModelPath) && File.Exists(model.ModelPath + ".part"), "İptal yalnız kısmi dosya bıraktı");
                server.Slow = false; await model.DownloadAsync(null, CancellationToken.None);
                Check(model.IsDownloaded, "İptal edilen indirme tekrar tamamlandı");
            }
            var secrets = new SecretStore(Path.Combine(test, "local.dpapi")); secrets.Save(new("", "Kutarya Us", "", Mode: AiMode.LocalModel));
            Check(secrets.Load().Mode == AiMode.LocalModel, "Yerel mod API adresi olmadan kalıcı kaydedildi");
            ConfigurationStore.AtomicWrite(Path.Combine(root, "model-kabul.json"), JsonSerializer.Serialize(new { passed = true, count = checks.Count, checks, realModel = false, evidence = "Kontrollü indirme sunucusu; gerçek model çıkarımı ayrı raporda." }, ConfigurationStore.JsonOptions)); return 0;
        }
        catch (Exception error) { ConfigurationStore.AtomicWrite(Path.Combine(root, "model-kabul.json"), JsonSerializer.Serialize(new { passed = false, checks, error = error.ToString() }, ConfigurationStore.JsonOptions)); return 1; }
    }
    private sealed class InlineProgress(Action<ModelProgress> report) : IProgress<ModelProgress> { public void Report(ModelProgress value) => report(value); }
    private sealed class DownloadServer : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _task;
        public byte[] Bytes { get; } = Encoding.UTF8.GetBytes("GGUF" + new string('x', 262140));
        public int LastRange { get; private set; }
        public bool IgnoreRange { get; set; }
        public bool Slow { get; set; }
        public string Url { get; }
        public DownloadServer() { _listener.Start(); Url = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/model"; _task = Task.Run(ServeAsync); }
        private async Task ServeAsync()
        {
            try
            {
                while (!_stop.IsCancellationRequested)
                {
                    using var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                    try
                    {
                        await using var stream = client.GetStream();
                        using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
                        string? line; var start = 0;
                        while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync(_stop.Token)))
                            if (line.StartsWith("Range:", StringComparison.OrdinalIgnoreCase)) start = int.Parse(line.Split('=')[1].TrimEnd('-'));
                        LastRange = start; if (IgnoreRange) start = 0;
                        var range = start > 0 ? $"Content-Range: bytes {start}-{Bytes.Length - 1}/{Bytes.Length}\r\n" : "";
                        var header = Encoding.ASCII.GetBytes($"HTTP/1.1 {(start > 0 ? "206 Partial Content" : "200 OK")}\r\nContent-Length: {Bytes.Length - start}\r\n{range}Connection: close\r\n\r\n");
                        await stream.WriteAsync(header, _stop.Token);
                        for (var i = start; i < Bytes.Length; i += 8192)
                        {
                            await stream.WriteAsync(Bytes.AsMemory(i, Math.Min(8192, Bytes.Length - i)), _stop.Token);
                            if (Slow) await Task.Delay(10, _stop.Token);
                        }
                    }
                    catch (IOException) { }
                }
            }
            catch (OperationCanceledException) { } catch (SocketException) when (_stop.IsCancellationRequested) { }
        }
        public void Dispose() { _stop.Cancel(); _listener.Stop(); _task.GetAwaiter().GetResult(); _stop.Dispose(); }
    }
}
