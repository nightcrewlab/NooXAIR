using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace NooXAIR
{
    /// <summary>Basit key=value ayar dosyasi: %APPDATA%\NooXAIR\settings.ini</summary>
    internal sealed class AppSettings
    {
        public string DeviceName = "NooXAIR";
        public bool Fullscreen = true;
        public bool UltraLowLatency = true;   // -vsync no
        public bool Fps60 = true;             // -fps 60
        public bool NoHold = true;            // yeni cihaz baglantiyi devralir
        public bool H265 = false;             // 4K/HEVC
        public bool AutoStartEngine = true;
        public string Decoder = "auto";       // auto | software
        public string VideoSink = "d3d11";    // d3d11 | d3d12 | auto
        public string Resolution = "screen";  // screen | 1920x1080 | 2560x1440 | 1280x720
        public bool FirstRun = true;

        public static string DataDir
        {
            get
            {
                string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NooXAIR");
                if (!Directory.Exists(d)) Directory.CreateDirectory(d);
                return d;
            }
        }

        public static string LocalDir
        {
            get
            {
                string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NooXAIR");
                if (!Directory.Exists(d)) Directory.CreateDirectory(d);
                return d;
            }
        }

        private static string FilePath { get { return Path.Combine(DataDir, "settings.ini"); } }

        public static AppSettings Load()
        {
            var s = new AppSettings();
            try
            {
                if (!File.Exists(FilePath)) return s;
                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string raw in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    int i = raw.IndexOf('=');
                    if (i <= 0) continue;
                    map[raw.Substring(0, i).Trim()] = raw.Substring(i + 1).Trim();
                }
                s.DeviceName = Get(map, "DeviceName", s.DeviceName);
                s.Fullscreen = GetB(map, "Fullscreen", s.Fullscreen);
                s.UltraLowLatency = GetB(map, "UltraLowLatency", s.UltraLowLatency);
                s.Fps60 = GetB(map, "Fps60", s.Fps60);
                s.NoHold = GetB(map, "NoHold", s.NoHold);
                s.H265 = GetB(map, "H265", s.H265);
                s.AutoStartEngine = GetB(map, "AutoStartEngine", s.AutoStartEngine);
                s.Decoder = Get(map, "Decoder", s.Decoder);
                s.VideoSink = Get(map, "VideoSink", s.VideoSink);
                s.Resolution = Get(map, "Resolution", s.Resolution);
                s.FirstRun = GetB(map, "FirstRun", false);
            }
            catch (Exception ex) { Logger.Write("Ayar okunamadi: " + ex.Message); }
            if (string.IsNullOrWhiteSpace(s.DeviceName)) s.DeviceName = "NooXAIR";
            return s;
        }

        public void Save()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("DeviceName=" + DeviceName);
                sb.AppendLine("Fullscreen=" + Fullscreen);
                sb.AppendLine("UltraLowLatency=" + UltraLowLatency);
                sb.AppendLine("Fps60=" + Fps60);
                sb.AppendLine("NoHold=" + NoHold);
                sb.AppendLine("H265=" + H265);
                sb.AppendLine("AutoStartEngine=" + AutoStartEngine);
                sb.AppendLine("Decoder=" + Decoder);
                sb.AppendLine("VideoSink=" + VideoSink);
                sb.AppendLine("Resolution=" + Resolution);
                sb.AppendLine("FirstRun=" + FirstRun);
                File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception ex) { Logger.Write("Ayar kaydedilemedi: " + ex.Message); }
        }

        private static string Get(Dictionary<string, string> m, string k, string d)
        {
            string v; return m.TryGetValue(k, out v) ? v : d;
        }

        private static bool GetB(Dictionary<string, string> m, string k, bool d)
        {
            string v; bool b;
            if (m.TryGetValue(k, out v) && bool.TryParse(v, out b)) return b;
            return d;
        }

        // ---- Windows ile baslat (HKCU\...\Run) ----
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static bool AutoStartEnabled
        {
            get
            {
                try
                {
                    using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                        return k != null && k.GetValue("NooXAIR") != null;
                }
                catch { return false; }
            }
            set
            {
                try
                {
                    using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                    {
                        if (value) k.SetValue("NooXAIR", "\"" + System.Windows.Forms.Application.ExecutablePath + "\" --tray");
                        else if (k.GetValue("NooXAIR") != null) k.DeleteValue("NooXAIR");
                    }
                }
                catch (Exception ex) { Logger.Write("Autostart ayarlanamadi: " + ex.Message); }
            }
        }
    }
}
