using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using DeILize.Models;
using DeILize.Runtime;

namespace DeILize.TestHost
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== DeILize Test Host ===");
            Console.WriteLine($"PID: {Process.GetCurrentProcess().Id}");
            Console.WriteLine();

            var sampleAsm = typeof(DeILize.SampleDll.SampleClass).Assembly;
            Console.WriteLine("=== BEFORE DeILize ===");
            ShowAssemblyInfo(sampleAsm);
            Console.WriteLine();

            Logger.LogEvent += (level, msg) =>
            {
                switch (level)
                {
                    case "section": Console.WriteLine(); Console.WriteLine(msg); break;
                    case "info":    Console.WriteLine($"[+] {msg}"); break;
                    case "warn":    Console.WriteLine($"[!] {msg}"); break;
                    case "error":   Console.WriteLine($"[-] {msg}"); break;
                    case "debug":   Console.WriteLine($"[>] {msg}"); break;
                }
            };

            string samplePath = sampleAsm.Location;
            string patchedPath = Path.ChangeExtension(samplePath, null) + ".patched.dll";

            Console.WriteLine("=== File patch ===");
            var fileOptions = new FilePatchOptions
            {
                Destructive = true,
                StripDebugInfo = true,
                RemoveAssemblyAttributes = true,
                PatchEmbeddedResources = true,
            };
            bool fileResult = DeILizeRuntime.PatchAssemblyFile(samplePath, patchedPath, fileOptions);
            Console.WriteLine($"File patch: {(fileResult ? "SUCCESS" : "FAILED")}");

            Console.WriteLine();
            Console.WriteLine("=== Verifying patched file ===");
            InspectPeFile(patchedPath);

            Console.WriteLine();
            Console.WriteLine("=== Runtime patch ===");
            IntPtr moduleBase = Marshal.GetHINSTANCE(sampleAsm.ManifestModule);
            Console.WriteLine($"GetHINSTANCE: 0x{moduleBase:X}");

            Console.WriteLine();
            Console.WriteLine("=== Manual PE parse at module base ===");
            DumpPeHeader(moduleBase);

            Console.WriteLine();
            Console.WriteLine("=== Runtime patch via HideCurrentAppDomain ===");
            var results = DeILizeRuntime.HideCurrentAppDomain(new RuntimePatchOptions
            {
                PatchAlreadyLoadedAssemblies = true,
                InstallHarmonyHooks = false,
                HideAssemblyDebugInfo = true,
                HideModuleFromPeb = false,
                IncludeGacAssemblies = true,
                IncludeFrameworkAssemblies = true,
                SuppressEtwEvents = true,
            });
            Console.WriteLine($"Patched {results.Count} assemblies");
            foreach (var r in results)
                Console.WriteLine($"  {r.AssemblyName}: CLR={r.ClrDirectoryZeroed}, Debug={r.DebugDirectoryZeroed}");

            Console.WriteLine();
            Console.WriteLine("=== After patch PE dump ===");
            DumpPeHeader(moduleBase);

            Console.WriteLine();
            Console.WriteLine("Press any key to exit (inspect with Process Hacker)...");
            Console.ReadKey();
        }

        static void DumpPeHeader(IntPtr baseAddr)
        {
            if (baseAddr == IntPtr.Zero) { Console.WriteLine("Base address is null"); return; }

            ushort dosMagic = (ushort)Marshal.ReadInt16(baseAddr);
            Console.WriteLine($"DOS magic: 0x{dosMagic:X4}");

            int e_lfanew = Marshal.ReadInt32(baseAddr, 0x3C);
            Console.WriteLine($"e_lfanew: 0x{e_lfanew:X}");

            IntPtr ntHeaders = IntPtr.Add(baseAddr, e_lfanew);
            uint ntSig = (uint)Marshal.ReadInt32(ntHeaders);
            Console.WriteLine($"NT signature: 0x{ntSig:X8}");

            IntPtr fileHeader = IntPtr.Add(ntHeaders, 4);
            ushort machine = (ushort)Marshal.ReadInt16(fileHeader);
            Console.WriteLine($"Machine: 0x{machine:X4}");

            IntPtr optionalHeader = IntPtr.Add(fileHeader, 20);
            ushort magic = (ushort)Marshal.ReadInt16(optionalHeader);
            Console.WriteLine($"PE magic: 0x{magic:X4} ({(magic == 0x10B ? "PE32" : magic == 0x20B ? "PE32+" : "unknown")})");

            int dataDirFileOffset = magic == 0x10B ? e_lfanew + 24 + 96 : e_lfanew + 24 + 112;
            Console.WriteLine($"Data directory file offset: 0x{dataDirFileOffset:X}");

            int clrRvaOff = dataDirFileOffset + 14 * 8;
            int clrSizeOff = clrRvaOff + 4;
            int clrRva = Marshal.ReadInt32(baseAddr, clrRvaOff);
            int clrSize = Marshal.ReadInt32(baseAddr, clrSizeOff);
            Console.WriteLine($"CLR directory at file offset 0x{clrRvaOff:X}: RVA=0x{clrRva:X8}, Size=0x{clrSize:X8}");

            int dbgRvaOff = dataDirFileOffset + 6 * 8;
            int dbgSizeOff = dbgRvaOff + 4;
            int dbgRva = Marshal.ReadInt32(baseAddr, dbgRvaOff);
            int dbgSize = Marshal.ReadInt32(baseAddr, dbgSizeOff);
            Console.WriteLine($"Debug directory at file offset 0x{dbgRvaOff:X}: RVA=0x{dbgRva:X8}, Size=0x{dbgSize:X8}");

            IntPtr clrAddr = IntPtr.Add(baseAddr, clrRvaOff);
            Console.WriteLine($"CLR entry absolute address: 0x{clrAddr:X}");
            Console.WriteLine($"  Read at addr: RVA=0x{Marshal.ReadInt32(clrAddr):X8}, Size=0x{Marshal.ReadInt32(IntPtr.Add(clrAddr, 4)):X8}");
        }

        static void InspectPeFile(string path)
        {
            byte[] peBytes = File.ReadAllBytes(path);
            int e_lfanew = BitConverter.ToInt32(peBytes, 0x3C);
            ushort magic = (ushort)(peBytes[e_lfanew + 24] | (peBytes[e_lfanew + 25] << 8));
            int dataDirOff = magic == 0x10B ? e_lfanew + 24 + 96 : e_lfanew + 24 + 112;
            int clrRva = BitConverter.ToInt32(peBytes, dataDirOff + 14 * 8);
            int clrSize = BitConverter.ToInt32(peBytes, dataDirOff + 14 * 8 + 4);
            Console.WriteLine($"File CLR: RVA=0x{clrRva:X8}, Size=0x{clrSize:X8}");
        }

        static void ShowAssemblyInfo(Assembly asm)
        {
            var name = asm.GetName();
            Console.WriteLine($"FullName: {name.FullName}");
            Console.WriteLine($"Location: {asm.Location}");
        }
    }
}
