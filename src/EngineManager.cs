using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace NooXAIR
{
    internal enum EngineState { Stopped, Starting, Ready, Connected, Streaming, Error }

    /// <summary>
    /// uxplay.exe (native C AirPlay motoru) surecini yonetir: arguman uretimi, GStreamer ortam
    /// degiskenleri, cokme izleme / otomatik yeniden baslatma, ag degisimi ve uykudan donus.
    /// </summary>
    internal sealed class EngineManager : IDisposable
    {
        public event EventHandler StateChanged;
        public event Action<string> LogLine;

        public EngineState State { get; private set; }
        public string StatusDetail { get; private set; }
        public string ClientName { get; private set; }
        public bool SafeMode { get; private set; }
        public DateTime StreamStart { get; private set; }

        private readonly object _sync = new object();
        private readonly LinkedList<string> _log = new LinkedList<string>();
        private Process _proc;
        private AppSettings _settings;
        private bool _wantRunning;
        private DateTime _startedAt;
        private int _quickFails;
        private int _restartDelayMs = 1000;
        private int _openSockets;
        private System.Threading.Timer _restartTimer;
        private System.Threading.Timer _netTimer;
        private System.Threading.Timer _readyTimer;
        private readonly IntPtr _job;

        public EngineManager(AppSettings settings)
        {
            _settings = settings;
            State = EngineState.Stopped;
            StatusDetail = "Motor durduruldu";
            _job = CreateKillOnCloseJob();
            NetworkChange.NetworkAddressChanged += OnNetworkChanged;
            NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailability;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
        }

        public static string AppDir { get { return Path.GetDirectoryName(Application.ExecutablePath); } }
        public static string EngineDir { get { return Path.Combine(AppDir, "engine"); } }
        public static string EngineExe { get { return Path.Combine(EngineDir, "bin", "uxplay.exe"); } }
        public static bool EngineInstalled { get { return File.Exists(EngineExe); } }

        public string[] RecentLog
        {
            get { lock (_log) { var a = new string[_log.Count]; _log.CopyTo(a, 0); return a; } }
        }

        public void UpdateSettings(AppSettings s) { _settings = s; }

        // ------------------------------------------------------------------ yasam dongusu
        public void Start()
        {
            lock (_sync)
            {
                _wantRunning = true;
                if (_proc != null && !_proc.HasExited) return;
                StartProcess();
            }
        }

        public void Stop()
        {
            lock (_sync)
            {
                _wantRunning = false;
                CancelTimer(ref _restartTimer);
                KillProcess();
                SetState(EngineState.Stopped, "Motor durduruldu", null);
            }
        }

        public void Restart(string reason)
        {
            lock (_sync)
            {
                Emit("[NooXAIR] Yeniden baslatiliyor: " + reason);
                _wantRunning = true;
                _quickFails = 0;
                _restartDelayMs = 1000;
                KillProcess();
                StartProcess();
            }
        }

        public void ResetSafeMode() { SafeMode = false; _quickFails = 0; }

        private void StartProcess()
        {
            if (!EngineInstalled)
            {
                Emit("[NooXAIR] HATA: motor dosyasi yok -> " + EngineExe);
                SetState(EngineState.Error, "uxplay.exe bulunamadı — kurulumu onarın", null);
                return;
            }

            KillOrphans();
            _openSockets = 0;
            string args = BuildArguments(_settings, SafeMode);
            Emit("[NooXAIR] uxplay " + args);

            var psi = new ProcessStartInfo(EngineExe, args);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;
            psi.WorkingDirectory = Path.Combine(EngineDir, "bin");
            ConfigureGStreamerEnv(psi);

            try
            {
                var p = new Process();
                p.StartInfo = psi;
                p.EnableRaisingEvents = true;
                p.OutputDataReceived += OnOutput;
                p.ErrorDataReceived += OnOutput;
                p.Exited += OnExited;
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                try { p.PriorityClass = ProcessPriorityClass.High; } catch { }
                if (_job != IntPtr.Zero) AssignProcessToJobObject(_job, p.Handle);
                _proc = p;
                _startedAt = DateTime.Now;
                SetState(EngineState.Starting, "Motor başlatılıyor...", null);

                // Bazi surumler "Initialized server socket" satirini yazmayabilir: 4 sn sonra hazir kabul et
                CancelTimer(ref _readyTimer);
                _readyTimer = new System.Threading.Timer(delegate
                {
                    if (State == EngineState.Starting && _proc != null && !_proc.HasExited)
                        SetState(EngineState.Ready, ReadyText(), null);
                }, null, 4000, Timeout.Infinite);
            }
            catch (Exception ex)
            {
                SetState(EngineState.Error, "Motor başlatılamadı: " + ex.Message, null);
                ScheduleRestart();
            }
        }

        private string ReadyText()
        {
            return "Hazır — iPhone'dan \"" + _settings.DeviceName + "\" seçin" + (SafeMode ? " (güvenli mod)" : "");
        }

        private void ConfigureGStreamerEnv(ProcessStartInfo psi)
        {
            string bin = Path.Combine(EngineDir, "bin");
            string plugins = Path.Combine(EngineDir, "lib", "gstreamer-1.0");
            string scanner = Path.Combine(EngineDir, "libexec", "gstreamer-1.0", "gst-plugin-scanner.exe");
            var env = psi.EnvironmentVariables;
            env["PATH"] = bin + ";" + Environment.GetFolderPath(Environment.SpecialFolder.System) + ";" + Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            env["GST_PLUGIN_SYSTEM_PATH_1_0"] = plugins;
            env["GST_PLUGIN_SYSTEM_PATH"] = plugins;
            env["GST_PLUGIN_PATH_1_0"] = "";
            env["GST_PLUGIN_PATH"] = "";
            if (File.Exists(scanner))
            {
                env["GST_PLUGIN_SCANNER_1_0"] = scanner;
                env["GST_PLUGIN_SCANNER"] = scanner;
            }
            env["GST_REGISTRY_1_0"] = Path.Combine(AppSettings.LocalDir, "gst-registry.bin");
            env["GST_REGISTRY_UPDATE"] = "yes";
            env["GST_DEBUG"] = "1"; // yalnizca hatalar
            env["HOME"] = AppSettings.DataDir; // uxplay anahtar/pin dosyalari burada tutulur
        }

        public static string BuildArguments(AppSettings s, bool safe)
        {
            var a = new List<string>();
            a.Add("-n"); a.Add(Quote(Sanitize(s.DeviceName)));
            a.Add("-nh");
            // Sabit -p portlari (7000/7100) KULLANILMAZ: Hyper-V/WinNAT bu araligi sik sik rezerve eder (WSAEACCES 10013).
            // Isletim sistemi bos port atar; guvenlik duvari kurali programa (uxplay.exe) gore tanimlidir.

            int refresh = s.Fps60 ? 60 : 30;
            a.Add("-fps"); a.Add(refresh.ToString());

            string res = s.Resolution;
            if (res == "screen" || string.IsNullOrEmpty(res))
            {
                var b = Screen.PrimaryScreen.Bounds;
                int w = b.Width, h = b.Height;
                if (!s.H265 && (w > 1920 || h > 1080)) { w = 1920; h = 1080; } // H.264 icin 1080p ust sinir
                res = w + "x" + h;
            }
            a.Add("-s"); a.Add(res + "@" + refresh);
            if (s.H265) a.Add("-h265");

            if (!safe)
            {
                if (s.VideoSink == "d3d11") { a.Add("-vs"); a.Add("d3d11videosink"); }
                else if (s.VideoSink == "d3d12") { a.Add("-vs"); a.Add("d3d12videosink"); }
                if (s.Decoder == "software") a.Add("-avdec");
                a.Add("-as"); a.Add(s.UltraLowLatency ? "\"wasapisink low-latency=true\"" : "wasapisink");
            }
            else
            {
                a.Add("-avdec");
            }

            if (s.Fullscreen) a.Add("-fs");
            if (s.UltraLowLatency) { a.Add("-vsync"); a.Add("no"); }
            if (s.NoHold) a.Add("-nohold");
            return string.Join(" ", a.ToArray());
        }

        private static string Sanitize(string n)
        {
            var sb = new StringBuilder();
            foreach (char c in (n ?? "NooXAIR").Trim())
                if (c != '"' && c != '\\' && !char.IsControl(c)) sb.Append(c);
            return sb.Length == 0 ? "NooXAIR" : sb.ToString();
        }

        private static string Quote(string v) { return "\"" + v + "\""; }

        // ------------------------------------------------------------------ cikti cozumleme
        private void OnOutput(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null) return;
            string line = e.Data.TrimEnd();
            if (line.Length == 0) return;
            Emit(line);
            Parse(line);
        }

        private void Parse(string line)
        {
            if (line.IndexOf("Initialized server socket", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (State == EngineState.Starting) SetState(EngineState.Ready, ReadyText(), null);
                _quickFails = 0;
                _restartDelayMs = 1000;
            }
            else if (line.IndexOf("connection request from ", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                string n = line.Substring(line.IndexOf("connection request from ", StringComparison.OrdinalIgnoreCase) + 24);
                int p = n.IndexOf(" (");
                if (p > 0) n = n.Substring(0, p);
                n = n.Trim();
                if (State != EngineState.Streaming) SetState(EngineState.Connected, "Bağlandı: " + n, n);
                else ClientName = n;
            }
            else if (line.IndexOf("starting mirroring", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                StreamStart = DateTime.Now;
                SetState(EngineState.Streaming, "Ekran yansıtılıyor" + (ClientName != null ? ": " + ClientName : ""), ClientName);
            }
            else if (line.IndexOf("client on socket", StringComparison.OrdinalIgnoreCase) >= 0 && line.StartsWith("Accepted", StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Increment(ref _openSockets);
            }
            else if (line.IndexOf("Connection closed on socket", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (Interlocked.Decrement(ref _openSockets) <= 0)
                {
                    _openSockets = 0;
                    if (State == EngineState.Connected || State == EngineState.Streaming)
                        SetState(EngineState.Ready, ReadyText(), null);
                }
            }
            else if (line.IndexOf("lost connection with client", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                SetState(EngineState.Ready, ReadyText(), null);
            }
            else if (line.IndexOf("mDNSResponder (lib/mdnsd) failed", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                SetState(EngineState.Error, "mDNS (UDP 5353) başlatılamadı — güvenlik duvarı / ağı kontrol edin", null);
                KillForRestart();
            }
            else if (line.IndexOf("Error initialising socket", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     line.IndexOf("gst_parse_launch error", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // Motor hata verip asili kalabiliyor: sureci sonlandir -> izleyici yeniden baslatir / guvenli moda gecer
                SetState(EngineState.Error, "Motor başlatma hatası — yeniden deneniyor", null);
                KillForRestart();
            }
        }

        private void OnExited(object sender, EventArgs e)
        {
            lock (_sync)
            {
                var p = sender as Process;
                if (p != _proc) return;
                int code = -1;
                try { code = p.ExitCode; } catch { }
                Emit("[NooXAIR] Motor kapandi (kod " + code + ")");
                _proc = null;
                if (!_wantRunning) return;

                double alive = (DateTime.Now - _startedAt).TotalSeconds;
                if (alive < 12)
                {
                    _quickFails++;
                    if (_quickFails >= 2 && !SafeMode)
                    {
                        SafeMode = true;
                        _quickFails = 0;
                        Emit("[NooXAIR] Motor art arda erken kapandi -> guvenli moda geciliyor (yazilim kod cozucu, otomatik video/ses cikisi)");
                    }
                }
                else { _quickFails = 0; _restartDelayMs = 1000; }

                SetState(EngineState.Error, "Motor durdu, yeniden başlatılıyor...", null);
                ScheduleRestart();
            }
        }

        private void ScheduleRestart()
        {
            CancelTimer(ref _restartTimer);
            int delay = _restartDelayMs;
            _restartDelayMs = Math.Min(_restartDelayMs * 2, 15000);
            _restartTimer = new System.Threading.Timer(delegate
            {
                lock (_sync)
                {
                    if (_wantRunning && (_proc == null || _proc.HasExited)) StartProcess();
                }
            }, null, delay, Timeout.Infinite);
        }

        // ------------------------------------------------------------------ ag / guc olaylari
        private void OnNetworkChanged(object sender, EventArgs e) { DebouncedNetRestart("ag adresi degisti"); }
        private void OnNetworkAvailability(object sender, NetworkAvailabilityEventArgs e)
        {
            if (e.IsAvailable) DebouncedNetRestart("ag yeniden kullanilabilir");
        }

        private void DebouncedNetRestart(string reason)
        {
            if (!_wantRunning) return;
            CancelTimer(ref _netTimer);
            _netTimer = new System.Threading.Timer(delegate
            {
                // Aktif yayini bozma; bosta ise mDNS kaydini yeni IP ile yenile
                if (State == EngineState.Streaming || State == EngineState.Connected) return;
                Restart(reason);
            }, null, 5000, Timeout.Infinite);
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume && _wantRunning)
            {
                CancelTimer(ref _netTimer);
                _netTimer = new System.Threading.Timer(delegate { Restart("uykudan donuldu"); }, null, 4000, Timeout.Infinite);
            }
        }

        // ------------------------------------------------------------------ yardimcilar
        private void SetState(EngineState s, string detail, string client)
        {
            bool changed = s != State || detail != StatusDetail;
            State = s;
            StatusDetail = detail;
            ClientName = client;
            if (changed)
            {
                Logger.Write("Durum: " + s + " - " + detail);
                var h = StateChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        private void Emit(string line)
        {
            string ll = line.ToLowerInvariant();
            if (ll.Contains("[nooxair]") || ll.Contains("error") || ll.Contains("connection request") || ll.Contains("mirroring") ||
                ll.Contains("initialized") || ll.Contains("using network ports") || ll.Contains("lost connection"))
                Logger.Write("motor> " + line);
            string l = DateTime.Now.ToString("HH:mm:ss") + "  " + line;
            lock (_log)
            {
                _log.AddLast(l);
                while (_log.Count > 600) _log.RemoveFirst();
            }
            var h = LogLine;
            if (h != null) h(l);
        }

        private void KillForRestart()
        {
            CancelTimer(ref _readyTimer);
            var p = _proc;
            try { if (p != null && !p.HasExited) p.Kill(); } catch { } // OnExited tetiklenir
        }

        private void KillProcess()
        {
            CancelTimer(ref _readyTimer);
            var p = _proc;
            _proc = null;
            if (p == null) return;
            try
            {
                p.Exited -= OnExited;
                if (!p.HasExited) { p.Kill(); p.WaitForExit(3000); }
            }
            catch { }
            try { p.Dispose(); } catch { }
        }

        private static void KillOrphans()
        {
            try
            {
                foreach (var p in Process.GetProcessesByName("uxplay"))
                {
                    try
                    {
                        string path = p.MainModule.FileName;
                        if (string.Equals(path, EngineExe, StringComparison.OrdinalIgnoreCase)) { p.Kill(); p.WaitForExit(2000); }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static void CancelTimer(ref System.Threading.Timer t)
        {
            var x = t; t = null;
            if (x != null) x.Dispose();
        }

        public void Dispose()
        {
            NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
            NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailability;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            Stop();
            CancelTimer(ref _netTimer);
        }

        // ---- Job object: NooXAIR kapanirsa uxplay.exe de kapanir (yetim surec kalmaz)
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr CreateJobObject(IntPtr a, string name);
        [DllImport("kernel32.dll")] private static extern bool SetInformationJobObject(IntPtr job, int infoClass, ref JOBOBJECT_EXTENDED_LIMIT_INFORMATION info, uint len);
        [DllImport("kernel32.dll")] private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit; public long PerJobUserTimeLimit; public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize; public UIntPtr MaximumWorkingSetSize; public uint ActiveProcessLimit;
            public UIntPtr Affinity; public uint PriorityClass; public uint SchedulingClass;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS { public ulong a, b, c, d, e, f; }
        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation; public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit; public UIntPtr JobMemoryLimit; public UIntPtr PeakProcessMemoryUsed; public UIntPtr PeakJobMemoryUsed;
        }

        private static IntPtr CreateKillOnCloseJob()
        {
            try
            {
                IntPtr job = CreateJobObject(IntPtr.Zero, null);
                var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
                info.BasicLimitInformation.LimitFlags = 0x2000; // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
                SetInformationJobObject(job, 9, ref info, (uint)Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION)));
                return job;
            }
            catch { return IntPtr.Zero; }
        }
    }
}
