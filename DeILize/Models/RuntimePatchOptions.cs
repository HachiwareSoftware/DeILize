namespace DeILize.Models
{
    public sealed class RuntimePatchOptions
    {
        public bool PatchAlreadyLoadedAssemblies { get; set; } = true;
        public bool InstallHarmonyHooks { get; set; } = true;
        public bool HideAssemblyDebugInfo { get; set; } = true;
        public bool HideModuleFromPeb { get; set; } = false;
        public bool IncludeGacAssemblies { get; set; } = true;
        public bool IncludeFrameworkAssemblies { get; set; } = true;
        public bool PatchEmbeddedFodyResources { get; set; } = true;
        public AssemblyRenameConfig HarmonyRename { get; set; } = null;
    }
}
