using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;

namespace CourseCapture
{
    public sealed class PdfPage
    {
        public string JpegPath;
        public double DurationSeconds;
    }

    public sealed class SimplePdfWriter
    {
        private readonly List<PdfPage> _pages = new List<PdfPage>();
        private string _title = "";
        private string _subject = "";

        public void SetInfo(string title, string subject)
        {
            _title = title ?? "";
            _subject = subject ?? "";
        }

        public void AddPage(string jpegPath, double durationSeconds)
        {
            if (string.IsNullOrEmpty(jpegPath) || !File.Exists(jpegPath)) return;
            PdfPage p = new PdfPage();
            p.JpegPath = jpegPath;
            p.DurationSeconds = durationSeconds;
            _pages.Add(p);
        }

        public int PageCount
        {
            get { return _pages.Count; }
        }

        public void Save(string targetPath)
        {
            if (_pages.Count == 0) throw new InvalidOperationException("没有可写入 PDF 的画面");
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath));

            int n = _pages.Count;
            int totalObjects = 4 + n * 3;
            long[] offsets = new long[totalObjects + 1];

            using (FileStream fs = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                WriteAscii(fs, "%PDF-1.4\r\n");
                byte[] bom = new byte[] { 0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0D, 0x0A };
                fs.Write(bom, 0, bom.Length);

                offsets[1] = fs.Position;
                WriteAscii(fs, "1 0 obj\r\n<< /Type /Catalog /Pages 2 0 R >>\r\nendobj\r\n");

                offsets[2] = fs.Position;
                StringBuilder kids = new StringBuilder();
                for (int i = 0; i < n; i++)
                {
                    if (i > 0) kids.Append(' ');
                    kids.Append((5 + i * 3).ToString(CultureInfo.InvariantCulture)).Append(" 0 R");
                }
                WriteAscii(fs, "2 0 obj\r\n<< /Type /Pages /Kids [" + kids + "] /Count " +
                    n.ToString(CultureInfo.InvariantCulture) + " >>\r\nendobj\r\n");

                offsets[3] = fs.Position;
                string info = "<< /Title " + PdfText(_title) + " /Author " + PdfText("课件捕手") +
                    " /Subject " + PdfText(_subject) + " /Creator " + PdfText("课件捕手 " + AppPaths.Version) +
                    " /Producer " + PdfText("CourseCapture PDF Writer") +
                    " /CreationDate (" + PdfDate(DateTime.Now) + ") >>";
                WriteAscii(fs, "3 0 obj\r\n" + info + "\r\nendobj\r\n");

                offsets[4] = fs.Position;
                WriteAscii(fs, "4 0 obj\r\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\r\nendobj\r\n");

                for (int i = 0; i < n; i++)
                {
                    PdfPage page = _pages[i];
                    int imageWidth;
                    int imageHeight;
                    using (Image img = Image.FromFile(page.JpegPath))
                    {
                        imageWidth = img.Width;
                        imageHeight = img.Height;
                    }
                    if (imageWidth <= 0 || imageHeight <= 0) throw new InvalidDataException("截图尺寸无效: " + page.JpegPath);

                    double pageWidth = 1024.0;
                    double pageHeight = pageWidth * imageHeight / imageWidth;
                    if (pageHeight > 1400)
                    {
                        pageHeight = 1400;
                        pageWidth = pageHeight * imageWidth / imageHeight;
                    }

                    int pageObj = 5 + i * 3;
                    int contentObj = pageObj + 1;
                    int imageObj = pageObj + 2;

                    offsets[pageObj] = fs.Position;
                    WriteAscii(fs, pageObj.ToString(CultureInfo.InvariantCulture) + " 0 obj\r\n" +
                        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 " + F(pageWidth) + " " + F(pageHeight) + "]" +
                        " /Resources << /XObject << /Im0 " + imageObj.ToString(CultureInfo.InvariantCulture) + " 0 R >>" +
                        " /Font << /F1 4 0 R >> >>" +
                        " /Contents " + contentObj.ToString(CultureInfo.InvariantCulture) + " 0 R" +
                        (page.DurationSeconds > 0.05 ? " /Dur " + F(page.DurationSeconds) : "") +
                        " >>\r\nendobj\r\n");

                    string content = "q\r\n" + F(pageWidth) + " 0 0 " + F(pageHeight) + " 0 0 cm\r\n/Im0 Do\r\nQ\r\n";
                    byte[] contentBytes = Encoding.ASCII.GetBytes(content);
                    offsets[contentObj] = fs.Position;
                    WriteAscii(fs, contentObj.ToString(CultureInfo.InvariantCulture) + " 0 obj\r\n<< /Length " +
                        contentBytes.Length.ToString(CultureInfo.InvariantCulture) + " >>\r\nstream\r\n");
                    fs.Write(contentBytes, 0, contentBytes.Length);
                    WriteAscii(fs, "\r\nendstream\r\nendobj\r\n");

                    byte[] jpeg = File.ReadAllBytes(page.JpegPath);
                    offsets[imageObj] = fs.Position;
                    WriteAscii(fs, imageObj.ToString(CultureInfo.InvariantCulture) + " 0 obj\r\n" +
                        "<< /Type /XObject /Subtype /Image /Width " + imageWidth.ToString(CultureInfo.InvariantCulture) +
                        " /Height " + imageHeight.ToString(CultureInfo.InvariantCulture) +
                        " /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length " +
                        jpeg.Length.ToString(CultureInfo.InvariantCulture) + " >>\r\nstream\r\n");
                    fs.Write(jpeg, 0, jpeg.Length);
                    WriteAscii(fs, "\r\nendstream\r\nendobj\r\n");
                }

