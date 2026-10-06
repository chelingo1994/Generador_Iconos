using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace GeneradorIconos
{
    /// <summary>Sub-icono que se dibuja encima de la imagen base.</summary>
    public enum BadgeKind
    {
        Agregar,   // círculo verde con el "+"
        Editar,    // lápiz
        Imprimir   // impresora
    }

    public enum BadgeCorner
    {
        InferiorDerecha,
        InferiorIzquierda,
        SuperiorDerecha,
        SuperiorIzquierda
    }

    public class BadgeOptions
    {
        /// <summary>Lado del PNG de salida en píxeles. 0 = mismo tamaño que la imagen original.</summary>
        public int OutputSize = 0;

        /// <summary>Tamaño del sub-icono, en % del lado de la imagen.</summary>
        public float BadgePercent = 42f;

        /// <summary>Separación del borde, en % del lado de la imagen.</summary>
        public float MarginXPercent = 6f;
        public float MarginYPercent = 0f;

        public BadgeCorner Corner = BadgeCorner.InferiorDerecha;
    }

    /// <summary>
    /// Toda la lógica de dibujo. No depende del formulario, así que se puede
    /// copiar tal cual a otro proyecto (consola, WPF, etc.).
    /// Los sub-iconos se dibujan como vectores, por eso salen nítidos a cualquier tamaño.
    /// </summary>
    public static class BadgeRenderer
    {
        public static string Suffix(BadgeKind kind)
        {
            switch (kind)
            {
                case BadgeKind.Agregar: return "_agregar";
                case BadgeKind.Editar: return "_editar";
                default: return "_imprimir";
            }
        }

        /// <summary>Carga una imagen sin dejar el archivo bloqueado en disco.</summary>
        public static Bitmap LoadImage(string path)
        {
            byte[] data = System.IO.File.ReadAllBytes(path);
            using (var ms = new System.IO.MemoryStream(data))
            {
                if (path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                {
                    using (var ico = new Icon(ms, new Size(256, 256)))
                    using (var bmp = ico.ToBitmap())
                        return ToArgb(bmp);
                }
                using (var img = Image.FromStream(ms))
                    return ToArgb(img);
            }
        }

        /// <summary>Devuelve la imagen base con el sub-icono ya aplicado (32 bits con transparencia).</summary>
        public static Bitmap Compose(Bitmap baseImage, BadgeKind kind, BadgeOptions opt)
        {
            Bitmap canvas = PrepareBase(baseImage, opt.OutputSize);
            int side = Math.Min(canvas.Width, canvas.Height);

            int badgeSize = Math.Max(4, (int)Math.Round(side * opt.BadgePercent / 100f));
            badgeSize = Math.Min(badgeSize, side);
            int mx = (int)Math.Round(canvas.Width * opt.MarginXPercent / 100f);
            int my = (int)Math.Round(canvas.Height * opt.MarginYPercent / 100f);

            bool right = opt.Corner == BadgeCorner.InferiorDerecha || opt.Corner == BadgeCorner.SuperiorDerecha;
            bool bottom = opt.Corner == BadgeCorner.InferiorDerecha || opt.Corner == BadgeCorner.InferiorIzquierda;
            int x = right ? canvas.Width - badgeSize - mx : mx;
            int y = bottom ? canvas.Height - badgeSize - my : my;

            using (Bitmap badge = RenderBadge(kind, badgeSize))
            using (Graphics g = Graphics.FromImage(canvas))
            {
                g.CompositingMode = CompositingMode.SourceOver;
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(badge, new Rectangle(x, y, badgeSize, badgeSize),
                    0, 0, badgeSize, badgeSize, GraphicsUnit.Pixel);
            }
            return canvas;
        }

        /// <summary>Copia la base a 32 bits y, si se pidió, la lleva a un lienzo cuadrado del tamaño indicado.</summary>
        public static Bitmap PrepareBase(Bitmap src, int outputSize)
        {
            if (outputSize <= 0 || (src.Width == outputSize && src.Height == outputSize))
                return ToArgb(src);

            var canvas = new Bitmap(outputSize, outputSize, PixelFormat.Format32bppArgb);
            canvas.SetResolution(96, 96);
            float scale = Math.Min(outputSize / (float)src.Width, outputSize / (float)src.Height);
            float w = src.Width * scale, h = src.Height * scale;
            using (Graphics g = Graphics.FromImage(canvas))
            using (var attrs = new ImageAttributes())
            {
                attrs.SetWrapMode(WrapMode.TileFlipXY);   // evita bordes oscuros al escalar
                g.Clear(Color.Transparent);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                var dest = new Rectangle(
                    (int)Math.Round((outputSize - w) / 2f), (int)Math.Round((outputSize - h) / 2f),
                    (int)Math.Round(w), (int)Math.Round(h));
                g.DrawImage(src, dest, 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, attrs);
            }
            return canvas;
        }

        /// <summary>Dibuja solo el sub-icono, con fondo transparente, en un cuadrado de sizePx píxeles.</summary>
        public static Bitmap RenderBadge(BadgeKind kind, int sizePx)
        {
            // Se dibuja más grande y después se reduce promediando píxeles:
            // así los bordes quedan suaves incluso en iconos de 16 o 24 px.
            int ss = sizePx <= 32 ? 8 : 4;
            using (var big = new Bitmap(sizePx * ss, sizePx * ss, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(big))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    // Todos los sub-iconos están diseñados en una grilla de 100 x 100 unidades.
                    g.ScaleTransform(big.Width / 100f, big.Height / 100f);
                    switch (kind)
                    {
                        case BadgeKind.Agregar: DrawPlus(g); break;
                        case BadgeKind.Editar: DrawPencil(g); break;
                        default: DrawPrinter(g); break;
                    }
                }
                return Downsample(big, ss);
            }
        }

        // ------------------------------------------------------------------
        //  Dibujo de cada sub-icono (grilla de 100 x 100)
        // ------------------------------------------------------------------

        private static void DrawPlus(Graphics g)
        {
            var circle = new RectangleF(3, 3, 94, 94);
            using (var rim = new SolidBrush(Color.FromArgb(0x1B, 0x8F, 0x52)))
                g.FillEllipse(rim, circle);

            var inner = new RectangleF(6, 6, 88, 88);
            using (var fill = new LinearGradientBrush(new RectangleF(0, 0, 100, 100),
                Color.FromArgb(0x2B, 0xBB, 0x6E), Color.FromArgb(0x1F, 0xA3, 0x5E), 45f))
                g.FillEllipse(fill, inner);

            using (var white = new SolidBrush(Color.White))
            {
                g.FillRectangle(white, 26f, 45f, 48f, 10f);   // barra horizontal
                g.FillRectangle(white, 45f, 26f, 10f, 48f);   // barra vertical
            }
        }

        private static void DrawPencil(Graphics g)
        {
            GraphicsState state = g.Save();
            // El lápiz se dibuja "acostado" sobre el eje X y se gira 45°:
            // punta abajo a la izquierda, goma arriba a la derecha.
            g.TranslateTransform(6f, 94f);
            g.RotateTransform(-45f);

            const float hw = 9f;        // mitad del ancho
            const float tip = 19f;      // largo de la punta
            const float body = 87f;     // fin del cuerpo amarillo
            const float ferrule = 99f;  // fin del aro metálico
            const float end = 119f;     // fin de la goma

            using (var eraser = new SolidBrush(Color.FromArgb(0xE0, 0x5A, 0x70)))
            using (GraphicsPath p = RoundedRect(body, -hw, end - body, hw * 2, 4f))
                g.FillPath(eraser, p);

            using (var metal = new SolidBrush(Color.FromArgb(0xD9, 0xDD, 0xE1)))
                g.FillRectangle(metal, body, -hw, ferrule - body, hw * 2);

            using (var light = new SolidBrush(Color.FromArgb(0xF6, 0xC5, 0x1C)))
                g.FillRectangle(light, tip - 0.5f, -hw, body - tip + 0.5f, hw);
            using (var dark = new SolidBrush(Color.FromArgb(0xE3, 0xAA, 0x04)))
                g.FillRectangle(dark, tip - 0.5f, 0f, body - tip + 0.5f, hw);

            using (var wood = new SolidBrush(Color.FromArgb(0xEB, 0xD3, 0xAA)))
                g.FillPolygon(wood, new[] { new PointF(0, 0), new PointF(tip, -hw), new PointF(tip, hw) });

            float lead = 8f;
            float lh = hw * lead / tip;
            using (var graphite = new SolidBrush(Color.FromArgb(0x5B, 0x66, 0x70)))
                g.FillPolygon(graphite, new[] { new PointF(0, 0), new PointF(lead, -lh), new PointF(lead, lh) });

            g.Restore(state);
        }

        private static void DrawPrinter(Graphics g)
        {
            Color cyan = Color.FromArgb(0x50, 0xE1, 0xFF);

            // Pestañas laterales de la bandeja
            using (var blue = new SolidBrush(Color.FromArgb(0x0F, 0x5F, 0xAE)))
            {
                g.FillRectangle(blue, 12f, 16f, 12f, 14f);
                g.FillRectangle(blue, 76f, 16f, 12f, 14f);
            }

            // Hoja que entra por arriba
            using (var paper = new SolidBrush(cyan))
                g.FillRectangle(paper, 22f, 4f, 56f, 26f);

            // Cuerpo
            using (var bodyBrush = new LinearGradientBrush(new RectangleF(0, 24, 100, 56),
                Color.FromArgb(0x76, 0x84, 0x92), Color.FromArgb(0x66, 0x73, 0x80), 90f))
            using (GraphicsPath p = RoundedRect(4f, 26f, 92f, 52f, 6f))
                g.FillPath(bodyBrush, p);

            // Luz
            using (var lightBrush = new SolidBrush(Color.FromArgb(0xEE, 0xF2, 0xF5)))
                g.FillRectangle(lightBrush, 77f, 36f, 9f, 9f);

            // Ranura de salida
            using (var slot = new SolidBrush(Color.FromArgb(0x3C, 0x46, 0x51)))
                g.FillRectangle(slot, 14f, 60f, 72f, 7f);

            // Hoja que sale por abajo
            using (var paper = new SolidBrush(cyan))
                g.FillRectangle(paper, 22f, 63f, 56f, 33f);
            using (var shade = new SolidBrush(Color.FromArgb(0x2F, 0xC4, 0xE8)))
                g.FillRectangle(shade, 22f, 63f, 56f, 4f);
        }

        // ------------------------------------------------------------------
        //  Utilidades
        // ------------------------------------------------------------------

        private static GraphicsPath RoundedRect(float x, float y, float w, float h, float r)
        {
            float d = r * 2f;
            var p = new GraphicsPath();
            p.AddArc(x, y, d, d, 180, 90);
            p.AddArc(x + w - d, y, d, d, 270, 90);
            p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            p.AddArc(x, y + h - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        private static Bitmap ToArgb(Image src)
        {
            var bmp = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
            bmp.SetResolution(96, 96);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.CompositingMode = CompositingMode.SourceCopy;
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(src, new Rectangle(0, 0, src.Width, src.Height),
                    0, 0, src.Width, src.Height, GraphicsUnit.Pixel);
            }
            return bmp;
        }

        /// <summary>Reduce la imagen promediando bloques de factor x factor píxeles (respetando la transparencia).</summary>
        private static Bitmap Downsample(Bitmap big, int factor)
        {
            int bw = big.Width, bh = big.Height;
            int w = bw / factor, h = bh / factor;

            BitmapData bd = big.LockBits(new Rectangle(0, 0, bw, bh), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var src = new byte[bw * bh * 4];
            for (int row = 0; row < bh; row++)
                Marshal.Copy(new IntPtr(bd.Scan0.ToInt64() + (long)row * bd.Stride), src, row * bw * 4, bw * 4);
            big.UnlockBits(bd);

            var dst = new byte[w * h * 4];
            int n = factor * factor;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    long sa = 0, sr = 0, sg = 0, sb = 0;
                    for (int yy = 0; yy < factor; yy++)
                    {
                        int i = ((y * factor + yy) * bw + x * factor) * 4;
                        for (int xx = 0; xx < factor; xx++, i += 4)
                        {
                            int a = src[i + 3];
                            sb += src[i] * a;
                            sg += src[i + 1] * a;
                            sr += src[i + 2] * a;
                            sa += a;
                        }
                    }
                    int o = (y * w + x) * 4;
                    if (sa > 0)
                    {
                        dst[o] = (byte)((sb + sa / 2) / sa);
                        dst[o + 1] = (byte)((sg + sa / 2) / sa);
                        dst[o + 2] = (byte)((sr + sa / 2) / sa);
                        dst[o + 3] = (byte)((sa + n / 2) / n);
                    }
                }
            }

            var result = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            result.SetResolution(96, 96);
            BitmapData rd = result.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            for (int row = 0; row < h; row++)
                Marshal.Copy(dst, row * w * 4, new IntPtr(rd.Scan0.ToInt64() + (long)row * rd.Stride), w * 4);
            result.UnlockBits(rd);
            return result;
        }
    }
}
