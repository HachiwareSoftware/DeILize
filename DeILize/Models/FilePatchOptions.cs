namespace DeILize.Models
{
    public sealed class FilePatchOptions
    {
        public bool Destructive { get; set; } = false;
        public bool StripDebugInfo { get; set; } = true;
        public bool RemoveAssemblyAttributes { get; set; } = true;
        public AssemblyRenameConfig Rename { get; set; } = null;
        public bool PatchEmbeddedResources { get; set; } = true;
    }
}
