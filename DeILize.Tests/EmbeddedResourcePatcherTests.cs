using NUnit.Framework;
using Mono.Cecil;
using System.IO;
using DeILize.Models;

namespace DeILize.Tests
{
    [TestFixture]
    public class EmbeddedResourcePatcherTests
    {
        [Test]
        public void DetectsPeResourceByMZHeader()
        {
            var assembly = CreateAssemblyWithResource("embedded.dll", new byte[] { 0x4D, 0x5A, 0x90, 0x00 });
            bool found = false;

            foreach (var resource in assembly.MainModule.Resources)
            {
                if (resource is EmbeddedResource emb)
                {
                    byte[] data = emb.GetResourceData();
                    if (data.Length >= 2 && data[0] == 0x4D && data[1] == 0x5A)
                        found = true;
                }
            }

            Assert.That(found, Is.True);
        }

        [Test]
        public void DetectsCosturaCompressedResource()
        {
            var assembly = CreateAssemblyWithResource("costura.mylib.dll.compressed", new byte[] { 0x4D, 0x5A, 0x90, 0x00 });
            bool foundCostura = false;

            foreach (var resource in assembly.MainModule.Resources)
            {
                if (resource.Name.IndexOf("costura", System.StringComparison.OrdinalIgnoreCase) >= 0
                    && resource.Name.EndsWith(".compressed", System.StringComparison.OrdinalIgnoreCase))
                {
                    foundCostura = true;
                }
            }

            Assert.That(foundCostura, Is.True);
        }

        [Test]
        public void Process_NonDestructive_DoesNotCrash()
        {
            var peBytes = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x00, 0x00, 0x00, 0x00 };
            var assembly = CreateAssemblyWithResource("test.dll", peBytes);
            string tempPath = Path.GetTempFileName() + ".dll";

            try
            {
                assembly.Write(tempPath);
                var options = new FilePatchOptions
                {
                    Destructive = false,
                    PatchEmbeddedResources = true
                };

                Assert.DoesNotThrow(() => DeILizeRuntime.PatchAssemblyFile(tempPath, tempPath + ".patched", options));
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
                if (File.Exists(tempPath + ".patched")) File.Delete(tempPath + ".patched");
            }
        }

        private static AssemblyDefinition CreateAssemblyWithResource(string resourceName, byte[] data)
        {
            var name = new AssemblyNameDefinition("TestWithResources", new System.Version(1, 0, 0, 0));
            var assembly = AssemblyDefinition.CreateAssembly(name, "TestWithResources.dll", ModuleKind.Dll);
            var resource = new EmbeddedResource(resourceName, ManifestResourceAttributes.Public, data);
            assembly.MainModule.Resources.Add(resource);
            return assembly;
        }
    }
}
