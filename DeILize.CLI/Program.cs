using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using DeILize.FilePatch;
using DeILize.Models;

namespace DeILize.CLI
{
    internal class Program
    {
        static int Main(string[] args)
        {
            var rootCommand = new RootCommand("DeILize - Remove IL-related information from .NET assemblies");

            var patchCommand = new Command("patch", "Patch a .NET assembly file");
            var renameCommand = new Command("rename-assembly", "Rename an assembly identity");
            var inspectCommand = new Command("inspect", "Inspect a .NET assembly");

            SetupPatchCommand(patchCommand);
            SetupRenameCommand(renameCommand);
            SetupInspectCommand(inspectCommand);

            rootCommand.AddCommand(patchCommand);
            rootCommand.AddCommand(renameCommand);
            rootCommand.AddCommand(inspectCommand);

            return rootCommand.Invoke(args);
        }

        static void SetupPatchCommand(Command cmd)
        {
            var inputArg = new Argument<string>("input", "Path to input assembly");
            var outputArg = new Argument<string>("output", () => null, "Path to output assembly (default: <input>.patched.dll)");
            var verboseOpt = new Option<bool>("--verbose", "Show detailed debug output");
            var destructiveOpt = new Option<bool>("--destructive", "Zero CLR data directory (output may not load)");
            var stripDebugOpt = new Option<bool>("--strip-debug", () => true, "Strip debug information");
            var noStripDebugOpt = new Option<bool>("--no-strip-debug", "Keep debug information");
            var noAttributesOpt = new Option<bool>("--no-attributes", "Keep assembly attributes");
            var renameOpt = new Option<string>("--rename", "Rename assembly reference (format: old=new)");

            cmd.AddArgument(inputArg);
            cmd.AddArgument(outputArg);
            cmd.AddOption(verboseOpt);
            cmd.AddOption(destructiveOpt);
            cmd.AddOption(stripDebugOpt);
            cmd.AddOption(noStripDebugOpt);
            cmd.AddOption(noAttributesOpt);
            cmd.AddOption(renameOpt);

            cmd.SetHandler((InvocationContext ctx) =>
            {
                var options = new FilePatchOptions();
                string inputPath = ctx.ParseResult.GetValueForArgument(inputArg);
                string outputPath = ctx.ParseResult.GetValueForArgument(outputArg);
                bool verbose = ctx.ParseResult.GetValueForOption(verboseOpt);
                bool destructive = ctx.ParseResult.GetValueForOption(destructiveOpt);
                bool stripDebug = ctx.ParseResult.GetValueForOption(stripDebugOpt);
                bool noStripDebug = ctx.ParseResult.GetValueForOption(noStripDebugOpt);
                bool noAttributes = ctx.ParseResult.GetValueForOption(noAttributesOpt);
                string rename = ctx.ParseResult.GetValueForOption(renameOpt);

                if (verbose)
                {
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
                }

                options.Destructive = destructive;
                options.StripDebugInfo = noStripDebug ? false : stripDebug;
                options.RemoveAssemblyAttributes = !noAttributes;

                if (!string.IsNullOrEmpty(rename))
                {
                    var parts = rename.Split('=');
                    if (parts.Length == 2)
                    {
                        options.Rename = new AssemblyRenameConfig
                        {
                            NewAssemblyName = parts[1]
                        };
                    }
                }

                if (string.IsNullOrEmpty(outputPath))
                    outputPath = Path.ChangeExtension(inputPath, null) + ".patched.dll";

                if (!File.Exists(inputPath))
                {
                    Console.WriteLine($"Error: input file not found: {inputPath}");
                    ctx.ExitCode = 1;
                    return;
                }

                Console.WriteLine($"[INFO] Input:  {inputPath}");
                Console.WriteLine($"[INFO] Output: {outputPath}");
                Console.WriteLine($"[INFO] Destructive: {options.Destructive}");
                Console.WriteLine($"[INFO] Strip debug: {options.StripDebugInfo}");
                Console.WriteLine($"[INFO] Remove attrs: {options.RemoveAssemblyAttributes}");
                Console.WriteLine($"[INFO] Patch embedded: {options.PatchEmbeddedResources}");

                try
                {
                    bool success = DeILizeRuntime.PatchAssemblyFile(inputPath, outputPath, options);
                    Console.WriteLine(success ? "Patched successfully" : "Patch failed");
                    ctx.ExitCode = success ? 0 : 1;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    ctx.ExitCode = 1;
                }
            });
        }

