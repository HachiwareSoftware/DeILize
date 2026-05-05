namespace DeILize
{
    public delegate void LogEventHandler(string level, string message);

    public static class Logger
    {
        public static event LogEventHandler LogEvent;

        public static void Section(string name)
        {
            string line = $"==== {name} ====";
            System.Diagnostics.Debug.WriteLine(line);
            LogEvent?.Invoke("section", line);
        }

        public static void Info(string msg)
        {
            System.Diagnostics.Debug.WriteLine(msg);
            LogEvent?.Invoke("info", msg);
        }

        public static void Warn(string msg)
        {
            System.Diagnostics.Debug.WriteLine(msg);
            LogEvent?.Invoke("warn", msg);
        }

        public static void Error(string msg)
        {
            System.Diagnostics.Debug.WriteLine(msg);
            LogEvent?.Invoke("error", msg);
        }

        public static void Debug(string msg)
        {
            System.Diagnostics.Debug.WriteLine(msg);
            LogEvent?.Invoke("debug", msg);
        }
    }
}
