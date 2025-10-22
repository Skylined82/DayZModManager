using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Reflection;



namespace DayZManager
{
    public class MainForm : Form
    {
        private Button btnHelp, btnBuild, btnSelectMods, btnStart, btnStop, btnPickMission, btnSettings, btnOpenRoot, btnPurgeLogs;
        private RichTextBox log;
        private SettingsModel? cfg;

        // force smooth repainting on any Control you don't own
        private static void Smooth(Control c)
        {
            // DoubleBuffered (protected) -> reflection
            typeof(Control).GetProperty("DoubleBuffered",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(c, true, null);

            // ResizeRedraw (protected) -> reflection
            typeof(Control).GetProperty("ResizeRedraw",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(c, true, null);

            // also set painting styles via protected SetStyle(...)
            typeof(Control).GetMethod("SetStyle",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(c, new object[] {
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer, true });

            // belt & suspenders: if all else fails, invalidate while resizing
            c.Resize += (_, __) => c.Invalidate();
        }


        public MainForm()
        {
            UI.StyleForm(this, "DayZ Mod Manager");
            // Use the EXE icon as the window icon
            try
            {
                this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? this.Icon;
            }
            catch { /* ignore */ }

            MinimumSize = new Size(1100, 720);
            FormClosing += MainForm_FormClosing;

            // Smooth repaints while resizing (kills header artifacts)
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            UpdateStyles();


            // header (dock top)
            var header = new HeaderPanel
            {
                Title = "DayZ Mod Manager",
                Subtitle = "Build mods • Manage selections • Start server & client"
            };
            Smooth(header);


            // main area (dock fill)
            var main = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty
            };
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320F)); // fixed left rail
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));  // log area grows

