using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace KutaryaOS.Core;

public record ExportProgress(int CompletedFiles, int TotalFiles, string Name);
public record ExportResult(string Destination, bool Moved, int FileCount, string Message);

public sealed partial class VirtualFileSystem
{
    // Yalnız kullanıcı arayüzü çağırır. AI araç sözleşmesine dışa aktarım eklenmez.
    public async Task<ExportResult> ExportMoveAsync(string relative, string hostDirectory,
        IProgress<ExportProgress>? progress = null, CancellationToken cancellation = default)
    {
        await _gate.WaitAsync(cancellation).ConfigureAwait(false);
        string? staging = null;
        var streams = new List<(string Path, FileStream Stream)>();
        var directories = new List<(string Path, SafeFileHandle Handle)>();
        ExportResult? result = null;
        try
        {
            var source = Resolve(relative);
            if (source.Equals(Root, StringComparison.OrdinalIgnoreCase) ||
                new[] { "Masaustu", "Belgeler", "Resimler", "Projeler" }.Any(name => source.Equals(Resolve(name), StringComparison.OrdinalIgnoreCase)))
                throw new IOException("Sanal diskin ana klasörleri taşınamaz; içindeki öğeyi seç.");
            var folder = Directory.Exists(source);
            if (!folder && !File.Exists(source)) throw new FileNotFoundException("Seçilen öğe artık bulunamıyor.");
            if (!Path.IsPathFullyQualified(hostDirectory)) throw new ArgumentException("Ana PC'de mevcut bir hedef klasör seç.");
            hostDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(hostDirectory));
            RejectLinks(hostDirectory);
            if (!Directory.Exists(hostDirectory) || hostDirectory.StartsWith(@"\\", StringComparison.Ordinal))
                throw new IOException("Mevcut bir yerel klasör seç; ağ yolları desteklenmiyor.");
            var machineRoot = Path.GetDirectoryName(Root)!;
            if (Inside(hostDirectory, machineRoot)) throw new IOException("Hedef sanal cihazın veri klasörü dışında olmalı.");
            var destination = UniquePath(hostDirectory, Path.GetFileName(source));
            if (Inside(machineRoot, destination)) throw new IOException("Hedef sanal cihazın verisini kapsayamaz.");
            // Klasörler yeniden adlandırılamaz; dosyalar kopyalama boyunca yazılamaz/silinemez.
            using var hostLock = LockDirectory(hostDirectory);
            void Visit(string directory)
            {
                cancellation.ThrowIfCancellationRequested(); RejectLinks(directory);
                if (directories.Count >= 256 || Path.GetRelativePath(source, directory).Split(Path.DirectorySeparatorChar).Length > 64)
                    throw new IOException("Klasör çok büyük veya çok derin; daha küçük bir alt klasör seç.");
                directories.Add((directory, LockDirectory(directory)));
                foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    RejectLinks(entry);
                    if (Directory.Exists(entry)) Visit(entry); else OpenSource(entry);
                }
            }
            void OpenSource(string path)
            {
                cancellation.ThrowIfCancellationRequested(); RejectLinks(path);
                if (streams.Count >= 5000) throw new IOException("Tek aktarımda en fazla 5.000 dosya seçilebilir; daha küçük bir alt klasör seç.");
                if ((File.GetAttributes(path) & FileAttributes.ReadOnly) != 0) throw new IOException("Salt okunur dosya taşınamaz: " + Path.GetFileName(path));
                var handle = CreateFile(path, 0x80000000u | 0x00010000u, 1, IntPtr.Zero, 3, 0x08000000, IntPtr.Zero);
                if (handle.IsInvalid) { handle.Dispose(); throw new IOException("Dosya kilitli veya taşıma izni yok: " + Path.GetFileName(path), new Win32Exception(Marshal.GetLastWin32Error())); }
                try { streams.Add((path, new FileStream(handle, FileAccess.Read, 81920, false))); }
                catch { handle.Dispose(); throw; }
            }
            if (folder) Visit(source); else OpenSource(source);
            var totalBytes = streams.Sum(item => item.Stream.Length);
            if (new DriveInfo(Path.GetPathRoot(hostDirectory)!).AvailableFreeSpace < totalBytes + 32 * HardwareSnapshot.MiB)
                throw new IOException("Hedef diskte yeterli boş alan yok.");
            staging = Path.Combine(hostDirectory, ".kutarya-aktarim-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            var payload = Path.Combine(staging, "icerik");
            if (folder)
                foreach (var directory in directories)
                    Directory.CreateDirectory(Path.Combine(payload, Path.GetRelativePath(source, directory.Path)));
            var completed = 0;
            foreach (var item in streams)
            {
                cancellation.ThrowIfCancellationRequested();
                var target = folder ? Path.Combine(payload, Path.GetRelativePath(source, item.Path)) : payload;
                await using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                {
                    await item.Stream.CopyToAsync(output, cancellation).ConfigureAwait(false);
                    await output.FlushAsync(cancellation).ConfigureAwait(false); output.Flush(true);
                }
                item.Stream.Position = 0;
                var expected = await SHA256.HashDataAsync(item.Stream, cancellation).ConfigureAwait(false);
                await using (var copied = File.OpenRead(target))
                {
                    var actual = await SHA256.HashDataAsync(copied, cancellation).ConfigureAwait(false);
                    if (!CryptographicOperations.FixedTimeEquals(expected, actual))
                        throw new IOException("Dosya doğrulanamadı; sanal kaynak korundu.");
                }
                File.SetLastWriteTimeUtc(target, File.GetLastWriteTimeUtc(item.Path));
                progress?.Report(new(++completed, streams.Count, Path.GetFileName(item.Path)));
            }
            cancellation.ThrowIfCancellationRequested();
            // Yeni eklenen öğe varsa hiçbir kaynak silinmeden işlemi durdur.
            if (folder)
            {
                var expected = new HashSet<string>(streams.Select(item => item.Path).Concat(directories.Skip(1).Select(item => item.Path)), StringComparer.OrdinalIgnoreCase);
                var actual = new HashSet<string>();
                void Snapshot(string directory)
                {
                    foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
                    { RejectLinks(entry); actual.Add(entry); if (Directory.Exists(entry)) Snapshot(entry); }
                }
                Snapshot(source);
                if (!expected.SetEquals(actual)) throw new IOException("Kaynak klasör aktarım sırasında değişti; sanal kaynak korundu.");
            }
            RejectLinks(hostDirectory);
            if (folder) Directory.Move(payload, destination); else File.Move(payload, destination);
            // Bu noktadan sonra iptal kopyayı geri almaz; doğrulanmış hedef mutlaka korunur.
            var removed = true;
            foreach (var item in streams)
            {
                var disposition = new FileDisposition { Delete = true };
                if (!SetFileInformationByHandle(item.Stream.SafeFileHandle, 4, ref disposition, (uint)Marshal.SizeOf<FileDisposition>())) removed = false;
                item.Stream.Dispose();
            }
            foreach (var directory in directories.AsEnumerable().Reverse())
            {
                directory.Handle.Dispose();
                try { RejectLinks(directory.Path); Directory.Delete(directory.Path, false); }
                catch (IOException) { removed = false; }
                catch (UnauthorizedAccessException) { removed = false; }
            }
            removed &= !File.Exists(source) && !Directory.Exists(source);
            result = new(destination, removed, streams.Count, removed
                ? "Dosyalar doğrulandı ve ana PC’ye taşındı. Sanal öğe kaldırıldı."
                : "Ana PC’deki kopya doğrulandı. Sanal kaynak tamamen kaldırılamadı; kalan öğeler yerinde korundu.");
        }
        finally
        {
            foreach (var item in streams) item.Stream.Dispose();
            foreach (var directory in directories) directory.Handle.Dispose();
            // Yalnız bu çağrının oluşturduğu, hedef altında doğrulanan geçici klasörü temizle.
            try
            {
                if (staging != null && Directory.Exists(staging))
                {
                    RejectLinks(staging);
                    if (Path.GetFileName(staging).StartsWith(".kutarya-aktarim-", StringComparison.Ordinal) &&
                        Path.GetDirectoryName(staging)!.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(hostDirectory)), StringComparison.OrdinalIgnoreCase))
                        Directory.Delete(staging, true);
                }
            }
            catch (IOException) { /* Kaynak korunur; geçici klasör başka süreçte açık olabilir. */ }
            catch (UnauthorizedAccessException) { }
            _gate.Release();
            if (result != null) Changed?.Invoke(this, EventArgs.Empty);
        }
        return result!;
    }

    private static SafeFileHandle LockDirectory(string directory)
    {
        var handle = CreateFile(directory, 0x00000080, 3, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero);
        if (!handle.IsInvalid) return handle;
        handle.Dispose(); throw new IOException("Klasör kilitli veya erişilemiyor: " + Path.GetFileName(directory), new Win32Exception(Marshal.GetLastWin32Error()));
    }
}