        static void SetupRenameCommand(Command cmd)
        {
            var inputArg = new Argument<string>("input", "Path to input assembly");
            var outputArg = new Argument<string>("output", "Path to output assembly");
            var nameOpt = new Option<string>("--name", "New assembly name") { IsRequired = true };

            cmd.AddArgument(inputArg);
            cmd.AddArgument(outputArg);
            cmd.AddOption(nameOpt);

            cmd.SetHandler((InvocationContext ctx) =>
            {
                string inputPath = ctx.ParseResult.GetValueForArgument(inputArg);
                string outputPath = ctx.ParseResult.GetValueForArgument(outputArg);
                string newName = ctx.ParseResult.GetValueForOption(nameOpt);

                if (!File.Exists(inputPath))
                {
                    Console.WriteLine($"Error: input file not found: {inputPath}");
                    ctx.ExitCode = 1;
                    return;
                }

                Console.WriteLine($"[INFO] Renaming assembly: {inputPath} -> {outputPath}, new name: {newName}");

                try
                {
                    AssemblyIdentityRewriter.RewriteFile(inputPath, outputPath,
                        new AssemblyRenameConfig { NewAssemblyName = newName });
                    Console.WriteLine($"Renamed assembly to '{newName}'");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    ctx.ExitCode = 1;
                }
            });
        }

        static void SetupInspectCommand(Command cmd)
        {
            var inputArg = new Argument<string>("input", "Path to assembly to inspect");

            cmd.AddArgument(inputArg);

            cmd.SetHandler((InvocationContext ctx) =>
            {
                string inputPath = ctx.ParseResult.GetValueForArgument(inputArg);

                if (!File.Exists(inputPath))
                {
                    Console.WriteLine($"Error: file not found: {inputPath}");
                    ctx.ExitCode = 1;
                    return;
                }

                Console.WriteLine($"[INFO] Inspecting: {inputPath}");

                byte[] peBytes = File.ReadAllBytes(inputPath);

                if (peBytes.Length < 2 || peBytes[0] != 0x4D || peBytes[1] != 0x5A)
                {
                    Console.WriteLine("Not a valid PE image");
                    ctx.ExitCode = 1;
                    return;
                }

                int e_lfanew = BitConverter.ToInt32(peBytes, 0x3C);
                if (e_lfanew < 0 || e_lfanew + 4 > peBytes.Length)
                {
                    Console.WriteLine("Invalid PE header offset");
                    ctx.ExitCode = 1;
                    return;
                }

                uint ntSig = (uint)BitConverter.ToInt32(peBytes, e_lfanew);
                if (ntSig != 0x00004550)
                {
                    Console.WriteLine("No NT signature");
                    ctx.ExitCode = 1;
                    return;
                }

                int fileHeaderOff = e_lfanew + 4;
                int optionalHeaderOff = fileHeaderOff + 20;

                ushort magic = (ushort)(peBytes[optionalHeaderOff] | (peBytes[optionalHeaderOff + 1] << 8));
                string peType = magic == 0x10B ? "PE32" : magic == 0x20B ? "PE32+" : "Unknown";
                Console.WriteLine($"PE Type: {peType}");

                int dataDirOff = magic == 0x10B ? optionalHeaderOff + 96 : optionalHeaderOff + 112;

                int clrEntryOff = dataDirOff + 14 * 8;
                int clrRva = BitConverter.ToInt32(peBytes, clrEntryOff);
                int clrSize = BitConverter.ToInt32(peBytes, clrEntryOff + 4);
                Console.WriteLine($"CLR Data Directory: RVA=0x{clrRva:X8}, Size=0x{clrSize:X8}");
                Console.WriteLine($"CLR Directory Present: {(clrRva != 0 && clrSize != 0)}");

                int debugEntryOff = dataDirOff + 6 * 8;
                int debugRva = BitConverter.ToInt32(peBytes, debugEntryOff);
                int debugSize = BitConverter.ToInt32(peBytes, debugEntryOff + 4);
                Console.WriteLine($"Debug Directory: RVA=0x{debugRva:X8}, Size=0x{debugSize:X8}");

                Console.WriteLine($"File Size: {peBytes.Length} bytes");
            });
        }
    }
}
