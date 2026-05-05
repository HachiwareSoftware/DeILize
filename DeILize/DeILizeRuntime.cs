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

            try
            {
                if (options.SuppressEtwEvents)
                    EtwEventFilter.Install();

                return AssemblyRuntimePatcher.PatchAll(options);
            }
            finally
            {
                if (options.SuppressEtwEvents)
                    EtwEventFilter.Uninstall();
            }
        }

        public static void InstallHooks(RuntimePatchOptions options = null)
        {
            if (options == null)
                options = new RuntimePatchOptions();

            try
            {
                if (options.SuppressEtwEvents)
                    EtwEventFilter.Install();

                HarmonyAssemblyLoadHooks.Install(options);

                if (options.PatchAlreadyLoadedAssemblies)
                {
                    AssemblyRuntimePatcher.PatchAll(options);
                }
            }
            finally
            {
                if (options.SuppressEtwEvents)
                    EtwEventFilter.Uninstall();
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
