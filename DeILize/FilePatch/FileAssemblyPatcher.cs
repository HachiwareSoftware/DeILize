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
            if (!File.Exists(inputPath))
                throw new FileNotFoundException("Input assembly not found", inputPath);

            byte[] peBytes = File.ReadAllBytes(inputPath);

            using (var module = ModuleDefinition.ReadModule(
                new MemoryStream(peBytes),
                new ReaderParameters { ReadWrite = false }))
            {
                if (options.RemoveAssemblyAttributes)
                {
                    RemoveAssemblyAttributes(module);
                }

                if (options.StripDebugInfo)
                {
                    StripDebugInfo(module);
                }

                if (options.Rename != null)
                {
                    AssemblyIdentityRewriter.Rewrite(module, options.Rename);
                    AssemblyIdentityRewriter.RewriteReferences(
                        module,
                        module.Assembly?.Name?.Name,
                        options.Rename.NewAssemblyName);
                }

                if (options.PatchEmbeddedResources)
                {
                    EmbeddedResourcePatcher.Process(module, options);
                }

                using (var ms = new MemoryStream())
                {
                    module.Write(ms);
                    peBytes = ms.ToArray();
                }
            }

            if (options.Destructive)
            {
                PeHeaderPatcher.PatchBytes(peBytes);
            }

            File.WriteAllBytes(outputPath, peBytes);
            return true;
        }

        private static void RemoveAssemblyAttributes(ModuleDefinition module)
        {
            var assembly = module.Assembly;
            if (assembly == null)
                return;

            var namesToRemove = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "System.Reflection.AssemblyTitleAttribute",
                "System.Reflection.AssemblyDescriptionAttribute",
                "System.Reflection.AssemblyCompanyAttribute",
                "System.Reflection.AssemblyProductAttribute",
                "System.Reflection.AssemblyCopyrightAttribute",
                "System.Reflection.AssemblyTrademarkAttribute",
                "System.Diagnostics.DebuggableAttribute"
            };

            var customAttributes = assembly.CustomAttributes;
            var toRemove = customAttributes
                .Where(a => namesToRemove.Contains(a.AttributeType.FullName))
                .ToList();

            foreach (var attr in toRemove)
            {
                customAttributes.Remove(attr);
            }
        }

        private static void StripDebugInfo(ModuleDefinition module)
        {
        }
    }
}
