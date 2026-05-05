using System;
using System.Linq;
using System.Runtime.InteropServices;
using DeILize.Native;

namespace DeILize.Runtime
{
    public static class PeHeaderPatcher
    {
        internal const ushort DOS_MAGIC = 0x5A4D;
        internal const uint NT_SIGNATURE = 0x00004550;
        internal const ushort PE32_MAGIC = 0x10B;
        internal const ushort PE32_PLUS_MAGIC = 0x20B;
        internal const int CLR_DIRECTORY_INDEX = 14;
        internal const int DEBUG_DIRECTORY_INDEX = 6;

        internal static bool TryGetClrDirectoryOffsets(
            IntPtr moduleBase, out int rvaOffset, out int sizeOffset)
            => TryGetDataDirectoryOffset(moduleBase, CLR_DIRECTORY_INDEX, out rvaOffset, out sizeOffset);

        internal static bool TryGetDebugDirectoryOffsets(
            IntPtr moduleBase, out int rvaOffset, out int sizeOffset)
            => TryGetDataDirectoryOffset(moduleBase, DEBUG_DIRECTORY_INDEX, out rvaOffset, out sizeOffset);

        internal static bool TryGetDataDirectoryOffset(
            IntPtr moduleBase, int directoryIndex, out int rvaOffset, out int sizeOffset)
        {
            rvaOffset = 0;
            sizeOffset = 0;
            if (moduleBase == IntPtr.Zero) { Logger.Error("Module base is null"); return false; }

            Logger.Debug($"DOS magic: 0x{(ushort)Marshal.ReadInt16(moduleBase):X4}");
            if ((ushort)Marshal.ReadInt16(moduleBase) != DOS_MAGIC) { Logger.Error("Invalid DOS magic"); return false; }

            int e_lfanew = Marshal.ReadInt32(moduleBase, 0x3C);
            Logger.Debug($"e_lfanew: 0x{e_lfanew:X}");

            IntPtr ntHeaders = IntPtr.Add(moduleBase, e_lfanew);
            uint ntSig = (uint)Marshal.ReadInt32(ntHeaders);
            Logger.Debug($"NT signature: 0x{ntSig:X8}");
            if (ntSig != NT_SIGNATURE) { Logger.Error("Invalid NT signature"); return false; }

            IntPtr fileHeader = IntPtr.Add(ntHeaders, 4);
            Logger.Debug($"Machine: 0x{(ushort)Marshal.ReadInt16(fileHeader):X4}");

            IntPtr optionalHeader = IntPtr.Add(fileHeader, 20);
            ushort magic = (ushort)Marshal.ReadInt16(optionalHeader);
            string peType = magic == PE32_MAGIC ? "PE32" : magic == PE32_PLUS_MAGIC ? "PE32+" : "unknown";
            Logger.Debug($"PE format: {peType}");

            int optionalHeaderOffset = e_lfanew + 24;
            int dataDirOffset = magic == PE32_MAGIC ? optionalHeaderOffset + 96
                              : magic == PE32_PLUS_MAGIC ? optionalHeaderOffset + 112
                              : -1;
            if (dataDirOffset == -1) { Logger.Error($"Unknown PE magic: 0x{magic:X4}"); return false; }

            Logger.Debug($"Data dirs at offset: 0x{dataDirOffset:X}");
            int entryOffset = dataDirOffset + directoryIndex * 8;
            Logger.Debug($"Data dir [{directoryIndex}] entry: 0x{entryOffset:X}");

            rvaOffset = entryOffset;
            sizeOffset = entryOffset + 4;
            return true;
        }

        internal static bool ZeroClrDirectory(IntPtr moduleBase)
        {
            Logger.Section("Zero CLR Data Directory (memory)");
            Logger.Debug($"Module base: 0x{moduleBase:X}");

            if (!TryGetClrDirectoryOffsets(moduleBase, out int rvaOff, out int sizeOff))
            { Logger.Error("Could not locate CLR directory"); return false; }

            IntPtr entryAddr = IntPtr.Add(moduleBase, rvaOff);
            IntPtr sizeAddr = IntPtr.Add(moduleBase, sizeOff);
            Logger.Debug($"CLR RVA before: 0x{Marshal.ReadInt32(entryAddr):X8}, size before: 0x{Marshal.ReadInt32(sizeAddr):X8}");

            if (!NativeMethods.VirtualProtect(entryAddr, (UIntPtr)8, NativeMethods.PAGE_READWRITE, out uint oldProtect))
            { Logger.Error("VirtualProtect failed"); return false; }
            Logger.Debug($"Old protection: 0x{oldProtect:X}");

            Marshal.WriteInt32(entryAddr, 0); Marshal.WriteInt32(sizeAddr, 0);
            Logger.Info("CLR directory zeroed (RVA=0, Size=0)");

            NativeMethods.VirtualProtect(entryAddr, (UIntPtr)8, oldProtect, out _);
            Logger.Debug("Memory protection restored");
            return true;
        }

