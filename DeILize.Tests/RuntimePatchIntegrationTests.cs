using NUnit.Framework;
using System.Linq;
using DeILize.Models;

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
    }
}
