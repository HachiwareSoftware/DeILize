using System;
using System.Diagnostics;

namespace DeILize.SampleDll
{
    public class SampleClass
    {
        public static string Greeting => "Hello from SampleDll v2.0!";

        public static void ShowInfo()
        {
            var asm = typeof(SampleClass).Assembly;
            var name = asm.GetName();
            Console.WriteLine($"Assembly: {name.FullName}");
            Console.WriteLine($"Location: {asm.Location}");

            var attrs = asm.GetCustomAttributes(false);
            foreach (var attr in attrs)
                Console.WriteLine($"  Attr: {attr.GetType().Name}");

            Console.WriteLine($"Process: {Process.GetCurrentProcess().ProcessName}");
            Console.WriteLine($"PID: {Process.GetCurrentProcess().Id}");
        }
    }
}
