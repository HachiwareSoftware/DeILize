using NUnit.Framework;
using System.IO;
using System.Linq;
using System.Reflection;
using DeILize.Models;
using DeILize.Runtime;

namespace DeILize.Tests
{
    [TestFixture]
    public class RuntimePatchIntegrationTests
    {
        [Test]
        public void HideCurrentAppDomain_DoesNotCrash()
        {
            var results = DeILizeRuntime.HideCurrentAppDomain(new RuntimePatchOptions
            {
                PatchAlreadyLoadedAssemblies = true,
                InstallHarmonyHooks = false,
                HideAssemblyDebugInfo = true,
                HideModuleFromPeb = false,
                IncludeFrameworkAssemblies = true,
                IncludeGacAssemblies = true
            });

            Assert.That(results, Is.Not.Null);
            Assert.That(results.Count, Is.GreaterThan(0));

            var deILizeResult = results.FirstOrDefault(r =>
                r.AssemblyName == "DeILize");
            Assert.That(deILizeResult, Is.Not.Null);
        }

        [Test]
        public void InstallHooks_DoesNotCrash()
        {
            Assert.DoesNotThrow(() =>
            {
                DeILizeRuntime.InstallHooks(new RuntimePatchOptions
                {
                    PatchAlreadyLoadedAssemblies = true,
                    InstallHarmonyHooks = true,
                    HideAssemblyDebugInfo = true,
                    HideModuleFromPeb = false,
                    IncludeFrameworkAssemblies = true,
                    IncludeGacAssemblies = true
                });
            });
        }

        [Test]
        public void HideCurrentAppDomain_ReturnsResultsForAllAssemblies()
        {
            var results = DeILizeRuntime.HideCurrentAppDomain();
            var totalAssemblies = System.AppDomain.CurrentDomain.GetAssemblies().Length;

            Assert.That(results.Count == totalAssemblies, Is.True);
        }

        [Test]
        public void ByteArrayAssembly_PatchedBeforeLoad_HasZeroedClrDirectory()
        {
            var name = new Mono.Cecil.AssemblyNameDefinition("ByteArrayTest", new System.Version(1, 0, 0, 0));
            var assembly = Mono.Cecil.AssemblyDefinition.CreateAssembly(name, "ByteArrayTest.dll", Mono.Cecil.ModuleKind.Dll);
            string tempPath = Path.GetTempFileName() + ".dll";
            try
            {
                assembly.Write(tempPath);
                byte[] rawAssembly = File.ReadAllBytes(tempPath);

                int e_lfanew = (rawAssembly[0x3C]) | (rawAssembly[0x3D] << 8) | (rawAssembly[0x3E] << 16) | (rawAssembly[0x3F] << 24);
                int optionalHeaderOff = e_lfanew + 4 + 20;
                ushort magic = (ushort)(rawAssembly[optionalHeaderOff] | (rawAssembly[optionalHeaderOff + 1] << 8));
                int dataDirOff = magic == 0x10B ? optionalHeaderOff + 96 : optionalHeaderOff + 112;
                int clrEntryOff = dataDirOff + 14 * 8;

                int clrRvaBefore = (rawAssembly[clrEntryOff]) | (rawAssembly[clrEntryOff + 1] << 8) | (rawAssembly[clrEntryOff + 2] << 16) | (rawAssembly[clrEntryOff + 3] << 24);
                int clrSizeBefore = (rawAssembly[clrEntryOff + 4]) | (rawAssembly[clrEntryOff + 5] << 8) | (rawAssembly[clrEntryOff + 6] << 16) | (rawAssembly[clrEntryOff + 7] << 24);
                Assert.That(clrRvaBefore, Is.Not.EqualTo(0), "Test assembly should have CLR directory before patching");

                ByteArrayAssemblyPatcher.PatchBytes(ref rawAssembly);

                int clrRvaAfter = (rawAssembly[clrEntryOff]) | (rawAssembly[clrEntryOff + 1] << 8) | (rawAssembly[clrEntryOff + 2] << 16) | (rawAssembly[clrEntryOff + 3] << 24);
                int clrSizeAfter = (rawAssembly[clrEntryOff + 4]) | (rawAssembly[clrEntryOff + 5] << 8) | (rawAssembly[clrEntryOff + 6] << 16) | (rawAssembly[clrEntryOff + 7] << 24);

                Assert.That(clrRvaAfter, Is.EqualTo(0), "CLR RVA should be zeroed");
                Assert.That(clrSizeAfter, Is.EqualTo(0), "CLR size should be zeroed");
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }
    }
}
