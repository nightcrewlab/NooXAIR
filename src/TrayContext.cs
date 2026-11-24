using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NooXAIR
{
    /// <summary>Sistem tepsisi uygulamasi: motoru arka planda surekli hazir tutar.</summary>
    internal sealed class TrayContext : ApplicationContext
    {
        private readonly AppSettings _settings;
        private readonly EngineManager _engine;
        private readonly NotifyIcon _tray;
        private readonly MainForm _form;
        private readonly Icon _baseIcon;
        private readonly Dictionary<EngineState, Icon> _icons = new Dictionary<EngineState, Icon>();
        private ToolStripMenuItem _miStatus, _miToggle;
        private bool _hintShown;
        private EngineState _lastState = EngineState.Stopped;

        public TrayContext(bool startInTray)
        {
            _settings = AppSettings.Load();
            if (_settings.FirstRun)
            {
                // Ilk calistirma: Windows ile otomatik baslatmayi ac
                AppSettings.AutoStartEnabled = true;
                _settings.FirstRun = false;
                _settings.Save();
            }

            try { _baseIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { _baseIcon = SystemIcons.Application; }

            _engine = new EngineManager(_settings);
            _form = new MainForm(_engine, _settings, ExitApp);
            IntPtr forceHandle = _form.Handle; // tek-ornek mesajini alabilmesi icin

            _tray = new NotifyIcon();
            _tray.Icon = StatusIcon(EngineState.Stopped);
            _tray.Text = "NooXAIR";
            _tray.Visible = true;
            _tray.ContextMenuStrip = BuildMenu();
            _tray.MouseClick += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) _form.ShowAndActivate(); };
            _tray.BalloonTipClicked += delegate { _form.ShowAndActivate(); };

            _engine.StateChanged += delegate { if (!_form.IsDisposed) _form.BeginInvoke(new Action(OnStateChanged)); };

            if (_settings.AutoStartEngine) _engine.Start();
            if (!startInTray) _form.Show();
        }

        private ContextMenuStrip BuildMenu()
        {
            var m = new ContextMenuStrip();
            m.Renderer = new DarkMenuRenderer();
            m.ShowImageMargin = false;
            m.Font = Theme.UI(9.5f);
            _miStatus = new ToolStripMenuItem("NooXAIR");
            _miStatus.Enabled = false;
            m.Items.Add(_miStatus);
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(new ToolStripMenuItem("NooXAIR'i aç", null, delegate { _form.ShowAndActivate(); }));
            m.Items.Add(new ToolStripMenuItem("Motoru yeniden başlat", null, delegate { _engine.ResetSafeMode(); _engine.Restart("tepsi menusu"); }));
            _miToggle = new ToolStripMenuItem("Yayını durdur", null, delegate
            {
                if (_engine.State == EngineState.Stopped) _engine.Start(); else _engine.Stop();
            });
            m.Items.Add(_miToggle);
            m.Items.Add(new ToolStripMenuItem("Günlük klasörünü aç", null, delegate
            {
                try { System.Diagnostics.Process.Start("explorer.exe", AppSettings.DataDir); } catch { }
            }));
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(new ToolStripMenuItem("Çıkış", null, delegate { ExitApp(); }));
            foreach (ToolStripItem it in m.Items) it.Padding = new Padding(4, 3, 4, 3);
            return m;
        }

        private void OnStateChanged()
        {
            var s = _engine.State;
            _tray.Icon = StatusIcon(s);
            string tip = "NooXAIR - " + _engine.StatusDetail;
            _tray.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip;
            _miStatus.Text = _engine.StatusDetail;
            _miToggle.Text = s == EngineState.Stopped ? "Yayını başlat" : "Yayını durdur";

            // Yayin sirasinda ekran/sistem uykuya girmesin
            if (s == EngineState.Streaming)
                NativeMethods.SetThreadExecutionState(NativeMethods.ES_CONTINUOUS | NativeMethods.ES_SYSTEM_REQUIRED | NativeMethods.ES_DISPLAY_REQUIRED);
            else
                NativeMethods.SetThreadExecutionState(NativeMethods.ES_CONTINUOUS);

            if (s == EngineState.Ready && !_hintShown && !_form.Visible)
            {
                _hintShown = true;
                _tray.ShowBalloonTip(3000, "NooXAIR hazır", "iPhone > Ekran Yansıtma > \"" + _settings.DeviceName + "\"", ToolTipIcon.None);
            }
            if (s == EngineState.Connected && _lastState != EngineState.Connected && _lastState != EngineState.Streaming && !_form.Visible)
            {
                _tray.ShowBalloonTip(2000, "NooXAIR", "Bağlandı: " + (_engine.ClientName ?? "iPhone"), ToolTipIcon.None);
            }
            _lastState = s;
        }

        private Icon StatusIcon(EngineState s)
        {
            Icon ic;
            if (_icons.TryGetValue(s, out ic)) return ic;
            Color c;
            switch (s)
            {
                case EngineState.Ready: c = Theme.Success; break;
                case EngineState.Connected: c = Theme.Accent; break;
                case EngineState.Streaming: c = Theme.Accent2; break;
                case EngineState.Starting: c = Theme.Warning; break;
                case EngineState.Error: c = Theme.Danger; break;
                default: c = Theme.Dim; break;
            }
            int size = SystemInformation.SmallIconSize.Width;
            using (var bmp = new Bitmap(size, size))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var src = new Icon(_baseIcon, size, size)) g.DrawIcon(src, new Rectangle(0, 0, size, size));
                    float d = size * 0.42f;
                    using (var b = new SolidBrush(Color.FromArgb(11, 14, 20))) g.FillEllipse(b, size - d - 0.5f, size - d - 0.5f, d + 0.5f, d + 0.5f);
                    float d2 = d * 0.72f, off = (d - d2) / 2f;
                    using (var b = new SolidBrush(c)) g.FillEllipse(b, size - d + off - 0.25f, size - d + off - 0.25f, d2, d2);
                }
                ic = Icon.FromHandle(bmp.GetHicon());
            }
            _icons[s] = ic;
            return ic;
        }

        private void ExitApp()
        {
            _tray.Visible = false;
            _engine.Dispose();
            NativeMethods.SetThreadExecutionState(NativeMethods.ES_CONTINUOUS);
            _tray.Dispose();
            _form.Dispose();
            ExitThread();
        }
    }
}
