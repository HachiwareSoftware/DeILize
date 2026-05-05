using System.Collections.Generic;
using System.IO;
using DeILize.FilePatch;
using DeILize.Models;
using DeILize.Runtime;

namespace DeILize
{
    public static class DeILizeRuntime
    {
        public static IReadOnlyList<RuntimePatchResult> HideCurrentAppDomain(RuntimePatchOptions options = null)
        {
            if (options == null)
                options = new RuntimePatchOptions();

            return AssemblyRuntimePatcher.PatchAll(options);
        }

        public static void InstallHooks(RuntimePatchOptions options = null)
        {
            if (options == null)
                options = new RuntimePatchOptions();

            HarmonyAssemblyLoadHooks.Install(options);

            if (options.PatchAlreadyLoadedAssemblies)
            {
                AssemblyRuntimePatcher.PatchAll(options);
            }
        }

        public static bool PatchAssemblyFile(string inputPath, string outputPath, FilePatchOptions options = null)
        {
            if (options == null)
                options = new FilePatchOptions();

            return FileAssemblyPatcher.PatchFile(inputPath, outputPath, options);
        }
    }
}
