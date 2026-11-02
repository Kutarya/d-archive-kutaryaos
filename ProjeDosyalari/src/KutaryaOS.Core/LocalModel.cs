using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using LLama;
using LLama.Common;
using LLama.Sampling;

namespace KutaryaOS.Core;

public record ModelDownload(string Url, string FileName, long Bytes, string Sha256)
{
    public static ModelDownload School => new(
        "https://huggingface.co/Qwen/Qwen3-0.6B-GGUF/resolve/23749fefcc72300e3a2ad315e1317431b06b590a/Qwen3-0.6B-Q8_0.gguf",
        "Kutarya-Us-Hafif.gguf", 639446688,
        "9465e63a22add5354d9bb4b99e90117043c7124007664907259bd16d043bb031");
}
public record ModelProgress(string Stage, long Received, long Total);

// Model dosyası, indirme ve CPU çıkarımı tek nesnenin yaşam döngüsüne aittir.
public sealed class LocalModelManager : IDisposable
{
    private readonly string _root;
    private readonly ModelDownload _manifest;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private LLamaWeights? _weights;
    private bool _verified;
    public bool IsReady => _weights != null;
    public string ModelPath => Path.Combine(_root, _manifest.FileName);
    public bool IsDownloaded => File.Exists(ModelPath) && new FileInfo(ModelPath).Length == _manifest.Bytes;
    public LocalModelManager(string root, ModelDownload? manifest = null)
    {
        _root = Path.GetFullPath(root); _manifest = manifest ?? ModelDownload.School;
        if (Path.GetFileName(_manifest.FileName) != _manifest.FileName || _manifest.FileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new ArgumentException("Geçersiz model adı.");
        Directory.CreateDirectory(_root); CheckPaths();
    }
    private void CheckPaths()
    {
        for (var dir = new DirectoryInfo(_root); dir != null; dir = dir.Parent)
            if ((dir.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Model klasöründe bağlantı/junction kullanılamaz.");
        foreach (var path in new[] { ModelPath, ModelPath + ".part" })
            if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Model dosyası bağlantı olamaz.");
    }
    public async Task DownloadAsync(IProgress<ModelProgress>? progress, CancellationToken token)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            CheckPaths();
            if (IsDownloaded)
            {
                try { await VerifyAsync(ModelPath, token).ConfigureAwait(false); progress?.Report(new("Hazır", _manifest.Bytes, _manifest.Bytes)); return; }
                catch (InvalidDataException) { File.Delete(ModelPath); _verified = false; }
            }
            var part = ModelPath + ".part";
            var received = File.Exists(part) ? new FileInfo(part).Length : 0;
            if (received > _manifest.Bytes) { File.Delete(part); received = 0; }
            if (new DriveInfo(Path.GetPathRoot(_root)!).AvailableFreeSpace < _manifest.Bytes - received + 128 * HardwareSnapshot.MiB)
                throw new IOException("Model indirmek için yeterli boş alan yok.");
            if (received < _manifest.Bytes)
            {
                using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
                using var request = new HttpRequestMessage(HttpMethod.Get, _manifest.Url);
                if (received > 0) request.Headers.Range = new RangeHeaderValue(received, null);
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                if (response.StatusCode == HttpStatusCode.PartialContent)
                {
                    var range = response.Content.Headers.ContentRange;
                    if (range?.From != received || range.Length != _manifest.Bytes) throw new IOException("İndirme aralığı doğrulanamadı.");
                }
                else received = 0;
                await using var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
                await using var output = new FileStream(part, received == 0 ? FileMode.Create : FileMode.Append, FileAccess.Write, FileShare.None, 131072, true);
                var buffer = new byte[131072]; int count;
                while ((count = await input.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
                {
                    if (received + count > _manifest.Bytes) throw new IOException("Model beklenen boyutu aşıyor.");
                    await output.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false); received += count;
                    progress?.Report(new("İndiriliyor", received, _manifest.Bytes));
                }
                await output.FlushAsync(token).ConfigureAwait(false);
            }
            progress?.Report(new("Doğrulanıyor", received, _manifest.Bytes));
            try { await VerifyAsync(part, token).ConfigureAwait(false); }
            catch (InvalidDataException) { File.Delete(part); throw; }
            File.Move(part, ModelPath, true); _verified = true;
            progress?.Report(new("Hazır", received, _manifest.Bytes));
        }
        finally { _gate.Release(); }
    }
    private async Task VerifyAsync(string path, CancellationToken token)
    {
        if (new FileInfo(path).Length != _manifest.Bytes) throw new InvalidDataException("Model indirmesi tamamlanmadı.");
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, true);
        var magic = new byte[4]; await stream.ReadExactlyAsync(magic, token).ConfigureAwait(false); stream.Position = 0;
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, token).ConfigureAwait(false));
        if (!magic.AsSpan().SequenceEqual("GGUF"u8) || !hash.Equals(_manifest.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Model bütünlük kontrolü başarısız. Yeniden indir.");
        _verified = true;
    }
    private ModelParams Parameters() => new(ModelPath) { ContextSize = 2048, GpuLayerCount = 0,
        Threads = Math.Clamp(Environment.ProcessorCount / 2, 1, 4), BatchThreads = Math.Clamp(Environment.ProcessorCount / 2, 1, 4),
        BatchSize = 128, UseMemorymap = true, UseMemoryLock = false };
    public async Task EnsureReadyAsync(CancellationToken token)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try { await LoadAsync(token).ConfigureAwait(false); } finally { _gate.Release(); }
    }
    private async Task LoadAsync(CancellationToken token)
    {
        if (_weights != null) return;
        CheckPaths();
        if (!IsDownloaded) throw new InvalidOperationException("Önce Ayarlar’dan yerel modeli indir.");
        if (!_verified) await VerifyAsync(ModelPath, token).ConfigureAwait(false);
        _weights = await Task.Run(() => LLamaWeights.LoadFromFileAsync(Parameters(), token), token).ConfigureAwait(false);
    }
    public async Task<AiReply> CompleteAsync(JsonArray history, CancellationToken token)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            await LoadAsync(token).ConfigureAwait(false);
            return await Task.Run(async () =>
            {
                var prompt = new StringBuilder("""
                <|im_start|>system
                Sen Kutarya Us, Türkçe konuşan bir cihaz yardımcısısın. Kısa yanıt ver. JSON alanları: action, path, content, application, text. action: sohbet için none; klasör oluşturmak için mkdir; metin dosyası yazmak için write; liste için list; okumak için read; uygulama için open_app. Yol yalnız Masaustu altında olmalı. Ana PC'ye aktarım yetkin yok. write content istenen dosyanın gerçek içeriğidir. application files, notes, calculator veya monitor olabilir. Kullanılmayan alanlar boş string. Dosya isteğinde text boş, action gerekli işlem. /no_think<|im_end|>
                <|im_start|>user
                Merhaba, kimsin?<|im_end|>
                <|im_start|>assistant
                {"action":"none","path":"","content":"","application":"","text":"Merhaba! Ben Kutarya Us, cihazındaki yardımcınım."}<|im_end|>
                <|im_start|>user
                Masaustu içinde Deneme klasörü oluştur.<|im_end|>
                <|im_start|>assistant
                {"action":"mkdir","path":"Masaustu/Deneme","content":"","application":"","text":""}<|im_end|>

                """);
                foreach (var item in history.Where(i => i?["role"]?.GetValue<string>() is "user" or "assistant").TakeLast(2))
                {
                    var content = item?["content"]?.GetValue<string>() ?? "";
                    if (content.Length > 1200) content = content[..1200];
                    content = content.Replace("<|im_start|>", "").Replace("<|im_end|>", "");
                    prompt.Append("<|im_start|>").Append(item!["role"]!.GetValue<string>()).Append('\n').Append(content).Append("<|im_end|>\n");
                }
                prompt.Append("<|im_start|>assistant\n<think>\n\n</think>\n");
                var ex = new StatelessExecutor(_weights!, Parameters()) { ApplyTemplate = false };
                using var pipeline = new DefaultSamplingPipeline { Temperature = .2f, Grammar = new(Grammar, "root") };
                var output = new StringBuilder();
                try
                {
                    await foreach (var text in ex.InferAsync(prompt.ToString(), new InferenceParams { MaxTokens = 384,
                        SamplingPipeline = pipeline, AntiPrompts = ["<|im_end|>", "<|endoftext|>"] }, token)) output.Append(text);
                }
                finally { ex.Context?.Dispose(); }
                var json = JsonNode.Parse(output.ToString())?.AsObject() ?? throw new IOException("Yerel model geçerli yanıt üretemedi.");
                string Get(string key) => json[key]?.GetValue<string>() ?? "";
                List<AiToolCall> calls = [];
                if (Get("action") != "none")
                {
                    var app = Get("action") == "open_app";
                    var args = app ? new JsonObject { ["application"] = Get("application") } : new JsonObject { ["operation"] = Get("action"), ["path"] = Get("path"), ["content"] = Get("content") };
                    calls.Add(new("local_" + Guid.NewGuid().ToString("N"), app ? "open_app" : "virtual_files", args.ToJsonString()));
                }
                if (calls.Count == 0 && string.IsNullOrWhiteSpace(Get("text"))) throw new IOException("Yerel model sohbet metni üretmedi; daha kısa bir mesajla tekrar dene.");
                var message = new JsonObject { ["role"] = "assistant", ["content"] = Get("text") };
                if (calls.Count > 0) message["tool_calls"] = new JsonArray(calls.Select(call => (JsonNode)new JsonObject { ["id"] = call.Id, ["type"] = "function", ["function"] = new JsonObject { ["name"] = call.Name, ["arguments"] = call.Arguments } }).ToArray());
                return new AiReply(Get("text"), calls, new JsonObject { ["choices"] = new JsonArray(new JsonObject { ["message"] = message }) });
            }, token).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }
    private const string Grammar = """
    root ::= "{" ws "\"action\":" ws (chat | operation) "}" ws
    chat ::= "\"none\"" "," ws "\"path\":" ws "\"\"" "," ws "\"content\":" ws "\"\"" "," ws "\"application\":" ws "\"\"" "," ws "\"text\":" ws nonempty
    operation ::= action "," ws "\"path\":" ws str "," ws "\"content\":" ws str "," ws "\"application\":" ws str "," ws "\"text\":" ws str
    action ::= "\"mkdir\"" | "\"write\"" | "\"list\"" | "\"read\"" | "\"open_app\""
    nonempty ::= "\"" ([^"\\\x00-\x20] | "\\" (["\\/bfnrt] | "u" [0-9a-fA-F] [0-9a-fA-F] [0-9a-fA-F] [0-9a-fA-F])) chars "\""
    str ::= "\"" chars "\""
    chars ::= ([^"\\\x00-\x1F] | "\\" (["\\/bfnrt] | "u" [0-9a-fA-F] [0-9a-fA-F] [0-9a-fA-F] [0-9a-fA-F]))*
    ws ::= [ \t\n\r]*
    """;
    public void Dispose() { _gate.Wait(); try { _weights?.Dispose(); } finally { _gate.Release(); } _gate.Dispose(); }
}

public sealed class RoutingAiProvider(HttpAiProvider remote, LocalModelManager local) : IAiProvider
{
    public Task<AiReply> CompleteAsync(AiSettings settings, JsonArray history, JsonArray tools, CancellationToken token) => settings.Mode switch
    {
        AiMode.LocalModel => local.CompleteAsync(history, token),
        AiMode.OwnApi => remote.CompleteAsync(settings, history, tools, token),
        _ => throw new InvalidOperationException("Ayarlar’dan kendi API’ni bağla veya yerel modeli indir.")
    };
    public Task<IReadOnlyList<string>> ModelsAsync(AiSettings settings, CancellationToken token) => settings.Mode == AiMode.LocalModel
        ? Task.FromResult<IReadOnlyList<string>>(["Kutarya Us"]) : remote.ModelsAsync(settings, token);
}
