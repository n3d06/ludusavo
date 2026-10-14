using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ludusavo.Services;

public static class MemoryOptimizer
{
    [DllImport("psapi.dll", SetLastError = true)]
    private static extern int EmptyWorkingSet(IntPtr hProcess);

    /// <summary>
    /// Forces Garbage Collection and trims working set memory back to Windows.
    /// Recommended to call only when idle (e.g. after scan finishes, or when minimized to tray).
    /// </summary>
    public static void TrimMemory()
    {
        try
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: false, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: false, compacting: true);

            if (OperatingSystem.IsWindows())
            {
                using var currentProcess = Process.GetCurrentProcess();
                EmptyWorkingSet(currentProcess.Handle);
            }
        }
        catch
        {
            // Best effort memory trimming
        }
    }
}
