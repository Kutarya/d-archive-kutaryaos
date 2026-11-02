using KutaryaOS.Core;

namespace KutaryaOS.WinForms;

public sealed class AiPanel : Panel
{
    private readonly DesktopContext _context;
    private readonly AiSession _session;
    private readonly RichTextBox _chat;
    private readonly TextBox _input;
    private readonly Button _send;
    private readonly Label _status;
    private CancellationTokenSource? _request;
    private readonly CancellationTokenSource _lifetime = new();
    private bool _preparing;
    private string? _lastAction;
    public bool IsBusy => _request != null;
    public string ConnectionStatus => _status.Text;
    public AiPanel(DesktopContext context, Action<string> openApplication, Action close)
    {
        _context = context; BackColor = Color.FromArgb(248, 250, 253); Padding = new(18, 15, 18, 15);
        var executor = new VirtualToolExecutor(context.Files, openApplication); _session = new(context.Ai, executor, context.Machine);
        var header = new Panel { Dock = DockStyle.Top, Height = 90 };
        var title = Ui.Label("Kutarya Us", 21, true); title.Location = new(0, 2);
        var badge = Ui.Label("Cihaz yardımcın", 10); badge.ForeColor = Ui.Muted; badge.Location = new(1, 42);
        var hide = Ui.Button("×", (_, _) => close()); hide.Size = new(35, 35); hide.Anchor = AnchorStyles.Right | AnchorStyles.Top; hide.Location = new(300, 0);
        header.Controls.AddRange([title, badge, hide]);
        header.Resize += (_, _) => hide.Left = header.ClientSize.Width - hide.Width;
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 175 };
        _input = new TextBox { Multiline = true, Font = Ui.Font(11), BorderStyle = BorderStyle.FixedSingle, PlaceholderText = "Bir klasör oluştur, not yaz veya soru sor…", MaxLength = 8000, Dock = DockStyle.Top, Height = 76 };
        var commands = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, WrapContents = false };
        _send = Ui.Button("Gönder ↑", async (_, _) => await SendAsync(), true); _send.Width = 120;
        var cancel = Ui.Button("Durdur", (_, _) => _request?.Cancel()); cancel.Width = 90;
        var settings = Ui.Button("Ayarlar", (_, _) => openApplication("settings")); settings.Width = 90;
        commands.Controls.AddRange([_send, cancel, settings]);
        _status = Ui.Label("Enter gönderir • Shift+Enter yeni satır", 9); _status.AutoSize = false; _status.Dock = DockStyle.Fill; _status.Padding = new(0, 8, 0, 0); _status.ForeColor = Ui.Muted;
        bottom.Controls.Add(_status); bottom.Controls.Add(_input); bottom.Controls.Add(commands);
        _chat = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = BackColor, Font = Ui.Font(11), DetectUrls = false };
        Controls.Add(_chat); Controls.Add(header); Controls.Add(bottom);
        Append("Kutarya Us", $"Bugün {context.Machine.Name} cihazında ne yapmak istersin?");
        var choices = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 94, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        var own = Ui.Button("Kendi API’ni bağla", (_, _) => openApplication("settings-api")); own.Width = 325;
        var local = Ui.Button("Yerel model indir", (_, _) => openApplication("settings-local")); local.Width = 325;
        choices.Controls.AddRange([own, local]); Controls.Add(choices);
        Controls.SetChildIndex(_chat, 0); Controls.SetChildIndex(bottom, 1); Controls.SetChildIndex(choices, 2); Controls.SetChildIndex(header, 3);
        executor.ActionCompleted += (_, action) => { _lastAction = action; Append("İşlem", action); };
        _input.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter && !e.Shift) { e.SuppressKeyPress = true; await SendAsync(); } };
        Disposed += (_, _) => { _request?.Cancel(); _lifetime.Cancel(); };
    }
    public async Task OnOpenedAsync()
    {
        if (_preparing || _context.Secrets.Load().Mode != AiMode.LocalModel || _context.LocalModel == null) return;
        _preparing = true; _status.Text = "Yerel model hazırlanıyor…";
        try { await _context.LocalModel.EnsureReadyAsync(_lifetime.Token); if (!IsDisposed) _status.Text = "Çevrimdışı hazır · Enter gönderir"; }
        catch (OperationCanceledException) { }
        catch (Exception error) { if (!IsDisposed) _status.Text = error.Message; }
        finally { _preparing = false; }
    }
    private void Append(string speaker, string message)
    {
        if (IsDisposed) return;
        _chat.SelectionStart = _chat.TextLength; _chat.SelectionColor = speaker == "Sen" ? Palette.All[0].Accent : Ui.Ink;
        _chat.SelectionFont = Ui.Font(10, FontStyle.Bold); _chat.AppendText(speaker.ToUpperInvariant() + "\n");
        _chat.SelectionFont = Ui.Font(11); _chat.SelectionColor = Ui.Ink; _chat.AppendText(message + "\n\n"); _chat.ScrollToCaret();
    }
    private async Task SendAsync()
    {
        var text = _input.Text.Trim(); if (text.Length == 0 || _request != null) return;
        _lastAction = null;
        Append("Sen", text); _input.Clear(); _send.Enabled = false; _status.Text = "Yanıt hazırlanıyor…";
        var request = new CancellationTokenSource(TimeSpan.FromMinutes(3)); _request = request;
        try
        {
            var settings = _context.Secrets.Load();
            if (settings.Mode == AiMode.LocalModel && text.Length > 1200) throw new InvalidOperationException("Yerel modelde tek mesajı 1.200 karakterden kısa tut.");
            var result = await _session.ChatAsync(settings, text, request.Token); if (result != _lastAction) Append("Kutarya Us", result);
        }
        catch (OperationCanceledException) { Append("Bağlantı", "İstek durduruldu veya zaman aşımına uğradı. Önceden tamamlanan dosya işlemleri korunur."); }
        catch (Exception error) { Append("Bağlantı", error.Message); }
        finally
        {
            _request = null; request.Dispose();
            if (!IsDisposed) { _send.Enabled = true; _status.Text = _context.LocalModel?.IsReady == true && _context.Secrets.Load().Mode == AiMode.LocalModel ? "Çevrimdışı hazır · Enter gönderir" : "Enter gönderir • Shift+Enter yeni satır"; }
        }
    }
}
