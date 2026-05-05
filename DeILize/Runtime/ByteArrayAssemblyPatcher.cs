using System;
using DeILize.Native;
using System.Runtime.InteropServices;

namespace DeILize.Runtime
{
    internal static class ByteArrayAssemblyPatcher
    {
        internal static bool PatchBytes(ref byte[] assemblyData)
        {
            Logger.Section("Patch Byte[] Assembly (pre-load)");
            if (assemblyData == null || assemblyData.Length < 0x40)
            {
                Logger.Error($"Buffer too small: {(assemblyData?.Length ?? 0)} bytes");
                return false;
            }

            Logger.Debug($"Buffer size: {assemblyData.Length} bytes");

            if (!PeHeaderPatcher.PatchBytes(assemblyData))
            {
                Logger.Error("PE header patching failed");
                return false;
            }

            Logger.Info("Byte[] assembly CLR directory zeroed");
            return true;
        }
    }
}
