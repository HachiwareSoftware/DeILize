using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using DeILize.Models;
using DeILize.Runtime;
using Mono.Cecil;

namespace DeILize.FilePatch
{
    internal static class EmbeddedResourcePatcher
    {
        internal static void Process(ModuleDefinition module, FilePatchOptions options)
        {
            var resources = module.Resources.OfType<EmbeddedResource>().ToList();
            Logger.Debug($"Total embedded resources: {resources.Count}");
            if (resources.Count == 0) { Logger.Info("No embedded resources to process"); return; }

            int peCount = 0, costuraCount = 0, patched = 0;

            foreach (var resource in resources)
            {
                byte[] data = resource.GetResourceData();
                Logger.Debug($"Resource: '{resource.Name}' ({data.Length} bytes)");

                bool isPe = data.Length >= 2 && data[0] == 0x4D && data[1] == 0x5A;
                bool isCostura = resource.Name.IndexOf("costura", StringComparison.OrdinalIgnoreCase) >= 0
                              && resource.Name.EndsWith(".compressed", StringComparison.OrdinalIgnoreCase);

                if (isPe) peCount++;
                if (isCostura) costuraCount++;

                if (isCostura)
                {
                    Logger.Debug("Costura compressed resource detected, decompressing...");
                    byte[] decompressed;
                    using (var input = new MemoryStream(data))
                    using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
                    using (var output = new MemoryStream()) { deflate.CopyTo(output); decompressed = output.ToArray(); }
                    Logger.Debug($"Decompressed: {data.Length} -> {decompressed.Length} bytes");

                    if (decompressed.Length >= 2 && decompressed[0] == 0x4D && decompressed[1] == 0x5A)
                    {
                        if (options.Destructive) PeHeaderPatcher.PatchBytes(decompressed);
                        using (var output = new MemoryStream())
                        {
                            using (var deflate = new DeflateStream(output, CompressionMode.Compress))
                                deflate.Write(decompressed, 0, decompressed.Length);
                            data = output.ToArray();
                        }
                        ReplaceResource(module, resource, data);
                        patched++;
                        Logger.Info($"Costura resource '{resource.Name}' patched");
                    }
                    else Logger.Warn("Decompressed content is not a PE image, skipping");
                }
                else if (isPe && options.Destructive)
                {
                    PeHeaderPatcher.PatchBytes(data);
                    ReplaceResource(module, resource, data);
                    patched++;
                    Logger.Info($"Embedded PE resource '{resource.Name}' patched");
                }
            }

            Logger.Debug($"PE resources: {peCount}, Costura: {costuraCount}, Patched: {patched}");
            Logger.Info("Embedded resource processing complete");
        }

        private static void ReplaceResource(ModuleDefinition module, EmbeddedResource oldResource, byte[] newData)
        {
            var newResource = new EmbeddedResource(oldResource.Name, oldResource.Attributes, newData);
            int index = module.Resources.IndexOf(oldResource);
            module.Resources.Remove(oldResource);
            module.Resources.Insert(index, newResource);
        }
    }
}
