using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeILize.Models;
using DeILize.Runtime;
using Mono.Cecil;

namespace DeILize.FilePatch
{
    internal static class FileAssemblyPatcher
    {
        internal static bool PatchFile(string inputPath, string outputPath, FilePatchOptions options)
        {
            Logger.Section("File Assembly Patch");
            Logger.Debug($"Input: {inputPath}");
            Logger.Debug($"Output: {outputPath}");
            Logger.Debug($"Destructive: {options.Destructive}");
            Logger.Debug($"Strip debug: {options.StripDebugInfo}");
            Logger.Debug($"Remove attrs: {options.RemoveAssemblyAttributes}");
            Logger.Debug($"Patch embedded: {options.PatchEmbeddedResources}");
            Logger.Debug($"Rename: {options.Rename?.NewAssemblyName ?? "(none)"}");

            if (!File.Exists(inputPath))
            { Logger.Error($"File not found: {inputPath}"); throw new FileNotFoundException(null, inputPath); }

            byte[] peBytes = File.ReadAllBytes(inputPath);
            Logger.Info($"Read {peBytes.Length} bytes");

            using (var module = ModuleDefinition.ReadModule(new MemoryStream(peBytes), new ReaderParameters { ReadWrite = false }))
            {
                Logger.Info($"Assembly: {module.Assembly?.Name?.Name ?? "(null)"}, Module: {module.Name}");

                if (options.RemoveAssemblyAttributes)
                {
                    Logger.Section("Remove Assembly Attributes");
                    RemoveAssemblyAttributes(module);
                }

                if (options.StripDebugInfo)
                    Logger.Warn("Debug stripping not implemented");

                if (options.Rename != null)
                {
                    Logger.Section("Rename Assembly");
                    Logger.Debug($"New name: {options.Rename.NewAssemblyName}");
                    AssemblyIdentityRewriter.Rewrite(module, options.Rename);
                    AssemblyIdentityRewriter.RewriteReferences(module, module.Assembly?.Name?.Name, options.Rename.NewAssemblyName);
                }

                if (options.PatchEmbeddedResources)
                {
                    Logger.Section("Patch Embedded Resources");
                    EmbeddedResourcePatcher.Process(module, options);
                }

                using (var ms = new MemoryStream())
                {
                    module.Write(ms);
                    peBytes = ms.ToArray();
                    Logger.Debug($"Cecil output: {peBytes.Length} bytes");
                }
            }

            if (options.Destructive)
            {
                Logger.Section("Destructive CLR Zeroing");
                bool ok = PeHeaderPatcher.PatchBytes(peBytes);
                Logger.Info(ok ? "CLR directory zeroed" : "CLR zeroing failed");
            }

            File.WriteAllBytes(outputPath, peBytes);
            Logger.Info($"Written to {outputPath} ({peBytes.Length} bytes)");
            return true;
        }

        private static void RemoveAssemblyAttributes(ModuleDefinition module)
        {
            var assembly = module.Assembly;
            if (assembly == null) { Logger.Warn("No assembly definition"); return; }

            var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "System.Reflection.AssemblyTitleAttribute",
                "System.Reflection.AssemblyDescriptionAttribute",
                "System.Reflection.AssemblyCompanyAttribute",
                "System.Reflection.AssemblyProductAttribute",
                "System.Reflection.AssemblyCopyrightAttribute",
                "System.Reflection.AssemblyTrademarkAttribute",
                "System.Diagnostics.DebuggableAttribute"
            };

            var toRemove = assembly.CustomAttributes.Where(a => targets.Contains(a.AttributeType.FullName)).ToList();
            Logger.Debug($"Found {toRemove.Count} attributes to remove");

            foreach (var attr in toRemove)
            {
                assembly.CustomAttributes.Remove(attr);
                Logger.Debug($"Removed: {attr.AttributeType.FullName}");
            }
            Logger.Info($"{toRemove.Count} assembly attributes removed");
        }
    }
}
