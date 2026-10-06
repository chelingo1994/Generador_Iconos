using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace GeneradorIconos
{
    public class MainForm : Form
    {
        private static readonly string[] Extensions = { ".png", ".ico", ".bmp", ".jpg", ".jpeg", ".gif" };
        private static readonly int[] Sizes = { 0, 16, 24, 32, 48, 64, 96, 128, 256 };

        private readonly List<string> _files = new List<string>();

        private ListBox lstFiles;
        private Button btnAdd, btnRemove, btnClear, btnFolder, btnGenerate;
        private ComboBox cboSize, cboCorner;
        private NumericUpDown numBadge, numMarginX, numMarginY;
        private CheckBox chkAgregar, chkEditar, chkImprimir, chkOpenFolder;
        private TextBox txtFolder;
        private Label lblStatus;
        private PreviewPanel preview;

        public MainForm() : this(null) { }

        /// <param name="initialFiles">Imágenes a cargar al abrir (por ejemplo, las que se arrastran sobre el .exe).</param>
        public MainForm(string[] initialFiles)
        {
            Text = "Generador de iconos con sub-iconos";
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(980, 640);
            MinimumSize = new Size(900, 660);
            AllowDrop = true;

            BuildUi();

            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;
            UpdatePreview();
            if (initialFiles != null && initialFiles.Length > 0) AddFiles(initialFiles);
        }

        // ------------------------------------------------------------------
        //  Interfaz
        // ------------------------------------------------------------------

        private void BuildUi()
        {
            // Columna izquierda: botón arriba, lista en el medio (se estira) y opciones abajo.
            var left = new Panel { Dock = DockStyle.Left, Width = 320 };
            var top = new Panel { Dock = DockStyle.Top, Height = 50 };
            var listHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 0, 12, 0) };
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 412 };

            btnAdd = new Button { Text = "Cargar imágenes…", Location = new Point(12, 12), Size = new Size(296, 32) };
            btnAdd.Click += delegate { BrowseFiles(); };

            lstFiles = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, HorizontalScrollbar = true };
            lstFiles.SelectedIndexChanged += delegate { UpdatePreview(); };

            btnRemove = new Button { Text = "Quitar", Location = new Point(12, 6), Size = new Size(144, 28) };
            btnRemove.Click += delegate { RemoveSelected(); };
            btnClear = new Button { Text = "Limpiar lista", Location = new Point(164, 6), Size = new Size(144, 28) };
            btnClear.Click += delegate { _files.Clear(); RefreshList(-1); };

            // --- Opciones ---
            var grpOptions = new GroupBox { Text = "Opciones", Location = new Point(12, 44), Size = new Size(296, 172) };

            grpOptions.Controls.Add(MakeLabel("Tamaño de salida:", 12, 27));
            cboSize = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(158, 23), Size = new Size(126, 23) };
            foreach (int s in Sizes)
                cboSize.Items.Add(s == 0 ? "Igual al original" : s + " x " + s + " px");
            cboSize.SelectedIndex = 0;
            cboSize.SelectedIndexChanged += delegate { UpdatePreview(); };
            grpOptions.Controls.Add(cboSize);

            grpOptions.Controls.Add(MakeLabel("Posición del sub-icono:", 12, 56));
            cboCorner = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(158, 52), Size = new Size(126, 23) };
            cboCorner.Items.AddRange(new object[] { "Abajo a la derecha", "Abajo a la izquierda", "Arriba a la derecha", "Arriba a la izquierda" });
            cboCorner.SelectedIndex = 0;
            cboCorner.SelectedIndexChanged += delegate { UpdatePreview(); };
            grpOptions.Controls.Add(cboCorner);

            grpOptions.Controls.Add(MakeLabel("Tamaño del sub-icono (%):", 12, 85));
            numBadge = MakeNumeric(42, 15, 80, 85 - 3);
            grpOptions.Controls.Add(numBadge);

            grpOptions.Controls.Add(MakeLabel("Margen horizontal (%):", 12, 114));
            numMarginX = MakeNumeric(6, 0, 40, 114 - 3);
            grpOptions.Controls.Add(numMarginX);

            grpOptions.Controls.Add(MakeLabel("Margen vertical (%):", 12, 143));
            numMarginY = MakeNumeric(0, 0, 40, 143 - 3);
            grpOptions.Controls.Add(numMarginY);

            // --- Qué generar ---
            var grpKinds = new GroupBox { Text = "Sub-iconos a generar", Location = new Point(12, 222), Size = new Size(296, 52) };
            chkAgregar = MakeCheck("Agregar (+)", 12, 22, 96);
            chkEditar = MakeCheck("Editar", 112, 22, 70);
            chkImprimir = MakeCheck("Imprimir", 190, 22, 90);
            grpKinds.Controls.AddRange(new Control[] { chkAgregar, chkEditar, chkImprimir });

            // --- Carpeta de salida ---
            var lblFolder = MakeLabel("Guardar en (vacío = junto a la imagen original):", 12, 284);
            txtFolder = new TextBox { Location = new Point(12, 304), Size = new Size(258, 23) };
            btnFolder = new Button { Text = "…", Location = new Point(276, 303), Size = new Size(32, 25) };
            btnFolder.Click += delegate { BrowseFolder(); };

            chkOpenFolder = new CheckBox { Text = "Abrir la carpeta al terminar", Checked = true, Location = new Point(14, 334), Size = new Size(290, 22) };

            btnGenerate = new Button
            {
                Text = "Generar PNG",
                Location = new Point(12, 362),
                Size = new Size(296, 40),
                Font = new Font(Font, FontStyle.Bold)
            };
            btnGenerate.Click += delegate { Generate(); };

            top.Controls.Add(btnAdd);
            listHost.Controls.Add(lstFiles);
            bottom.Controls.AddRange(new Control[] { btnRemove, btnClear, grpOptions, grpKinds, lblFolder, txtFolder, btnFolder, chkOpenFolder, btnGenerate });
            left.Controls.Add(listHost);   // Fill primero, después los acoplados
            left.Controls.Add(top);
            left.Controls.Add(bottom);

            lblStatus = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 26,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                BackColor = SystemColors.ControlLight,
                Text = "Cargá una o varias imágenes, o arrastralas a esta ventana."
            };

            preview = new PreviewPanel { Dock = DockStyle.Fill };

            // El orden importa: primero el que ocupa el resto (Fill), después los acoplados.
            Controls.Add(preview);
            Controls.Add(left);
            Controls.Add(lblStatus);
        }

        private static Label MakeLabel(string text, int x, int y)
        {
            return new Label { Text = text, Location = new Point(x, y), AutoSize = true };
        }

        private NumericUpDown MakeNumeric(int value, int min, int max, int y)
        {
            var n = new NumericUpDown { Minimum = min, Maximum = max, Value = value, Location = new Point(214, y), Size = new Size(70, 23) };
            n.ValueChanged += delegate { UpdatePreview(); };
            return n;
        }

        private CheckBox MakeCheck(string text, int x, int y, int width)
        {
            var c = new CheckBox { Text = text, Checked = true, Location = new Point(x, y), Size = new Size(width, 22) };
            c.CheckedChanged += delegate { UpdatePreview(); };
            return c;
        }

        // ------------------------------------------------------------------
        //  Lista de archivos
        // ------------------------------------------------------------------

        private void BrowseFiles()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Elegí la imagen base (podés seleccionar varias)";
                dlg.Filter = "Imágenes (*.png;*.ico;*.bmp;*.jpg;*.gif)|*.png;*.ico;*.bmp;*.jpg;*.jpeg;*.gif|Todos los archivos (*.*)|*.*";
                dlg.Multiselect = true;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    AddFiles(dlg.FileNames);
            }
        }

        private void BrowseFolder()
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Carpeta donde se guardan los PNG generados";
                if (Directory.Exists(txtFolder.Text)) dlg.SelectedPath = txtFolder.Text;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    txtFolder.Text = dlg.SelectedPath;
            }
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            var dropped = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (dropped != null) AddFiles(dropped);
        }

        private void AddFiles(IEnumerable<string> paths)
        {
            int last = -1;
            foreach (string path in paths)
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (Array.IndexOf(Extensions, ext) < 0 || !File.Exists(path)) continue;

                int existing = _files.FindIndex(delegate (string f) { return string.Equals(f, path, StringComparison.OrdinalIgnoreCase); });
                if (existing >= 0) { last = existing; continue; }
                _files.Add(path);
                last = _files.Count - 1;
            }
            if (last >= 0) RefreshList(last);
        }

        private void RemoveSelected()
        {
            int i = lstFiles.SelectedIndex;
            if (i < 0) return;
            _files.RemoveAt(i);
            RefreshList(Math.Min(i, _files.Count - 1));
        }

        private void RefreshList(int select)
        {
            lstFiles.BeginUpdate();
            lstFiles.Items.Clear();
            foreach (string f in _files) lstFiles.Items.Add(Path.GetFileName(f));
            lstFiles.EndUpdate();
            if (select >= 0 && select < _files.Count) lstFiles.SelectedIndex = select;
            else UpdatePreview();
        }

        // ------------------------------------------------------------------
        //  Vista previa y generación
        // ------------------------------------------------------------------

        private BadgeOptions ReadOptions()
        {
            return new BadgeOptions
            {
                OutputSize = Sizes[Math.Max(0, cboSize.SelectedIndex)],
                Corner = (BadgeCorner)Math.Max(0, cboCorner.SelectedIndex),
                BadgePercent = (float)numBadge.Value,
                MarginXPercent = (float)numMarginX.Value,
                MarginYPercent = (float)numMarginY.Value
            };
        }

        private List<BadgeKind> SelectedKinds()
        {
            var kinds = new List<BadgeKind>();
            if (chkAgregar.Checked) kinds.Add(BadgeKind.Agregar);
            if (chkEditar.Checked) kinds.Add(BadgeKind.Editar);
            if (chkImprimir.Checked) kinds.Add(BadgeKind.Imprimir);
            return kinds;
        }

        private void UpdatePreview()
        {
            if (preview == null || numMarginY == null) return;   // todavía se está armando la ventana

            var tiles = new List<PreviewTile>();
            int i = lstFiles.SelectedIndex;
            if (i >= 0 && i < _files.Count)
            {
                try
                {
                    BadgeOptions opt = ReadOptions();
                    using (Bitmap source = BadgeRenderer.LoadImage(_files[i]))
                    {
                        tiles.Add(new PreviewTile("Original", BadgeRenderer.PrepareBase(source, opt.OutputSize)));
                        foreach (BadgeKind kind in SelectedKinds())
                            tiles.Add(new PreviewTile(Path.GetFileNameWithoutExtension(_files[i]) + BadgeRenderer.Suffix(kind) + ".png",
                                BadgeRenderer.Compose(source, kind, opt)));
                    }
                    Bitmap first = tiles[0].Image;
                    lblStatus.Text = Path.GetFileName(_files[i]) + "  —  salida de " + first.Width + " x " + first.Height + " px";
                }
                catch (Exception ex)
                {
                    foreach (PreviewTile t in tiles) t.Image.Dispose();
                    tiles.Clear();
                    lblStatus.Text = "No se pudo abrir " + Path.GetFileName(_files[i]) + ": " + ex.Message;
                }
            }
            preview.SetTiles(tiles);
            btnGenerate.Enabled = _files.Count > 0;
            btnRemove.Enabled = i >= 0;
            btnClear.Enabled = _files.Count > 0;
        }

        private void Generate()
        {
            List<BadgeKind> kinds = SelectedKinds();
            if (_files.Count == 0) return;
            if (kinds.Count == 0)
            {
                MessageBox.Show(this, "Marcá al menos un sub-icono para generar.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string customFolder = txtFolder.Text.Trim();
            BadgeOptions opt = ReadOptions();
            int saved = 0;
            string lastFolder = null;
            var errors = new List<string>();

            Cursor = Cursors.WaitCursor;
            try
            {
                if (customFolder.Length > 0) Directory.CreateDirectory(customFolder);

                foreach (string file in _files)
                {
                    try
                    {
                        string folder = customFolder.Length > 0 ? customFolder : Path.GetDirectoryName(Path.GetFullPath(file));
                        string name = Path.GetFileNameWithoutExtension(file);
                        using (Bitmap source = BadgeRenderer.LoadImage(file))
                        {
                            foreach (BadgeKind kind in kinds)
                            {
                                using (Bitmap result = BadgeRenderer.Compose(source, kind, opt))
                                    result.Save(Path.Combine(folder, name + BadgeRenderer.Suffix(kind) + ".png"), ImageFormat.Png);
                                saved++;
                            }
                        }
                        lastFolder = folder;
                    }
                    catch (Exception ex)
                    {
                        errors.Add(Path.GetFileName(file) + ": " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add(ex.Message);
            }
            finally
            {
                Cursor = Cursors.Default;
            }

            lblStatus.Text = "Listo: " + saved + " PNG generados" + (lastFolder != null ? " en " + lastFolder : "") + ".";
            if (errors.Count > 0)
                MessageBox.Show(this, "Algunas imágenes no se pudieron procesar:\n\n" + string.Join("\n", errors.ToArray()),
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);

            if (saved > 0 && chkOpenFolder.Checked && lastFolder != null)
            {
                try { Process.Start("explorer.exe", "\"" + lastFolder + "\""); }
                catch { /* si no se puede abrir el explorador, no pasa nada */ }
            }
        }
    }

    public class PreviewTile
    {
        public readonly string Title;
        public readonly Bitmap Image;
        public PreviewTile(string title, Bitmap image) { Title = title; Image = image; }
    }

    /// <summary>Muestra el original y los resultados ampliados sobre un fondo a cuadros (para ver la transparencia).</summary>
    public class PreviewPanel : Panel
    {
        private List<PreviewTile> _tiles = new List<PreviewTile>();

        public PreviewPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Color.White;
        }

        public void SetTiles(List<PreviewTile> tiles)
        {
            foreach (PreviewTile t in _tiles) t.Image.Dispose();
            _tiles = tiles;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;

            if (_tiles.Count == 0)
            {
                using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString("Cargá una imagen para ver la vista previa", Font, SystemBrushes.GrayText, ClientRectangle, fmt);
                return;
            }

            int cols = _tiles.Count <= 1 ? 1 : 2;
            int rows = (_tiles.Count + cols - 1) / cols;
            const int pad = 14, titleH = 22;
            int cellW = (ClientSize.Width - pad * (cols + 1)) / cols;
            int cellH = (ClientSize.Height - pad * (rows + 1)) / rows;
            if (cellW < 40 || cellH < 60) return;

            for (int i = 0; i < _tiles.Count; i++)
            {
                PreviewTile tile = _tiles[i];
                var cell = new Rectangle(pad + (i % cols) * (cellW + pad), pad + (i / cols) * (cellH + pad), cellW, cellH);
                var area = new Rectangle(cell.X, cell.Y + titleH, cell.Width, cell.Height - titleH);

                using (var fmt = new StringFormat { Trimming = StringTrimming.EllipsisPath, FormatFlags = StringFormatFlags.NoWrap })
                    g.DrawString(tile.Title, Font, SystemBrushes.ControlText, new RectangleF(cell.X, cell.Y, cell.Width, titleH), fmt);

                DrawCheckerboard(g, area);
                g.DrawRectangle(SystemPens.ControlDark, area);

                // Ampliación con píxeles nítidos (zoom entero cuando la imagen es chica).
                Bitmap img = tile.Image;
                float zoom = Math.Min((area.Width - 24) / (float)img.Width, (area.Height - 24) / (float)img.Height);
                if (zoom >= 1f) zoom = (float)Math.Floor(zoom);
                if (zoom <= 0f) continue;
                int w = (int)(img.Width * zoom), h = (int)(img.Height * zoom);
                var dest = new Rectangle(area.X + (area.Width - w) / 2, area.Y + (area.Height - h) / 2, w, h);

                g.InterpolationMode = zoom >= 1f ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(img, dest, 0, 0, img.Width, img.Height, GraphicsUnit.Pixel);

                // Tamaño real, en la esquina, para ver cómo queda en la aplicación.
                if (zoom > 1.5f && img.Width + 12 < area.Width / 3 && img.Height + 12 < area.Height / 3)
                {
                    var real = new Rectangle(area.X + 6, area.Y + 6, img.Width, img.Height);
                    using (var back = new SolidBrush(SystemColors.Control))
                        g.FillRectangle(back, Rectangle.Inflate(real, 3, 3));
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.DrawImage(img, real, 0, 0, img.Width, img.Height, GraphicsUnit.Pixel);
                }
            }
        }

        private static void DrawCheckerboard(Graphics g, Rectangle area)
        {
            using (var back = new SolidBrush(Color.White))
                g.FillRectangle(back, area);
            using (var hatch = new HatchBrush(HatchStyle.LargeCheckerBoard, Color.FromArgb(232, 232, 232), Color.White))
                g.FillRectangle(hatch, area);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                foreach (PreviewTile t in _tiles) t.Image.Dispose();
            base.Dispose(disposing);
        }
    }
}