        internal static bool ZeroDebugDirectory(IntPtr moduleBase)
        {
            Logger.Section("Zero Debug Directory (memory)");
            Logger.Debug($"Module base: 0x{moduleBase:X}");

            if (!TryGetDebugDirectoryOffsets(moduleBase, out int rvaOff, out int sizeOff))
            { Logger.Error("Could not locate debug directory"); return false; }

            IntPtr entryAddr = IntPtr.Add(moduleBase, rvaOff);
            IntPtr sizeAddr = IntPtr.Add(moduleBase, sizeOff);
            Logger.Debug($"Debug RVA before: 0x{Marshal.ReadInt32(entryAddr):X8}, size before: 0x{Marshal.ReadInt32(sizeAddr):X8}");

            if (!NativeMethods.VirtualProtect(entryAddr, (UIntPtr)8, NativeMethods.PAGE_READWRITE, out uint oldProtect))
            { Logger.Error("VirtualProtect failed"); return false; }

            Marshal.WriteInt32(entryAddr, 0); Marshal.WriteInt32(sizeAddr, 0);
            Logger.Info("Debug directory zeroed");
            NativeMethods.VirtualProtect(entryAddr, (UIntPtr)8, oldProtect, out _);
            return true;
        }

        internal static bool PatchBytes(byte[] peBytes)
        {
            Logger.Section("Zero CLR Data Directory (file bytes)");
            if (peBytes == null || peBytes.Length < 0x40)
            { Logger.Error($"Buffer too small: {(peBytes?.Length ?? 0)} bytes"); return false; }

            Logger.Debug($"File size: {peBytes.Length} bytes");
            ushort dosMagic = (ushort)(peBytes[0] | (peBytes[1] << 8));
            Logger.Debug($"DOS magic: 0x{dosMagic:X4}");
            if (dosMagic != DOS_MAGIC) { Logger.Error("Not a PE file"); return false; }

            int e_lfanew = BitConverter.ToInt32(peBytes, 0x3C);
            Logger.Debug($"e_lfanew: 0x{e_lfanew:X}");
            if (e_lfanew < 0 || e_lfanew + 4 > peBytes.Length) { Logger.Error("e_lfanew out of bounds"); return false; }

            uint ntSig = (uint)BitConverter.ToInt32(peBytes, e_lfanew);
            Logger.Debug($"NT signature: 0x{ntSig:X8}");
            if (ntSig != NT_SIGNATURE) { Logger.Error("Invalid NT signature"); return false; }

            int fileHeaderOff = e_lfanew + 4;
            int optionalHeaderOff = fileHeaderOff + 20;
            ushort magic = (ushort)(peBytes[optionalHeaderOff] | (peBytes[optionalHeaderOff + 1] << 8));
            Logger.Debug($"PE format: {(magic == PE32_MAGIC ? "PE32" : magic == PE32_PLUS_MAGIC ? "PE32+" : "unknown")}");

            int dataDirOff = magic == PE32_MAGIC ? optionalHeaderOff + 96
                           : magic == PE32_PLUS_MAGIC ? optionalHeaderOff + 112
                           : -1;
            if (dataDirOff == -1) { Logger.Error($"Unknown PE magic: 0x{magic:X4}"); return false; }

            int clrEntryOff = dataDirOff + CLR_DIRECTORY_INDEX * 8;
            Logger.Debug($"CLR entry offset: 0x{clrEntryOff:X}");
            if (clrEntryOff + 8 > peBytes.Length) { Logger.Error("CLR entry extends beyond file"); return false; }

            int oldRva = BitConverter.ToInt32(peBytes, clrEntryOff);
            int oldSize = BitConverter.ToInt32(peBytes, clrEntryOff + 4);
            Logger.Debug($"Before: RVA=0x{oldRva:X8}, Size=0x{oldSize:X8}");

            Array.Clear(peBytes, clrEntryOff, 8);
            Logger.Info("CLR directory zeroed in file buffer");
            return true;
        }
    }
}
