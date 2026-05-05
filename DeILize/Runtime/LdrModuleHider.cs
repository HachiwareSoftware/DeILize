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
            try
            {
                using (var process = Process.GetCurrentProcess())
                {
                    var pbi = default(NativeMethods.PROCESS_BASIC_INFORMATION);
                    int returnLength;
                    int status = NativeMethods.NtQueryInformationProcess(
                        process.Handle,
                        NativeMethods.ProcessBasicInformation,
                        out pbi,
                        Marshal.SizeOf(typeof(NativeMethods.PROCESS_BASIC_INFORMATION)),
                        out returnLength);

                    if (status != 0)
                        return false;

                    IntPtr pebBase = pbi.PebBaseAddress;
                    if (pebBase == IntPtr.Zero)
                        return false;

                    IntPtr ldrPtr = Marshal.ReadIntPtr(pebBase, 0x018);
                    if (ldrPtr == IntPtr.Zero)
                        return false;

                    IntPtr flink = Marshal.ReadIntPtr(ldrPtr, 0x010);
                    IntPtr current = flink;

                    while (true)
                    {
                        IntPtr entryFlink = Marshal.ReadIntPtr(current, 0x00);
                        IntPtr entryBlink = Marshal.ReadIntPtr(current, 0x08);
                        IntPtr entryDllBase = Marshal.ReadIntPtr(current, 0x20);

                        if (entryDllBase == moduleBase)
                        {
                            Marshal.WriteIntPtr(entryFlink, 0x08, entryBlink);
                            Marshal.WriteIntPtr(entryBlink, 0x00, entryFlink);

                            return true;
                        }

                        if (entryFlink == flink)
                            break;

                        current = entryFlink;
                    }
                }
            }
            catch
            {
            }

            return false;
        }
    }
}
