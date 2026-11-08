using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace CourseCapture
{
    public static class ScreenCapturer
    {
        public static Bitmap Capture(MonitoredWindow window, bool foreground)
        {
            if (window == null || window.Handle == IntPtr.Zero) return null;

            NativeMethods.RECT client = GetClientScreenRect(window.Handle);
            if (client.Width < 80 || client.Height < 60)
            {
                client = window.Bounds;
            }

            Bitmap bitmap = null;
            if (foreground)
            {
                bitmap = CopyScreen(client);
                if (bitmap != null && IsNearlyUniform(bitmap))
                {
                    Bitmap printed = PrintWindow(window.Handle, client);
                    if (printed != null)
                    {
                        bitmap.Dispose();
                        bitmap = printed;
                    }
                }
            }
            else
            {
                bitmap = PrintWindow(window.Handle, client);
                if (bitmap == null) bitmap = CopyScreen(client);
            }

            return bitmap;
        }

        public static NativeMethods.RECT GetClientScreenRect(IntPtr hwnd)
        {
            NativeMethods.RECT rect = new NativeMethods.RECT();
            NativeMethods.RECT client;
            if (!NativeMethods.GetClientRect(hwnd, out client)) return rect;
            NativeMethods.POINT p = new NativeMethods.POINT();
            p.X = 0;
            p.Y = 0;
            if (!NativeMethods.ClientToScreen(hwnd, ref p)) return rect;
            rect.Left = p.X;
            rect.Top = p.Y;
            rect.Right = p.X + client.Width;
            rect.Bottom = p.Y + client.Height;
            return rect;
        }

        private static Bitmap CopyScreen(NativeMethods.RECT rect)
        {
            try
            {
                int left = rect.Left;
                int top = rect.Top;
                int width = rect.Width;
                int height = rect.Height;
                int vx = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
                int vy = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
                int vw = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
                int vh = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);
                if (vw <= 0 || vh <= 0) return null;

                int x1 = Math.Max(left, vx);
                int y1 = Math.Max(top, vy);
                int x2 = Math.Min(left + width, vx + vw);
                int y2 = Math.Min(top + height, vy + vh);
                if (x2 - x1 < 80 || y2 - y1 < 60) return null;

                Bitmap bmp = new Bitmap(x2 - x1, y2 - y1, PixelFormat.Format24bppRgb);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(x1, y1, 0, 0, new Size(x2 - x1, y2 - y1), CopyPixelOperation.SourceCopy);
                }
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        private static Bitmap PrintWindow(IntPtr hwnd, NativeMethods.RECT client)
        {
            NativeMethods.RECT windowRect;
            if (!NativeMethods.GetWindowRect(hwnd, out windowRect)) return null;
            int width = windowRect.Width;
            int height = windowRect.Height;
            if (width < 80 || height < 60) return null;

            Bitmap full = null;
            try
            {
                full = new Bitmap(width, height, PixelFormat.Format24bppRgb);
                using (Graphics g = Graphics.FromImage(full))
                {
                    g.Clear(Color.Black);
                    IntPtr hdc = g.GetHdc();
                    bool ok = false;
                    try
                    {
                        ok = NativeMethods.PrintWindow(hwnd, hdc, NativeMethods.PW_RENDERFULLCONTENT);
                        if (!ok) ok = NativeMethods.PrintWindow(hwnd, hdc, 0);
                    }
                    finally
                    {
                        g.ReleaseHdc(hdc);
                    }
                    if (!ok) return null;
                }

                int offsetX = client.Left - windowRect.Left;
                int offsetY = client.Top - windowRect.Top;
                int cropX = Math.Max(0, Math.Min(offsetX, width - 1));
                int cropY = Math.Max(0, Math.Min(offsetY, height - 1));
                int cropW = Math.Min(client.Width, width - cropX);
                int cropH = Math.Min(client.Height, height - cropY);
                if (cropW < 80 || cropH < 60) return full;

                Bitmap cropped = new Bitmap(cropW, cropH, PixelFormat.Format24bppRgb);
                using (Graphics g = Graphics.FromImage(cropped))
                {
                    g.DrawImage(full, new Rectangle(0, 0, cropW, cropH),
                        new Rectangle(cropX, cropY, cropW, cropH), GraphicsUnit.Pixel);
                }
                full.Dispose();
                return cropped;
            }
            catch
            {
                if (full != null) full.Dispose();
                return null;
            }
        }

        public static bool IsNearlyUniform(Bitmap bitmap)
        {
            try
            {
                int min = 255;
                int max = 0;
                long total = 0;
                long count = 0;
                int stepX = Math.Max(1, bitmap.Width / 24);
                int stepY = Math.Max(1, bitmap.Height / 16);
                for (int y = 0; y < bitmap.Height; y += stepY)
                {
                    for (int x = 0; x < bitmap.Width; x += stepX)
                    {
                        Color c = bitmap.GetPixel(x, y);
                        int l = (c.R * 30 + c.G * 59 + c.B * 11) / 100;
                        if (l < min) min = l;
                        if (l > max) max = l;
                        total += l;
                        count++;
                    }
                }
                if (count == 0) return true;
                int avg = (int)(total / count);
                return (max - min) < 5 || (avg < 4 && max < 12);
            }
            catch
            {
                return false;
            }
        }

        public static void SaveJpeg(Bitmap bitmap, string path, int maxWidth, long quality)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            Bitmap output = bitmap;
            Bitmap resized = null;
            try
            {
                if (bitmap.Width > maxWidth)
                {
                    int w = maxWidth;
                    int h = (int)Math.Round(bitmap.Height * (double)maxWidth / bitmap.Width);
                    if (h < 2) h = 2;
                    resized = new Bitmap(w, h, PixelFormat.Format24bppRgb);
                    using (Graphics g = Graphics.FromImage(resized))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.DrawImage(bitmap, 0, 0, w, h);
                    }
                    output = resized;
                }

                ImageCodecInfo codec = null;
                foreach (ImageCodecInfo c in ImageCodecInfo.GetImageEncoders())
                {
                    if (c.FormatID == ImageFormat.Jpeg.Guid) { codec = c; break; }
                }
                if (codec == null)
                {
                    output.Save(path, ImageFormat.Jpeg);
                }
                else
                {
                    using (EncoderParameters parameters = new EncoderParameters(1))
                    {
                        parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
                        output.Save(path, codec, parameters);
                    }
                }
            }
            finally
            {
                if (resized != null) resized.Dispose();
            }
        }
    }

    public static class ImageFingerprint
    {
        private const int Width = 64;
        private const int Height = 36;

        public static byte[] FromBitmap(Bitmap bitmap)
        {
            byte[] result = new byte[Width * Height];
            using (Bitmap thumb = new Bitmap(Width, Height, PixelFormat.Format24bppRgb))
            {
                using (Graphics g = Graphics.FromImage(thumb))
                {
                    g.Clear(Color.Black);
                    g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    g.DrawImage(bitmap, 0, 0, Width, Height);
                }
                BitmapData data = thumb.LockBits(new Rectangle(0, 0, Width, Height),
                    ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                try
                {
                    int stride = data.Stride;
                    byte[] buffer = new byte[stride * Height];
                    Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);
                    for (int y = 0; y < Height; y++)
                    {
                        for (int x = 0; x < Width; x++)
                        {
                            int offset = y * stride + x * 3;
                            int b = buffer[offset];
                            int gg = buffer[offset + 1];
                            int r = buffer[offset + 2];
                            result[y * Width + x] = (byte)((r * 30 + gg * 59 + b * 11) / 100);
                        }
                    }
                }
                finally
                {
                    thumb.UnlockBits(data);
                }
            }
            return result;
        }

        public static bool IsDifferent(byte[] a, byte[] b, out double changedRatio)
        {
            changedRatio = 0;
            if (a == null || b == null || a.Length != b.Length) return true;
            int changed = 0;
            foreach (int diff in Differences(a, b))
            {
                if (diff > 10) changed++;
            }
            changedRatio = changed / (double)a.Length;
            return changedRatio >= 0.0035;
        }

        private static System.Collections.Generic.IEnumerable<int> Differences(byte[] a, byte[] b)
        {
            for (int i = 0; i < a.Length; i++)
            {
                yield return Math.Abs(a[i] - b[i]);
            }
        }
    }
}
