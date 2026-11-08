using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;

namespace CourseCapture
{
    /// <summary>
    /// 应用图标：优先使用编译时嵌入的 resources\课件捕手.ico（DeepSeek 鲸鱼），
    /// 只有资源缺失时才退回旧的“蓝底白课”绘制图标。
    /// </summary>
    public static class IconFactory
    {
        private const string ResourceName = "CourseCapture.ico";

        // .NET Framework 4.0 的 Icon 在解码 ICO 里 128px/256px 的 PNG 条目时会抛
        // “请求的范围扩展超过了数组的结尾”，实测最高只能稳定取到 64px。
        // 托盘（16/20/24）和窗口标题栏（32）都用不到更大尺寸；128/256px
        // 条目保留给 Windows 资源管理器，由系统直接从 exe 的 win32 图标资源读取。
        private const int MaxManagedSize = 64;

        public static Icon CreateTrayIcon()
        {
            int size = 16;
            try { size = SystemInformation.SmallIconSize.Width; }
            catch (Exception) { }
            return CreateIcon(size);
        }

        /// <summary>
        /// 从嵌入的 ICO 中挑选最接近 size 的尺寸。返回的 Icon 由调用方负责释放。
        /// </summary>
        public static Icon CreateIcon(int size)
        {
            if (size < 16) size = 16;
            if (size > MaxManagedSize) size = MaxManagedSize;

            try
            {
                Stream stream = typeof(IconFactory).Assembly.GetManifestResourceStream(ResourceName);
                if (stream != null)
                {
                    using (stream)
                    {
                        return new Icon(stream, new Size(size, size));
                    }
                }
            }
            catch (Exception)
            {
                // 资源损坏时继续走 exe 关联图标/绘制兜底。
            }

            try
            {
                Icon associated = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (associated != null)
                {
                    using (associated)
                    {
                        return (Icon)associated.Clone();
                    }
                }
            }
            catch (Exception)
            {
            }

            return CreateFallbackIcon(size);
        }

        public static Bitmap CreateLogo(int size)
        {
            using (Icon icon = CreateIcon(size))
            {
                return icon.ToBitmap();
            }
        }

        private static Icon CreateFallbackIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.Transparent);

                using (GraphicsPath path = RoundedRect(new Rectangle(1, 1, size - 2, size - 2), Math.Max(3, size / 4)))
                using (LinearGradientBrush brush = new LinearGradientBrush(
                    new Rectangle(0, 0, size, size), Color.FromArgb(96, 128, 255), Color.FromArgb(47, 77, 255), 45f))
                {
                    g.FillPath(brush, path);
                }

                using (Font font = new Font("Microsoft YaHei", Math.Max(8f, size * 0.52f), FontStyle.Bold, GraphicsUnit.Pixel))
                using (SolidBrush white = new SolidBrush(Color.White))
                using (StringFormat format = new StringFormat())
                {
                    format.Alignment = StringAlignment.Center;
                    format.LineAlignment = StringAlignment.Center;
                    g.DrawString("课", font, white, new RectangleF(0, 0, size, size), format);
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