            // Add header + main inside a root layout so they never overlap
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // header height
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));  // main area

            header.Dock = DockStyle.Fill; // fills row 0
            main.Dock = DockStyle.Fill; // fills row 1

            root.Controls.Add(header, 0, 0);
            root.Controls.Add(main, 0, 1);
            Controls.Add(root);


            // LEFT: actions card
            var leftCard = new CardPanel { Dock = DockStyle.Fill, Padding = new Padding(16) };
            Smooth(leftCard);

            main.Controls.Add(leftCard, 0, 0);

            // Scroll container + table stack (no FlowLayoutPanel shenanigans)
            var leftScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            leftCard.Controls.Add(leftScroll);

            var leftStack = new TableLayoutPanel
            {
                Dock = DockStyle.Top, // stack keeps its own height; panel scrolls if needed
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            leftStack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            leftScroll.Controls.Add(leftStack);

            // Buttons (final labels + order)
            btnBuild = new OutlineButton { Text = "🧱  Build mod(s)" }; btnBuild.Click += (_, __) => BuildMods();
            btnSelectMods = new OutlineButton { Text = "🧩  Select mods…" }; btnSelectMods.Click += (_, __) => SelectMods();

            btnStart = new OutlineButton { Text = "▶️  Start server + client" }; btnStart.Click += (_, __) => StartServer();
            btnStop = new OutlineButton { Text = "⏹️  Stop server + client" }; btnStop.Click += (_, __) => StopAll();

            btnPickMission = new OutlineButton { Text = "🎯  Select mission…" }; btnPickMission.Click += (_, __) => PickMission();
            btnOpenRoot = new OutlineButton { Text = "📁  Set workspace folder…" }; btnOpenRoot.Click += (_, __) => SetWorkspaceFolder();
            btnSettings = new OutlineButton { Text = "⚙️  Settings…" }; btnSettings.Click += (_, __) => EditSettings();

            btnPurgeLogs = new OutlineButton { Text = "🧹  Purge logs" }; btnPurgeLogs.Click += (_, __) => PurgeLogs();
            btnHelp = new AccentButton { Text = "❓  Help" }; btnHelp.Click += (_, __) => ShowHelp();


            // Order: build/start/stop → content → setup
            var menu = new Control[]
            {
    // Build & select
    btnBuild, btnSelectMods,
    // Run loop
    btnStart, btnStop,
    // Content management
    btnPickMission,
    // Setup/rare actions
    btnOpenRoot, btnSettings, btnPurgeLogs, btnHelp
            };

            // Uniform, full-width buttons with clean spacing
            for (int i = 0; i < menu.Length; i++)
            {
                var b = menu[i];
                b.Dock = DockStyle.Fill;
                b.Margin = new Padding(0, i == 0 ? 0 : 8, 0, 0);
                leftStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                leftStack.Controls.Add(b, 0, i);
            }

            // RIGHT: log card
            var rightCard = new CardPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0),
                AutoSize = false,
                MinimumSize = Size.Empty
            };
            Smooth(rightCard);


            main.Controls.Add(rightCard, 1, 0);

            log = new RichTextBox
            {
                Multiline = true,
                ReadOnly = true,
                DetectUrls = false,
                WordWrap = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                ForeColor = Color.Gainsboro,
                Font = UI.MonoFont,
                BorderStyle = BorderStyle.None,
                Margin = new Padding(0)
            };


            rightCard.Controls.Add(log);
            // keep wrapping perfect when the console is resized
            log.Resize += (_, __) =>
            {
                // RightMargin in pixels; a few px of padding so it doesn't hug the border
                log.RightMargin = Math.Max(20, log.ClientSize.Width - 8);
            };
            // initialize once
            log.RightMargin = Math.Max(20, log.ClientSize.Width - 8);

            Load += MainForm_Load;
        }
        private void SetWorkspaceFolder()
        {
            cfg ??= SettingsModel.Load();

            using var fb = new FolderBrowserDialog
            {
                Description = "Select DayZ Manager workspace folder",
                UseDescriptionForTitle = true, // puts text in the title bar (single line)
                SelectedPath = string.IsNullOrWhiteSpace(cfg.RepoRoot) ? AppContext.BaseDirectory : cfg.RepoRoot,
                ShowNewFolderButton = true
            };


            if (fb.ShowDialog(this) == DialogResult.OK)
            {
                cfg.RepoRoot = fb.SelectedPath;

                // Ensure expected subfolders exist
                Directory.CreateDirectory(Path.Combine(cfg.RepoRoot, "Missions"));
                Directory.CreateDirectory(Path.Combine(cfg.RepoRoot, "WorkingMods"));
                Directory.CreateDirectory(Path.Combine(cfg.RepoRoot, "BuiltMods"));
                Directory.CreateDirectory(Path.Combine(cfg.RepoRoot, "Servers"));

                try
                {
                    cfg.Save();
                    Log($"Workspace set to: {cfg.RepoRoot}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Save error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void MainForm_Load(object? sender, EventArgs e)
        {
            try
            {
                cfg = SettingsModel.Load();
                ApplyWindowGeometry();

                Log($"Loaded settings: {cfg.SettingsPath}");

                var missing = cfg.GetMissingCriticalPaths(includeOptional: true).ToList();
                if (missing.Any())
                {
                    Log("Tip: some paths are missing. Open Settings to configure them, or click Help for a quick start.");
                }


            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Settings error"); }
        }

        // ----- Window geometry memory -----
        private void ApplyWindowGeometry()
        {
            if (cfg == null) return;

            if (cfg.StartMaximized)
            {
                WindowState = FormWindowState.Maximized;
                return;
            }

            int w = (cfg.MainW > 0 ? cfg.MainW : 1280);
            int h = (cfg.MainH > 0 ? cfg.MainH : 800);
            int x = (cfg.MainX >= 0 ? cfg.MainX : (Screen.PrimaryScreen!.WorkingArea.Width - w) / 2);
            int y = (cfg.MainY >= 0 ? cfg.MainY : (Screen.PrimaryScreen!.WorkingArea.Height - h) / 2);

            StartPosition = FormStartPosition.Manual;
            Bounds = EnsureOnScreen(new Rectangle(x, y, w, h));
        }

        private Rectangle EnsureOnScreen(Rectangle r)
        {
            var screen = Screen.FromRectangle(r).WorkingArea;
            int x = Math.Max(screen.Left, Math.Min(r.X, screen.Right - 100));
            int y = Math.Max(screen.Top, Math.Min(r.Y, screen.Bottom - 100));
            int w = Math.Max(MinimumSize.Width, Math.Min(r.Width, screen.Width));
            int h = Math.Max(MinimumSize.Height, Math.Min(r.Height, screen.Height));
            return new Rectangle(x, y, w, h);
        }

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (cfg == null) return;

            var b = (WindowState == FormWindowState.Normal) ? Bounds : RestoreBounds;
            cfg.MainX = b.X; cfg.MainY = b.Y; cfg.MainW = b.Width; cfg.MainH = b.Height;
            cfg.StartMaximized = (WindowState == FormWindowState.Maximized);
            try { cfg.Save(); } catch { /* ignore */ }
        }

        private void ShowHelp()
        {
            using var dlg = new Form
            {
                Text = "Help — Quick start",
                StartPosition = FormStartPosition.CenterParent,
                Width = 860,
                Height = 600,
                MinimumSize = new Size(720, 520),
                BackColor = UI.Bg,
                ForeColor = UI.Text,
                AutoScaleMode = AutoScaleMode.Font
            };
            // bump the base font for the whole dialog (body text, numbers, tips, etc.)
            dlg.Font = new Font(dlg.Font.FontFamily, 11.0f, dlg.Font.Style); // try 11–12 if you want bigger


            // Root: header + content + bottom bar
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(12)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // header
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // scrollable content
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // buttons
            dlg.Controls.Add(root);
            dlg.Icon = this.Icon;

            // Header
            var header = new Panel { Dock = DockStyle.Top, Padding = new Padding(12, 10, 12, 8) };
            var title = new Label
            {
                Text = "Quick start",
                Font = UI.TitleFont,
                ForeColor = UI.Text,
                Dock = DockStyle.Top,
                AutoSize = true
            };
            var subtitle = new Label
            {
                Text = "Do these once, then iterate fast on your mods.",
                Font = UI.SubTitleFont,
                ForeColor = UI.Muted,
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(0, 2, 0, 0)
            };
            header.Controls.Add(subtitle);
            header.Controls.Add(title);
            root.Controls.Add(header, 0, 0);

            // Scrollable content
            var scroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(12, 4, 12, 4)
            };

            root.Controls.Add(scroll, 0, 1);

            var stack = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                Padding = new Padding(0, 0, 0, 0)
            };

            scroll.Controls.Add(stack);

            // Steps (edit text freely)
            string[] steps =
            {
    "Set workspace folder — creates \\Missions, \\WorkingMods, \\BuiltMods, \\Servers.",
    "Open Settings — set paths: DayZ Game, DayZ Server, AddonBuilder, Workshop (optional), Profiles. Toggle Diagnostics if you want to use DayZDiag.",
    "Put missions into \\Missions and press “Select mission…”. This updates serverDZ.cfg → template = \"<mission>\".",
    "Put your mod sources into \\WorkingMods\\<YourMod>\\.",
    "Build mod(s) — pack your sources; output goes to \\BuiltMods\\@<YourMod>\\addons.",
    "Select mods… — pick from Workshop and Your Built Mods; arrange the Load Order (right list).",
    "Start server + client — launches the server (Diag or normal from Settings) and auto-launches the client.",
    "Stop server + client — kills DayZServer/DayZDiag and DayZ/DayZ_BE processes.",
    "Purge logs — clears *.log, *.RPT, *.mdmp, *.adm under your Profiles folder (Servers\\profiles) to keep things tidy."
};



            // Render steps nicely
            int i = 1;
            var stepLabels = new List<Label>();

            foreach (var s in steps)
            {
                var row = new Panel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    Padding = new Padding(0, 2, 0, 2),
                    Margin = new Padding(0, 2, 0, 0)
                };



                var num = new Label
                {
                    Text = i.ToString() + ")",
                    AutoSize = false,                   // don’t let it wrap
                    Width = 44,                         // wider column so “10)” fits too
                    TextAlign = ContentAlignment.TopRight,
                    ForeColor = UI.Muted,
                    Dock = DockStyle.Left,
                    Padding = new Padding(0, 0, 8, 0)

                };


                var text = new Label
                {
                    Text = s,
                    AutoSize = true,          // allow height to grow when wrapped
                    Dock = DockStyle.Top,     // stack within the row
                    Padding = new Padding(2, 0, 2, 0)
                };
                // wrapping needs a width cap; we'll set MaximumSize after the loop



                row.Controls.Add(text);
                row.Controls.Add(num);
                stack.Controls.Add(row);
                stepLabels.Add(text);
                i++;
            }
            void ResizeSteps()
            {
                // width available for text = stack width minus number column + paddings
                // num.Width is 28, plus its right padding (8) and text’s left/right padding (≈4)
                int usable = Math.Max(120, stack.ClientSize.Width - 28 - 8 - 6);
                foreach (var lbl in stepLabels)
                    lbl.MaximumSize = new Size(usable, 0); // 0 height => no cap; enables wrapping
            }

            // set once and on resize
            ResizeSteps();
            stack.Resize += (_, __) => ResizeSteps();

            // Bottom bar
            var bottom = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = false,
                Padding = new Padding(0, 6, 0, 0)
            };

            var btnClose = new AccentButton
            {
                Text = "Close",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(14, 6, 14, 6),
                Margin = new Padding(8, 0, 0, 0)
            };
            btnClose.Click += (_, __) => dlg.Close();

            bottom.Controls.Add(btnClose);
            root.Controls.Add(bottom, 0, 2);

            dlg.AcceptButton = btnClose;
            dlg.CancelButton = btnClose;

            dlg.ShowDialog(this);
        }


        private void Log(string s)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {s}\r\n";
            void append()
            {
                log.SelectionStart = log.TextLength;
                log.SelectionColor = Color.Gainsboro;
                log.SelectionFont = UI.MonoFont;
                log.AppendText(line);
                log.SelectionColor = Color.Gainsboro; // reset
                log.SelectionFont = UI.MonoFont;
            }
            if (InvokeRequired) BeginInvoke(new Action(append));
            else append();
        }

        // Styled “label: value” (label bold + colored; value normal)
        // Label + colored tokens with smart wrapping under the timestamp
        private void LogLabelTokens(string label, IList<string> tokens, Color? labelColor = null, Color? tokenColor = null)
        {
            string ts = $"[{DateTime.Now:HH:mm:ss}] ";
            int indentWidth = ts.Length; // align wrapped lines under timestamp

            // derive a soft wrap column from the current console width
            int charPx = TextRenderer.MeasureText("W", UI.MonoFont).Width; // monospace, close enough
            int usablePx = Math.Max(200, log.ClientSize.Width - 24);        // padding
            int wrapAt = Math.Max(40, usablePx / Math.Max(6, charPx));      // columns


            void append()
            {
                // timestamp
                log.SelectionStart = log.TextLength;
                log.SelectionColor = Color.DimGray;
                log.SelectionFont = UI.MonoFont;
                log.AppendText(ts);

                // label (bold + colored)
                log.SelectionColor = (labelColor ?? Color.DeepSkyBlue);
                log.SelectionFont = new Font(UI.MonoFont, FontStyle.Bold);
                log.AppendText(label);

                // tokens
                log.SelectionFont = UI.MonoFont;

                int currentLineLen = 0;
                for (int i = 0; i < tokens.Count; i++)
                {
                    string sep = (i == 0 ? "" : " ");
                    string tok = tokens[i];

                    // wrap if too long
                    int extra = sep.Length + tok.Length;
                    if (currentLineLen > 0 && (indentWidth + label.Length + currentLineLen + extra) > wrapAt)
                    {
                        log.SelectionColor = Color.Gainsboro;
                        log.AppendText("\r\n" + new string(' ', indentWidth)); // align under timestamp
                        currentLineLen = 0;
                    }

                    // separator (plain)
                    log.SelectionColor = Color.Gainsboro;
                    log.AppendText(sep);

                    // token (accent)
                    log.SelectionColor = (tokenColor ?? Color.LightSkyBlue);
                    log.AppendText(tok);

                    currentLineLen += extra;
                }

                log.SelectionColor = Color.Gainsboro;
                log.AppendText("\r\n");
            }

            if (InvokeRequired) BeginInvoke(new Action(append));
            else append();
        }




        // ---------------------- BUILD ----------------------
        private void BuildMods()
        {
            try
            {
                cfg ??= SettingsModel.Load();

                string working = Path.Combine(cfg.RepoRoot, "WorkingMods");
                string built = Path.Combine(cfg.RepoRoot, "BuiltMods");
                Directory.CreateDirectory(working);
                Directory.CreateDirectory(built);

                var allMods = new DirectoryInfo(working).GetDirectories()
                                 .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                                 .ToList();
                if (allMods.Count == 0) { Log("No mods in WorkingMods\\"); return; }

                using var dlg = new Form
                {
                    Text = "Build mod(s) — select which to pack",
                    Width = 860,
                    Height = 560,
                    MinimumSize = new Size(820, 520),
                    StartPosition = FormStartPosition.CenterParent,
                    BackColor = UI.Bg,
                    ForeColor = UI.Text,
                    AutoScaleMode = AutoScaleMode.Font
                };

                // Root: main grid + bottom bar
                var root = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 2,
                    Padding = new Padding(12)
                };
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
                root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                dlg.Controls.Add(root);
                dlg.Icon = this.Icon;

                // Grid: left | center(AutoSize) | right
                var grid = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 3,
                    RowCount = 1
                };
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
                root.Controls.Add(grid, 0, 0);

                // Left list (available)
                // Left column: source mods (available)
                var leftWrap = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0, 0, 8, 0) };
                leftWrap.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // title
                leftWrap.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // list

                var lblAvail = new Label { Text = "Your source mods", Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(2, 0, 0, 6), ForeColor = UI.Muted };

                var plAvail = new Panel { Dock = DockStyle.Fill, Padding = new Padding(1), BorderStyle = BorderStyle.FixedSingle };
                var lbAvail = new ListBox
                {
                    Dock = DockStyle.Fill,
                    SelectionMode = SelectionMode.MultiExtended,
                    BackColor = UI.CardAlt,
                    ForeColor = UI.Text,
                    BorderStyle = BorderStyle.None,
                    IntegralHeight = false,
                    HorizontalScrollbar = true
                };
                foreach (var d in allMods) lbAvail.Items.Add(d.Name);
                plAvail.Controls.Add(lbAvail);

                leftWrap.Controls.Add(lblAvail, 0, 0);
                leftWrap.Controls.Add(plAvail, 0, 1);
                grid.Controls.Add(leftWrap, 0, 0);



                // Right list (selected) — STICKY PREFILL
                // Right column: mods to build
                var rightWrap = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(8, 0, 0, 0) };
                rightWrap.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // title
                rightWrap.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // list

                var lblSel = new Label { Text = "Mods to build", Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(2, 0, 0, 6), ForeColor = UI.Muted };

                var plSel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(1), BorderStyle = BorderStyle.FixedSingle };
                var lbSel = new ListBox
                {
                    Dock = DockStyle.Fill,
                    BackColor = UI.CardAlt,
                    ForeColor = UI.Text,
                    BorderStyle = BorderStyle.None,
                    IntegralHeight = false,
                    HorizontalScrollbar = true
                };
                plSel.Controls.Add(lbSel);

                rightWrap.Controls.Add(lblSel, 0, 0);
                rightWrap.Controls.Add(plSel, 0, 1);
                grid.Controls.Add(rightWrap, 2, 0);


                // Center column: stacked arrow buttons
                var center = new FlowLayoutPanel
                {
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    FlowDirection = FlowDirection.TopDown,
                    WrapContents = false,
                    Anchor = AnchorStyles.None,
                    Margin = new Padding(0),
                    Padding = new Padding(8, 0, 8, 0)
                };
                var btnAdd = new AccentButton { Text = "→", AutoSize = false, Size = new Size(112, 44), Margin = new Padding(0, 0, 0, 8), TextAlign = ContentAlignment.MiddleCenter, Anchor = AnchorStyles.None };
                var btnRem = new OutlineButton { Text = "←", AutoSize = false, Size = new Size(112, 44), Margin = new Padding(0), TextAlign = ContentAlignment.MiddleCenter, Anchor = AnchorStyles.None };
                btnAdd.Font = new Font("Segoe UI Symbol", btnAdd.Font.Size + 4, FontStyle.Bold);
                btnRem.Font = new Font("Segoe UI Symbol", btnRem.Font.Size + 4, FontStyle.Bold);
                center.Controls.AddRange(new Control[] { btnAdd, btnRem });
                grid.Controls.Add(center, 1, 0);

                // Add/remove helpers
                void AddSelected()
                {
                    foreach (var it in lbAvail.SelectedItems.Cast<string>().ToList())
                        if (!lbSel.Items.Contains(it)) lbSel.Items.Add(it);
                }
                void RemoveSelected()
                {
                    foreach (var it in lbSel.SelectedItems.Cast<string>().ToList())
                        lbSel.Items.Remove(it);
                }
                btnAdd.Click += (_, __) => AddSelected();
                btnRem.Click += (_, __) => RemoveSelected();
                lbAvail.DoubleClick += (_, __) => AddSelected();
                lbSel.DoubleClick += (_, __) => RemoveSelected();
                lbAvail.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) AddSelected(); };
                lbSel.KeyDown += (_, e) => { if (e.KeyCode == Keys.Delete) RemoveSelected(); };

                // Bottom bar
                // Bottom bar (tip + bottom-right Build)
                var bottom = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 2,
                    RowCount = 2,
                    Padding = new Padding(0, 8, 0, 0)
                };
                bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f)); // left filler / tip
                bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      // right button
                bottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));            // tip row
                bottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));            // buttons row
                root.Controls.Add(bottom, 0, 1);

                // tip (moved from left column so list heights match)
                var tipAvail = new Label
                {
                    Text = "Tip: Double-click a mod to add/remove it from the build list.",
                    Dock = DockStyle.Left,
                    AutoSize = true,
                    ForeColor = UI.Muted,
                    Padding = new Padding(2, 0, 0, 6),
                    Margin = new Padding(0, 0, 0, 0)
                };
                bottom.Controls.Add(tipAvail, 0, 0);
                bottom.SetColumnSpan(tipAvail, 1); // keep it under the left side

                var btnBuild = new AccentButton
                {
                    Text = "Build",
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    Padding = new Padding(12, 6, 12, 6),
                    Margin = new Padding(8, 0, 0, 0)
                };

                // filler on row 1, col 0 keeps Build pinned at bottom-right
                bottom.Controls.Add(new Label() { AutoSize = true }, 0, 1);
                bottom.Controls.Add(btnBuild, 1, 1);


                // Persist selection on "Build"
                var selected = new List<string>(); // capture chosen mods locally
                btnBuild.Click += (_, __) =>
                {
                    selected = lbSel.Items.Cast<string>().ToList();
                    dlg.DialogResult = DialogResult.OK;
                };



                dlg.AcceptButton = btnBuild;

                if (dlg.ShowDialog(this) != DialogResult.OK) { Log("Build cancelled."); return; }

                if (selected.Count == 0) { Log("No mods selected."); return; }


                var ab = cfg.Resolve(cfg.AddonBuilderPath);
                if (!File.Exists(ab)) { MessageBox.Show($"AddonBuilder not found: {ab}"); return; }

                foreach (var name in selected)
                {
                    var modDir = allMods.First(d => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    string modName = modDir.Name.Trim('@');
                    string outRoot = Path.Combine(built, $"@{modName}");
                    string outAddons = Path.Combine(outRoot, "addons");
                    Directory.CreateDirectory(outAddons);

                    var args = $"\"{modDir.FullName}\" \"{outRoot}\" -clear -packonly";
                    Log($"Packing {modName} …");
                    var rc = RunProcess(ab, args, Path.GetDirectoryName(ab) ?? "",
                        onOut: a => Log(a), onErr: e => Log("[err] " + e));
                    if (rc != 0) { Log($"AddonBuilder exited {rc} for {modName}"); continue; }

                    // AddonBuilder writes PBOs under outRoot\addons already.
                    // Optionally sanity-check output:
                    var builtPbos = Directory.EnumerateFiles(outAddons, "*.pbo", SearchOption.AllDirectories).ToList();
                    if (builtPbos.Count == 0)
                        Log("⚠️ No .pbo found under " + outAddons + " after packing. Check AddonBuilder output settings.");

                    Log($"Built @ {outAddons}");

                }

                Log($"Done. Built {selected.Count} mod(s).");
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Build error"); }
        }



        // ---------------------- START SERVER ----------------------
        // ---------------------- START SERVER ----------------------
        private void StartServer()
        {
            try
            {
                cfg ??= SettingsModel.Load();

                string dzGame = cfg.Resolve(cfg.DayZGamePath);
                if (string.IsNullOrWhiteSpace(dzGame) || !Directory.Exists(dzGame))
                    throw new InvalidOperationException("DayZGamePath not set or invalid in settings.json");

                string dzServer = cfg.Resolve(cfg.DayZServerPath);
                if (string.IsNullOrWhiteSpace(dzServer) || !Directory.Exists(dzServer))
                {
                    var parent = Directory.GetParent(dzGame);
                    var guess = parent is null ? "" : Path.Combine(parent.FullName, "DayZServer");
                    if (!string.IsNullOrEmpty(guess) && Directory.Exists(guess)) dzServer = guess;
                }
                if (string.IsNullOrWhiteSpace(dzServer)) throw new InvalidOperationException("DayZServerPath not set and could not guess.");

                // Server exe + working dir based on Diagnostics setting
                string serverExe, serverWorkDir;
                bool useDiag = cfg.RunServerInDiag;
                if (useDiag)
                {
                    serverExe = Path.Combine(dzGame, "DayZDiag_x64.exe"); // diag lives in game folder
                    serverWorkDir = dzGame;
                }
                else
                {
                    serverExe = Path.Combine(dzServer, "DayZServer_x64.exe");
                    serverWorkDir = dzServer;
                }

                if (!File.Exists(serverExe))
                {
                    var want = useDiag ? "DayZDiag_x64.exe (in Game folder)" : "DayZServer_x64.exe (in Server folder)";
                    throw new FileNotFoundException($"{want} not found.\nTried: {serverExe}", serverExe);
                }

                // Build mod list strictly from the saved selection (load order)
                string wk = cfg.Resolve(cfg.WorkshopPath);
                string builtRoot = Path.Combine(cfg.RepoRoot, "BuiltMods");
                string[] selected = cfg.ExtraMods ?? Array.Empty<string>();
                var workshopMods = new List<string>();
                var builtMods = new List<string>();

                string ResolveModPath(string name)
                {
                    string n = name.StartsWith("@") ? name : "@" + name;

                    var fromWorkshop = !string.IsNullOrWhiteSpace(wk) ? Path.Combine(wk, n) : null;
                    if (!string.IsNullOrWhiteSpace(fromWorkshop) && Directory.Exists(fromWorkshop))
                    {
                        workshopMods.Add(n);
                        return fromWorkshop;
                    }

                    var fromBuilt = Path.Combine(builtRoot, n);
                    if (Directory.Exists(fromBuilt))
                    {
                        builtMods.Add(n);
                        return fromBuilt;
                    }

                    // Not found in either; treat as “workshop-ish” for display purposes only.
                    workshopMods.Add(n);
                    return n; // server will attempt to resolve
                }


                string modArg = selected.Length == 0 ? "" : string.Join(";", selected.Select(ResolveModPath));

                string serverCfg = cfg.Resolve(cfg.ServerConfig);
                string profiles = cfg.Resolve(cfg.ProfilesDir);
                string missionDir = cfg.Resolve(cfg.MissionPath);
                Directory.CreateDirectory(profiles);

                var flags = new List<string>
        {
            useDiag ? "-server" : "",
            $"\"-config={serverCfg}\"",
            $"\"-profiles={profiles}\"",
            $"\"-mission={missionDir}\"",
            string.IsNullOrWhiteSpace(modArg) ? "" : $"\"-mod={modArg}\"",
            "-doLogs","-adminLog","-netLog","-freezeChecker"
        }.Where(s => !string.IsNullOrWhiteSpace(s));

                Log("Starting server…");
                Log($"  EXE: {serverExe}");
                if (selected.Length > 0)
                {
                    if (workshopMods.Count > 0)
                        LogLabelTokens("Workshop Mods: ", workshopMods, Color.DeepSkyBlue, Color.LightSkyBlue);

                    if (builtMods.Count > 0)
                        LogLabelTokens("Your Built Mods: ", builtMods, Color.MediumSpringGreen, Color.PaleGreen);
                }

                RunDetached(serverExe, string.Join(" ", flags), serverWorkDir);



                // Client — Diagnostics client if requested, else BE if present, else x64
                if (cfg.ClientAutoLaunch)
                {
                    string beExe = Path.Combine(dzGame, "DayZ_BE.exe");
                    string x64Exe = Path.Combine(dzGame, "DayZ_x64.exe");
                    string diagExe = Path.Combine(dzGame, "DayZDiag_x64.exe");
                    bool useDiagClient = cfg.UseDiagClient && File.Exists(diagExe);

                    string clientExe;
                    if (useDiagClient) clientExe = diagExe;
                    else clientExe = File.Exists(beExe) ? beExe : x64Exe;

                    if (!File.Exists(clientExe))
                    {
                        Log("Client not found, skipping auto-launch.");
                        return;
                    }

                    string clientProfiles = Path.Combine(profiles, "client");
                    Directory.CreateDirectory(clientProfiles);

                    var clientFlags = new List<string>
            {
                $"\"-profiles={clientProfiles}\"",
                string.IsNullOrWhiteSpace(modArg) ? "" : $"\"-mod={modArg}\"",
                "-filePatching","-noPause","-noSplash","-skipIntro",
                "-connect=127.0.0.1","-port=2302"
            };
                    if (!clientExe.EndsWith("DayZ_BE.exe", StringComparison.OrdinalIgnoreCase))
                        clientFlags.Add("-nolauncher");

                    Log(useDiagClient
                        ? "Launching client (DayZDiag_x64.exe) …"
                        : (clientExe.EndsWith("DayZ_BE.exe", StringComparison.OrdinalIgnoreCase)
                            ? "Launching client with BattlEye (DayZ_BE.exe) …"
                            : "Launching client (DayZ_x64.exe) …"));

                    RunDetached(clientExe, string.Join(" ", clientFlags.Where(s => !string.IsNullOrWhiteSpace(s))), dzGame);
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Start error"); }
        }



        // ---------------------- STOP ----------------------
        private void StopAll()
        {
            try
            {
                var names = new[] { "DayZServer_x64", "DayZ_x64", "DayZDiag_x64", "DayZ_BE" };
                var survivors = KillProcesses(names, tryElevate: true);
                if (survivors.Count == 0) Log("All DayZ processes stopped. ✅");
                else
                {
                    Log("Some processes resisted termination:");
                    foreach (var p in survivors) Log($" - {p}");
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Stop error"); }
        }
        private void PurgeLogs()
        {
            try
            {
                cfg ??= SettingsModel.Load();
                string profiles = cfg.Resolve(cfg.ProfilesDir);
                if (string.IsNullOrWhiteSpace(profiles) || !Directory.Exists(profiles))
                {
                    Log("Profiles folder not found; nothing to purge.");
                    return;
                }

                string[] patterns = { "*.log", "*.RPT", "*.mdmp", "*.adm" };
                int total = 0;
                var byType = new List<string>();

                foreach (var pat in patterns)
                {
                    int count = 0;
                    try
                    {
                        foreach (var f in Directory.EnumerateFiles(profiles, pat, SearchOption.AllDirectories))
                        {
                            try { File.Delete(f); count++; }
                            catch { /* ignore individual file errors */ }
                        }
                    }
                    catch { /* ignore pattern errors */ }

                    total += count;
                    byType.Add($"{pat}:{count}");
                }

                Log($"Purged logs in {profiles} → {total} files deleted ({string.Join(", ", byType)}).");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Purge Logs error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ---------------------- PICK MISSION ----------------------
        // ---------------------- PICK MISSION ----------------------
        private void PickMission()
        {
            try
            {
                cfg ??= SettingsModel.Load();

                string missionsRoot = Path.Combine(cfg.RepoRoot, "Missions");
                if (!Directory.Exists(missionsRoot)) { MessageBox.Show($"Missing Missions folder at {missionsRoot}"); return; }

                var dirs = new DirectoryInfo(missionsRoot)
                    .GetDirectories()
                    .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (dirs.Count == 0) { MessageBox.Show("No missions found under Missions\\"); return; }

                using var dlg = new Form
                {
                    Text = "Select Mission",
                    StartPosition = FormStartPosition.CenterParent,
                    Width = 600,
                    Height = 480,
                    MinimumSize = new Size(520, 420),
                    BackColor = UI.Bg,
                    ForeColor = UI.Text,
                    AutoScaleMode = AutoScaleMode.Font
                };

                // root: header | list | bottom
                var root = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 3,
                    Padding = new Padding(12, 8, 12, 12) // slight top nudge
                };
                root.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // header
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // list
                root.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // bottom
                dlg.Controls.Add(root);
                dlg.Icon = this.Icon;

                // Header (compact)
                var headerPanel = new Panel { Dock = DockStyle.Top, Padding = new Padding(6, 6, 6, 6) };
                headerPanel.Controls.Add(new Label
                {
                    Text = "Choose a mission",
                    Font = UI.TitleFont,
                    AutoSize = true,
                    Dock = DockStyle.Top
                });
                headerPanel.Controls.Add(new Label
                {
                    Text = "This updates serverDZ.cfg → template = \"<mission>\".",
                    Font = UI.SubTitleFont,
                    ForeColor = UI.Muted,
                    AutoSize = true,
                    Dock = DockStyle.Top,
                    Padding = new Padding(0, 2, 0, 0)
                });
                root.Controls.Add(headerPanel, 0, 0);

                // Framed list (shifted up; no search box)
                var listHost = new Panel
                {
                    Dock = DockStyle.Fill,
                    Padding = new Padding(1),
                    BorderStyle = BorderStyle.FixedSingle,
                    Margin = new Padding(0, 2, 0, 0) // sit a touch higher
                };
                var list = new ListBox
                {
                    Dock = DockStyle.Fill,
                    BackColor = UI.CardAlt,
                    ForeColor = UI.Text,
                    BorderStyle = BorderStyle.None,
                    IntegralHeight = false
                };
                foreach (var d in dirs) list.Items.Add(d.Name);
                listHost.Controls.Add(list);
                root.Controls.Add(listHost, 0, 1);

                // Preselect current
                if (!string.IsNullOrWhiteSpace(cfg.MissionPath))
                {
                    var cur = cfg.MissionPath.Replace(".\\Missions\\", "").Trim('\\', '/');
                    int idx = dirs.FindIndex(d => d.Name.Equals(cur, StringComparison.OrdinalIgnoreCase));
                    if (idx >= 0) list.SelectedIndex = idx;
                }
                list.Focus();

                // Bottom bar (compact; equal-width buttons)
                var bottom = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 2,
                    RowCount = 1,
                    Padding = new Padding(0, 6, 0, 0)
                };
                bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
                bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                root.Controls.Add(bottom, 0, 2);

                var tip = new Label
                {
                    Text = "Tip: Double-click a mission or press Enter to select. Esc cancels.",
                    AutoSize = true,
                    ForeColor = UI.Muted,
                    Dock = DockStyle.Fill
                };
                bottom.Controls.Add(tip, 0, 0);

                // RIGHT: fixed-size, equal buttons
                const int BtnW = 96;   // was 120
                const int BtnH = 34;   // a touch shorter; keep if you like 38

                var btnBar = new FlowLayoutPanel
                {
                    FlowDirection = FlowDirection.RightToLeft,
                    WrapContents = false,
                    AutoSize = false,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    Dock = DockStyle.Fill,
                    Padding = new Padding(0),
                    Margin = new Padding(0),
                    Height = BtnH
                };

                var btnSave = new AccentButton { Text = "Save", AutoSize = false, Size = new Size(BtnW, BtnH), Margin = new Padding(0), Padding = Padding.Empty };
                var btnCancel = new OutlineButton { Text = "Cancel", AutoSize = false, Size = new Size(BtnW, BtnH), Margin = new Padding(6, 0, 0, 0), Padding = Padding.Empty };

                // tiny gap between buttons (8px)
                var gap = new Panel { Width = 8, Height = 1, Margin = Padding.Empty };

                btnSave.Margin = Padding.Empty;
                btnCancel.Margin = Padding.Empty;

                btnBar.Controls.Add(btnSave);   // rightmost (RTL flow)
                btnBar.Controls.Add(gap);       // sits between
                btnBar.Controls.Add(btnCancel); // to the left of Save

                bottom.Controls.Add(btnBar, 1, 0);

                // confirm helper
                void Confirm()
                {
                    if (list.SelectedItem is string name)
                    {
                        cfg.MissionPath = @".\Missions\" + name;
                        try
                        {
                            cfg.Save();
                            Log($"MissionPath = {cfg.MissionPath}");
                            UpdateServerCfgTemplateLine(cfg, name);
                            Log("serverDZ.cfg updated with selected mission template.");
                        }
                        catch (Exception ex) { Log("Could not update serverDZ.cfg: " + ex.Message); }
                        dlg.DialogResult = DialogResult.OK;
                    }
                }

                // actions
                btnSave.Click += (_, __) => Confirm();
                btnCancel.Click += (_, __) => dlg.DialogResult = DialogResult.Cancel;
                list.DoubleClick += (_, __) => Confirm();
                list.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Confirm(); } };
                dlg.KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) dlg.DialogResult = DialogResult.Cancel; };

                dlg.AcceptButton = btnSave;
                dlg.CancelButton = btnCancel;

                dlg.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Pick Mission error");
            }
        }


        // ---------------------- SELECT MODS ----------------------
        private void SelectMods()
        {
            try
            {
                cfg ??= SettingsModel.Load();

                string wk = cfg.Resolve(cfg.WorkshopPath);
                string builtRoot = Path.Combine(cfg.RepoRoot, "BuiltMods");

                var workshop = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                var built = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

                if (Directory.Exists(wk))
                    foreach (var d in new DirectoryInfo(wk).GetDirectories("@*", SearchOption.TopDirectoryOnly))
                        workshop.Add(d.Name);

                if (Directory.Exists(builtRoot))
                    foreach (var d in new DirectoryInfo(builtRoot).GetDirectories("@*", SearchOption.TopDirectoryOnly))
                        built.Add(d.Name);

                using var dlg = new Form
                {
                    Text = "Select Mods (Load Order = Right list)",
                    Width = 1020,
                    Height = 620,
                    MinimumSize = new Size(940, 540),
                    StartPosition = FormStartPosition.CenterParent,
                    BackColor = UI.Bg,
                    ForeColor = UI.Text,
                    AutoScaleMode = AutoScaleMode.Font
                };

                // root: 3 columns on top, save bar bottom
                var root = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 2,
                    Padding = new Padding(12)
                };
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
                root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                dlg.Controls.Add(root);
                dlg.Icon = this.Icon;

                // top: Workshop | Built | Load Order
                var top = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 3,
                    RowCount = 1
                };
                top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f)); // workshop
                top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f)); // built
                top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f)); // load order

                root.Controls.Add(top, 0, 0);

                // helper: titled list + footer button row
                (TableLayoutPanel wrap, ListBox list, FlowLayoutPanel footer) MakeColumn(string title, IEnumerable<string> items, Padding margin)
                {
                    var wrap = new TableLayoutPanel
                    {
                        Dock = DockStyle.Fill,
                        ColumnCount = 1,
                        RowCount = 3,
                        Margin = margin
                    };
                    wrap.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // title
                    wrap.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));   // list
                    wrap.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // footer

                    var lbl = new Label
                    {
                        Text = title,
                        Dock = DockStyle.Top,
                        AutoSize = true,
                        Padding = new Padding(2, 0, 0, 6),
                        ForeColor = UI.Muted
                    };

                    var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(1), BorderStyle = BorderStyle.FixedSingle };
                    var lb = new ListBox
                    {
                        Dock = DockStyle.Fill,
                        SelectionMode = SelectionMode.MultiExtended,
                        BackColor = UI.CardAlt,
                        ForeColor = UI.Text,
                        BorderStyle = BorderStyle.None,
                        IntegralHeight = false,
                        HorizontalScrollbar = true
                    };
                    foreach (var s in items) lb.Items.Add(s);
                    host.Controls.Add(lb);

                    var footer = new FlowLayoutPanel
                    {
                        Dock = DockStyle.Fill,
                        FlowDirection = FlowDirection.LeftToRight,
                        WrapContents = false,
                        AutoSize = true,
                        Padding = new Padding(0, 8, 0, 0)
                    };

                    wrap.Controls.Add(lbl, 0, 0);
                    wrap.Controls.Add(host, 0, 1);
                    wrap.Controls.Add(footer, 0, 2);
                    return (wrap, lb, footer);
                }

                // column 1: workshop
                var (colWorkshop, lbWorkshop, ftWorkshop) = MakeColumn("Workshop Mods", workshop, new Padding(0, 0, 8, 0));
                // column 2: built
                var (colBuilt, lbBuilt, ftBuilt) = MakeColumn("Your Built Mods", built, new Padding(0, 0, 8, 0));
                // column 3: load order
                var (colOrder, lbSel, ftOrder) = MakeColumn("Load Order", (cfg.ExtraMods ?? Array.Empty<string>()), new Padding(0, 0, 0, 0));

                top.Controls.Add(colWorkshop, 0, 0);
                top.Controls.Add(colBuilt, 1, 0);
                top.Controls.Add(colOrder, 2, 0);

                // ensure all three footers consume the same vertical space so bottoms align perfectly
                void NormalizeFooters(params FlowLayoutPanel[] fps)
                {
                    foreach (var f in fps)
                    {
                        f.AutoSize = false;
                        f.MinimumSize = new Size(0, 40);
                        f.Height = 40;

                        // nudge 1px right so left edge lines up with the list's 1px padded border
                        f.Margin = new Padding(2, 8, 0, 0);
                        f.Padding = new Padding(0, 0, 0, 0);
                    }
                }
                NormalizeFooters(ftWorkshop, ftBuilt, ftOrder);



                // buttons — small, consistent
                AccentButton MakeAccent(string text) => new AccentButton
                {
                    Text = text,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    Padding = new Padding(10, 3, 10, 3),   // a touch shorter
                    Margin = new Padding(0, 0, 8, 0)
                };


                OutlineButton MakeOutline(string text) => new OutlineButton { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0, 0, 8, 0) };

                var btnAddWorkshop = MakeAccent("Add →");
                var btnAddBuilt = MakeAccent("Add →");
                ftWorkshop.Controls.Add(btnAddWorkshop);
                ftBuilt.Controls.Add(btnAddBuilt);

                var btnUp = MakeOutline("Up");
                var btnDown = MakeOutline("Down");
                var btnRemove = MakeOutline("Remove");
                ftOrder.Controls.AddRange(new Control[] { btnUp, btnDown, btnRemove });

                // add/remove logic
                void AddRange(ListBox src)
                {
                    foreach (var it in src.SelectedItems.Cast<string>().ToList())
                        if (!lbSel.Items.Contains(it))
                            lbSel.Items.Add(it);
                }
                void RemoveSelected()
                {
                    foreach (var it in lbSel.SelectedItems.Cast<string>().ToList())
                        lbSel.Items.Remove(it);
                }

                btnAddWorkshop.Click += (_, __) => AddRange(lbWorkshop);
                btnAddBuilt.Click += (_, __) => AddRange(lbBuilt);

                lbWorkshop.DoubleClick += (_, __) => AddRange(lbWorkshop);
                lbBuilt.DoubleClick += (_, __) => AddRange(lbBuilt);
                lbSel.DoubleClick += (_, __) => RemoveSelected();
                lbSel.KeyDown += (_, e) => { if (e.KeyCode == Keys.Delete) RemoveSelected(); };

                btnRemove.Click += (_, __) => RemoveSelected();

                btnUp.Click += (_, __) =>
                {
                    if (lbSel.SelectedItem is string it)
                    {
                        int i = lbSel.SelectedIndex;
                        if (i > 0)
                        {
                            lbSel.Items.RemoveAt(i);
                            lbSel.Items.Insert(i - 1, it);
                            lbSel.SelectedIndex = i - 1;
                        }
                    }
                };
                btnDown.Click += (_, __) =>
                {
                    if (lbSel.SelectedItem is string it)
                    {
                        int i = lbSel.SelectedIndex;
                        if (i >= 0 && i < lbSel.Items.Count - 1)
                        {
                            lbSel.Items.RemoveAt(i);
                            lbSel.Items.Insert(i + 1, it);
                            lbSel.SelectedIndex = i + 1;
                        }
                    }
                };

                var bottom = new TableLayoutPanel
                {
                    Dock = DockStyle.Bottom,                 // sit at the bottom edge
                    ColumnCount = 2,
                    RowCount = 1,
                    Padding = new Padding(0, 8, 0, 0)
                };
                bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
                bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                root.Controls.Add(bottom, 0, 1);

                var btnSave = new AccentButton
                {
                    Text = "Save",
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    Padding = new Padding(12, 6, 12, 6),
                    Anchor = AnchorStyles.Right | AnchorStyles.Bottom,   // pin to bottom-right of its cell
                    Margin = new Padding(8, 0, 0, 0)
                };

                var tip = new Label
                {
                    AutoSize = true,
                    ForeColor = UI.Muted,
                    Dock = DockStyle.Fill,                    // occupies left cell fully so baselines align
                    Text = "Tip: Double-click Workshop/Built to add to Load Order. In Load Order, double-click to remove. Use Up/Down to change priority."
                };
                bottom.Controls.Add(tip, 0, 0);
                bottom.Controls.Add(btnSave, 1, 0);



                dlg.AcceptButton = btnSave;
                btnSave.Click += (_, __) =>
                {
                    cfg.ExtraMods = lbSel.Items.Cast<string>().ToArray();
                    try { cfg.Save(); } catch { /* ignore */ }
                    Log($"Saved {cfg.ExtraMods.Length} selected mod(s).");
                    dlg.DialogResult = DialogResult.OK;
                };

                dlg.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Select Mods error");
            }
        }



        private static void UpdateServerCfgTemplateLine(SettingsModel cfg, string missionFolderName)
        {
            string cfgPath = cfg.Resolve(cfg.ServerConfig);
            string? dir = Path.GetDirectoryName(cfgPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            // If no file yet, write a minimal config and bail.
            if (!File.Exists(cfgPath))
            {
                var minimal =
                    "hostname = \"DayZ Local Dev\";\r\n" +
                    "password = \"\";\r\n" +
                    "passwordAdmin = \"\";\r\n" +
                    "enableWhitelist = 0;\r\n" +
                    "maxPlayers = 60;\r\n" +
                    "BattlEye = 0;\r\n" +
                    "verifySignatures = 0;\r\n" +
                    "allowFilePatching = 1;\r\n" +
                    "forceSameBuild = 1;\r\n\r\n" +
                    "class Missions\r\n" +
                    "{\r\n" +
                    "    class DayZ\r\n" +
                    "    {\r\n" +
                    $"        template = \"{missionFolderName}\";\r\n" +
                    "    };\r\n" +
                    "};\r\n";
                File.WriteAllText(cfgPath, minimal);
                return;
            }

            var text = File.ReadAllText(cfgPath);

            // 1) Remove ALL existing 'template = "...";' lines to prevent duplicates.
            //    (Full-line match; safe to run multiple times.)
            text = Regex.Replace(
                text,
                @"(?im)^\s*template\s*=\s*""[^""]*""\s*;\s*$",
                string.Empty
            );

            // 2) Try to insert inside an existing 'class DayZ { ... }' block.
            //    We just find 'class DayZ' (case-insensitive), then the next '{', and insert after it.
            int dayzIdx = text.IndexOf("class DayZ", StringComparison.OrdinalIgnoreCase);
            if (dayzIdx >= 0)
            {
                int braceIdx = text.IndexOf('{', dayzIdx);
                if (braceIdx >= 0)
                {
                    // Insert right after the '{'
                    string insertion = "\r\n        template = \"" + missionFolderName + "\";\r\n";
                    string result = text.Insert(braceIdx + 1, insertion);
                    File.WriteAllText(cfgPath, result);
                    return;
                }
            }

            // 3) If there's a Missions block but no DayZ block we can safely append a DayZ block at end.
            if (text.IndexOf("class Missions", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var dayzBlock =
                    "\r\n\r\n" +
                    "    class DayZ\r\n" +
                    "    {\r\n" +
                    $"        template = \"{missionFolderName}\";\r\n" +
                    "    };\r\n";
                File.WriteAllText(cfgPath, text.TrimEnd() + dayzBlock + "\r\n");
                return;
            }

            // 4) No Missions block at all → append a full Missions/DayZ block.
            var missionsBlock =
                "\r\n\r\n" +
                "class Missions\r\n" +
                "{\r\n" +
                "    class DayZ\r\n" +
                "    {\r\n" +
                $"        template = \"{missionFolderName}\";\r\n" +
                "    };\r\n" +
                "};\r\n";
            File.WriteAllText(cfgPath, text.TrimEnd() + missionsBlock);
        }

        // ---------------------- EDIT SETTINGS ----------------------
        private void EditSettings()
        {
            try
            {
                cfg ??= SettingsModel.Load();

                using var dlg = new Form
                {
                    Text = "Settings",
                    StartPosition = FormStartPosition.CenterParent,
                    Width = 920,
                    Height = 600,
                    MinimumSize = new Size(860, 520),
                    BackColor = UI.Bg,
                    ForeColor = UI.Text,
                    AutoScaleMode = AutoScaleMode.Font
                };

                // ===== Root =====
                var root = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 2,
                    Padding = new Padding(16, 16, 16, 12)
                };
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
                root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                dlg.Controls.Add(root);

                // Header
                var header = new Panel { Dock = DockStyle.Top, Padding = new Padding(6, 0, 6, 14) };
                header.Controls.Add(new Label
                {
                    Text = "Settings",
                    Font = UI.TitleFont,
                    Dock = DockStyle.Top,
                    AutoSize = true
                });
                header.Controls.Add(new Label
                {
                    Text = "Paths and diagnostics options used by Build/Start/Stop.",
                    Font = UI.SubTitleFont,
                    ForeColor = UI.Muted,
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    Padding = new Padding(0, 0, 0, 8)
                });

                // Stack to keep header above grid
                var stack = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 2,
                    Padding = Padding.Empty,
                    Margin = Padding.Empty
                };
                stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                stack.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
                stack.Controls.Add(header, 0, 0);
                root.Controls.Add(stack, 0, 0);

                // ===== Field grid: Label | TextBox | Button =====
                const int LabelCol = 190;
                const int BtnW = 96;
                const int VGap = 6;

                // We align bottoms by using a fixed row height = TextBox.PreferredHeight
                var probe = new TextBox();
                int RowH = probe.PreferredHeight;        // native textbox height on this system

                var grid = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 3,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    Padding = new Padding(6, 6, 6, 0),
                    Margin = Padding.Empty
                };
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, LabelCol));
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, BtnW));
                stack.Controls.Add(grid, 0, 1);

                int r = 0;
                void AddRowStyle() => grid.RowStyles.Add(new RowStyle(SizeType.Absolute, RowH + VGap));

                TextBox AddPathRow(string label, string initial, bool isFile, string? filter = null, string? btnTextOverride = null)
                {
                    AddRowStyle();

                    var lbl = new Label
                    {
                        Text = label,
                        Dock = DockStyle.Fill,
                        TextAlign = ContentAlignment.MiddleLeft,
                        AutoSize = false,
                        Margin = new Padding(0, 0, 8, VGap)
                    };

                    var tb = new TextBox
                    {
                        Text = initial ?? string.Empty,
                        Dock = DockStyle.Fill,
                        BackColor = UI.CardAlt,
                        ForeColor = UI.Text,
                        BorderStyle = BorderStyle.FixedSingle,
                        Margin = new Padding(0, 0, 8, VGap)
                    };
                    // keep textbox at its native height so row bottoms match
                    tb.MinimumSize = new Size(0, RowH);
                    tb.MaximumSize = new Size(int.MaxValue, RowH);

                    // Button inside a host panel, anchored Bottom|Right -> bottoms align perfectly
                    var btnHost = new Panel
                    {
                        Dock = DockStyle.Fill,
                        Margin = new Padding(0, 0, 0, VGap)
                    };
                    var btn = new OutlineButton
                    {
                        Text = string.IsNullOrWhiteSpace(btnTextOverride)
                               ? (isFile ? "Browse…" : "Pick…")
                               : btnTextOverride!,
                        AutoSize = false,
                        Dock = DockStyle.Fill,           // <- makes it show reliably
                        Margin = new Padding(0, 0, 0, VGap),
                        Padding = Padding.Empty
                    };
                    btnHost.Controls.Add(btn);

                    btn.Click += (_, __) =>
                    {
                        if (isFile)
                        {
                            using var ofd = new OpenFileDialog
                            {
                                Filter = filter ?? "Executable (*.exe)|*.exe|All files (*.*)|*.*",
                                CheckFileExists = false,
                                Title = $"Select {label}"
                            };
                            if (!string.IsNullOrWhiteSpace(tb.Text))
                            {
                                try { ofd.InitialDirectory = Path.GetDirectoryName(tb.Text)!; } catch { }
                            }
                            if (ofd.ShowDialog(dlg) == DialogResult.OK) tb.Text = ofd.FileName;
                        }
                        else
                        {
                            using var fbd = new FolderBrowserDialog
                            {
                                Description = $"Select folder for {label}",
                                UseDescriptionForTitle = true,
                                ShowNewFolderButton = true,
                                SelectedPath = Directory.Exists(tb.Text) ? tb.Text : cfg.RepoRoot
                            };
                            if (fbd.ShowDialog(dlg) == DialogResult.OK) tb.Text = fbd.SelectedPath;
                        }
                    };

                    grid.Controls.Add(lbl, 0, r);
                    grid.Controls.Add(tb, 1, r);
                    grid.Controls.Add(btnHost, 2, r);
                    r++;
                    return tb;
                }

                // Fields
                var tbGame = AddPathRow("DayZ Game folder", cfg.Resolve(cfg.DayZGamePath), isFile: false);
                var tbServer = AddPathRow("DayZ Server folder", cfg.Resolve(cfg.DayZServerPath), isFile: false);
                var tbAddonBuilder = AddPathRow("AddonBuilder.exe", cfg.Resolve(cfg.AddonBuilderPath), isFile: true);
                var tbWorkshop = AddPathRow("Workshop folder", cfg.Resolve(cfg.WorkshopPath), isFile: false);
                var tbProfiles = AddPathRow("Profiles folder", cfg.Resolve(cfg.ProfilesDir), isFile: false);
                var tbServerCfg = AddPathRow("serverDZ.cfg file", cfg.Resolve(cfg.ServerConfig), isFile: true, filter: "Config (*.cfg)|*.cfg|All files (*.*)|*.*");

                // Tip row
                AddRowStyle();
                var tip = new Label
                {
                    Text = "Tip: We'll update template = \"<mission>\" when you pick a mission.",
                    ForeColor = UI.Muted,
                    AutoSize = true,
                    Dock = DockStyle.Fill,
                    Margin = new Padding(0, 2, 0, 2)
                };
                grid.Controls.Add(new Label() { AutoSize = true, Margin = new Padding(0, 0, 8, 0) }, 0, r);
                grid.Controls.Add(tip, 1, r);
                grid.SetColumnSpan(tip, 2);
                r++;

                // Auto-detect (row) + checkbox BELOW it
                AddRowStyle();
                var detectStack = new TableLayoutPanel
                {
                    ColumnCount = 1,
                    RowCount = 2,
                    AutoSize = true,
                    Dock = DockStyle.Fill,
                    Margin = new Padding(0, 2, 0, 2),
                    Padding = Padding.Empty
                };
                detectStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                detectStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));

                var btnDetect = new OutlineButton
                {
                    Text = "Auto-detect common Steam paths",
                    AutoSize = true,
                    Margin = new Padding(0, 0, 0, 6)
                };
                var chkDiagBoth = new CheckBox
                {
                    Text = "Run in Diagnostics (server + client)",
                    AutoSize = true,
                    Margin = new Padding(2, 0, 0, 0)
                };
                detectStack.Controls.Add(btnDetect, 0, 0);
                detectStack.Controls.Add(chkDiagBoth, 0, 1);

                grid.Controls.Add(new Label() { AutoSize = true, Margin = new Padding(0, 0, 8, 0) }, 0, r);
                grid.Controls.Add(detectStack, 1, r);
                grid.SetColumnSpan(detectStack, 2);
                r++;

                btnDetect.Click += (_, __) =>
                {
                    string[] steamCommon =
                    {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),    "Steam", "steamapps", "common")
            };

                    string Guess(params string[] parts) => Path.Combine(parts);
                    string FindFirstExisting(params string[] candidates) =>
                        candidates.FirstOrDefault(p => Directory.Exists(p) || File.Exists(p)) ?? string.Empty;

                    var game = FindFirstExisting(steamCommon.Select(c => Guess(c, "DayZ")).ToArray());
                    if (!string.IsNullOrWhiteSpace(game)) tbGame.Text = game;

                    var server = FindFirstExisting(steamCommon.Select(c => Guess(c, "DayZServer")).ToArray());
                    if (string.IsNullOrWhiteSpace(server) && Directory.Exists(tbGame.Text))
                    {
                        var parent = Directory.GetParent(tbGame.Text)?.FullName ?? "";
                        var sib = Path.Combine(parent, "DayZServer");
                        if (Directory.Exists(sib)) server = sib;
                    }
                    if (!string.IsNullOrWhiteSpace(server)) tbServer.Text = server;

                    var addon = FindFirstExisting(steamCommon.Select(c => Guess(c, "DayZ Tools", "Bin", "AddonBuilder", "AddonBuilder.exe")).ToArray());
                    if (!string.IsNullOrWhiteSpace(addon)) tbAddonBuilder.Text = addon;

                    if (Directory.Exists(tbGame.Text))
                    {
                        var wk1 = Path.Combine(tbGame.Text, "!Workshop");
                        var wk2 = Path.Combine(tbGame.Text, "!dzsal");
                        if (Directory.Exists(wk1)) tbWorkshop.Text = wk1;
                        else if (Directory.Exists(wk2)) tbWorkshop.Text = wk2;
                    }
                };

                // ===== Bottom buttons =====
                var bottom = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 3,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    Padding = new Padding(0, 10, 0, 0)
                };
                bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
                bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
                bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
                root.Controls.Add(bottom, 0, 1);

                var btnCancel = new OutlineButton { Text = "Cancel", AutoSize = false, Width = 100, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 10, 0), Padding = Padding.Empty };
                var btnOK = new AccentButton { Text = "Save", AutoSize = false, Width = 100, Dock = DockStyle.Fill, Margin = new Padding(0), Padding = Padding.Empty };

                btnOK.Click += (_, __) =>
                {
                    if (!Directory.Exists(tbGame.Text)) { MessageBox.Show(dlg, "DayZ Game folder must exist."); tbGame.Focus(); return; }
                    if (!Directory.Exists(tbServer.Text)) { MessageBox.Show(dlg, "DayZ Server folder must exist."); tbServer.Focus(); return; }
                    if (!File.Exists(tbAddonBuilder.Text)) { MessageBox.Show(dlg, "AddonBuilder.exe must exist."); tbAddonBuilder.Focus(); return; }

                    if (!string.IsNullOrWhiteSpace(tbProfiles.Text))
                    {
                        try { Directory.CreateDirectory(tbProfiles.Text); } catch { }
                    }
                    if (!string.IsNullOrWhiteSpace(tbServerCfg.Text))
                    {
                        try
                        {
                            var dir = Path.GetDirectoryName(tbServerCfg.Text);
                            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
                            if (!File.Exists(tbServerCfg.Text)) File.WriteAllText(tbServerCfg.Text, "");
                        }
                        catch { }
                    }

                    cfg.DayZGamePath = tbGame.Text;
                    cfg.DayZServerPath = tbServer.Text;
                    cfg.AddonBuilderPath = tbAddonBuilder.Text;
                    cfg.WorkshopPath = tbWorkshop.Text;
                    cfg.ProfilesDir = tbProfiles.Text;
                    cfg.ServerConfig = tbServerCfg.Text;

                    cfg.RunServerInDiag = chkDiagBoth.Checked;
                    cfg.UseDiagClient = chkDiagBoth.Checked;

                    try { cfg.Save(); } catch { /* ignore */ }
                    Log("Settings saved.");
                    dlg.DialogResult = DialogResult.OK;
                };
                btnCancel.Click += (_, __) => dlg.DialogResult = DialogResult.Cancel;

                bottom.Controls.Add(new Label() { AutoSize = true }, 0, 0);
                bottom.Controls.Add(btnCancel, 1, 0);
                bottom.Controls.Add(btnOK, 2, 0);

                dlg.AcceptButton = btnOK;
                dlg.CancelButton = btnCancel;
                dlg.Icon = this.Icon;

                dlg.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Settings error");
            }
        }


        // ---------------------- helpers ----------------------
        private int RunProcess(string exe, string args, string? workingDir, Action<string>? onOut = null, Action<string>? onErr = null)
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = workingDir ?? ""
            };
            using var p = Process.Start(psi)!;
            p.OutputDataReceived += (_, e) => { if (e.Data != null) onOut?.Invoke(e.Data); };
            p.ErrorDataReceived += (_, e) => { if (e.Data != null) onErr?.Invoke(e.Data); };
            p.BeginOutputReadLine(); p.BeginErrorReadLine();
            p.WaitForExit();
            return p.ExitCode;
        }

        private void RunDetached(string exe, string args, string? workingDir)
        {
            var psi = new ProcessStartInfo(exe, args) { UseShellExecute = true, WorkingDirectory = workingDir ?? "" };
            Process.Start(psi);
        }

        private List<string> KillProcesses(IEnumerable<string> names, bool tryElevate)
        {
            var survivors = new List<string>();
            foreach (var name in names)
            {
                try { foreach (var p in Process.GetProcessesByName(name)) { p.Kill(true); p.WaitForExit(250); } }
                catch { /* ignore */ }
            }

            var exeNames = new HashSet<string>(new[] { "DayZServer_x64.exe", "DayZ_x64.exe", "DayZDiag_x64.exe", "DayZ_BE.exe" }, StringComparer.OrdinalIgnoreCase);
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    var n = p.ProcessName + ".exe";
                    if (exeNames.Contains(n))
                    {
                        try { p.Kill(true); p.WaitForExit(250); }
                        catch { survivors.Add(n + " (PID " + p.Id + ")"); }
                    }
                }
                catch { }
            }

            if (survivors.Count > 0 && tryElevate && !IsAdmin())
            {
                try
                {
                    var psi = new ProcessStartInfo(Application.ExecutablePath, "--stop-elevated") { UseShellExecute = true, Verb = "runas" };
                    Process.Start(psi);
                }
                catch { }
            }
            return survivors;
        }

        private static bool IsAdmin()
        {
            try { return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator); }
            catch { return false; }
        }

        private static void StartProcess(string exe, string args) =>
            Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = true });
    }
}
