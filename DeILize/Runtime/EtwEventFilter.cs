using System;

namespace DeILize.Runtime
{
    public static class EtwEventFilter
    {
        internal static void Install()
        {
            Logger.Section("ETW Event Filter");
            Logger.Warn("ETW filtering not supported on .NET Framework 4.8");
            Logger.Warn("Process Hacker .NET tab uses DAC (IXCLRDataProcess), not ETW");
        }

        internal static void Uninstall() { }
    }
}
