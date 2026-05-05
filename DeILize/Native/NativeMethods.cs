using System;
using System.Runtime.InteropServices;

namespace DeILize.Native
{
    internal static class NativeMethods
    {
        internal const uint PAGE_READWRITE = 0x04;
        internal const uint PAGE_EXECUTE_READWRITE = 0x40;

        internal const int ProcessBasicInformation = 0;

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool VirtualProtect(
            IntPtr lpAddress,
            UIntPtr dwSize,
            uint flNewProtect,
            out uint lpflOldProtect);

        [DllImport("ntdll.dll", SetLastError = true)]
        internal static extern int NtQueryInformationProcess(
            IntPtr processHandle,
            int processInformationClass,
            out PROCESS_BASIC_INFORMATION processInformation,
            int processInformationLength,
            out int returnLength);

        [StructLayout(LayoutKind.Sequential)]
        internal struct PROCESS_BASIC_INFORMATION
        {
            public IntPtr Reserved1;
            public IntPtr PebBaseAddress;
            public IntPtr Reserved2_0;
            public IntPtr Reserved2_1;
            public IntPtr UniqueProcessId;
            public IntPtr InheritedFromUniqueProcessId;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PEB_LDR_DATA
        {
            public byte Reserved1_0;
            public byte Reserved1_1;
            public byte Reserved1_2;
            public byte Reserved1_3;
            public IntPtr Reserved2_0;
            public IntPtr Reserved2_1;
            public IntPtr InMemoryOrderModuleList_Flink;
            public IntPtr InMemoryOrderModuleList_Blink;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct LDR_DATA_TABLE_ENTRY
        {
            public IntPtr Reserved1_0;
            public IntPtr Reserved1_1;
            public IntPtr Reserved1_2;
            public IntPtr Reserved2_0;
            public IntPtr Reserved2_1;
            public IntPtr Reserved2_2;
            public IntPtr DllBase;
        }
    }
}
