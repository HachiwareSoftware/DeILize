using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using DeILize.Native;

namespace DeILize.Runtime
{
    internal static class LdrModuleHider
    {
        internal static bool Unlink(IntPtr moduleBase)
        {
            Logger.Section("PEB LDR Module Unlink");
            Logger.Debug($"Target: 0x{moduleBase:X}");

            try
            {
                using (var process = Process.GetCurrentProcess())
                {
                    var pbi = default(NativeMethods.PROCESS_BASIC_INFORMATION);
                    int status = NativeMethods.NtQueryInformationProcess(
                        process.Handle, NativeMethods.ProcessBasicInformation, out pbi,
                        Marshal.SizeOf(typeof(NativeMethods.PROCESS_BASIC_INFORMATION)), out _);
                    if (status != 0) { Logger.Error($"NtQueryInfoProcess: 0x{status:X8}"); return false; }

                    IntPtr pebBase = pbi.PebBaseAddress;
                    Logger.Debug($"PEB: 0x{pebBase:X}");
                    if (pebBase == IntPtr.Zero) { Logger.Error("PEB is null"); return false; }

                    IntPtr ldrPtr = Marshal.ReadIntPtr(pebBase, 0x018);
                    Logger.Debug($"LDR: 0x{ldrPtr:X}");
                    if (ldrPtr == IntPtr.Zero) { Logger.Error("LDR is null"); return false; }

                    IntPtr flink = Marshal.ReadIntPtr(ldrPtr, 0x010);
                    Logger.Debug($"InMemoryOrder head: 0x{flink:X}");

                    IntPtr current = flink;
                    int idx = 0;
                    while (true)
                    {
                        IntPtr entryFlink = Marshal.ReadIntPtr(current, 0x00);
                        IntPtr entryBlink = Marshal.ReadIntPtr(current, 0x08);
                        IntPtr entryDllBase = Marshal.ReadIntPtr(current, 0x20);
                        Logger.Debug($"Entry[{idx}] dllBase=0x{entryDllBase:X}");

                        if (entryDllBase == moduleBase)
                        {
                            Logger.Debug($"Found at entry[{idx}], unlinking...");
                            Marshal.WriteIntPtr(entryFlink, 0x08, entryBlink);
                            Marshal.WriteIntPtr(entryBlink, 0x00, entryFlink);
                            Logger.Info("Module unlinked from PEB LDR list");
                            return true;
                        }
                        if (entryFlink == flink) break;
                        current = entryFlink;
                        idx++;
                    }
                    Logger.Warn("Module not found in InMemoryOrder list");
                }
            }
            catch (Exception ex) { Logger.Error($"{ex.GetType().Name}: {ex.Message}"); }
            return false;
        }
    }
}
