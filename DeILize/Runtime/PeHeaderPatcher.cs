using System;
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
            IntPtr moduleBase,
            out int rvaOffset,
            out int sizeOffset)
        {
            return TryGetDataDirectoryOffset(moduleBase, CLR_DIRECTORY_INDEX, out rvaOffset, out sizeOffset);
        }

        internal static bool TryGetDebugDirectoryOffsets(
            IntPtr moduleBase,
            out int rvaOffset,
            out int sizeOffset)
        {
            return TryGetDataDirectoryOffset(moduleBase, DEBUG_DIRECTORY_INDEX, out rvaOffset, out sizeOffset);
        }

        internal static bool TryGetDataDirectoryOffset(
            IntPtr moduleBase,
            int directoryIndex,
            out int rvaOffset,
            out int sizeOffset)
        {
            rvaOffset = 0;
            sizeOffset = 0;

            if (moduleBase == IntPtr.Zero)
                return false;

            ushort dosMagic = (ushort)Marshal.ReadInt16(moduleBase);
            if (dosMagic != DOS_MAGIC)
                return false;

            int e_lfanew = Marshal.ReadInt32(moduleBase, 0x3C);
            IntPtr ntHeaders = IntPtr.Add(moduleBase, e_lfanew);

            uint ntSig = (uint)Marshal.ReadInt32(ntHeaders);
            if (ntSig != NT_SIGNATURE)
                return false;

            IntPtr fileHeader = IntPtr.Add(ntHeaders, 4);
            ushort machine = (ushort)Marshal.ReadInt16(fileHeader);
            _ = machine;

            IntPtr optionalHeader = IntPtr.Add(fileHeader, 20);
            ushort magic = (ushort)Marshal.ReadInt16(optionalHeader);

            int dataDirOffset;
            if (magic == PE32_MAGIC)
                dataDirOffset = optionalHeader.ToInt32() + 96;
            else if (magic == PE32_PLUS_MAGIC)
                dataDirOffset = optionalHeader.ToInt32() + 112;
            else
                return false;

            int entryOffset = dataDirOffset + directoryIndex * 8;

            rvaOffset = entryOffset;
            sizeOffset = entryOffset + 4;
            return true;
        }

        internal static bool ZeroClrDirectory(IntPtr moduleBase)
        {
            if (!TryGetClrDirectoryOffsets(moduleBase, out int rvaOff, out int sizeOff))
                return false;

            IntPtr entryAddr = (IntPtr)rvaOff;
            IntPtr sizeAddr = (IntPtr)sizeOff;

            if (!NativeMethods.VirtualProtect(entryAddr, (UIntPtr)8, NativeMethods.PAGE_READWRITE, out uint oldProtect))
                return false;

            Marshal.WriteInt32(entryAddr, 0);
            Marshal.WriteInt32(sizeAddr, 0);

            NativeMethods.VirtualProtect(entryAddr, (UIntPtr)8, oldProtect, out _);
            return true;
        }

        internal static bool ZeroDebugDirectory(IntPtr moduleBase)
        {
            if (!TryGetDebugDirectoryOffsets(moduleBase, out int rvaOff, out int sizeOff))
                return false;

            IntPtr entryAddr = (IntPtr)rvaOff;
            IntPtr sizeAddr = (IntPtr)sizeOff;

            if (!NativeMethods.VirtualProtect(entryAddr, (UIntPtr)8, NativeMethods.PAGE_READWRITE, out uint oldProtect))
                return false;

            Marshal.WriteInt32(entryAddr, 0);
            Marshal.WriteInt32(sizeAddr, 0);

            NativeMethods.VirtualProtect(entryAddr, (UIntPtr)8, oldProtect, out _);
            return true;
        }

        internal static bool PatchBytes(byte[] peBytes)
        {
            if (peBytes == null || peBytes.Length < 0x40)
                return false;

            ushort dosMagic = (ushort)(peBytes[0] | (peBytes[1] << 8));
            if (dosMagic != DOS_MAGIC)
                return false;

            int e_lfanew = BitConverter.ToInt32(peBytes, 0x3C);
            if (e_lfanew < 0 || e_lfanew + 4 > peBytes.Length)
                return false;

            uint ntSig = (uint)BitConverter.ToInt32(peBytes, e_lfanew);
            if (ntSig != NT_SIGNATURE)
                return false;

            int fileHeaderOff = e_lfanew + 4;
            int optionalHeaderOff = fileHeaderOff + 20;

            ushort magic = (ushort)(peBytes[optionalHeaderOff] | (peBytes[optionalHeaderOff + 1] << 8));

            int dataDirOff;
            if (magic == PE32_MAGIC)
                dataDirOff = optionalHeaderOff + 96;
            else if (magic == PE32_PLUS_MAGIC)
                dataDirOff = optionalHeaderOff + 112;
            else
                return false;

            int clrEntryOff = dataDirOff + CLR_DIRECTORY_INDEX * 8;

            if (clrEntryOff + 8 > peBytes.Length)
                return false;

            Array.Clear(peBytes, clrEntryOff, 8);
            return true;
        }
    }
}
