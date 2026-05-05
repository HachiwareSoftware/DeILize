using NUnit.Framework;
using Mono.Cecil;
using System.IO;
using DeILize.FilePatch;
using DeILize.Models;

namespace DeILize.Tests
{
    [TestFixture]
    public class AssemblyIdentityRewriterTests
    {
        private string _testAssemblyPath;

        [SetUp]
        public void Setup()
        {
            _testAssemblyPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestAssembly.dll");
            CreateTestAssembly(_testAssemblyPath);
        }

        [TearDown]
        public void Teardown()
        {
            if (File.Exists(_testAssemblyPath))
                File.Delete(_testAssemblyPath);
            string output = _testAssemblyPath.Replace(".dll", "_renamed.dll");
            if (File.Exists(output))
                File.Delete(output);
        }

        [Test]
        public void RewriteFile_ChangesAssemblyName()
        {
            string outputPath = _testAssemblyPath.Replace(".dll", "_renamed.dll");
            var config = new AssemblyRenameConfig { NewAssemblyName = "RenamedLib" };

            AssemblyIdentityRewriter.RewriteFile(_testAssemblyPath, outputPath, config);

            using (var module = ModuleDefinition.ReadModule(outputPath))
            {
                Assert.That(module.Assembly.Name.Name, Is.EqualTo("RenamedLib"));
                Assert.That(module.Name, Is.EqualTo("RenamedLib.dll"));
            }
        }

        [Test]
        public void RewriteFile_UpdatesReferences()
        {
            string outputPath = _testAssemblyPath.Replace(".dll", "_renamed.dll");
            var config = new AssemblyRenameConfig { NewAssemblyName = "RenamedLib" };

            AssemblyIdentityRewriter.RewriteFile(_testAssemblyPath, outputPath, config);

            using (var module = ModuleDefinition.ReadModule(outputPath))
            {
                bool hasOldRef = false;
                foreach (var reference in module.AssemblyReferences)
                {
                    if (reference.Name == "TestAssembly")
                        hasOldRef = true;
                }
                Assert.That(hasOldRef, Is.False);
            }
        }

        private static void CreateTestAssembly(string path)
        {
            var name = new AssemblyNameDefinition("TestAssembly", new System.Version(1, 0, 0, 0));
            var assembly = AssemblyDefinition.CreateAssembly(name, "TestAssembly.dll", ModuleKind.Dll);
            assembly.Write(path);
        }
    }
}
