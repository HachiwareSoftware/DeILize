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
            if (_installed)
                return;

            _installed = true;
            _options = options;

            var harmony = new Harmony(HarmonyId);

            try
            {
                var loadByte = typeof(Assembly).GetMethod("Load", new[] { typeof(byte[]) });
                if (loadByte != null)
                {
                    harmony.Patch(loadByte, postfix: new HarmonyMethod(typeof(HarmonyAssemblyLoadHooks),
                        nameof(AssemblyLoadPostfix)));
                }
            }
            catch
            {
            }

            try
            {
                var loadByteWithSymbols = typeof(Assembly).GetMethod("Load", new[] { typeof(byte[]), typeof(byte[]) });
                if (loadByteWithSymbols != null)
                {
                    harmony.Patch(loadByteWithSymbols, postfix: new HarmonyMethod(typeof(HarmonyAssemblyLoadHooks),
                        nameof(AssemblyLoadPostfix)));
                }
            }
            catch
            {
            }

            try
            {
                var loadFile = typeof(Assembly).GetMethod("LoadFile", new[] { typeof(string) });
                if (loadFile != null)
                {
                    harmony.Patch(loadFile, postfix: new HarmonyMethod(typeof(HarmonyAssemblyLoadHooks),
                        nameof(AssemblyLoadStringPostfix)));
                }
            }
            catch
            {
            }

            try
            {
                var loadFrom = typeof(Assembly).GetMethod("LoadFrom", new[] { typeof(string) });
                if (loadFrom != null)
                {
                    harmony.Patch(loadFrom, postfix: new HarmonyMethod(typeof(HarmonyAssemblyLoadHooks),
                        nameof(AssemblyLoadStringPostfix)));
                }
            }
            catch
            {
            }

            try
            {
                var appDomainLoad = typeof(AppDomain).GetMethod("Load", new[] { typeof(byte[]) });
                if (appDomainLoad != null)
                {
                    harmony.Patch(appDomainLoad, postfix: new HarmonyMethod(typeof(HarmonyAssemblyLoadHooks),
                        nameof(AssemblyLoadPostfix)));
                }
            }
            catch
            {
            }

            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
        }

        internal static void AssemblyLoadPostfix(ref Assembly __result)
        {
            if (__result != null)
            {
                AssemblyRuntimePatcher.TryPatch(__result, _options);
            }
        }

        internal static void AssemblyLoadStringPostfix(ref Assembly __result)
        {
            if (__result != null)
            {
                AssemblyRuntimePatcher.TryPatch(__result, _options);
            }
        }

        private static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            if (args.LoadedAssembly != null)
            {
                AssemblyRuntimePatcher.TryPatch(args.LoadedAssembly, _options);
            }
        }
    }
}
