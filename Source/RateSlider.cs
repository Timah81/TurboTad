using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TurboTad
{
    // A lightweight presentation control. The NumericUpDown is authoritative;
    // this slider never changes persistence, IPC serialization or flight physics.
    internal sealed class RateSlider : Control
    {
        private readonly int minimum;
        private readonly int maximum;
        private readonly int step;
        private int current;
        private bool dragging;
        private static readonly Color Track = Color.FromArgb(69, 73, 79);
        private static readonly Color Accent = Color.FromArgb(255, 143, 40);

        internal event EventHandler ValueChanged;

        internal RateSlider(int minimum, int maximum, int step, int initial)
        {
            if (maximum <= minimum || step <= 0) throw new ArgumentException("Invalid slider limits");
            this.minimum = minimum;
            this.maximum = maximum;
            this.step = step;
            current = Snap(initial);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                ControlStyles.Selectable, true);
            TabStop = true;
            Cursor = Cursors.Hand;
            BackColor = Color.FromArgb(37, 40, 44);
            Height = 32;
            AccessibleRole = AccessibleRole.Slider;
        }

        internal int Value
        {
            get { return current; }
            set
            {
                int revised = Snap(value);
                if (revised == current) return;
                current = revised;
                Invalidate();
                EventHandler changed = ValueChanged;
                if (changed != null) changed(this, EventArgs.Empty);
            }
        }

        private int Snap(int value)
        {
            value = Math.Max(minimum, Math.Min(maximum, value));
            int snapped = minimum + (int)Math.Round((value - minimum) / (double)step,
                MidpointRounding.AwayFromZero) * step;
            return Math.Max(minimum, Math.Min(maximum, snapped));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            const int margin = 13;
            int left = margin;
            int right = Math.Max(left + 1, Width - margin);
            int y = Height / 2;
            int x = left + (int)Math.Round((right - left) * (current - minimum) / (double)(maximum - minimum));
            using (Pen track = new Pen(Track, 7))
            using (Pen filled = new Pen(Accent, 7))
            {
                track.StartCap = LineCap.Round;
                track.EndCap = LineCap.Round;
                filled.StartCap = LineCap.Round;
                filled.EndCap = LineCap.Round;
                e.Graphics.DrawLine(track, left, y, right, y);
                if (x > left) e.Graphics.DrawLine(filled, left, y, x, y);
            }
            using (Brush knob = new SolidBrush(Accent))
                e.Graphics.FillEllipse(knob, x - 9, y - 9, 18, 18);
            using (Pen outline = new Pen(Color.FromArgb(20, 20, 20), 1.4F))
                e.Graphics.DrawEllipse(outline, x - 9, y - 9, 18, 18);
            if (Focused)
                using (Pen focusPen = new Pen(Color.FromArgb(168, 168, 168)))
                    e.Graphics.DrawRectangle(focusPen, 1, 1, Width - 3, Height - 3);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Left || keyData == Keys.Right ||
                keyData == Keys.Up || keyData == Keys.Down ||
                keyData == Keys.Home || keyData == Keys.End) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Down) Value = current - step;
            else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Up) Value = current + step;
            else if (e.KeyCode == Keys.Home) Value = minimum;
            else if (e.KeyCode == Keys.End) Value = maximum;
            else return;
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private void SetFromMouse(int position)
        {
            const int margin = 13;
            int width = Math.Max(1, Width - 2 * margin);
            double fraction = Math.Max(0, Math.Min(1, (position - margin) / (double)width));
            Value = minimum + (int)Math.Round((maximum - minimum) * fraction);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            dragging = true;
            Focus();
            Capture = true;
            SetFromMouse(e.X);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging) SetFromMouse(e.X);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!dragging) return;
            dragging = false;
            Capture = false;
            SetFromMouse(e.X);
        }
        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            dragging = false;
        }
    }
}
