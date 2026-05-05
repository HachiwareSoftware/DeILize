using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeILize.FilePatch;
using DeILize.Models;

namespace DeILize.CLI
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return;
            }

            string command = args[0].ToLowerInvariant();

            switch (command)
            {
                case "patch-file":
                    RunPatchFile(args.Skip(1).ToArray());
                    break;
                case "rename-assembly":
                    RunRenameAssembly(args.Skip(1).ToArray());
                    break;
                case "inspect":
                    RunInspect(args.Skip(1).ToArray());
                    break;
                default:
                    Console.WriteLine($"Unknown command: {command}");
                    PrintUsage();
                    break;
            }
        }

        static void PrintUsage()
        {
            Console.WriteLine("DeILize - Remove IL-related information from .NET assemblies");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  DeILize.CLI patch-file <input> <output> [options]");
            Console.WriteLine("  DeILize.CLI rename-assembly <input> <output> --name <new-name>");
            Console.WriteLine("  DeILize.CLI inspect <input>");
            Console.WriteLine();
            Console.WriteLine("patch-file options:");
            Console.WriteLine("  --destructive       Zero CLR data directory (output may not load)");
            Console.WriteLine("  --strip-debug       Strip debug information (default: true)");
            Console.WriteLine("  --rename <old>=<new> Rename assembly reference");
            Console.WriteLine("  --no-strip-debug    Keep debug information");
            Console.WriteLine("  --no-attributes     Keep assembly attributes");
        }

        static void RunPatchFile(string[] args)
        {
            var options = new FilePatchOptions();
            string inputPath = null;
            string outputPath = null;
            var positionalArgs = new List<string>();

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLowerInvariant())
                {
                    case "--destructive":
                        options.Destructive = true;
                        break;
                    case "--strip-debug":
                        options.StripDebugInfo = true;
                        break;
                    case "--no-strip-debug":
                        options.StripDebugInfo = false;
                        break;
                    case "--no-attributes":
                        options.RemoveAssemblyAttributes = false;
                        break;
                    default:
                        if (args[i].StartsWith("--rename="))
                        {
                            var renamePart = args[i].Substring("--rename=".Length);
                            var parts = renamePart.Split('=');
                            if (parts.Length == 2)
                            {
                                options.Rename = new AssemblyRenameConfig
                                {
                                    NewAssemblyName = parts[1]
                                };
                            }
                        }
                        else
                        {
                            positionalArgs.Add(args[i]);
                        }
                        break;
                }
            }

            if (positionalArgs.Count < 2)
            {
                Console.WriteLine("Error: input and output paths required");
                return;
            }

            inputPath = positionalArgs[0];
            outputPath = positionalArgs[1];

            if (!File.Exists(inputPath))
            {
                Console.WriteLine($"Error: input file not found: {inputPath}");
                return;
            }

            try
            {
                bool success = DeILizeRuntime.PatchAssemblyFile(inputPath, outputPath, options);
                Console.WriteLine(success ? "Patched successfully" : "Patch failed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        static void RunRenameAssembly(string[] args)
        {
            if (args.Length < 3 || args[0] == "--help")
            {
                Console.WriteLine("Usage: DeILize.CLI rename-assembly <input> <output> --name <new-name>");
                return;
            }

            string inputPath = args[0];
            string outputPath = args[1];
            string newName = null;

            for (int i = 2; i < args.Length; i++)
            {
                if (args[i] == "--name" && i + 1 < args.Length)
                {
                    newName = args[i + 1];
                    i++;
                }
            }

            if (string.IsNullOrEmpty(newName))
            {
                Console.WriteLine("Error: --name is required");
                return;
            }

            if (!File.Exists(inputPath))
            {
                Console.WriteLine($"Error: input file not found: {inputPath}");
                return;
            }

            try
            {
                AssemblyIdentityRewriter.RewriteFile(inputPath, outputPath,
                    new AssemblyRenameConfig { NewAssemblyName = newName });
                Console.WriteLine($"Renamed assembly to '{newName}'");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        static void RunInspect(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: DeILize.CLI inspect <input>");
                return;
            }

            string inputPath = args[0];

            if (!File.Exists(inputPath))
            {
                Console.WriteLine($"Error: file not found: {inputPath}");
                return;
            }

            byte[] peBytes = File.ReadAllBytes(inputPath);

            if (peBytes.Length < 2 || peBytes[0] != 0x4D || peBytes[1] != 0x5A)
            {
                Console.WriteLine("Not a valid PE image");
                return;
            }

            int e_lfanew = System.BitConverter.ToInt32(peBytes, 0x3C);
            if (e_lfanew < 0 || e_lfanew + 4 > peBytes.Length)
            {
                Console.WriteLine("Invalid PE header offset");
                return;
            }

            uint ntSig = (uint)System.BitConverter.ToInt32(peBytes, e_lfanew);
            if (ntSig != 0x00004550)
            {
                Console.WriteLine("No NT signature");
                return;
            }

            int fileHeaderOff = e_lfanew + 4;
            int optionalHeaderOff = fileHeaderOff + 20;

            ushort magic = (ushort)(peBytes[optionalHeaderOff] | (peBytes[optionalHeaderOff + 1] << 8));
            string peType = magic == 0x10B ? "PE32" : magic == 0x20B ? "PE32+" : "Unknown";
            Console.WriteLine($"PE Type: {peType}");

            int dataDirOff = magic == 0x10B ? optionalHeaderOff + 96 : optionalHeaderOff + 112;

            int clrEntryOff = dataDirOff + 14 * 8;
            int clrRva = System.BitConverter.ToInt32(peBytes, clrEntryOff);
            int clrSize = System.BitConverter.ToInt32(peBytes, clrEntryOff + 4);
            Console.WriteLine($"CLR Data Directory: RVA=0x{clrRva:X8}, Size=0x{clrSize:X8}");
            Console.WriteLine($"CLR Directory Present: {(clrRva != 0 && clrSize != 0)}");

            int debugEntryOff = dataDirOff + 6 * 8;
            int debugRva = System.BitConverter.ToInt32(peBytes, debugEntryOff);
            int debugSize = System.BitConverter.ToInt32(peBytes, debugEntryOff + 4);
            Console.WriteLine($"Debug Directory: RVA=0x{debugRva:X8}, Size=0x{debugSize:X8}");

            Console.WriteLine($"File Size: {peBytes.Length} bytes");
        }
    }
}
