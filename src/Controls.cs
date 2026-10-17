using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Wincy
{
    /// <summary>Borderless text box that paints its own placeholder.</summary>
    internal sealed class SearchBox : TextBox
    {
        public string Placeholder = string.Empty;
        public Color PlaceholderColor = Color.Gray;

        public SearchBox()
        {
            BorderStyle = BorderStyle.None;
            AutoSize = true;
            TextAlign = HorizontalAlignment.Left;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg != Native.WM_PAINT) return;
            if (TextLength > 0 || string.IsNullOrEmpty(Placeholder)) return;
            if (Focused) return;
            using (Graphics g = CreateGraphics())
            {
                Rectangle bounds = new Rectangle(0, 0, Width, Height);
                TextRenderer.DrawText(g, Placeholder, Font, bounds, PlaceholderColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
                    TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            }
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            Invalidate();
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }
    }

    /// <summary>iOS-style switch used in the settings window.</summary>
    internal sealed class ToggleSwitch : Control
    {
        private bool _checked;
        private bool _hover;
        private Palette _palette = Palette.MakeLight();

        public event EventHandler CheckedChanged;

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            Size = new Size(46, 26);
            TabStop = true;
            Cursor = Cursors.Hand;
        }

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value) return;
                _checked = value;
                Invalidate();
                EventHandler handler = CheckedChanged;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        public void ApplyPalette(Palette palette)
        {
            _palette = palette;
            BackColor = palette.Bg;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            using (SolidBrush back = new SolidBrush(_palette.Bg)) g.FillRectangle(back, ClientRectangle);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle track = new Rectangle(0, 0, Width - 1, Height - 1);
            Color trackColor = _checked ? (_hover ? Ui.Mix(_palette.Accent, Color.Black, 0.08) : _palette.Accent)
                                        : (_hover ? Ui.Mix(_palette.BgHover, _palette.TextFaint, 0.25) : Ui.Mix(_palette.BgHover, _palette.TextFaint, 0.14));
            Ui.FillRound(g, trackColor, track, Height / 2);

            int knob = Height - 6;
            int knobX = _checked ? Width - knob - 3 : 3;
            Rectangle knobRect = new Rectangle(knobX, 3, knob, knob);
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(38, 0, 0, 0)))
            {
                g.FillEllipse(shadow, knobRect.X, knobRect.Y + 1, knobRect.Width, knobRect.Height);
            }
            using (SolidBrush brush = new SolidBrush(Color.White)) g.FillEllipse(brush, knobRect);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) Checked = !Checked;
            base.OnMouseDown(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space)
            {
                Checked = !Checked;
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            Invalidate();
            base.OnGotFocus(e);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            Invalidate();
            base.OnLostFocus(e);
        }
    }

    /// <summary>− [value] + numeric control.</summary>
    internal sealed class Stepper : Control
    {
        private int _value;
        private Palette _palette = Palette.MakeLight();
        private int _hoverZone; // 0 none, 1 minus, 2 plus

        public event EventHandler ValueChanged;

        public int Minimum = 10;
        public int Maximum = 5000;
        public int Step = 25;
        public string Suffix = string.Empty;

        public Stepper()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            Size = new Size(150, 30);
        }

        public int Value
        {
            get { return _value; }
            set
            {
                int clamped = AppSettings.Clamp(value, Minimum, Maximum);
                if (clamped == _value) return;
                _value = clamped;
                Invalidate();
                EventHandler handler = ValueChanged;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        public void SetValueSilent(int value)
        {
            _value = AppSettings.Clamp(value, Minimum, Maximum);
            Invalidate();
        }

        public void ApplyPalette(Palette palette)
        {
            _palette = palette;
            BackColor = palette.Bg;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            using (SolidBrush back = new SolidBrush(_palette.Bg)) g.FillRectangle(back, ClientRectangle);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle track = new Rectangle(0, 0, Width - 1, Height - 1);
            Ui.FillRound(g, _palette.BgInput, track, 8);
            Ui.StrokeRound(g, _palette.Border, track, 8, 1f);

            int zoneWidth = Height;
            Rectangle minusRect = new Rectangle(2, 2, zoneWidth, Height - 4);
            Rectangle plusRect = new Rectangle(Width - zoneWidth - 2, 2, zoneWidth, Height - 4);

            if (_hoverZone == 1) Ui.FillRound(g, _palette.BgHover, minusRect, 6);
            if (_hoverZone == 2) Ui.FillRound(g, _palette.BgHover, plusRect, 6);

            Glyph.Minus(g, minusRect, _value > Minimum ? _palette.Text : _palette.TextFaint, 1.6f);
            Glyph.Plus(g, plusRect, _value < Maximum ? _palette.Text : _palette.TextFaint, 1.6f);

            string label = _value.ToString() + Suffix;
            Rectangle textRect = new Rectangle(zoneWidth, 0, Width - zoneWidth * 2, Height);
            TextRenderer.DrawText(g, label, Font, textRect, _palette.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
                TextFormatFlags.NoPadding);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int zone = HitZone(e.X);
            if (zone != _hoverZone)
            {
                _hoverZone = zone;
                Cursor = zone == 0 ? Cursors.Default : Cursors.Hand;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hoverZone = 0;
            Cursor = Cursors.Default;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                int zone = HitZone(e.X);
                if (zone == 1) Value -= Step;
                else if (zone == 2) Value += Step;
            }
            base.OnMouseDown(e);
        }

        private int HitZone(int x)
        {
            if (x < Height) return 1;
            if (x > Width - Height) return 2;
            return 0;
        }
    }

    /// <summary>Segmented control used for the theme picker.</summary>
    internal sealed class Segmented : Control
    {
        private readonly string[] _labels;
        private readonly string[] _values;
        private int _selected;
        private Palette _palette = Palette.MakeLight();
        private int _hover = -1;

        public event EventHandler SelectionChanged;

        public Segmented(string[] labels, string[] values)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            _labels = labels;
            _values = values;
            Height = 30;
        }

        public string SelectedValue
        {
            get { return _values[_selected]; }
        }

        public int SelectedIndex
        {
            get { return _selected; }
            set
            {
                int index = Math.Max(0, Math.Min(_values.Length - 1, value));
                if (index == _selected) return;
                _selected = index;
                Invalidate();
                EventHandler handler = SelectionChanged;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        public void SelectSilent(string value)
        {
            for (int i = 0; i < _values.Length; i++)
            {
                if (string.Equals(_values[i], value, StringComparison.OrdinalIgnoreCase))
                {
                    _selected = i;
                    Invalidate();
                    return;
                }
            }
        }

        public void ApplyPalette(Palette palette)
        {
            _palette = palette;
            BackColor = palette.Bg;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            using (SolidBrush back = new SolidBrush(_palette.Bg)) g.FillRectangle(back, ClientRectangle);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle track = new Rectangle(0, 0, Width - 1, Height - 1);
            Ui.FillRound(g, _palette.BgInput, track, 8);

            int segment = Width / _labels.Length;
            for (int i = 0; i < _labels.Length; i++)
            {
                Rectangle cell = new Rectangle(i * segment + 2, 2, segment - 4, Height - 4);
                bool selected = i == _selected;
                if (selected) Ui.FillRound(g, _palette.Accent, cell, 6);
                else if (i == _hover) Ui.FillRound(g, _palette.BgHover, cell, 6);
                TextRenderer.DrawText(g, _labels[i], Font, cell,
                    selected ? _palette.OnAccent : _palette.Text,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
                    TextFormatFlags.NoPadding);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int index = IndexAt(e.X);
            if (index != _hover)
            {
                _hover = index;
                Cursor = index >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = -1;
            Cursor = Cursors.Default;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                int index = IndexAt(e.X);
                if (index >= 0) SelectedIndex = index;
            }
            base.OnMouseDown(e);
        }

        private int IndexAt(int x)
        {
            if (x < 0 || x >= Width) return -1;
            return Math.Min(_labels.Length - 1, x / Math.Max(1, Width / _labels.Length));
        }
    }

    /// <summary>Click-to-record keyboard shortcut field.</summary>
    internal sealed class HotkeyBox : Control
    {
        private HotkeySpec _value;
        private bool _recording;
        private bool _hover;
        private Palette _palette = Palette.MakeLight();

        public event EventHandler RecordingStarted;
        public event EventHandler RecordingFinished;
        public event EventHandler ValueChanged;

        public HotkeyBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            Size = new Size(170, 30);
            TabStop = true;
            Cursor = Cursors.Hand;
        }

        public HotkeySpec Value
        {
            get { return _value; }
            set
            {
                _value = value;
                Invalidate();
            }
        }

        public bool Recording
        {
            get { return _recording; }
        }

        public void ApplyPalette(Palette palette)
        {
            _palette = palette;
            BackColor = palette.Bg;
            Invalidate();
        }

        public void BeginRecording()
        {
            if (_recording) return;
            _recording = true;
            Focus();
            EventHandler started = RecordingStarted;
            if (started != null) started(this, EventArgs.Empty);
            Invalidate();
        }

        public void CancelRecording()
        {
            if (!_recording) return;
            _recording = false;
            EventHandler finished = RecordingFinished;
            if (finished != null) finished(this, EventArgs.Empty);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            using (SolidBrush back = new SolidBrush(_palette.Bg)) g.FillRectangle(back, ClientRectangle);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle box = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill = _recording ? _palette.AccentSoft : (_hover ? _palette.BgHover : _palette.BgInput);
            Ui.FillRound(g, fill, box, 8);
            Ui.StrokeRound(g, _recording ? _palette.Accent : _palette.Border, box, 8, 1f);

            string text = _recording
                ? I18n.T("settings.shortcutRecording")
                : (_value == null ? "—" : _value.Pretty);
            Color color = _recording ? _palette.Accent : _palette.Text;
            TextRenderer.DrawText(g, text, Font, new Rectangle(10, 0, Width - 20, Height), color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
                TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) BeginRecording();
            base.OnMouseDown(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (!_recording)
            {
                base.OnKeyDown(e);
                return;
            }
            e.Handled = true;
            e.SuppressKeyPress = true;

            if (e.KeyCode == Keys.Escape)
            {
                CancelRecording();
                return;
            }

            HotkeySpec candidate = HotkeySpec.FromKeyData(e.KeyData);
            if (!candidate.IsValid)
            {
                // Only modifiers so far: keep waiting.
                return;
            }
            _recording = false;
            _value = candidate;
            EventHandler finished = RecordingFinished;
            if (finished != null) finished(this, EventArgs.Empty);
            EventHandler changed = ValueChanged;
            if (changed != null) changed(this, EventArgs.Empty);
            Invalidate();
            Parent.SelectNextControl(this, true, true, true, true);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            if (_recording) CancelRecording();
            Invalidate();
            base.OnLostFocus(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return true;
        }
    }

    internal enum GlyphKind
    {
        Sliders,
        Close,
        ChevronDown,
        Trash,
        Bookmark
    }    /// <summary>Square icon-only button with a hover wash.</summary>
    internal sealed class IconButton : Control
    {
        private Palette _palette = Palette.MakeLight();
        private bool _hover;
        private bool _pressed;

        public GlyphKind Kind = GlyphKind.Close;
        public Color GlyphColor;
        public int Radius = 8;

        public IconButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            Size = new Size(28, 28);
            TabStop = false;
            Cursor = Cursors.Hand;
        }

        public void ApplyPalette(Palette palette)
        {
            _palette = palette;
            BackColor = palette.Bg;
            if (GlyphColor.IsEmpty) GlyphColor = palette.TextMuted;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            using (SolidBrush back = new SolidBrush(_palette.Bg)) g.FillRectangle(back, ClientRectangle);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (_hover || _pressed)
            {
                Color wash = _pressed ? _palette.BgInput : _palette.BgHover;
                Ui.FillRound(g, wash, new Rectangle(0, 0, Width - 1, Height - 1), Radius);
            }
            Rectangle glyphRect = new Rectangle(Width / 5, Height / 5, Width - Width * 2 / 5, Height - Height * 2 / 5);
            switch (Kind)
            {
                case GlyphKind.Sliders:
                    Glyph.Sliders(g, glyphRect, GlyphColor, 1.6f);
                    break;
                case GlyphKind.ChevronDown:
                    Glyph.ChevronDown(g, glyphRect, GlyphColor, 1.6f);
                    break;
                case GlyphKind.Trash:
                    Glyph.Trash(g, glyphRect, GlyphColor, 1.5f);
                    break;
                case GlyphKind.Bookmark:
                    Glyph.Bookmark(g, glyphRect, GlyphColor);
                    break;
                default:
                    Glyph.Close(g, glyphRect, GlyphColor, 1.6f);
                    break;
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            _pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) _pressed = true;
            Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _pressed = false;
            Invalidate();
            base.OnMouseUp(e);
        }
    }
    /// <summary>Text button with primary / plain / danger variants.</summary>
    internal sealed class TextButton : Control
    {
        private Palette _palette = Palette.MakeLight();
        private bool _hover;
        private bool _pressed;

        public string Label = string.Empty;
        public bool Primary;
        public bool Danger;

        public TextButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            SetStyle(ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, false);
            Height = 32;
            TabStop = true;
            Cursor = Cursors.Hand;
            Font = Fonts.Ui(9f);
        }

        public void ApplyPalette(Palette palette)
        {
            _palette = palette;
            BackColor = palette.Bg;
            Invalidate();
        }

        public void FitToLabel()
        {
            using (Graphics g = CreateGraphics())
            {
                Width = Ui.MeasureWidth(g, Font, Label) + Dpi.S(28);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            using (SolidBrush back = new SolidBrush(_palette.Bg)) g.FillRectangle(back, ClientRectangle);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle box = new Rectangle(0, 0, Width - 1, Height - 1);
            Color textColor;
            if (Primary)
            {
                Color fill = _pressed ? Ui.Mix(_palette.Accent, Color.Black, 0.12)
                           : (_hover ? Ui.Mix(_palette.Accent, Color.White, 0.08) : _palette.Accent);
                Ui.FillRound(g, fill, box, 8);
                textColor = _palette.OnAccent;
            }
            else
            {
                if (_pressed) Ui.FillRound(g, _palette.BgInput, box, 8);
                else if (_hover) Ui.FillRound(g, _palette.BgHover, box, 8);
                textColor = Danger ? _palette.Danger : _palette.Text;
            }
            TextRenderer.DrawText(g, Label, Font, box, textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
                TextFormatFlags.NoPadding);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            _pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) _pressed = true;
            Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _pressed = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                OnClick(EventArgs.Empty);
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }
    }
}