using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace CourseCapture
{
    public static class IconFactory
    {
        public static Icon CreateTrayIcon()
        {
            using (Bitmap bmp = new Bitmap(32, 32))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.Transparent);

                using (GraphicsPath path = RoundedRect(new Rectangle(1, 1, 30, 30), 8))
                using (LinearGradientBrush brush = new LinearGradientBrush(
                    new Rectangle(0, 0, 32, 32), Color.FromArgb(14, 116, 144), Color.FromArgb(30, 64, 175), 45f))
                {
                    g.FillPath(brush, path);
                }

                using (Font font = new Font("Microsoft YaHei", 17f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (SolidBrush white = new SolidBrush(Color.White))
                using (StringFormat format = new StringFormat())
                {
                    format.Alignment = StringAlignment.Center;
                    format.LineAlignment = StringAlignment.Center;
                    RectangleF rect = new RectangleF(0, -1, 32, 32);
                    g.DrawString("课", font, white, rect, format);
                }

                IntPtr handle = bmp.GetHicon();
                try
                {
                    using (Icon temporary = Icon.FromHandle(handle))
                    {
                        return (Icon)temporary.Clone();
                    }
                }
                finally
                {
                    NativeMethods.DestroyIcon(handle);
                }
            }
        }

        public static Bitmap CreateLogo(int size)
        {
            Bitmap bmp = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (GraphicsPath path = RoundedRect(new Rectangle(1, 1, size - 2, size - 2), size / 4))
                using (LinearGradientBrush brush = new LinearGradientBrush(
                    new Rectangle(0, 0, size, size), Color.FromArgb(14, 116, 144), Color.FromArgb(30, 64, 175), 45f))
                {
                    g.FillPath(brush, path);
                }
                using (Font font = new Font("Microsoft YaHei", size * 0.52f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (SolidBrush white = new SolidBrush(Color.White))
                using (StringFormat format = new StringFormat())
                {
                    format.Alignment = StringAlignment.Center;
                    format.LineAlignment = StringAlignment.Center;
                    g.DrawString("课", font, white, new RectangleF(0, 0, size, size), format);
                }
            }
            return bmp;
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
