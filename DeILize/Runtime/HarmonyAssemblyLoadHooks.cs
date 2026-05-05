using System;
using System.Reflection;
using DeILize.Models;
using HarmonyLib;

namespace DeILize.Runtime
{
    internal static class HarmonyAssemblyLoadHooks
    {
        private const string HarmonyId = "deilize.runtime";
        private static RuntimePatchOptions _options;
        private static bool _installed;

        internal static void Install(RuntimePatchOptions options)
        {
            Logger.Section("Install Harmony Assembly Load Hooks");
            if (_installed) { Logger.Warn("Already installed, skipping"); return; }
            _installed = true;
            _options = options;

            var harmony = new Harmony(HarmonyId);
            Logger.Debug($"Harmony instance created (ID: {HarmonyId})");

            TryPatchPrefix(harmony, typeof(Assembly), "Load", new[] { typeof(byte[]) }, nameof(AssemblyLoadByteArrayPrefix));
            TryPatchPrefix(harmony, typeof(Assembly), "Load", new[] { typeof(byte[]), typeof(byte[]) }, nameof(AssemblyLoadByteArrayPrefix));
            TryPatchPostfix(harmony, typeof(Assembly), "LoadFile", new[] { typeof(string) }, nameof(AssemblyLoadStringPostfix));
            TryPatchPostfix(harmony, typeof(Assembly), "LoadFrom", new[] { typeof(string) }, nameof(AssemblyLoadStringPostfix));
            TryPatchPrefix(harmony, typeof(AppDomain), "Load", new[] { typeof(byte[]) }, nameof(AssemblyLoadByteArrayPrefix));

            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
            Logger.Info("Assembly load hooks installed");
        }

        private static void TryPatchPrefix(Harmony harmony, Type type, string method, Type[] paramTypes, string prefix)
        {
            try
            {
                var mi = type.GetMethod(method, paramTypes);
                if (mi == null) { Logger.Warn($"{type.Name}.{method} not found, skipping"); return; }
                string sig = $"{type.Name}.{method}({string.Join(", ", Array.ConvertAll(paramTypes, t => t.Name))})";
                Logger.Debug($"Prefix patching {sig} -> {prefix}");
                harmony.Patch(mi, prefix: new HarmonyMethod(typeof(HarmonyAssemblyLoadHooks), prefix));
            }
            catch (Exception ex) { Logger.Warn($"Failed to prefix patch {type.Name}.{method}: {ex.Message}"); }
        }

        private static void TryPatchPostfix(Harmony harmony, Type type, string method, Type[] paramTypes, string postfix)
        {
            try
            {
                var mi = type.GetMethod(method, paramTypes);
                if (mi == null) { Logger.Warn($"{type.Name}.{method} not found, skipping"); return; }
                string sig = $"{type.Name}.{method}({string.Join(", ", Array.ConvertAll(paramTypes, t => t.Name))})";
                Logger.Debug($"Postfix patching {sig} -> {postfix}");
                harmony.Patch(mi, postfix: new HarmonyMethod(typeof(HarmonyAssemblyLoadHooks), postfix));
            }
            catch (Exception ex) { Logger.Warn($"Failed to postfix patch {type.Name}.{method}: {ex.Message}"); }
        }

        internal static bool AssemblyLoadByteArrayPrefix(ref byte[] rawAssembly)
        {
            if (rawAssembly != null && _options.PatchByteArrayAssemblies)
            {
                Logger.Debug($"Prefix: patching byte[] before Assembly.Load ({rawAssembly.Length} bytes)");
                ByteArrayAssemblyPatcher.PatchBytes(ref rawAssembly);
            }
            return true;
        }

        internal static void AssemblyLoadStringPostfix(ref Assembly __result)
        {
            if (__result != null)
            { Logger.Debug($"Hook: Assembly.LoadFile/From returned '{__result.GetName().Name}'"); AssemblyRuntimePatcher.TryPatch(__result, _options); }
        }

        private static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            if (args.LoadedAssembly != null)
            { Logger.Debug($"Event: '{args.LoadedAssembly.GetName().Name}' loaded"); AssemblyRuntimePatcher.TryPatch(args.LoadedAssembly, _options); }
        }
    }
}
