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
            Logger.Section("Runtime Patch All Assemblies");
            Logger.Debug("Enumerating loaded assemblies...");
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            Logger.Info($"Found {assemblies.Length} loaded assemblies");

            var results = new List<RuntimePatchResult>();
            foreach (var assembly in assemblies)
                results.Add(TryPatch(assembly, options));

            int ok = results.Count(r => r.ClrDirectoryZeroed);
            Logger.Info($"{ok}/{results.Count} assemblies patched");
            return results;
        }

        internal static RuntimePatchResult TryPatch(Assembly assembly, RuntimePatchOptions options)
        {
            var result = new RuntimePatchResult
            {
                AssemblyName = assembly.GetName().Name,
                Location = assembly.Location
            };

            Logger.Section($"Patch Assembly: {result.AssemblyName}");

            try
            {
                if (assembly.IsDynamic)
                { Logger.Warn("Dynamic assembly, skipping"); result.Warning = "Dynamic assembly"; return result; }

                if (string.IsNullOrEmpty(assembly.Location))
                { Logger.Warn("No location (loaded from bytes), skipping"); result.Warning = "No location"; return result; }

                Logger.Debug($"Location: {assembly.Location}");

                IntPtr moduleBase = Marshal.GetHINSTANCE(assembly.ManifestModule);
                Logger.Debug($"GetHINSTANCE: 0x{moduleBase:X}");

                if (moduleBase == IntPtr.Zero || moduleBase == (IntPtr)(-1))
                {
                    Logger.Debug("GetHINSTANCE failed, trying ProcessModule lookup");
                    moduleBase = FindModuleBaseByPath(assembly.Location);
                    Logger.Debug($"ProcessModule lookup: 0x{moduleBase:X}");
                }

                if (moduleBase == IntPtr.Zero || moduleBase == (IntPtr)(-1))
                { Logger.Error("Could not resolve module base"); result.Warning = "No module base"; return result; }

                Logger.Info($"Module base: 0x{moduleBase:X}");

                bool clrPatched = PeHeaderPatcher.ZeroClrDirectory(moduleBase);
                result.ClrDirectoryZeroed = clrPatched;

                if (options.HideAssemblyDebugInfo)
                    result.DebugDirectoryZeroed = PeHeaderPatcher.ZeroDebugDirectory(moduleBase);

                if (options.HideModuleFromPeb)
                    result.PebUnlinked = LdrModuleHider.Unlink(moduleBase);

                if (!clrPatched)
                { result.Warning = "CLR zeroing failed"; Logger.Error("CLR directory zeroing failed"); }
                else
                    Logger.Info("Assembly patched");
            }
            catch (Exception ex)
            {
                Logger.Error($"{ex.GetType().Name}: {ex.Message}");
                result.Warning = $"Exception: {ex.Message}";
            }

            return result;
        }

        private static IntPtr FindModuleBaseByPath(string assemblyPath)
        {
            try
            {
                using (var process = Process.GetCurrentProcess())
                    foreach (ProcessModule m in process.Modules)
                        if (string.Equals(m.FileName, assemblyPath, StringComparison.OrdinalIgnoreCase))
                        { Logger.Debug($"Found module: {m.ModuleName} @ 0x{m.BaseAddress:X}"); return m.BaseAddress; }
            }
            catch (Exception ex) { Logger.Warn($"Process module enum failed: {ex.Message}"); }
            return IntPtr.Zero;
        }
    }
}