                long xref = fs.Position;
                WriteAscii(fs, "xref\r\n0 " + (totalObjects + 1).ToString(CultureInfo.InvariantCulture) + "\r\n");
                WriteAscii(fs, "0000000000 65535 f \r\n");
                for (int i = 1; i <= totalObjects; i++)
                {
                    WriteAscii(fs, offsets[i].ToString("0000000000", CultureInfo.InvariantCulture) + " 00000 n \r\n");
                }
                WriteAscii(fs, "trailer\r\n<< /Size " + (totalObjects + 1).ToString(CultureInfo.InvariantCulture) +
                    " /Root 1 0 R /Info 3 0 R >>\r\nstartxref\r\n" +
                    xref.ToString(CultureInfo.InvariantCulture) + "\r\n%%EOF\r\n");
            }
        }

        private static void WriteAscii(Stream stream, string text)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static string F(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string PdfDate(DateTime time)
        {
            TimeSpan offset = TimeZoneInfo.Local.GetUtcOffset(time);
            string sign = offset.TotalMinutes >= 0 ? "+" : "-";
            int minutes = Math.Abs((int)offset.TotalMinutes);
            return "D:" + time.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) +
                sign + (minutes / 60).ToString("00", CultureInfo.InvariantCulture) + "'" +
                (minutes % 60).ToString("00", CultureInfo.InvariantCulture) + "'";
        }

        private static string PdfText(string text)
        {
            if (text == null) text = "";
            byte[] bytes = Encoding.BigEndianUnicode.GetBytes(text);
            StringBuilder sb = new StringBuilder("<FEFF");
            foreach (byte b in bytes) sb.Append(b.ToString("X2", CultureInfo.InvariantCulture));
            sb.Append('>');
            return sb.ToString();
        }
    }

    public static class CoverRenderer
    {
        public static string Render(string targetJpeg, string title, string subject, DateTime start, DateTime end,
            int frameCount)
        {
            const int width = 1280;
            const int height = 720;
            using (Bitmap bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                using (LinearGradientBrush bg = new LinearGradientBrush(
                    new Rectangle(0, 0, width, height), Color.FromArgb(15, 23, 42), Color.FromArgb(30, 41, 59), 35f))
                {
                    g.FillRectangle(bg, 0, 0, width, height);
                }

                using (SolidBrush accent = new SolidBrush(Color.FromArgb(56, 189, 248)))
                {
                    g.FillRectangle(accent, 72, 74, 10, 110);
                }

                using (Font small = new Font("Microsoft YaHei", 24f, FontStyle.Regular, GraphicsUnit.Pixel))
                using (SolidBrush muted = new SolidBrush(Color.FromArgb(148, 163, 184)))
                {
                    g.DrawString("课堂笔记 · 自动保存", small, muted, 104, 72);
                }

                float titleSize = 54f;
                if (title != null && title.Length > 20) titleSize = 44f;
                if (title != null && title.Length > 36) titleSize = 36f;
                using (Font titleFont = new Font("Microsoft YaHei", titleSize, FontStyle.Bold, GraphicsUnit.Pixel))
                using (SolidBrush white = new SolidBrush(Color.FromArgb(248, 250, 252)))
                {
                    RectangleF titleRect = new RectangleF(100, 118, 1080, 160);
                    StringFormat format = new StringFormat();
                    format.Trimming = StringTrimming.EllipsisCharacter;
                    format.FormatFlags = StringFormatFlags.LineLimit;
                    g.DrawString(title == null ? "课堂" : title, titleFont, white, titleRect, format);
                }

                int y = 320;
                using (Font label = new Font("Microsoft YaHei", 22f, FontStyle.Regular, GraphicsUnit.Pixel))
                using (SolidBrush labelBrush = new SolidBrush(Color.FromArgb(148, 163, 184)))
                using (Font value = new Font("Microsoft YaHei", 30f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (SolidBrush valueBrush = new SolidBrush(Color.FromArgb(226, 232, 240)))
                {
                    g.DrawString("科目", label, labelBrush, 100, y);
                    g.DrawString(string.IsNullOrEmpty(subject) ? "未分类" : subject, value, valueBrush, 220, y - 4);
                    y += 70;
                    g.DrawString("日期", label, labelBrush, 100, y);
                    g.DrawString(start.ToString("yyyy年M月d日  dddd", CultureInfo.GetCultureInfo("zh-CN")), value, valueBrush, 220, y - 4);
                    y += 70;
                    g.DrawString("时间", label, labelBrush, 100, y);
                    g.DrawString(start.ToString("HH:mm") + " — " + end.ToString("HH:mm") + "   （" +
                        FileUtil.FormatDuration(end - start) + "）", value, valueBrush, 220, y - 4);
                    y += 70;
                    g.DrawString("画面", label, labelBrush, 100, y);
                    g.DrawString(frameCount.ToString(CultureInfo.InvariantCulture) + " 个关键画面", value, valueBrush, 220, y - 4);
                }

                using (Pen line = new Pen(Color.FromArgb(51, 65, 85), 1f))
                {
                    g.DrawLine(line, 100, 648, 1180, 648);
                }
                using (Font footer = new Font("Microsoft YaHei", 19f, FontStyle.Regular, GraphicsUnit.Pixel))
                using (SolidBrush footerBrush = new SolidBrush(Color.FromArgb(100, 116, 139)))
                {
                    g.DrawString("由课件捕手自动生成 · 原始截图保留课堂书写过程", footer, footerBrush, 100, 666);
                }

                SaveJpeg(bmp, targetJpeg, 92L);
            }
            return targetJpeg;
        }

        private static void SaveJpeg(Image image, string path, long quality)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            ImageCodecInfo codec = null;
            foreach (ImageCodecInfo c in ImageCodecInfo.GetImageEncoders())
            {
                if (c.FormatID == ImageFormat.Jpeg.Guid) { codec = c; break; }
            }
            if (codec == null)
            {
                image.Save(path, ImageFormat.Jpeg);
                return;
            }
            using (EncoderParameters parameters = new EncoderParameters(1))
            {
                parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
                image.Save(path, codec, parameters);
            }
        }
    }
}
