using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("NooXAIR")]
[assembly: System.Reflection.AssemblyProduct("NooXAIR")]
[assembly: System.Reflection.AssemblyDescription("NooXAIR - Low latency AirPlay receiver")]
[assembly: System.Reflection.AssemblyVersion("3.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("3.0.0.0")]

namespace NooXAIR
{
    internal static class Program
    {
        [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();
        [DllImport("winmm.dll")] internal static extern uint timeBeginPeriod(uint ms);
        [DllImport("winmm.dll")] internal static extern uint timeEndPeriod(uint ms);
        [DllImport("user32.dll")] private static extern uint RegisterWindowMessage(string msg);

        internal static readonly uint WM_SHOWME = RegisterWindowMessage("NooXAIR_SHOW_WINDOW");

        [STAThread]
        private static void Main(string[] args)
        {
            bool startInTray = false;
            foreach (string a in args)
            {
                if (a.Equals("--tray", StringComparison.OrdinalIgnoreCase)) startInTray = true;
            }

            bool created;
            using (Mutex mutex = new Mutex(true, "Global\\NooXAIR_SingleInstance", out created))
            {
                if (!created)
                {
                    // Zaten calisiyor: mevcut pencereyi one getir
                    NativeMethods.PostMessage((IntPtr)0xffff, WM_SHOWME, IntPtr.Zero, IntPtr.Zero);
                    return;
                }

                try { SetProcessDPIAware(); } catch { }
                timeBeginPeriod(1); // dusuk gecikmeli zamanlayici cozunurlugu
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Logger.Write("UI hata: " + e.Exception); };
                AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { Logger.Write("Kritik hata: " + e.ExceptionObject); };

                try
                {
                    Application.Run(new TrayContext(startInTray));
                }
                finally
                {
                    timeEndPeriod(1);
                }
            }
        }
    }

    internal static class NativeMethods
    {
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool ReleaseCapture();
        [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint flags);
        public const uint ES_CONTINUOUS = 0x80000000;
        public const uint ES_SYSTEM_REQUIRED = 0x00000001;
        public const uint ES_DISPLAY_REQUIRED = 0x00000002;
    }

    internal static class Logger
    {
        private static readonly object Sync = new object();
        public static void Write(string line)
        {
            try
            {
                lock (Sync)
                {
                    string dir = AppSettings.DataDir;
                    string path = System.IO.Path.Combine(dir, "nooxair.log");
                    var fi = new System.IO.FileInfo(path);
                    if (fi.Exists && fi.Length > 2 * 1024 * 1024)
                    {
                        string old = path + ".1";
                        if (System.IO.File.Exists(old)) System.IO.File.Delete(old);
                        System.IO.File.Move(path, old);
                    }
                    System.IO.File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + line + Environment.NewLine);
                }
            }
            catch { }
        }
    }
}
