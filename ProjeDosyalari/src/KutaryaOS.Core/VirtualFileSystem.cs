using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace KutaryaOS.Core;

public record VirtualEntry(string Name, string Path, bool IsDirectory, long Bytes, DateTime Modified);
public record ImportResult(string Source, string Destination, bool Moved, string Message);

public sealed partial class VirtualFileSystem
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    public string Root { get; }
    public string TrashRoot { get; }
    public long CapacityBytes { get; }
    public event EventHandler? Changed;
    public VirtualFileSystem(string machineRoot, long capacityBytes)
    {
        if (capacityBytes <= 0) throw new ArgumentOutOfRangeException(nameof(capacityBytes));
        Root = Path.GetFullPath(Path.Combine(machineRoot, "Disk"));
        TrashRoot = Path.GetFullPath(Path.Combine(machineRoot, "CopKutusu"));
        CapacityBytes = capacityBytes;
        Directory.CreateDirectory(Root); Directory.CreateDirectory(TrashRoot);
        foreach (var name in new[] { "Masaustu", "Belgeler", "Resimler", "Projeler" }) Directory.CreateDirectory(Path.Combine(Root, name));
        RejectLinks(Root); RejectLinks(TrashRoot);
    }

    public long UsedBytes => DirectoryBytes(Root) + DirectoryBytes(TrashRoot);
    private static long DirectoryBytes(string root)
    {
        long total = 0;
        foreach (var entry in Directory.EnumerateFileSystemEntries(root))
        {
            RejectLinks(entry);
            total = checked(total + (Directory.Exists(entry) ? DirectoryBytes(entry) : new FileInfo(entry).Length));
        }
        return total;
    }

    public string Resolve(string relative)
    {
        relative = relative.Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(relative) || relative.Contains(':')) throw new ArgumentException("Yalnız sanal cihaz içindeki göreli yollar kullanılabilir.");
        foreach (var part in relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
            ValidateName(part);
        var path = Path.GetFullPath(Path.Combine(Root, relative));
        if (!Inside(path, Root)) throw new ArgumentException("Sanal cihaz dışına erişim engellendi.");
        RejectLinks(path);
        return path;
    }
    private static bool Inside(string path, string root) => path.Equals(root, StringComparison.OrdinalIgnoreCase) || path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    private static void ValidateName(string name)
    {
        var stem = name.Split('.')[0].ToUpperInvariant();
        if (name is "." or ".." || name.EndsWith(' ') || name.EndsWith('.') || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Any(char.IsControl) ||
            stem is "CON" or "PRN" or "AUX" or "NUL" || (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && char.IsDigit(stem[3])))
            throw new ArgumentException("Dosya veya klasör adı geçersiz.");
    }
    private static void RejectLinks(string path)
    {
        for (var current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Bağlantı/junction içeren yollar desteklenmiyor; normal yerel dosya kullan.");
    }

    public IReadOnlyList<VirtualEntry> List(string relative = "")
    {
        var directory = Resolve(relative);
        return Directory.EnumerateFileSystemEntries(directory).Select(path =>
        {
            RejectLinks(path); var folder = Directory.Exists(path);
            return new VirtualEntry(Path.GetFileName(path), Path.GetRelativePath(Root, path).Replace('\\', '/'), folder, folder ? 0 : new FileInfo(path).Length, File.GetLastWriteTime(path));
        }).OrderByDescending(e => e.IsDirectory).ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    public async Task CreateDirectoryAsync(string relative, CancellationToken cancellation = default)
    {
        await _gate.WaitAsync(cancellation);
        try { Directory.CreateDirectory(Resolve(relative)); }
        finally { _gate.Release(); }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task WriteTextAsync(string relative, string content, bool overwrite = false, CancellationToken cancellation = default)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        await _gate.WaitAsync(cancellation);
        string? temp = null;
        try
        {
            var destination = Resolve(relative);
            if (File.Exists(destination) && !overwrite) throw new IOException("Bu isimde dosya var; başka bir isim seç.");
            var oldLength = File.Exists(destination) ? new FileInfo(destination).Length : 0;
            CheckSpace(bytes.LongLength - oldLength);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            temp = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllBytesAsync(temp, bytes, cancellation);
            cancellation.ThrowIfCancellationRequested();
            File.Move(temp, destination, overwrite);
        }
        finally { if (temp != null && File.Exists(temp)) File.Delete(temp); _gate.Release(); }
        Changed?.Invoke(this, EventArgs.Empty);
    }
    private void CheckSpace(long additional)
    {
        if (additional <= 0) return;
        if (UsedBytes > CapacityBytes - additional) throw new IOException("Sanal depolama kotası dolu. Çöp kutusundaki dosyalar da kotaya dahildir.");
        if (new DriveInfo(Path.GetPathRoot(Root)!).AvailableFreeSpace < additional + 32 * HardwareSnapshot.MiB) throw new IOException("Gerçek diskte yeterli boş alan yok.");
    }
    public async Task<string> ReadTextAsync(string relative, CancellationToken cancellation = default)
    {
        var path = Resolve(relative);
        if (new FileInfo(path).Length > 2 * HardwareSnapshot.MiB) throw new IOException("Not defteri ve AI en fazla 2 MB metin açabilir.");
        var content = await File.ReadAllTextAsync(path, cancellation);
        if (content.Contains('\0')) throw new IOException("Bu dosya metin değil; sanal depoda saklanmaya devam eder.");
        return content;
    }

    public async Task<ImportResult> ImportMoveAsync(string source, string directory, CancellationToken cancellation = default)
    {
        source = Path.GetFullPath(source);
        RejectLinks(source);
        if (!File.Exists(source)) throw new FileNotFoundException("Taşınacak dosya bulunamadı.", source);
        if (Inside(source, Root) || Inside(source, TrashRoot)) throw new IOException("Dosya zaten bu sanal cihazda.");
        ValidateName(Path.GetFileName(source));
        await _gate.WaitAsync(cancellation);
        string? temp = null;
        ImportResult result;
        try
        {
            var targetDirectory = Resolve(directory); Directory.CreateDirectory(targetDirectory);
            var destination = UniquePath(targetDirectory, Path.GetFileName(source));
            // DELETE yetkili açık tanıtıcı kaynakta yazma/yeniden adlandırma yarışını kapatır.
            using var handle = CreateFile(source, 0x80000000u | 0x00010000u, 1, IntPtr.Zero, 3, 0x08000000, IntPtr.Zero);
            if (handle.IsInvalid) throw new IOException("Kaynak dosya kilitli veya taşınması için izin yok.", new Win32Exception(Marshal.GetLastWin32Error()));
            using var input = new FileStream(handle, FileAccess.Read, 81920, false);
            CheckSpace(input.Length);
            temp = Path.Combine(targetDirectory, ".aktarim-" + Guid.NewGuid().ToString("N") + ".tmp");
            await using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                await input.CopyToAsync(output, cancellation); await output.FlushAsync(cancellation); output.Flush(true);
            }
            input.Position = 0;
            var originalHash = await SHA256.HashDataAsync(input, cancellation);
            await using (var copied = File.OpenRead(temp))
            {
                var copiedHash = await SHA256.HashDataAsync(copied, cancellation);
                if (!CryptographicOperations.FixedTimeEquals(originalHash, copiedHash)) throw new IOException("Dosya doğrulaması başarısız; kaynak korundu.");
            }
            cancellation.ThrowIfCancellationRequested();
            File.Move(temp, destination); temp = null;
            var disposition = new FileDisposition { Delete = true };
            var deleted = SetFileInformationByHandle(handle, 4, ref disposition, (uint)Marshal.SizeOf<FileDisposition>());
            result = new(source, Path.GetRelativePath(Root, destination).Replace('\\', '/'), deleted,
                deleted ? "Doğrulandı ve sanal cihaza taşındı." : "Kopya doğrulandı; kaynak kaldırılamadığı için asıl dosya yerinde korundu.");
        }
        finally { if (temp != null && File.Exists(temp)) File.Delete(temp); _gate.Release(); }
        Changed?.Invoke(this, EventArgs.Empty); return result;
    }

    public async Task MoveAsync(string from, string to, CancellationToken cancellation = default)
    {
        await _gate.WaitAsync(cancellation);
        try
        {
            var source = Resolve(from); var target = Resolve(to);
            if (source.Equals(Root, StringComparison.OrdinalIgnoreCase)) throw new IOException("Sanal disk taşınamaz.");
            if (File.Exists(target) || Directory.Exists(target)) throw new IOException("Hedef isim zaten kullanılıyor.");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (Directory.Exists(source)) Directory.Move(source, target); else File.Move(source, target);
        }
        finally { _gate.Release(); }
        Changed?.Invoke(this, EventArgs.Empty);
    }
    public async Task TrashAsync(string relative)
    {
        await _gate.WaitAsync();
        try
        {
            var source = Resolve(relative);
            if (source.Equals(Root, StringComparison.OrdinalIgnoreCase)) throw new IOException("Sanal disk silinemez.");
            var id = Guid.NewGuid().ToString("N"); var box = Path.Combine(TrashRoot, id); Directory.CreateDirectory(box);
            ConfigurationStore.AtomicWrite(Path.Combine(box, "konum.txt"), relative);
            var target = Path.Combine(box, "icerik");
            if (Directory.Exists(source)) Directory.Move(source, target); else File.Move(source, target);
        }
        finally { _gate.Release(); }
        Changed?.Invoke(this, EventArgs.Empty);
    }
    public IReadOnlyList<(string Id, string Original)> ListTrash() => Directory.EnumerateDirectories(TrashRoot)
        .Where(d => File.Exists(Path.Combine(d, "konum.txt")) && (File.Exists(Path.Combine(d, "icerik")) || Directory.Exists(Path.Combine(d, "icerik"))))
        .Select(d => (Path.GetFileName(d), File.ReadAllText(Path.Combine(d, "konum.txt")))).ToArray();
    public async Task RestoreAsync(string id)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new ArgumentException("Çöp kaydı geçersiz.");
        await _gate.WaitAsync();
        try
        {
            var box = Path.Combine(TrashRoot, id); RejectLinks(box);
            var target = Resolve(File.ReadAllText(Path.Combine(box, "konum.txt")));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            target = UniquePath(Path.GetDirectoryName(target)!, Path.GetFileName(target));
            var source = Path.Combine(box, "icerik"); RejectLinks(source);
            if (Directory.Exists(source)) Directory.Move(source, target); else File.Move(source, target);
            File.Delete(Path.Combine(box, "konum.txt")); Directory.Delete(box);
        }
        finally { _gate.Release(); }
        Changed?.Invoke(this, EventArgs.Empty);
    }
    private static string UniquePath(string directory, string name)
    {
        var path = Path.Combine(directory, name); var index = 2;
        while (File.Exists(path) || Directory.Exists(path)) path = Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(name)} ({index++}){Path.GetExtension(name)}");
        return path;
    }
    [StructLayout(LayoutKind.Sequential)] private struct FileDisposition { [MarshalAs(UnmanagedType.U1)] public bool Delete; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint attributes, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int informationClass, ref FileDisposition information, uint size);
}
