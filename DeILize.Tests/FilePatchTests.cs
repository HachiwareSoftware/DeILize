using NUnit.Framework;
using System.IO;
using DeILize.Models;

namespace DeILize.Tests
{
    [TestFixture]
    public class FilePatchTests
    {
        private string _testAssemblyPath;

        [SetUp]
        public void Setup()
        {
            _testAssemblyPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "FilePatchTestAssembly.dll");
            CreateTestAssembly(_testAssemblyPath);
        }

        [TearDown]
        public void Teardown()
        {
            if (File.Exists(_testAssemblyPath)) File.Delete(_testAssemblyPath);
            if (File.Exists(_testAssemblyPath + ".safe")) File.Delete(_testAssemblyPath + ".safe");
            if (File.Exists(_testAssemblyPath + ".destructive")) File.Delete(_testAssemblyPath + ".destructive");
        }

        [Test]
        public void PatchAssemblyFile_SafeMode_ProducesLoadableAssembly()
        {
            string outputPath = _testAssemblyPath + ".safe";
            var options = new FilePatchOptions
            {
                Destructive = false,
                StripDebugInfo = true,
                RemoveAssemblyAttributes = true,
                PatchEmbeddedResources = false
            };

            bool result = DeILizeRuntime.PatchAssemblyFile(_testAssemblyPath, outputPath, options);

            Assert.That(result, Is.True);
            Assert.That(File.Exists(outputPath), Is.True);
            Assert.That(new FileInfo(outputPath).Length, Is.GreaterThan(0));
        }

        [Test]
        public void PatchAssemblyFile_DestructiveMode_ZeroesClrDirectory()
        {
            string outputPath = _testAssemblyPath + ".destructive";
            var options = new FilePatchOptions
            {
                Destructive = true,
                StripDebugInfo = true,
                RemoveAssemblyAttributes = true
            };

            bool result = DeILizeRuntime.PatchAssemblyFile(_testAssemblyPath, outputPath, options);

            Assert.That(result, Is.True);

            byte[] patchedBytes = File.ReadAllBytes(outputPath);
            int e_lfanew = (patchedBytes[0x3C]) | (patchedBytes[0x3D] << 8) | (patchedBytes[0x3E] << 16) | (patchedBytes[0x3F] << 24);
            int optionalHeaderOff = e_lfanew + 4 + 20;
            ushort magic = (ushort)(patchedBytes[optionalHeaderOff] | (patchedBytes[optionalHeaderOff + 1] << 8));
            int dataDirOff = magic == 0x10B ? optionalHeaderOff + 96 : optionalHeaderOff + 112;
            int clrEntryOff = dataDirOff + 14 * 8;

            int clrRva = (patchedBytes[clrEntryOff]) | (patchedBytes[clrEntryOff + 1] << 8) | (patchedBytes[clrEntryOff + 2] << 16) | (patchedBytes[clrEntryOff + 3] << 24);
            int clrSize = (patchedBytes[clrEntryOff + 4]) | (patchedBytes[clrEntryOff + 5] << 8) | (patchedBytes[clrEntryOff + 6] << 16) | (patchedBytes[clrEntryOff + 7] << 24);

            Assert.That(clrRva == 0, Is.True);
            Assert.That(clrSize == 0, Is.True);
        }

        private static void CreateTestAssembly(string path)
        {
            var name = new Mono.Cecil.AssemblyNameDefinition("FilePatchTest", new System.Version(1, 0, 0, 0));
            var assembly = Mono.Cecil.AssemblyDefinition.CreateAssembly(name, "FilePatchTest.dll", Mono.Cecil.ModuleKind.Dll);
            assembly.Write(path);
        }
    }
}
