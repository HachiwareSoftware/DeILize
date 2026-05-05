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
            if (module.Assembly == null)
                return;

            var name = module.Assembly.Name;
            name.Name = config.NewAssemblyName;
            module.Name = config.NewAssemblyName + ".dll";
        }

        internal static void RewriteReferences(ModuleDefinition module, string oldName, string newName)
        {
            var assembly = module.Assembly;
            if (assembly == null)
                return;

            foreach (var reference in module.AssemblyReferences)
            {
                if (string.Equals(reference.Name, oldName, StringComparison.OrdinalIgnoreCase))
                {
                    reference.Name = newName;
                }
            }
        }

        public static void RewriteFile(string inputPath, string outputPath, AssemblyRenameConfig config)
        {
            using (var module = ModuleDefinition.ReadModule(inputPath))
            {
                Rewrite(module, config);
                RewriteReferences(module, module.Assembly.Name.Name, config.NewAssemblyName);
                module.Write(outputPath);
            }
        }

        internal static void RewriteFileWithReferences(
            string inputPath,
            string outputPath,
            AssemblyRenameConfig config,
            string oldReferenceName)
        {
            using (var module = ModuleDefinition.ReadModule(inputPath))
            {
                Rewrite(module, config);
                RewriteReferences(module, oldReferenceName, config.NewAssemblyName);
                module.Write(outputPath);
            }
        }
    }
}
