using System.Text.Json;

namespace KutaryaOS.Core;

public record GpuInfo(string Name, long DedicatedBytes, long SharedBytes);
public record HardwareSnapshot(long TotalRamBytes, long AvailableRamBytes, long FreeDiskBytes, string Drive, IReadOnlyList<GpuInfo> Gpus)
{
    public const long MiB = 1024 * 1024;
    public const long GiB = 1024 * MiB;
    public int MaximumRamMb => (int)Math.Max(256, (TotalRamBytes - Math.Min(2 * GiB, TotalRamBytes / 4)) / MiB);
    public int MaximumStorageGb => (int)Math.Max(1, (FreeDiskBytes - Math.Min(2 * GiB, FreeDiskBytes / 4)) / GiB);
}

// Değerler yalnız doğrulanmış kurucu üzerinden oluşturulur: kapsülleme.
public sealed class MachineConfig
{
    public string Id { get; }
    public string Name { get; }
    public int RamMb { get; }
    public int VramMb { get; }
    public int StorageGb { get; }
    public string GpuName { get; }
    public DateTime CreatedUtc { get; }

    public MachineConfig(string id, string name, int ramMb, int vramMb, int storageGb, string gpuName, DateTime createdUtc)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new ArgumentException("Cihaz kimliği geçersiz.");
        name = name.Trim();
        if (name.Length is < 2 or > 40 || name.Any(char.IsControl)) throw new ArgumentException("Cihaz adı 2–40 karakter olmalı.");
        if (ramMb < 256 || vramMb < 0 || storageGb < 1) throw new ArgumentException("Kaynak değerleri geçersiz.");
        Id = id; Name = name; RamMb = ramMb; VramMb = vramMb; StorageGb = storageGb; GpuName = gpuName; CreatedUtc = createdUtc;
    }

    public static MachineConfig Create(string name, int ramMb, int vramMb, int storageGb, GpuInfo gpu, HardwareSnapshot hardware)
    {
        if (ramMb > hardware.MaximumRamMb || storageGb > hardware.MaximumStorageGb || vramMb > gpu.DedicatedBytes / HardwareSnapshot.MiB)
            throw new ArgumentException("Seçilen kaynaklar gerçek donanım sınırını aşıyor.");
        if (!hardware.Gpus.Contains(gpu)) throw new ArgumentException("Seçilen GPU donanım listesinde yok.");
        return new(Guid.NewGuid().ToString("N"), name, ramMb, vramMb, storageGb, gpu.Name, DateTime.UtcNow);
    }
}

public sealed class ConfigurationStore(string root)
{
    public string Root { get; } = Path.GetFullPath(root);
    public MachineConfig? LoadMachine()
    {
        var path = Path.Combine(Root, "cihaz.json");
        return File.Exists(path) ? JsonSerializer.Deserialize<MachineConfig>(File.ReadAllText(path), JsonOptions) : null;
    }
    public void SaveMachine(MachineConfig config) => AtomicWrite(Path.Combine(Root, "cihaz.json"), JsonSerializer.Serialize(config, JsonOptions));
    public string LoadTheme() => File.Exists(Path.Combine(Root, "tema.txt")) ? File.ReadAllText(Path.Combine(Root, "tema.txt")).Trim() : "Okyanus";
    public void SaveTheme(string theme) => AtomicWrite(Path.Combine(Root, "tema.txt"), theme);
    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public static void AtomicWrite(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, content, new System.Text.UTF8Encoding(false)); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

public sealed class ResourceBudget(int ramMb, int vramMb)
{
    private readonly Dictionary<Guid, (int Ram, int Vram, string Name)> _allocations = [];
    public int RamLimit { get; } = ramMb;
    public int VramLimit { get; } = vramMb;
    public int UsedRam => _allocations.Values.Sum(a => a.Ram);
    public int UsedVram => _allocations.Values.Sum(a => a.Vram);
    public IReadOnlyList<(string Name, int Ram)> Applications => _allocations.Values.Select(a => (a.Name, a.Ram)).ToArray();
    public event EventHandler? Changed;
    public IDisposable Acquire(string name, int ram, int vram = 0)
    {
        if (ram < 0 || vram < 0) throw new ArgumentOutOfRangeException(nameof(ram));
        if (UsedRam + ram > RamLimit || UsedVram + vram > VramLimit) throw new InvalidOperationException("Sanal cihaz kaynak bütçesi dolu. Bir uygulamayı kapat.");
        var id = Guid.NewGuid(); _allocations.Add(id, (ram, vram, name)); Changed?.Invoke(this, EventArgs.Empty);
        return new Allocation(() => { if (_allocations.Remove(id)) Changed?.Invoke(this, EventArgs.Empty); });
    }
    private sealed class Allocation(Action release) : IDisposable
    {
        private Action? _release = release;
        public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
    }
}
