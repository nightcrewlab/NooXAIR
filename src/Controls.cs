using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace NooXAIR
{
    internal static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(11, 14, 20);
        public static readonly Color Surface = Color.FromArgb(18, 23, 34);
        public static readonly Color Surface2 = Color.FromArgb(25, 32, 47);
        public static readonly Color Hover = Color.FromArgb(32, 41, 60);
        public static readonly Color Border = Color.FromArgb(35, 44, 63);
        public static readonly Color Text = Color.FromArgb(230, 234, 242);
        public static readonly Color Muted = Color.FromArgb(138, 148, 168);
        public static readonly Color Dim = Color.FromArgb(92, 102, 122);
        public static readonly Color Accent = Color.FromArgb(34, 211, 238);
        public static readonly Color Accent2 = Color.FromArgb(139, 92, 246);
        public static readonly Color Success = Color.FromArgb(34, 197, 94);
        public static readonly Color Warning = Color.FromArgb(245, 158, 11);
        public static readonly Color Danger = Color.FromArgb(239, 68, 68);

        public static float Scale = 1f;
        public static int S(int v) { return (int)Math.Round(v * Scale); }

        public static Font UI(float pt) { return new Font("Segoe UI", pt, FontStyle.Regular, GraphicsUnit.Point); }
        public static Font Semi(float pt) { return new Font("Segoe UI Semibold", pt, FontStyle.Regular, GraphicsUnit.Point); }
        public static Font Icons(float pt) { return new Font("Segoe MDL2 Assets", pt, FontStyle.Regular, GraphicsUnit.Point); }

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            if (d <= 0.5f) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Hq(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        }

        public static Color Mix(Color a, Color b, float t)
        {
            return Color.FromArgb((int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }
    }

    /// <summary>Yuvarlatilmis koseli kart</summary>
    internal class Card : Panel
    {
        public string Title;
        public string Glyph;

        public Card()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.Hq(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var path = Theme.Round(r, Theme.S(12)))
            using (var b = new SolidBrush(Theme.Surface))
            using (var pen = new Pen(Theme.Border))
            {
                g.FillPath(b, path);
                g.DrawPath(pen, path);
            }
            if (!string.IsNullOrEmpty(Title))
            {
                int x = Theme.S(18);
                if (!string.IsNullOrEmpty(Glyph))
                {
                    using (var f = Theme.Icons(10.5f))
                        TextRenderer.DrawText(g, Glyph, f, new Point(x, Theme.S(17)), Theme.Accent, TextFormatFlags.NoPadding);
                    x += Theme.S(24);
                }
                using (var f = Theme.Semi(10.5f))
                    TextRenderer.DrawText(g, Title, f, new Point(x, Theme.S(14)), Theme.Text, TextFormatFlags.NoPadding);
            }
        }
    }

    /// <summary>Ozel cizilmis, animasyonlu acma/kapama anahtari</summary>
    internal class ToggleSwitch : Control
    {
        private bool _checked;
        private float _anim;
        private readonly Timer _t = new Timer();
        private bool _hover;
        public event EventHandler CheckedChanged;
        public string Description;

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            Cursor = Cursors.Hand;
            _t.Interval = 15;
            _t.Tick += delegate
            {
                float target = _checked ? 1f : 0f;
                _anim += (target - _anim) * 0.35f;
                if (Math.Abs(target - _anim) < 0.02f) { _anim = target; _t.Stop(); }
                Invalidate();
            };
            Height = Theme.S(46);
        }

        public bool Checked
        {
            get { return _checked; }
            set { if (_checked == value) return; _checked = value; _t.Start(); var h = CheckedChanged; if (h != null) h(this, EventArgs.Empty); }
        }

        public void SetSilently(bool v) { _checked = v; _anim = v ? 1 : 0; Invalidate(); }

        protected override void OnClick(EventArgs e) { Checked = !Checked; base.OnClick(e); }
        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.Hq(g);
            g.Clear(Parent != null ? Theme.Surface : Theme.Bg);
            int sw = Theme.S(40), sh = Theme.S(22);
            var pill = new RectangleF(Width - sw - 1, (Height - sh) / 2f, sw, sh);
            Color track = Theme.Mix(_hover ? Theme.Hover : Theme.Surface2, Theme.Accent, _anim);
            using (var p = Theme.Round(pill, sh / 2f))
            {
                using (var b = new SolidBrush(track)) g.FillPath(b, p);
                if (_anim < 0.99f) using (var pen = new Pen(Theme.Mix(Theme.Border, Theme.Accent, _anim))) g.DrawPath(pen, p);
            }
            float kd = sh - Theme.S(6);
            float kx = pill.X + Theme.S(3) + (sw - kd - Theme.S(6)) * _anim;
            using (var b = new SolidBrush(Theme.Mix(Theme.Muted, Color.FromArgb(10, 14, 22), _anim)))
                g.FillEllipse(b, kx, pill.Y + Theme.S(3), kd, kd);

            using (var f = Theme.UI(9.75f))
                TextRenderer.DrawText(g, Text, f, new Point(0, string.IsNullOrEmpty(Description) ? (Height - f.Height) / 2 : Theme.S(5)), Theme.Text, TextFormatFlags.NoPadding);
            if (!string.IsNullOrEmpty(Description))
                using (var f = Theme.UI(8.25f))
                    TextRenderer.DrawText(g, Description, f, new Point(0, Theme.S(25)), Theme.Muted, TextFormatFlags.NoPadding);
        }
    }

    /// <summary>Modern buton (birincil = gradient, ikincil = yuzey)</summary>
    internal class ModernButton : Control
    {
        public bool Primary;
        public string Glyph;
        private bool _hover, _down;

        public ModernButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            Cursor = Cursors.Hand;
            Height = Theme.S(38);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.Hq(g);
            g.Clear(Theme.Surface);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var path = Theme.Round(r, Theme.S(9)))
            {
                Color text = Theme.Text;
                if (Primary && Enabled)
                {
                    Color a = Theme.Accent, b = Theme.Accent2;
                    if (_hover) { a = Theme.Mix(a, Color.White, 0.12f); b = Theme.Mix(b, Color.White, 0.12f); }
                    if (_down) { a = Theme.Mix(a, Color.Black, 0.15f); b = Theme.Mix(b, Color.Black, 0.15f); }
                    using (var br = new LinearGradientBrush(new RectangleF(0, 0, Width, Height), a, b, 0f)) g.FillPath(br, path);
                    text = Color.FromArgb(8, 11, 18);
                }
                else
                {
                    Color fill = _down ? Theme.Border : _hover ? Theme.Hover : Theme.Surface2;
                    using (var br = new SolidBrush(fill)) g.FillPath(br, path);
                    using (var pen = new Pen(Theme.Border)) g.DrawPath(pen, path);
                    if (!Enabled) text = Theme.Dim;
                }

                using (var f = Theme.Semi(9.5f))
                using (var fi = Theme.Icons(10f))
                {
                    Size ts = TextRenderer.MeasureText(g, Text, f, Size.Empty, TextFormatFlags.NoPadding);
                    int gw = string.IsNullOrEmpty(Glyph) ? 0 : Theme.S(22);
                    int x = (Width - ts.Width - gw) / 2;
                    if (gw > 0) TextRenderer.DrawText(g, Glyph, fi, new Point(x, (Height - fi.Height) / 2 + Theme.S(1)), text, TextFormatFlags.NoPadding);
                    TextRenderer.DrawText(g, Text, f, new Point(x + gw, (Height - ts.Height) / 2), text, TextFormatFlags.NoPadding);
                }
            }
        }
    }

    /// <summary>Acilir secim kutusu (tamamen koyu tema, ContextMenu tabanli)</summary>
    internal class DropSelect : Control
    {
        public string Label;
        private string[] _keys = new string[0];
        private string[] _texts = new string[0];
        private int _index;
        private bool _hover;
        public event EventHandler SelectionChanged;

        public DropSelect()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            Cursor = Cursors.Hand;
            Height = Theme.S(58);
        }

        public void SetItems(string[] keys, string[] texts) { _keys = keys; _texts = texts; Invalidate(); }

        public string SelectedKey
        {
            get { return _index >= 0 && _index < _keys.Length ? _keys[_index] : null; }
            set { int i = Array.IndexOf(_keys, value); _index = i < 0 ? 0 : i; Invalidate(); }
        }

        private RectangleF BoxRect { get { return new RectangleF(0.5f, Theme.S(22), Width - 1.5f, Height - Theme.S(22) - 1.5f); } }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            var menu = new ContextMenuStrip();
            menu.Renderer = new DarkMenuRenderer();
            menu.ShowImageMargin = false;
            menu.Font = Theme.UI(9.5f);
            for (int i = 0; i < _texts.Length; i++)
            {
                int idx = i;
                var it = new ToolStripMenuItem(_texts[i]);
                it.ForeColor = i == _index ? Theme.Accent : Theme.Text;
                it.AutoSize = false;
                it.Size = new Size(Width - Theme.S(4), Theme.S(32));
                it.Click += delegate
                {
                    if (_index != idx) { _index = idx; Invalidate(); var h = SelectionChanged; if (h != null) h(this, EventArgs.Empty); }
                };
                menu.Items.Add(it);
            }
            menu.Closed += delegate { menu.BeginInvoke(new Action(menu.Dispose)); };
            menu.Show(this, new Point(0, Height + Theme.S(2)));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.Hq(g);
            g.Clear(Theme.Surface);
            using (var f = Theme.UI(8.5f))
                TextRenderer.DrawText(g, Label, f, new Point(0, 0), Theme.Muted, TextFormatFlags.NoPadding);
            var r = BoxRect;
            using (var p = Theme.Round(r, Theme.S(8)))
            using (var b = new SolidBrush(_hover ? Theme.Hover : Theme.Surface2))
            using (var pen = new Pen(_hover ? Theme.Mix(Theme.Border, Theme.Accent, 0.5f) : Theme.Border))
            {
                g.FillPath(b, p); g.DrawPath(pen, p);
            }
            string txt = _index >= 0 && _index < _texts.Length ? _texts[_index] : "";
            using (var f = Theme.UI(9.5f))
                TextRenderer.DrawText(g, txt, f, new Rectangle((int)r.X + Theme.S(12), (int)r.Y, (int)r.Width - Theme.S(40), (int)r.Height), Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            using (var f = Theme.Icons(8f))
                TextRenderer.DrawText(g, "", f, new Rectangle((int)r.Right - Theme.S(30), (int)r.Y, Theme.S(22), (int)r.Height), Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
        }
    }

    /// <summary>Koyu tema TextBox kapsayicisi</summary>
    internal class DarkTextBox : Control
    {
        public readonly TextBox Box = new TextBox();
        public string Label;

        public DarkTextBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Height = Theme.S(58);
            Box.BorderStyle = BorderStyle.None;
            Box.BackColor = Theme.Surface2;
            Box.ForeColor = Theme.Text;
            Box.Font = Theme.UI(10f);
            Box.MaxLength = 40;
            Controls.Add(Box);
            Box.GotFocus += delegate { Invalidate(); };
            Box.LostFocus += delegate { Invalidate(); };
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            int top = Theme.S(22);
            Box.Location = new Point(Theme.S(12), top + (Height - top - Box.Height) / 2);
            Box.Width = Width - Theme.S(24);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.Hq(g);
            g.Clear(Theme.Surface);
            using (var f = Theme.UI(8.5f))
                TextRenderer.DrawText(g, Label, f, new Point(0, 0), Theme.Muted, TextFormatFlags.NoPadding);
            var r = new RectangleF(0.5f, Theme.S(22), Width - 1.5f, Height - Theme.S(22) - 1.5f);
            using (var p = Theme.Round(r, Theme.S(8)))
            using (var b = new SolidBrush(Theme.Surface2))
            using (var pen = new Pen(Box.Focused ? Theme.Accent : Theme.Border))
            {
                g.FillPath(b, p); g.DrawPath(pen, p);
            }
        }
    }

    /// <summary>Durum gostergesi: nabiz atan halka</summary>
    internal class StatusOrb : Control
    {
        private Color _color = Theme.Dim;
        private float _phase;
        private readonly Timer _t = new Timer();
        public bool Pulse = true;

        public StatusOrb()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            _t.Interval = 33;
            _t.Tick += delegate { _phase += 0.02f; if (_phase > 1) _phase -= 1; Invalidate(); };
            _t.Start();
        }

        public Color OrbColor { get { return _color; } set { _color = value; Invalidate(); } }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible) _t.Start(); else _t.Stop();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; Theme.Hq(g);
            g.Clear(Theme.Surface);
            float c = Width / 2f, cy = Height / 2f;
            float core = Width * 0.22f;
            if (Pulse)
            {
                for (int i = 0; i < 2; i++)
                {
                    float ph = (_phase + i * 0.5f) % 1f;
                    float rr = core + (Width / 2f - core - 1) * ph;
                    int alpha = (int)(110 * (1 - ph));
                    using (var pen = new Pen(Color.FromArgb(alpha, _color), Theme.S(2)))
                        g.DrawEllipse(pen, c - rr, cy - rr, rr * 2, rr * 2);
                }
            }
            using (var glow = new GraphicsPath())
            {
                float gr = core * 1.9f;
                glow.AddEllipse(c - gr, cy - gr, gr * 2, gr * 2);
                using (var pb = new PathGradientBrush(glow))
                {
                    pb.CenterColor = Color.FromArgb(90, _color);
                    pb.SurroundColors = new[] { Color.FromArgb(0, _color) };
                    g.FillPath(pb, glow);
                }
            }
            using (var b = new SolidBrush(_color)) g.FillEllipse(b, c - core, cy - core, core * 2, core * 2);
            using (var f = ThemeFontExt.Icons(core * 0.95f, true))
                TextRenderer.DrawText(g, "", f, new Rectangle(0, 0, Width, Height), Color.FromArgb(10, 14, 22), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    internal static class ThemeFontExt
    {
        public static Font Icons(float px, bool pixel) { return new Font("Segoe MDL2 Assets", Math.Max(6f, px), FontStyle.Regular, GraphicsUnit.Pixel); }
    }

    internal class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColors()) { RoundedEdges = false; }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (e.Item.ForeColor != Theme.Accent) e.TextColor = e.Item.Enabled ? Theme.Text : Theme.Dim;
            e.TextRectangle = new Rectangle(e.TextRectangle.X + Theme.S(4), e.TextRectangle.Y, e.TextRectangle.Width, e.TextRectangle.Height);
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e) { e.ArrowColor = Theme.Muted; base.OnRenderArrow(e); }

        private class DarkColors : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground { get { return Theme.Surface2; } }
            public override Color MenuBorder { get { return Theme.Border; } }
            public override Color MenuItemBorder { get { return Theme.Hover; } }
            public override Color MenuItemSelected { get { return Theme.Hover; } }
            public override Color MenuItemSelectedGradientBegin { get { return Theme.Hover; } }
            public override Color MenuItemSelectedGradientEnd { get { return Theme.Hover; } }
            public override Color MenuItemPressedGradientBegin { get { return Theme.Hover; } }
            public override Color MenuItemPressedGradientEnd { get { return Theme.Hover; } }
            public override Color ImageMarginGradientBegin { get { return Theme.Surface2; } }
            public override Color ImageMarginGradientMiddle { get { return Theme.Surface2; } }
            public override Color ImageMarginGradientEnd { get { return Theme.Surface2; } }
            public override Color SeparatorDark { get { return Theme.Border; } }
            public override Color SeparatorLight { get { return Theme.Border; } }
        }
    }
}
