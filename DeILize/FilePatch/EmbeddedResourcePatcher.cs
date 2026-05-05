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

            foreach (var resource in resources)
            {
                byte[] data = resource.GetResourceData();

                if (IsPeImage(data))
                {
                    if (options.Destructive)
                    {
                        PeHeaderPatcher.PatchBytes(data);
                        ReplaceResource(module, resource, data);
                    }
                }

                if (IsCosturaCompressed(resource.Name))
                {
                    byte[] decompressed = DecompressDeflate(data);
                    if (IsPeImage(decompressed))
                    {
                        if (options.Destructive)
                        {
                            PeHeaderPatcher.PatchBytes(decompressed);
                        }

                        byte[] recompressed = CompressDeflate(decompressed);
                        ReplaceResource(module, resource, recompressed);
                    }
                }
            }
        }

        private static bool IsPeImage(byte[] data)
        {
            if (data == null || data.Length < 2)
                return false;

            return data[0] == 0x4D && data[1] == 0x5A;
        }

        private static bool IsCosturaCompressed(string resourceName)
        {
            return resourceName.IndexOf("costura", StringComparison.OrdinalIgnoreCase) >= 0
                && resourceName.EndsWith(".compressed", StringComparison.OrdinalIgnoreCase);
        }

        private static byte[] DecompressDeflate(byte[] compressedData)
        {
            using (var input = new MemoryStream(compressedData))
            using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                deflate.CopyTo(output);
                return output.ToArray();
            }
        }

        private static byte[] CompressDeflate(byte[] data)
        {
            using (var output = new MemoryStream())
            {
                using (var deflate = new DeflateStream(output, CompressionMode.Compress))
                {
                    deflate.Write(data, 0, data.Length);
                }
                return output.ToArray();
            }
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
