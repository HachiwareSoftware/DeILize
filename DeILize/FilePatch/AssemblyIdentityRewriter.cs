using System;
using System.Linq;
using DeILize.Models;
using Mono.Cecil;

namespace DeILize.FilePatch
{
    public static class AssemblyIdentityRewriter
    {
        internal static void Rewrite(ModuleDefinition module, AssemblyRenameConfig config)
        {
            if (module.Assembly == null) { Logger.Warn("No assembly definition"); return; }
            string oldName = module.Assembly.Name.Name;
            Logger.Debug($"Assembly: {oldName} -> {config.NewAssemblyName}");
            Logger.Debug($"Module: {module.Name} -> {config.NewAssemblyName}.dll");
            module.Assembly.Name.Name = config.NewAssemblyName;
            module.Name = config.NewAssemblyName + ".dll";
            Logger.Info($"Assembly renamed: '{oldName}' -> '{config.NewAssemblyName}'");
        }

        internal static void RewriteReferences(ModuleDefinition module, string oldName, string newName)
        {
            if (module.Assembly == null) return;
            int updated = 0;
            foreach (var r in module.AssemblyReferences)
                if (string.Equals(r.Name, oldName, StringComparison.OrdinalIgnoreCase))
                { r.Name = newName; updated++; Logger.Debug($"Reference updated: '{oldName}' -> '{newName}'"); }
            if (updated > 0) Logger.Info($"{updated} assembly references updated");
        }

        public static void RewriteFile(string inputPath, string outputPath, AssemblyRenameConfig config)
        {
            Logger.Section("Assembly Rename");
            Logger.Debug($"Input: {inputPath}");
            Logger.Debug($"Output: {outputPath}");
            Logger.Debug($"New name: {config.NewAssemblyName}");

            using (var module = ModuleDefinition.ReadModule(inputPath))
            {
                Logger.Info($"Original: {module.Assembly?.Name?.Name ?? "(null)"}");
                Rewrite(module, config);
                RewriteReferences(module, module.Assembly.Name.Name, config.NewAssemblyName);
                module.Write(outputPath);
                Logger.Info($"Written to {outputPath}");
            }
        }

        internal static void RewriteFileWithReferences(string inputPath, string outputPath, AssemblyRenameConfig config, string oldReferenceName)
        {
            Logger.Section("Assembly Rename (with external refs)");
            Logger.Debug($"Input: {inputPath}, Output: {outputPath}, New: {config.NewAssemblyName}, Old ref: {oldReferenceName}");
            using (var module = ModuleDefinition.ReadModule(inputPath))
            {
                Rewrite(module, config);
                RewriteReferences(module, oldReferenceName, config.NewAssemblyName);
                module.Write(outputPath);
                Logger.Info($"Written to {outputPath}");
            }
        }
    }
}
