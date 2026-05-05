using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using DeILize.Models;

namespace DeILize.Runtime
{
    internal static class AssemblyRuntimePatcher
    {
        internal static IReadOnlyList<RuntimePatchResult> PatchAll(RuntimePatchOptions options)
        {
            var results = new List<RuntimePatchResult>();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                results.Add(TryPatch(assembly, options));
            }

            return results;
        }

        internal static RuntimePatchResult TryPatch(Assembly assembly, RuntimePatchOptions options)
        {
            var result = new RuntimePatchResult
            {
                AssemblyName = assembly.GetName().Name,
                Location = assembly.Location
            };

            try
            {
                if (assembly.IsDynamic)
                {
                    result.Warning = "Dynamic assembly, cannot patch";
                    return result;
                }

                if (string.IsNullOrEmpty(assembly.Location))
                {
                    result.Warning = "No location (loaded from bytes)";
                    return result;
                }

                IntPtr moduleBase = Marshal.GetHINSTANCE(assembly.ManifestModule);
                if (moduleBase == IntPtr.Zero || moduleBase == (IntPtr)(-1))
                {
                    moduleBase = FindModuleBaseByPath(assembly.Location);
                }

                if (moduleBase == IntPtr.Zero || moduleBase == (IntPtr)(-1))
                {
                    result.Warning = "Could not resolve module base address";
                    return result;
                }

                bool clrPatched = PeHeaderPatcher.ZeroClrDirectory(moduleBase);
                result.ClrDirectoryZeroed = clrPatched;

                if (options.HideAssemblyDebugInfo)
                {
                    bool debugPatched = PeHeaderPatcher.ZeroDebugDirectory(moduleBase);
                    result.DebugDirectoryZeroed = debugPatched;
                }

                if (options.HideModuleFromPeb)
                {
                    bool pebUnlinked = LdrModuleHider.Unlink(moduleBase);
                    result.PebUnlinked = pebUnlinked;
                }

                if (!clrPatched)
                {
                    result.Warning = "CLR directory zeroing failed";
                }
            }
            catch (Exception ex)
            {
                result.Warning = $"Exception: {ex.Message}";
            }

            return result;
        }

        private static IntPtr FindModuleBaseByPath(string assemblyPath)
        {
            if (string.IsNullOrEmpty(assemblyPath))
                return IntPtr.Zero;

            try
            {
                using (var process = Process.GetCurrentProcess())
                {
                    foreach (ProcessModule module in process.Modules)
                    {
                        if (string.Equals(module.FileName, assemblyPath, StringComparison.OrdinalIgnoreCase))
                        {
                            return module.BaseAddress;
                        }
                    }
                }
            }
            catch
            {
            }

            return IntPtr.Zero;
        }
    }
}
