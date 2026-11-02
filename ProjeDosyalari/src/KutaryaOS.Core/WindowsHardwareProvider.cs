using System.Runtime.InteropServices;

namespace KutaryaOS.Core;

public interface IHardwareProvider { HardwareSnapshot Read(string dataRoot); }

public sealed class WindowsHardwareProvider : IHardwareProvider
{
    public HardwareSnapshot Read(string dataRoot)
    {
        var status = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (!GlobalMemoryStatusEx(ref status)) throw new InvalidOperationException("Windows bellek bilgisi okunamadı.");
        var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(dataRoot))!);
        var gpus = ReadGpus();
        return new((long)status.TotalPhysical, (long)status.AvailablePhysical, drive.AvailableFreeSpace, drive.Name, gpus);
    }

    // DXGI vtable doğrudan kullanılır; WMI AdapterRAM alanındaki 32-bit sınırı aşılır.
    private static unsafe IReadOnlyList<GpuInfo> ReadGpus()
    {
        List<GpuInfo> result = [];
        var iid = new Guid("7b7166ec-21c7-44ae-b21a-c9ae321ae369");
        IntPtr factory = IntPtr.Zero;
        if (CreateDXGIFactory1(ref iid, out factory) < 0) return [new("GPU bilgisi okunamadı", 0, 0)];
        try
        {
            var table = *(IntPtr**)factory;
            var enumerate = (delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr*, int>)table[12];
            for (uint i = 0; i < 16; i++)
            {
                IntPtr adapter = IntPtr.Zero;
                if (enumerate(factory, i, &adapter) < 0) break;
                try
                {
                    var adapterTable = *(IntPtr**)adapter;
                    var describe = (delegate* unmanaged[Stdcall]<IntPtr, AdapterDescription*, int>)adapterTable[10];
                    AdapterDescription description = default;
                    if (describe(adapter, &description) >= 0 && (description.Flags & 2) == 0)
                        result.Add(new(new string(description.Description).TrimEnd('\0'), checked((long)description.DedicatedVideoMemory), checked((long)description.SharedSystemMemory)));
                }
                finally { Marshal.Release(adapter); }
            }
        }
        finally { Marshal.Release(factory); }
        return result.Count > 0 ? result : [new("Paylaşımlı grafik", 0, 0)];
    }

    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
    [DllImport("dxgi.dll")] private static extern int CreateDXGIFactory1(ref Guid iid, out IntPtr factory);
    [StructLayout(LayoutKind.Sequential)] private struct MemoryStatus
    {
        public uint Length, Load;
        public ulong TotalPhysical, AvailablePhysical, TotalPage, AvailablePage, TotalVirtual, AvailableVirtual, AvailableExtended;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private unsafe struct AdapterDescription
    {
        public fixed char Description[128];
        public uint VendorId, DeviceId, SubsystemId, Revision;
        public nuint DedicatedVideoMemory, DedicatedSystemMemory, SharedSystemMemory;
        public uint LuidLow; public int LuidHigh; public uint Flags;
    }
}
