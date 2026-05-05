namespace DeILize.Models
{
    public sealed class RuntimePatchResult
    {
        public string AssemblyName { get; set; }
        public string Location { get; set; }
        public bool ClrDirectoryZeroed { get; set; }
        public bool DebugDirectoryZeroed { get; set; }
        public bool PebUnlinked { get; set; }
        public string Warning { get; set; }
    }
}
