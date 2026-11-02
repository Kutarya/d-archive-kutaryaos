using KutaryaOS.Core;

namespace KutaryaOS.WinForms;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var installedRoot = Directory.GetParent(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))?.Parent?.FullName;
        var defaultRoot = installedRoot != null && File.Exists(Path.Combine(installedRoot, "ProjeDosyalari", "KutaryaOS.sln"))
            ? Path.Combine(installedRoot, "Veri") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KutaryaOS", "Veri");
        var root = GetArgument(args, "--data") ?? defaultRoot;
        root = Path.GetFullPath(root); Directory.CreateDirectory(root);
        if (args.Contains("--make-assets")) { AppAssets.WriteIcon(root); return 0; }
        if (args.Contains("--self-test")) return AcceptanceTests.RunAsync(root).GetAwaiter().GetResult();
        if (args.Contains("--transfer-tests")) return DesktopTransferTests.RunAsync(root).GetAwaiter().GetResult();
        if (args.Contains("--ui-smoke")) return UiSmoke.Run(root);
        if (args.Contains("--ui-local-smoke")) return UiSmoke.Run(root, Path.Combine(GetArgument(args, "--model-data") ?? root, "YerelAI"));
        if (args.Contains("--model-tests")) return ModelDownloadTests.RunAsync(root).GetAwaiter().GetResult();
        if (args.Contains("--download-model")) return LocalAiDiagnostics.RunAsync(root, true).GetAwaiter().GetResult();
        if (args.Contains("--local-ai-test")) return LocalAiDiagnostics.RunAsync(root, false).GetAwaiter().GetResult();
        var mutexId = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(root.ToUpperInvariant())))[..24];
        using var mutex = new Mutex(true, @"Local\KutaryaOS-" + mutexId, out var first);
        if (!first) { MessageBox.Show("Bu Kutarya OS cihazı zaten açık.", "Kutarya OS"); return 1; }
        try
        {
            var config = new ConfigurationStore(root);
            var machine = config.LoadMachine();
            if (machine == null)
            {
                var hardware = new WindowsHardwareProvider().Read(root);
                using var setup = new SetupForm(hardware);
                if (setup.ShowDialog() != DialogResult.OK || setup.Configuration == null) return 0;
                machine = setup.Configuration; config.SaveMachine(machine);
            }
            var files = new VirtualFileSystem(Path.Combine(root, machine.Id), machine.StorageGb * HardwareSnapshot.GiB);
            var secrets = new SecretStore(Path.Combine(root, "ai.dpapi"));
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(3) };
            using var local = new LocalModelManager(Path.Combine(root, "YerelAI"));
            if (secrets.Load().Mode == AiMode.NotConfigured && local.IsDownloaded) secrets.Save(new("", "Kutarya Us", "", Mode: AiMode.LocalModel));
            DesktopForm? desktop = null;
            var context = new DesktopContext(machine, files, new(machine.RamMb, machine.VramMb), config, secrets, new RoutingAiProvider(new HttpAiProvider(client), local), theme => desktop?.ApplyTheme(theme), local);
            using var shell = new DesktopForm(context); desktop = shell;
            Application.Run(shell); return 0;
        }
        catch (Exception error)
        {
            ConfigurationStore.AtomicWrite(Path.Combine(root, "son-hata.txt"), error.ToString());
            MessageBox.Show(error.Message + "\n\nAyrıntı: " + Path.Combine(root, "son-hata.txt"), "Kutarya OS başlatılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1;
        }
    }
    private static string? GetArgument(string[] args, string name)
    {
        var index = Array.IndexOf(args, name); return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
