using System;
using System.Drawing;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Windows.Forms;

namespace NooXAIR
{
    internal sealed class MainForm : Form
    {
        private readonly EngineManager _engine;
        private readonly AppSettings _settings;
        private readonly Action _onExit;

        private Card _status, _howto, _logCard, _setCard;
        private StatusOrb _orb;
        private Label _lblState, _lblDetail, _lblInfo;
        private ModernButton _btnRestart, _btnToggle, _btnApply;
        private RichTextBox _log;
        private DarkTextBox _txtName;
        private ToggleSwitch _tFull, _tLatency, _tFps, _tAuto, _tNoHold, _tH265;
        private DropSelect _dDecoder, _dSink, _dRes;
        private Label _lblChips;
        private readonly Timer _tick = new Timer();
        
        private Rectangle _btnMinRect, _btnCloseRect;
        private int _hoverBtn = -1;

        public MainForm(EngineManager engine, AppSettings settings, Action onExit)
        {
            _engine = engine;
            _settings = settings;
            _onExit = onExit;

            using (var g = CreateGraphics()) Theme.Scale = g.DpiX / 96f;

            Text = "NooXAIR";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            ClientSize = new Size(Theme.S(920), Theme.S(664));
            DoubleBuffered = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            ShowInTaskbar = true;

            BuildUi();
            LoadSettingsToUi();

            _engine.StateChanged += delegate { SafeUi(RefreshStatus); };
            _engine.LogLine += delegate(string l) { SafeUi(delegate { AppendLog(l); }); };
            foreach (string l in _engine.RecentLog) AppendLog(l);

            _tick.Interval = 1000;
            _tick.Tick += delegate { RefreshInfo(); };
            _tick.Start();
            RefreshStatus();
        }

        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.ClassStyle |= 0x20000; /* CS_DROPSHADOW */ return cp; }
        }

        private void SafeUi(Action a)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke(a); } catch { }
        }

        // ------------------------------------------------------------------ arayuz
        private Label MkLabel(Control parent, string text, Font f, Color c, int x, int y, int w, int h)
        {
            var l = new Label();
            l.Text = text; l.Font = f; l.ForeColor = c; l.BackColor = Theme.Surface;
            l.Location = new Point(x, y); l.Size = new Size(w, h);
            l.AutoEllipsis = true;
            parent.Controls.Add(l);
            return l;
        }

        private void BuildUi()
        {
            int S20 = Theme.S(20);
            int leftW = Theme.S(528), rightX = Theme.S(568), rightW = Theme.S(332);

            // ---- Durum karti
            _status = new Card();
            _status.Bounds = new Rectangle(S20, Theme.S(64), leftW, Theme.S(236));
            Controls.Add(_status);

            _orb = new StatusOrb();
            _orb.Bounds = new Rectangle(Theme.S(22), Theme.S(28), Theme.S(132), Theme.S(132));
            _status.Controls.Add(_orb);

            int tx = Theme.S(172), tw = leftW - tx - Theme.S(18);
            _lblState = MkLabel(_status, "", Theme.Semi(17f), Theme.Text, tx, Theme.S(30), tw, Theme.S(38));
            _lblDetail = MkLabel(_status, "", Theme.UI(9.75f), Theme.Muted, tx, Theme.S(70), tw, Theme.S(22));
            _lblInfo = MkLabel(_status, "", Theme.UI(9f), Theme.Text, tx, Theme.S(98), tw, Theme.S(66));

            _btnToggle = new ModernButton();
            _btnToggle.Bounds = new Rectangle(tx, Theme.S(172), Theme.S(156), Theme.S(38));
            _btnToggle.Click += delegate
            {
                if (_engine.State == EngineState.Stopped) _engine.Start(); else _engine.Stop();
            };
            _status.Controls.Add(_btnToggle);

            _btnRestart = new ModernButton();
            _btnRestart.Text = "Yeniden Başlat"; _btnRestart.Glyph = "";
            _btnRestart.Bounds = new Rectangle(tx + Theme.S(166), Theme.S(172), Theme.S(166), Theme.S(38));
            _btnRestart.Click += delegate { _engine.ResetSafeMode(); _engine.Restart("kullanici istegi"); };
            _status.Controls.Add(_btnRestart);

            _lblChips = MkLabel(_status, "", Theme.UI(8.25f), Theme.Dim, Theme.S(22), Theme.S(196), Theme.S(140), Theme.S(32));

            // ---- Nasil baglanirim
            _howto = new Card();
            _howto.Title = "iPhone'dan nasıl bağlanırım?"; _howto.Glyph = "";
            _howto.Bounds = new Rectangle(S20, Theme.S(314), leftW, Theme.S(104));
            Controls.Add(_howto);
            MkLabel(_howto,
                "1.  iPhone ile bu bilgisayar aynı Wi-Fi / modem ağında olsun\n" +
                "2.  Denetim Merkezi  →  Ekran Yansıtma  →  \"" + _settings.DeviceName + "\" seçin\n" +
                "3.  Sadece ses için: müzik uygulamasında AirPlay simgesi  →  " + _settings.DeviceName,
                Theme.UI(9f), Theme.Muted, Theme.S(18), Theme.S(40), leftW - Theme.S(36), Theme.S(58)).Name = "howtoText";

            // ---- Gunluk
            _logCard = new Card();
            _logCard.Title = "Motor günlüğü"; _logCard.Glyph = "";
            _logCard.Bounds = new Rectangle(S20, Theme.S(432), leftW, Theme.S(212));
            Controls.Add(_logCard);
            _log = new RichTextBox();
            _log.BorderStyle = BorderStyle.None;
            _log.BackColor = Theme.Surface;
            _log.ForeColor = Theme.Muted;
            _log.Font = new Font("Consolas", 8.25f);
            _log.ReadOnly = true;
            _log.WordWrap = false;
            _log.ScrollBars = RichTextBoxScrollBars.Vertical;
            _log.Bounds = new Rectangle(Theme.S(16), Theme.S(42), leftW - Theme.S(28), Theme.S(158));
            _logCard.Controls.Add(_log);

            // ---- Ayarlar
            _setCard = new Card();
            _setCard.Title = "Ayarlar"; _setCard.Glyph = "";
            _setCard.Bounds = new Rectangle(rightX, Theme.S(64), rightW, Theme.S(580));
            Controls.Add(_setCard);

            int x = Theme.S(18), w = rightW - Theme.S(36), y = Theme.S(46);
            _txtName = new DarkTextBox(); _txtName.Label = "Cihaz adı (iPhone'da görünen)";
            _txtName.Bounds = new Rectangle(x, y, w, Theme.S(58)); _setCard.Controls.Add(_txtName);
            _txtName.Box.TextChanged += delegate { MarkDirty(); };
            y += Theme.S(66);

            _tFull = MkToggle("Tam ekran", "Yansıtma başlayınca tam ekran aç (Alt+Enter)", x, ref y, w);
            _tLatency = MkToggle("Ultra düşük gecikme", "Zaman damgası senkronunu kapatır (önerilen)", x, ref y, w);
            _tFps = MkToggle("60 FPS", "iPhone'dan 60 kare/sn talep et", x, ref y, w);
            _tNoHold = MkToggle("Yeni cihaz devralsın", "Başka iPhone bağlanınca bağlantıyı devret", x, ref y, w);
            _tH265 = MkToggle("4K / H.265 (HEVC)", "Destekleyen cihaz ve GPU için", x, ref y, w);
            _tAuto = MkToggle("Windows ile başlat", "Açılışta arka planda otomatik hazır olur", x, ref y, w);
            _tAuto.CheckedChanged += delegate { AppSettings.AutoStartEnabled = _tAuto.Checked; };

            y += Theme.S(4);
            int half = (w - Theme.S(10)) / 2;
            _dDecoder = new DropSelect(); _dDecoder.Label = "Kod çözücü";
            _dDecoder.SetItems(new[] { "auto", "software" }, new[] { "Donanım (GPU)", "Yazılım (CPU)" });
            _dDecoder.Bounds = new Rectangle(x, y, half, Theme.S(58)); _setCard.Controls.Add(_dDecoder);
            _dSink = new DropSelect(); _dSink.Label = "Görüntü motoru";
            _dSink.SetItems(new[] { "d3d11", "d3d12", "auto" }, new[] { "Direct3D 11", "Direct3D 12", "Otomatik" });
            _dSink.Bounds = new Rectangle(x + half + Theme.S(10), y, half, Theme.S(58)); _setCard.Controls.Add(_dSink);
            y += Theme.S(64);
            _dRes = new DropSelect(); _dRes.Label = "Çözünürlük";
            _dRes.SetItems(new[] { "screen", "1920x1080", "2560x1440", "3840x2160", "1280x720" },
                           new[] { "Ekran çözünürlüğü (önerilen)", "1920 × 1080", "2560 × 1440 (H.265)", "3840 × 2160 (H.265)", "1280 × 720 (zayıf Wi-Fi)" });
            _dRes.Bounds = new Rectangle(x, y, w, Theme.S(58)); _setCard.Controls.Add(_dRes);
            y += Theme.S(70);
            _dDecoder.SelectionChanged += delegate { MarkDirty(); };
            _dSink.SelectionChanged += delegate { MarkDirty(); };
            _dRes.SelectionChanged += delegate { MarkDirty(); };

            _btnApply = new ModernButton();
            _btnApply.Primary = true; _btnApply.Text = "Uygula ve motoru yenile"; _btnApply.Glyph = "";
            _btnApply.Bounds = new Rectangle(x, _setCard.Height - Theme.S(54), w, Theme.S(40));
            _btnApply.Enabled = false;
            _btnApply.Click += delegate { ApplySettings(); };
            _setCard.Controls.Add(_btnApply);
        }

        private ToggleSwitch MkToggle(string text, string desc, int x, ref int y, int w)
        {
            var t = new ToggleSwitch();
            t.Text = text; t.Description = desc;
            t.Bounds = new Rectangle(x, y, w, Theme.S(44));
            _setCard.Controls.Add(t);
            t.CheckedChanged += delegate { if (t != _tAuto) MarkDirty(); };
            y += Theme.S(46);
            return t;
        }

        private bool _loading;
        private void LoadSettingsToUi()
        {
            _loading = true;
            _txtName.Box.Text = _settings.DeviceName;
            _tFull.SetSilently(_settings.Fullscreen);
            _tLatency.SetSilently(_settings.UltraLowLatency);
            _tFps.SetSilently(_settings.Fps60);
            _tNoHold.SetSilently(_settings.NoHold);
            _tH265.SetSilently(_settings.H265);
            _tAuto.SetSilently(AppSettings.AutoStartEnabled);
            _dDecoder.SelectedKey = _settings.Decoder;
            _dSink.SelectedKey = _settings.VideoSink;
            _dRes.SelectedKey = _settings.Resolution;
            _loading = false;

            if (_btnApply != null) _btnApply.Enabled = false;
        }

        private void MarkDirty()
        {
            if (_loading) return;

            _btnApply.Enabled = true;
        }

        private void ApplySettings()
        {
            string n = _txtName.Box.Text.Trim();
            _settings.DeviceName = n.Length == 0 ? "NooXAIR" : n;
            _settings.Fullscreen = _tFull.Checked;
            _settings.UltraLowLatency = _tLatency.Checked;
            _settings.Fps60 = _tFps.Checked;
            _settings.NoHold = _tNoHold.Checked;
            _settings.H265 = _tH265.Checked;
            _settings.Decoder = _dDecoder.SelectedKey;
            _settings.VideoSink = _dSink.SelectedKey;
            _settings.Resolution = _dRes.SelectedKey;
            if ((_settings.Resolution == "2560x1440" || _settings.Resolution == "3840x2160") && !_settings.H265)
            {
                _settings.H265 = true; _tH265.SetSilently(true);
            }
            _settings.Save();
            _engine.UpdateSettings(_settings);
            _engine.ResetSafeMode();
            _engine.Restart("ayarlar degisti");

            _btnApply.Enabled = false;
            var how = _howto.Controls.Find("howtoText", false);
            if (how.Length > 0)
                how[0].Text = "1.  iPhone ile bu bilgisayar aynı Wi-Fi / modem ağında olsun\n" +
                              "2.  Denetim Merkezi  →  Ekran Yansıtma  →  \"" + _settings.DeviceName + "\" seçin\n" +
                              "3.  Sadece ses için: müzik uygulamasında AirPlay simgesi  →  " + _settings.DeviceName;
        }

        // ------------------------------------------------------------------ durum
        private void RefreshStatus()
        {
            var s = _engine.State;
            Color c; string head;
            switch (s)
            {
                case EngineState.Ready: c = Theme.Success; head = "Bağlantıya hazır"; break;
                case EngineState.Connected: c = Theme.Accent; head = "Bağlandı"; break;
                case EngineState.Streaming: c = Theme.Accent2; head = "Yayında"; break;
                case EngineState.Starting: c = Theme.Warning; head = "Başlatılıyor"; break;
                case EngineState.Error: c = Theme.Danger; head = "Sorun var"; break;
                default: c = Theme.Dim; head = "Durduruldu"; break;
            }
            _orb.OrbColor = c;
            _orb.Pulse = s != EngineState.Stopped;
            _lblState.Text = head;
            _lblState.ForeColor = s == EngineState.Stopped ? Theme.Muted : Theme.Text;
            _lblDetail.Text = _engine.StatusDetail;
            _btnToggle.Text = s == EngineState.Stopped ? "Başlat" : "Durdur";
            _btnToggle.Glyph = s == EngineState.Stopped ? "" : "";
            _btnToggle.Primary = s == EngineState.Stopped;
            _btnToggle.Invalidate();
            RefreshInfo();
        }

        private void RefreshInfo()
        {
            if (!Visible) return;
            string ips = string.Join("   ", LocalIPv4().ToArray());
            string info = "Cihaz adı:  " + _settings.DeviceName + "\n" +
                          "IP adresi:  " + (ips.Length > 0 ? ips : "ağ bağlantısı yok") + "\n";
            if (_engine.State == EngineState.Streaming)
            {
                var d = DateTime.Now - _engine.StreamStart;
                info += "Süre:  " + ((int)d.TotalHours).ToString("00") + ":" + d.Minutes.ToString("00") + ":" + d.Seconds.ToString("00");
            }
            else
            {
                info += "Mod:  " + (_engine.SafeMode ? "Güvenli (yazılım)" : (_settings.UltraLowLatency ? "Ultra düşük gecikme" : "Senkron")) +
                        (_settings.Fps60 ? " · 60 FPS" : " · 30 FPS");
            }
            _lblInfo.Text = info;
            _lblChips.Text = "Motor: " + (EngineManager.EngineInstalled ? "kurulu" : "YOK") + "\nBonjour: " + BonjourState();
        }

        private static string _bonjourCache; private static DateTime _bonjourAt;
        private static string BonjourState()
        {
            if (_bonjourCache != null && (DateTime.Now - _bonjourAt).TotalSeconds < 15) return _bonjourCache;
            string r;
            try
            {
                var sc = ServiceController.GetServices().FirstOrDefault(x => x.ServiceName == "Bonjour Service");
                r = sc == null ? "yok (dahili mDNS)" : (sc.Status == ServiceControllerStatus.Running ? "çalışıyor" : "durdu");
            }
            catch { r = "?"; }
            _bonjourCache = r; _bonjourAt = DateTime.Now;
            return r;
        }

        public static System.Collections.Generic.List<string> LocalIPv4()
        {
            var list = new System.Collections.Generic.List<string>();
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback || ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                    string d = (ni.Description + " " + ni.Name).ToLowerInvariant();
                    if (d.Contains("virtual") || d.Contains("vmware") || d.Contains("hyper-v") || d.Contains("vethernet") || d.Contains("wsl")) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        string ip = ua.Address.ToString();
                        if (ip.StartsWith("169.254.") || ip.StartsWith("127.")) continue;
                        list.Add(ip);
                    }
                }
            }
            catch { }
            return list;
        }

        private void AppendLog(string line)
        {
            if (_log.IsDisposed) return;
            if (_log.Lines.Length > 400)
            {
                _log.Select(0, _log.GetFirstCharIndexFromLine(120));
                _log.ReadOnly = false; _log.SelectedText = ""; _log.ReadOnly = true;
            }
            Color c = Theme.Muted;
            string l = line.ToLowerInvariant();
            if (l.Contains("error") || l.Contains("failed") || l.Contains("hata")) c = Theme.Danger;
            else if (l.Contains("[nooxair]")) c = Theme.Accent;
            else if (l.Contains("connection request") || l.Contains("starting mirroring")) c = Theme.Success;
            _log.SelectionStart = _log.TextLength;
            _log.SelectionLength = 0;
            _log.SelectionColor = c;
            _log.AppendText(line + "\n");
            _log.ScrollToCaret();
        }

        // ------------------------------------------------------------------ baslik cubugu
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.Hq(g);
            int th = Theme.S(52);
            var ico = Icon;
            if (ico != null) g.DrawIcon(new Icon(ico, Theme.S(24), Theme.S(24)), new Rectangle(Theme.S(20), Theme.S(15), Theme.S(24), Theme.S(24)));
            using (var f = Theme.Semi(12f))
            using (var br = new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(Theme.S(52), 0, Theme.S(90), th), Theme.Accent, Theme.Accent2, 0f))
                g.DrawString("NooXAIR", f, br, Theme.S(52), Theme.S(14));
            using (var f = Theme.UI(9f))
                TextRenderer.DrawText(g, "AirPlay Alıcısı  ·  v3.0", f, new Point(Theme.S(142), Theme.S(20)), Theme.Dim, TextFormatFlags.NoPadding);

            int bw = Theme.S(46);
            _btnCloseRect = new Rectangle(ClientSize.Width - bw, 0, bw, Theme.S(40));
            _btnMinRect = new Rectangle(ClientSize.Width - bw * 2, 0, bw, Theme.S(40));
            if (_hoverBtn == 0) using (var b = new SolidBrush(Theme.Hover)) g.FillRectangle(b, _btnMinRect);
            if (_hoverBtn == 1) using (var b = new SolidBrush(Theme.Danger)) g.FillRectangle(b, _btnCloseRect);
            using (var f = Theme.Icons(8f))
            {
                TextRenderer.DrawText(g, "", f, _btnMinRect, Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(g, "", f, _btnCloseRect, Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            using (var p = new Pen(Theme.Border)) g.DrawRectangle(p, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = _btnMinRect.Contains(e.Location) ? 0 : _btnCloseRect.Contains(e.Location) ? 1 : -1;
            if (h != _hoverBtn) { _hoverBtn = h; Invalidate(new Rectangle(_btnMinRect.X, 0, _btnMinRect.Width * 2, _btnMinRect.Height)); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (_hoverBtn != -1) { _hoverBtn = -1; Invalidate(); }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (_btnCloseRect.Contains(e.Location)) { Hide(); return; }
                if (_btnMinRect.Contains(e.Location)) { WindowState = FormWindowState.Minimized; return; }
                if (e.Y < Theme.S(52))
                {
                    NativeMethods.ReleaseCapture();
                    NativeMethods.SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
                }
            }
            base.OnMouseDown(e);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); return; }
            base.OnFormClosing(e);
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible) RefreshStatus();
        }

        public void ShowAndActivate()
        {
            if (!Visible) Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            NativeMethods.SetForegroundWindow(Handle);
        }

        protected override void WndProc(ref Message m)
        {
            if ((uint)m.Msg == Program.WM_SHOWME) { ShowAndActivate(); return; }
            base.WndProc(ref m);
        }
    }
}
