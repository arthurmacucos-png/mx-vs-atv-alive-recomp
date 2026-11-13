// MX vs. ATV Alive - launcher for the native PC recompilation.
// Plain C# 5 / WinForms so it builds with the csc.exe that ships with Windows
// (see launcher\build-launcher.bat). No game assets are embedded: the banner is
// read at runtime from the user's own game folder.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace MxAliveLauncher
{
    static class Theme
    {
        public static readonly Color Back = Color.FromArgb(18, 18, 20);
        public static readonly Color Panel = Color.FromArgb(30, 30, 33);
        public static readonly Color Field = Color.FromArgb(42, 42, 46);
        public static readonly Color Border = Color.FromArgb(58, 58, 64);
        public static readonly Color Text = Color.FromArgb(232, 232, 232);
        public static readonly Color Dim = Color.FromArgb(150, 150, 156);
        public static readonly Color Accent = Color.FromArgb(222, 228, 46);   // Alive yellow
        public static readonly Color AccentDark = Color.FromArgb(110, 114, 24);
        public static readonly Color Good = Color.FromArgb(90, 200, 110);
        public static readonly Color Bad = Color.FromArgb(235, 80, 70);
        public static readonly Font Base = new Font("Segoe UI", 9.5f);
        public static readonly Font Small = new Font("Segoe UI", 8.5f);
        public static readonly Font Section = new Font("Segoe UI Semibold", 10.5f);
        public static readonly Font Title = new Font("Segoe UI Semibold", 20f);
        public static readonly Font Play = new Font("Segoe UI Semibold", 13f);
        public static readonly Font Mono = new Font("Consolas", 9f);
    }

    // A dark card with an orange accent bar and a title, like the UFC 3 launcher.
    class Section : Panel
    {
        readonly string title;
        public Section(string title, int x, int y, int w, int h)
        {
            this.title = title;
            SetBounds(x, y, w, h);
            BackColor = Theme.Panel;
            DoubleBuffered = true;
            Font = Theme.Section;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Theme.Border))
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            float k = Font.Size / Theme.Section.Size;
            using (var b = new SolidBrush(Theme.Accent))
                e.Graphics.FillRectangle(b, 12 * k, 12 * k, Math.Max(3, 3 * k), 18 * k);
            TextRenderer.DrawText(e.Graphics, title.ToUpperInvariant(), Font,
                new Point((int)(20 * k), (int)(10 * k)), Theme.Text);
        }
    }

    // Owner-drawn checkbox: the flat WinForms one hides the tick on dark backgrounds.
    class DarkCheck : CheckBox
    {
        public DarkCheck()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Cursor = Cursors.Hand;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Panel);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int box = Math.Max(12, (int)Math.Round(Font.Height * 0.94)), y = (Height - box) / 2;
            float q = box / 16f;
            var rect = new Rectangle(1, y, box, box);
            using (var fill = new SolidBrush(Checked ? Theme.Accent : Theme.Field))
                g.FillRectangle(fill, rect);
            using (var pen = new Pen(Checked ? Theme.Accent : (Enabled ? Color.FromArgb(110, 110, 118) : Theme.Border)))
                g.DrawRectangle(pen, rect);
            if (Checked)
                using (var pen = new Pen(Color.White, 2.2f))
                    g.DrawLines(pen, new[] { new PointF(1 + 3.5f * q, y + 8.5f * q), new PointF(1 + 6.5f * q, y + 11.5f * q), new PointF(1 + 12.5f * q, y + 4.5f * q) });
            TextRenderer.DrawText(g, Text, Font, new Point(box + 8, (Height - Font.Height) / 2),
                Enabled ? ForeColor : Theme.Dim);
        }
    }

    class Settings
    {
        readonly string path;
        readonly Dictionary<string, string> values = new Dictionary<string, string>();
        public Settings(string path)
        {
            this.path = path;
            try
            {
                if (File.Exists(path))
                    foreach (var line in File.ReadAllLines(path))
                    {
                        int i = line.IndexOf('=');
                        if (i > 0) values[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
                    }
            }
            catch { }
        }
        public string Get(string key, string def)
        {
            string v;
            return values.TryGetValue(key, out v) ? v : def;
        }
        public bool GetBool(string key, bool def) { return Get(key, def ? "1" : "0") == "1"; }
        public void Set(string key, string v) { values[key] = v; }
        public void Set(string key, bool v) { values[key] = v ? "1" : "0"; }
        public void Save()
        {
            try
            {
                File.WriteAllLines(path, values.OrderBy(k => k.Key).Select(k => k.Key + "=" + k.Value).ToArray());
            }
            catch { }
        }
    }

    class Option
    {
        public string Label, Value;
        public Option(string label, string value) { Label = label; Value = value; }
        public override string ToString() { return Label; }
    }

    static class Native
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct JOYCAPS
        {
            public ushort wMid, wPid;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szPname;
            public uint wXmin, wXmax, wYmin, wYmax, wZmin, wZmax, wNumButtons, wPeriodMin, wPeriodMax;
            public uint wRmin, wRmax, wUmin, wUmax, wVmin, wVmax, wCaps, wMaxAxes, wNumAxes, wMaxButtons;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szRegKey;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szOEMVxD;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct JOYINFOEX
        {
            public uint dwSize, dwFlags, dwXpos, dwYpos, dwZpos, dwRpos, dwUpos, dwVpos;
            public uint dwButtons, dwButtonNumber, dwPOV, dwReserved1, dwReserved2;
        }
        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        public static extern int joyGetDevCapsW(IntPtr id, ref JOYCAPS caps, int size);
        [DllImport("winmm.dll")]
        public static extern int joyGetPosEx(int id, ref JOYINFOEX info);
        [DllImport("winmm.dll")]
        public static extern int joyGetNumDevs();
        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();
    }

    // Reads Xbox 360 STFS packages (LIVE/PIRS/CON). Block and hash-table math
    // follows rexglue's StfsContainerDevice; same as tools\install-dlc.py.
    class StfsPackage : IDisposable
    {
        const int Block = 0x1000;
        const uint L0 = 170, L1 = 170 * 170, EndOfChain = 0xFFFFFF;

        readonly FileStream f;
        readonly Dictionary<long, byte[]> hashCache = new Dictionary<long, byte[]>();
        public uint TitleId, ContentType, VolumeType;
        public string DisplayName;
        uint headerSize, totalBlocks, ftBlock, bpht, step0, step1;
        int ftCount;
        bool readOnly, rootActive;
        long baseOffset;

        class Entry { public string Name; public bool Dir, Contiguous; public uint Start, Size; public int Parent; }

        static uint BE32(byte[] d, int o) { return (uint)(d[o] << 24 | d[o + 1] << 16 | d[o + 2] << 8 | d[o + 3]); }
        static uint U24LE(byte[] d, int o) { return (uint)(d[o] | d[o + 1] << 8 | d[o + 2] << 16); }

        StfsPackage(string path) { f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16); }

        public static StfsPackage Open(string path)
        {
            var p = new StfsPackage(path);
            try
            {
                var h = new byte[0x971A];
                if (p.f.Length < h.Length || p.f.Read(h, 0, h.Length) != h.Length) throw new InvalidDataException();
                string magic = Encoding.ASCII.GetString(h, 0, 4);
                if (magic != "LIVE" && magic != "PIRS" && magic != "CON ") throw new InvalidDataException();
                p.headerSize = BE32(h, 0x340);
                p.ContentType = BE32(h, 0x344);
                p.TitleId = BE32(h, 0x360);
                p.VolumeType = BE32(h, 0x3A9);
                string name = Encoding.BigEndianUnicode.GetString(h, 0x411, 0x80);
                int z = name.IndexOf('\0');
                p.DisplayName = z >= 0 ? name.Substring(0, z) : name;
                const int vd = 0x379;
                p.readOnly = (h[vd + 2] & 1) != 0;
                p.rootActive = (h[vd + 2] & 2) != 0;
                p.ftCount = h[vd + 3] | h[vd + 4] << 8;
                p.ftBlock = U24LE(h, vd + 5);
                p.totalBlocks = BE32(h, vd + 0x1C);
                p.bpht = p.readOnly ? 1u : 2u;
                p.step0 = L0 + p.bpht;
                p.step1 = L1 + (L0 + 1) * p.bpht;
                p.baseOffset = (p.headerSize + Block - 1) & ~(long)(Block - 1);
                return p;
            }
            catch { p.Dispose(); throw; }
        }

        public void Dispose() { f.Dispose(); }

        long BlockOffset(uint index)
        {
            ulong b = L0, block = index;
            for (int i = 0; i < 3; i++)
            {
                block += ((index + b) / b) * bpht;
                if (index < b) break;
                b *= L0;
            }
            return baseOffset + ((long)block << 12);
        }

        uint HashBlockNumber(uint index, int level)
        {
            if (level == 0)
            {
                if (index < L0) return 0;
                uint block = (index / L0) * step0 + ((index / L1) + 1) * bpht;
                return index < L1 ? block : block + bpht;
            }
            if (level == 1)
                return index < L1 ? step0 : (index / L1) * step1 + bpht;
            return step1;
        }

        byte[] HashTable(long offset)
        {
            byte[] t;
            if (!hashCache.TryGetValue(offset, out t))
            {
                t = new byte[Block];
                f.Seek(offset, SeekOrigin.Begin);
                f.Read(t, 0, Block);
                hashCache[offset] = t;
            }
            return t;
        }

        static uint Info(byte[] table, uint record) { return BE32(table, (int)(record * 0x18 + 0x14)); }

        uint NextBlock(uint index)
        {
            long secondary = rootActive ? Block : 0;
            if (readOnly) secondary = 0;
            else if (totalBlocks > L0)
            {
                if (totalBlocks > L1)
                {
                    var t2 = HashTable(baseOffset + ((long)HashBlockNumber(index, 2) << 12) + secondary);
                    secondary = (Info(t2, (index / L1) % L0) & 0x40000000) != 0 ? Block : 0;
                }
                var t1 = HashTable(baseOffset + ((long)HashBlockNumber(index, 1) << 12) + secondary);
                secondary = (Info(t1, (index / L0) % L0) & 0x40000000) != 0 ? Block : 0;
            }
            var t0 = HashTable(baseOffset + ((long)HashBlockNumber(index, 0) << 12) + secondary);
            return Info(t0, index % L0) & 0xFFFFFF;
        }

        List<Entry> Entries()
        {
            var list = new List<Entry>();
            uint block = ftBlock;
            var data = new byte[Block];
            for (int n = 0; n < ftCount; n++)
            {
                f.Seek(BlockOffset(block), SeekOrigin.Begin);
                f.Read(data, 0, Block);
                for (int m = 0; m < 64; m++)
                {
                    int o = m * 0x40;
                    if (data[o] == 0) break;
                    int flags = data[o + 0x28];
                    list.Add(new Entry
                    {
                        Name = Encoding.GetEncoding(28591).GetString(data, o, flags & 0x3F),
                        Dir = (flags & 0x80) != 0,
                        Contiguous = (flags & 0x40) != 0,
                        Start = U24LE(data, o + 0x2F),
                        Parent = data[o + 0x32] << 8 | data[o + 0x33],
                        Size = BE32(data, o + 0x34)
                    });
                }
                block = NextBlock(block);
                if (block == EndOfChain) break;
            }
            return list;
        }

        public void ExtractTo(string outDir)
        {
            if (VolumeType != 0) throw new NotSupportedException("pacote SVOD não suportado");
            var entries = Entries();
            var buf = new byte[Block];
            foreach (var e in entries)
            {
                var parts = new List<string> { e.Name };
                int p = e.Parent, guard = 0;
                while (p != 0xFFFF && p < entries.Count && guard++ < 64) { parts.Insert(0, entries[p].Name); p = entries[p].Parent; }
                string target = Path.Combine(outDir, Path.Combine(parts.ToArray()));
                if (e.Dir) { Directory.CreateDirectory(target); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                using (var o = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16))
                {
                    long remaining = e.Size;
                    uint block = e.Start;
                    while (remaining > 0 && block != EndOfChain)
                    {
                        int count = (int)Math.Min(Block, remaining);
                        f.Seek(BlockOffset(block), SeekOrigin.Begin);
                        if (f.Read(buf, 0, count) != count) throw new IOException("pacote truncado");
                        o.Write(buf, 0, count);
                        remaining -= count;
                        block = e.Contiguous ? block + 1 : NextBlock(block);
                    }
                    if (remaining > 0) throw new IOException(e.Name + ": faltam " + remaining + " bytes");
                }
            }
        }

        // XCONTENT_AGGREGATE_DATA (0x148 bytes, big-endian) + host-order u32 license.
        public static byte[] AggregateHeader(string fileName, string displayName, uint titleId, uint license)
        {
            var b = new byte[0x148 + 4];
            b[3] = 1;                       // device_id = HDD
            b[7] = 2;                       // content_type = marketplace
            var name = Encoding.BigEndianUnicode.GetBytes(displayName);
            Array.Copy(name, 0, b, 8, Math.Min(name.Length, 254));
            var fn = Encoding.ASCII.GetBytes(fileName);
            Array.Copy(fn, 0, b, 0x108, Math.Min(fn.Length, 42));
            // xuid (0x138) stays 0: marketplace content is shared by all profiles.
            b[0x140] = (byte)(titleId >> 24); b[0x141] = (byte)(titleId >> 16);
            b[0x142] = (byte)(titleId >> 8); b[0x143] = (byte)titleId;
            BitConverter.GetBytes(license).CopyTo(b, 0x148);
            return b;
        }
    }

    class LauncherForm : Form
    {
        const string GameDirName = @"Game\MX vs. ATV Alive (USA Europe)";
        const string ExeRel = @"out\build\win-amd64-release\mx_alive.exe";
        const string Version = "1.0";

        readonly string root;
        readonly Settings settings;
        readonly string saveRoot;

        TextBox gameDir;
        Label gameStatus, dlcStatus;
        Button installDlc;
        ComboBox language, resolution, fps, upscaling, backend, gpuMode;
        CheckBox fullscreen, vsync, mnk, mnkMouse, backup, clearCache, debugLog, asyncReadback, mute;
        NumericUpDown sensitivity;
        ListBox controllers;
        Button play, stop;
        Label fpsLabel;
        TextBox log;
        PictureBox banner;

        Process game;
        string tailPath;
        long tailPos;
        DateTime launchTime;
        readonly Timer tailTimer = new Timer();

        public LauncherForm(string root)
        {
            this.root = root;
            settings = new Settings(Path.Combine(root, "launcher.ini"));
            saveRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "mx_alive");

            Text = "MX vs. ATV Alive - port nativo para PC";
            BackColor = Theme.Back;
            ForeColor = Theme.Text;
            Font = Theme.Base;
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(920, 930);
            // Small minimum: on a TV / small screen / high Windows scaling 820x780
            // did not fit. The layout scales down (see Rescale).
            MinimumSize = new Size(480, 480);
            WindowState = FormWindowState.Maximized;
            KeyPreview = true;
            AutoScaleMode = AutoScaleMode.Dpi;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            BuildHeader();
            BuildGame();
            BuildVideo();
            BuildControls();
            BuildAdvanced();
            BuildTools();
            BuildBottom();
            WrapInScalableContent();
            Load += delegate { FitRestoredSizeToScreen(); };

            LoadSettings();
            EventHandler syncMnk = delegate { mnkMouse.Enabled = sensitivity.Enabled = mnk.Checked; };
            mnk.CheckedChanged += syncMnk;
            syncMnk(null, EventArgs.Empty);
            tailTimer.Interval = 500;
            tailTimer.Tick += delegate { TailLog(); };

            Shown += delegate
            {
                ActiveControl = play;
                gameDir.SelectionStart = 0; gameDir.SelectionLength = 0;
                Log("Launcher v" + Version + "  |  pasta: " + root);
                CheckGame();
                ScanControllers();
                Log("Saves: " + saveRoot);
                UpdateDlcStatus(true);
            };
            FormClosing += delegate { SaveSettings(); };
        }

        // ---------- scaling (fullscreen / any window size) ----------
        // The layout is designed at 920x930. Everything lives in one panel that
        // is scaled uniformly to fit the window and kept centered.
        Panel content;
        float currentScale = 1f;
        Size designSize;
        readonly Dictionary<Control, float> baseFontSizes = new Dictionary<Control, float>();

        void WrapInScalableContent()
        {
            content = new Panel { BackColor = Theme.Back, Size = ClientSize, Location = Point.Empty };
            var children = Controls.Cast<Control>().ToList();
            foreach (var c in children) content.Controls.Add(c);
            Controls.Add(content);
            designSize = content.Size;
            RememberFonts(content);
            Resize += delegate { Rescale(); };
            Shown += delegate { Rescale(); };
            KeyDown += (s, e) => { if (e.KeyCode == Keys.F11) ToggleFullscreen(); };
        }

        void RememberFonts(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                baseFontSizes[c] = c.Font.Size;
                RememberFonts(c);
            }
        }

        void Rescale()
        {
            if (content == null || ClientSize.Width < 50 || ClientSize.Height < 50) return;
            float target = Math.Min(ClientSize.Width / (float)designSize.Width,
                                    ClientSize.Height / (float)designSize.Height);
            target = Math.Max(0.4f, Math.Min(3f, target));
            if (Math.Abs(target - currentScale) > 0.005f)
            {
                SuspendLayout();
                content.SuspendLayout();
                content.Scale(new SizeF(target / currentScale, target / currentScale));
                foreach (var kv in baseFontSizes)
                {
                    var f = kv.Key.Font;
                    kv.Key.Font = new Font(f.FontFamily, kv.Value * target, f.Style);
                }
                currentScale = target;
                content.ResumeLayout();
                ResumeLayout();
                content.Invalidate(true);
            }
            content.Location = new Point(Math.Max(0, (ClientSize.Width - content.Width) / 2),
                                         Math.Max(0, (ClientSize.Height - content.Height) / 2));
        }

        // The window opens maximized; make sure "restore down" also fits the screen.
        void FitRestoredSizeToScreen()
        {
            Rectangle wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            int w = Math.Min(Width, (int)(wa.Width * 0.95f)), h = Math.Min(Height, (int)(wa.Height * 0.95f));
            MinimumSize = new Size(Math.Min(MinimumSize.Width, wa.Width), Math.Min(MinimumSize.Height, wa.Height));
            if (w == Width && h == Height) return;
            var b = new Rectangle(wa.Left + (wa.Width - w) / 2, wa.Top + (wa.Height - h) / 2, w, h);
            if (WindowState == FormWindowState.Normal) Bounds = b;
            else { WindowState = FormWindowState.Normal; Bounds = b; WindowState = FormWindowState.Maximized; }
        }

        FormWindowState stateBeforeFullscreen = FormWindowState.Maximized;
        void ToggleFullscreen()
        {
            if (FormBorderStyle == FormBorderStyle.None)
            {
                FormBorderStyle = FormBorderStyle.Sizable;
                WindowState = stateBeforeFullscreen;
            }
            else
            {
                stateBeforeFullscreen = WindowState;
                WindowState = FormWindowState.Normal;
                FormBorderStyle = FormBorderStyle.None;
                WindowState = FormWindowState.Maximized;
            }
        }

        // ---------- layout helpers ----------
        static Label MakeLabel(Control parent, string text, int x, int y, int w = 152)
        {
            var l = new Label { Text = text, ForeColor = Theme.Text, BackColor = Color.Transparent, AutoSize = false };
            l.SetBounds(x, y + 3, w, 20);
            parent.Controls.Add(l);
            return l;
        }
        static ComboBox MakeCombo(Control parent, int x, int y, int w, params Option[] items)
        {
            var c = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Field,
                ForeColor = Theme.Text
            };
            c.SetBounds(x, y, w, 24);
            c.Items.AddRange(items);
            c.SelectedIndex = 0;
            parent.Controls.Add(c);
            return c;
        }
        static CheckBox MakeCheck(Control parent, string text, int x, int y, int w = 200)
        {
            var c = new DarkCheck { Text = text, ForeColor = Theme.Text, BackColor = Theme.Panel };
            c.SetBounds(x, y, w, 24);
            parent.Controls.Add(c);
            return c;
        }
        static Button MakeButton(Control parent, string text, int x, int y, int w, int h = 28)
        {
            var b = new Button { Text = text, FlatStyle = FlatStyle.Flat, BackColor = Theme.Field, ForeColor = Theme.Text, Cursor = Cursors.Hand };
            b.FlatAppearance.BorderColor = Theme.Border;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 60, 66);
            b.SetBounds(x, y, w, h);
            parent.Controls.Add(b);
            return b;
        }
        static Label MakeHint(Control parent, string text, int x, int y, int w, int h)
        {
            var l = new Label { Text = text, ForeColor = Theme.Dim, Font = Theme.Small, BackColor = Color.Transparent };
            l.SetBounds(x, y, w, h);
            parent.Controls.Add(l);
            return l;
        }
        static void SelectByValue(ComboBox c, string value)
        {
            for (int i = 0; i < c.Items.Count; i++)
                if (((Option)c.Items[i]).Value == value) { c.SelectedIndex = i; return; }
        }
        static string Val(ComboBox c) { return ((Option)c.SelectedItem).Value; }

        // ---------- sections ----------
        void BuildHeader()
        {
            var header = new Panel { BackColor = Color.FromArgb(14, 22, 34) };
            header.SetBounds(0, 0, 920, 150);
            header.Paint += (s, e) =>
            {
                var hp = (Control)s;
                using (var b = new SolidBrush(Theme.Accent)) e.Graphics.FillRectangle(b, 0, hp.Height - 3, hp.Width, 3);
            };
            Controls.Add(header);

            banner = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Black };
            banner.SetBounds(16, 13, 160, 122);
            header.Controls.Add(banner);

            var t = new Label { Text = "MX vs. ATV Alive", Font = Theme.Title, ForeColor = Theme.Accent, AutoSize = true, BackColor = Color.Transparent };
            t.Location = new Point(196, 30);
            header.Controls.Add(t);
            var t2 = new Label { Text = "port nativo para PC", Font = new Font("Segoe UI Semibold", 13f), ForeColor = Theme.Text, AutoSize = true, BackColor = Color.Transparent };
            t2.Location = new Point(199, 70);
            header.Controls.Add(t2);
            var t3 = new Label { Text = "Xbox 360  5451087B  ·  rexglue v0.10.0  ·  use sua própria cópia do jogo", Font = Theme.Small, ForeColor = Theme.Dim, AutoSize = true, BackColor = Color.Transparent };
            t3.Location = new Point(200, 102);
            header.Controls.Add(t3);
        }

        void BuildGame()
        {
            var s = new Section("Jogo", 16, 162, 436, 196);
            Controls.Add(s);
            MakeLabel(s, "Pasta do jogo (com o default.xex)", 16, 38, 400);
            gameDir = new TextBox { BackColor = Theme.Field, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle };
            gameDir.SetBounds(16, 62, 316, 24);
            s.Controls.Add(gameDir);
            var browse = MakeButton(s, "Procurar", 338, 61, 82, 26);
            browse.Click += delegate
            {
                using (var d = new FolderBrowserDialog { Description = "Escolha a pasta do jogo (a que tem o default.xex)", SelectedPath = gameDir.Text })
                    if (d.ShowDialog(this) == DialogResult.OK) { gameDir.Text = d.SelectedPath; CheckGame(); }
            };
            gameStatus = new Label { ForeColor = Theme.Dim, BackColor = Color.Transparent, Font = Theme.Small };
            gameStatus.SetBounds(16, 92, 404, 20);
            s.Controls.Add(gameStatus);

            MakeLabel(s, "Idioma", 16, 114);
            language = MakeCombo(s, 170, 114, 250,
                new Option("English", "1"),
                new Option("Español", "5"),
                new Option("Français", "4"),
                new Option("Deutsch", "3"),
                new Option("Italiano", "6"));
            MakeLabel(s, "Áudio", 16, 144);
            mute = MakeCheck(s, "Sem som (mudo)", 170, 144, 250);
            MakeLabel(s, "DLC", 16, 168);
            dlcStatus = new Label { ForeColor = Theme.Dim, BackColor = Color.Transparent, Font = Theme.Small };
            dlcStatus.SetBounds(170, 172, 250, 18);
            s.Controls.Add(dlcStatus);
        }

        void BuildVideo()
        {
            var s = new Section("Vídeo", 468, 162, 436, 272);
            Controls.Add(s);
            MakeLabel(s, "Resolução do jogo", 16, 42);
            resolution = MakeCombo(s, 170, 42, 250,
                new Option("1280x720 (original)", "720"),
                new Option("1920x1080 (Full HD)", "1080"));
            MakeLabel(s, "Limite de FPS", 16, 76);
            fps = MakeCombo(s, 170, 76, 250,
                new Option("30 FPS (console)", "30"),
                new Option("60 FPS (recomendado)", "60"),
                new Option("120 FPS (instável)", "120"),
                new Option("144 FPS (instável)", "144"),
                new Option("240 FPS (instável)", "240"),
                new Option("Sem limite (teste)", "500"));
            MakeLabel(s, "Upscaling", 16, 110);
            upscaling = MakeCombo(s, 170, 110, 250,
                new Option("Bilinear (padrão)", "bilinear"),
                new Option("AMD CAS (nitidez)", "cas"),
                new Option("AMD FSR 1.0", "fsr"),
                new Option("AMD FSR 2 (experimental)", "fsr2"),
                new Option("AMD FSR 3 (experimental)", "fsr3"));
            fullscreen = MakeCheck(s, "Tela cheia", 16, 144, 190);
            vsync = MakeCheck(s, "VSync", 220, 144, 190);
            MakeLabel(s, "Renderização", 16, 174);
            gpuMode = MakeCombo(s, 170, 174, 250,
                new Option("Padrão (rápido)", "default"),
                new Option("ROV (precisão, NVIDIA)", "rov"),
                new Option("Cópia exata (sem piscar)", "full"),
                new Option("ROV + cópia exata", "rov_full"));
            MakeHint(s,
                "60 FPS é o modo testado e estável. Acima disso as corridas ficam\n" +
                "entre 85 e 95 FPS (limite do processador). 1080p é um modo nativo\n" +
                "do Xbox 360. O modo 2K foi removido de propósito: ele corrompe o save.",
                16, 204, 410, 62);
        }

        void BuildControls()
        {
            var s = new Section("Controle", 16, 370, 436, 302);
            Controls.Add(s);
            MakeLabel(s, "Controles detectados", 16, 38, 300);
            controllers = new ListBox { BackColor = Theme.Field, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, Font = Theme.Small, IntegralHeight = false };
            controllers.SetBounds(16, 62, 316, 72);
            s.Controls.Add(controllers);
            var rescan = MakeButton(s, "Procurar", 338, 62, 82, 28);
            rescan.Click += delegate { ScanControllers(); };

            MakeLabel(s, "Driver de controle", 16, 146);
            backend = MakeCombo(s, 170, 146, 250,
                new Option("SDL3 - PS5, PS4, Switch, genéricos", "sdl"),
                new Option("XInput - só controle de Xbox", "xinput"));
            mnk = MakeCheck(s, "Teclado e mouse", 16, 182, 190);
            mnkMouse = MakeCheck(s, "Mouse move a câmera", 220, 182, 200);
            MakeLabel(s, "Sensibilidade", 16, 216, 154);
            sensitivity = new NumericUpDown { DecimalPlaces = 1, Increment = 0.1m, Minimum = 0.1m, Maximum = 5m, Value = 1m, BackColor = Theme.Field, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle };
            sensitivity.SetBounds(170, 216, 80, 24);
            s.Controls.Add(sensitivity);
            MakeHint(s,
                "Conecte o controle antes de clicar em JOGAR. Se o DualSense não\n" +
                "responder, feche a Steam (o Steam Input pode prender o controle).",
                16, 250, 410, 40);
        }

        void BuildAdvanced()
        {
            var s = new Section("Avançado", 468, 446, 436, 226);
            Controls.Add(s);
            backup = MakeCheck(s, "Backup do save antes de jogar (recomendado)", 16, 42, 410);
            asyncReadback = MakeCheck(s, "Cópia de vídeo assíncrona (mais FPS)", 16, 72, 410);
            clearCache = MakeCheck(s, "Limpar o cache de shaders ao iniciar", 16, 102, 410);
            debugLog = MakeCheck(s, "Log detalhado (para investigar travamentos)", 16, 132, 410);
            MakeHint(s,
                "Para salvar o progresso, volte ao menu da carreira depois da\n" +
                "prova. Sair direto da tela de resultado não grava o save.",
                16, 168, 410, 40);
        }

        void BuildTools()
        {
            var s = new Section("Arquivos", 16, 684, 888, 64);
            Controls.Add(s);
            int x = 128;
            var b1 = MakeButton(s, "Abrir logs", x, 14, 110, 36); x += 118;
            b1.Click += delegate { OpenFolder(Path.Combine(root, @"out\build\win-amd64-release\logs")); };
            var b2 = MakeButton(s, "Abrir saves", x, 14, 110, 36); x += 118;
            b2.Click += delegate { OpenFolder(saveRoot); };
            var b3 = MakeButton(s, "Backup agora", x, 14, 120, 36); x += 128;
            b3.Click += delegate { string d = BackupSave(); Log(d == null ? "Nada para copiar ainda (nenhum save)." : "Backup criado: " + d); };
            installDlc = MakeButton(s, "Instalar DLC...", x, 14, 130, 36); x += 138;
            installDlc.FlatAppearance.BorderColor = Theme.Accent;
            installDlc.Click += delegate { InstallDlcDialog(); };
            var b4 = MakeButton(s, "Criar atalho", x, 14, 118, 36); x += 126;
            b4.Click += delegate { CreateShortcut(); };
            var b5 = MakeButton(s, "Gerar relatório", x, 14, 132, 36);
            b5.FlatAppearance.BorderColor = Theme.Accent;
            b5.Click += delegate { MakeReport(); };
        }

        void BuildBottom()
        {
            play = new Button { Text = "JOGAR", Font = Theme.Play, FlatStyle = FlatStyle.Flat, BackColor = Theme.Accent, ForeColor = Color.FromArgb(18, 20, 24), Cursor = Cursors.Hand };
            play.FlatAppearance.BorderColor = Color.FromArgb(240, 245, 120);
            play.FlatAppearance.MouseOverBackColor = Color.FromArgb(238, 244, 90);
            play.SetBounds(16, 760, 180, 48);
            play.Click += delegate { Launch(); };
            Controls.Add(play);

            stop = new Button { Text = "PARAR", Font = Theme.Play, FlatStyle = FlatStyle.Flat, BackColor = Theme.Panel, ForeColor = Theme.Dim, Enabled = false };
            stop.FlatAppearance.BorderColor = Theme.Border;
            stop.SetBounds(206, 760, 120, 48);
            stop.Click += delegate { StopGame(); };
            Controls.Add(stop);

            fpsLabel = new Label { Text = "", Font = new Font("Segoe UI Semibold", 12f), ForeColor = Theme.Dim, TextAlign = ContentAlignment.MiddleRight };
            fpsLabel.SetBounds(560, 760, 344, 48);
            Controls.Add(fpsLabel);

            log = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.FromArgb(12, 12, 14), ForeColor = Color.FromArgb(200, 200, 200), Font = Theme.Mono, BorderStyle = BorderStyle.FixedSingle, WordWrap = true };
            log.SetBounds(16, 820, 888, 96);
            Controls.Add(log);
        }

        // ---------- settings ----------
        void LoadSettings()
        {
            gameDir.Text = settings.Get("game_dir", Path.Combine(root, GameDirName));
            SelectByValue(language, settings.Get("language", "1"));
            SelectByValue(resolution, settings.Get("resolution", "720"));
            SelectByValue(fps, settings.Get("fps", "60"));
            SelectByValue(upscaling, settings.Get("upscaling", "bilinear"));
            SelectByValue(gpuMode, settings.Get("gpu_mode", "default"));
            SelectByValue(backend, settings.Get("backend", "sdl"));
            fullscreen.Checked = settings.GetBool("fullscreen", true);
            vsync.Checked = settings.GetBool("vsync", true);
            mnk.Checked = settings.GetBool("mnk", true);
            mnkMouse.Checked = settings.GetBool("mnk_mouse", false);
            decimal sens;
            if (decimal.TryParse(settings.Get("mnk_sensitivity", "1.0"), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out sens))
                sensitivity.Value = Math.Max(sensitivity.Minimum, Math.Min(sensitivity.Maximum, sens));
            backup.Checked = settings.GetBool("backup", true);
            asyncReadback.Checked = settings.GetBool("async_readback", true);
            clearCache.Checked = settings.GetBool("clear_cache", false);
            debugLog.Checked = settings.GetBool("debug_log", false);
            mute.Checked = settings.GetBool("mute", false);
        }

        void SaveSettings()
        {
            settings.Set("game_dir", gameDir.Text);
            settings.Set("language", Val(language));
            settings.Set("resolution", Val(resolution));
            settings.Set("fps", Val(fps));
            settings.Set("upscaling", Val(upscaling));
            settings.Set("gpu_mode", Val(gpuMode));
            settings.Set("backend", Val(backend));
            settings.Set("fullscreen", fullscreen.Checked);
            settings.Set("vsync", vsync.Checked);
            settings.Set("mnk", mnk.Checked);
            settings.Set("mnk_mouse", mnkMouse.Checked);
            settings.Set("mnk_sensitivity", sensitivity.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            settings.Set("backup", backup.Checked);
            settings.Set("async_readback", asyncReadback.Checked);
            settings.Set("clear_cache", clearCache.Checked);
            settings.Set("debug_log", debugLog.Checked);
            settings.Set("mute", mute.Checked);
            settings.Save();
        }

        // ---------- game checks ----------
        bool CheckGame()
        {
            string dir = gameDir.Text.Trim();
            string xex = Path.Combine(dir, "default.xex");
            LoadBanner(dir);
            if (!File.Exists(xex))
            {
                SetStatus("default.xex não encontrado nesta pasta.", Theme.Bad);
                return false;
            }
            string info = ReadXexInfo(xex);
            if (info == null)
            {
                SetStatus("default.xex inválido ou ilegível.", Theme.Bad);
                return false;
            }
            bool exact = info.Contains("5451087B") && info.Contains("5306E1D5") && info.Contains("0.0.0.3");
            SetStatus((exact ? "OK  " : "Versão diferente da testada  ") + info, exact ? Theme.Good : Theme.Accent);
            Log("Jogo: " + info + (exact ? " (versão testada)" : " (ATENÇÃO: versão não testada)"));
            if (!File.Exists(Path.Combine(root, ExeRel)))
            {
                Log("mx_alive.exe não encontrado. Rode tools\\build-game.bat primeiro.");
                return false;
            }
            return true;
        }

        void SetStatus(string text, Color c) { gameStatus.Text = text; gameStatus.ForeColor = c; }

        // Cover art and the "A" icon come from the game's own dashboard art
        // package (nxeart): the largest embedded JPEG and the 64x64 PNG.
        // (TitleMarketPlace.png in the disc is a leftover MX vs ATV Reflex banner.)
        void LoadBanner(string dir)
        {
            try
            {
                if (banner.Image != null) { banner.Image.Dispose(); banner.Image = null; }
                string art = Path.Combine(dir, "nxeart");
                if (!File.Exists(art)) return;
                byte[] d = File.ReadAllBytes(art);
                Image best = null;
                foreach (var blob in FindBlobs(d, new byte[] { 0xFF, 0xD8, 0xFF }, new byte[] { 0xFF, 0xD9 }, 2))
                {
                    var img = TryImage(blob);
                    if (img == null) continue;
                    if (best == null || img.Width * img.Height > best.Width * best.Height) { if (best != null) best.Dispose(); best = img; }
                    else img.Dispose();
                }
                banner.Image = best;
                foreach (var blob in FindBlobs(d, new byte[] { 0x89, 0x50, 0x4E, 0x47 }, new byte[] { 0x49, 0x45, 0x4E, 0x44 }, 8))
                {
                    var img = TryImage(blob) as Bitmap;
                    if (img == null) continue;
                    Icon = Icon.FromHandle(img.GetHicon());
                    break;
                }
            }
            catch { }
        }

        static Image TryImage(byte[] data)
        {
            try
            {
                using (var ms = new MemoryStream(data))
                using (var img = Image.FromStream(ms))
                    return new Bitmap(img);
            }
            catch { return null; }
        }

        static IEnumerable<byte[]> FindBlobs(byte[] d, byte[] start, byte[] end, int endExtra)
        {
            int i = 0;
            while (true)
            {
                i = IndexOf(d, start, i);
                if (i < 0) yield break;
                int j = IndexOf(d, end, i + start.Length);
                if (j < 0) yield break;
                int len = j + endExtra - i;
                var blob = new byte[len];
                Buffer.BlockCopy(d, i, blob, 0, len);
                yield return blob;
                i += start.Length;
            }
        }

        static int IndexOf(byte[] d, byte[] p, int from)
        {
            for (int i = from; i <= d.Length - p.Length; i++)
            {
                int k = 0;
                while (k < p.Length && d[i + k] == p[k]) k++;
                if (k == p.Length) return i;
            }
            return -1;
        }

        static uint BE32(byte[] d, int o) { return (uint)(d[o] << 24 | d[o + 1] << 16 | d[o + 2] << 8 | d[o + 3]); }
        static string Ver(uint v) { return (v >> 28) + "." + ((v >> 24) & 0xF) + "." + ((v >> 8) & 0xFFFF) + "." + (v & 0xFF); }

        static string ReadXexInfo(string path)
        {
            try
            {
                byte[] d = new byte[0x4000];
                using (var f = File.OpenRead(path)) f.Read(d, 0, d.Length);
                if (d[0] != 'X' || d[1] != 'E' || d[2] != 'X' || d[3] != '2') return null;
                uint count = BE32(d, 0x14);
                for (int i = 0; i < count && 0x20 + i * 8 <= d.Length; i++)
                {
                    uint key = BE32(d, 0x18 + i * 8), val = BE32(d, 0x1C + i * 8);
                    if (key == 0x00040006 && val + 16 <= d.Length)
                    {
                        int o = (int)val;
                        return string.Format("Title {0:X8}  Media {1:X8}  v{2}", BE32(d, o + 12), BE32(d, o), Ver(BE32(d, o + 4)));
                    }
                }
            }
            catch { }
            return null;
        }

        // ---------- controllers ----------
        static string PadName(ushort vid, ushort pid, string fallback)
        {
            if (vid == 0x054C)
            {
                if (pid == 0x0CE6) return "DualSense (PS5)";
                if (pid == 0x0DF2) return "DualSense Edge (PS5)";
                if (pid == 0x05C4 || pid == 0x09CC || pid == 0x0BA0) return "DualShock 4 (PS4)";
                if (pid == 0x0268) return "DualShock 3 (PS3)";
                return "Controle Sony";
            }
            if (vid == 0x045E) return "Controle Xbox";
            if (vid == 0x057E) return pid == 0x2009 ? "Switch Pro Controller" : "Controle Nintendo";
            if (vid == 0x2DC8) return "8BitDo";
            if (vid == 0x0F0D) return "Hori";
            if (vid == 0x0E6F || vid == 0x24C6) return "PowerA / PDP";
            return "Controle genérico" + (string.IsNullOrEmpty(fallback) ? "" : " - " + fallback);
        }

        void ScanControllers()
        {
            controllers.Items.Clear();
            int n = 0;
            try
            {
                int devs = Native.joyGetNumDevs();
                for (int id = 0; id < Math.Min(devs, 16); id++)
                {
                    var info = new Native.JOYINFOEX { dwSize = (uint)Marshal.SizeOf(typeof(Native.JOYINFOEX)), dwFlags = 0xFF };
                    if (Native.joyGetPosEx(id, ref info) != 0) continue; // not plugged in
                    var caps = new Native.JOYCAPS();
                    if (Native.joyGetDevCapsW((IntPtr)id, ref caps, Marshal.SizeOf(typeof(Native.JOYCAPS))) != 0) continue;
                    controllers.Items.Add(string.Format("{0}   [{1:X4}:{2:X4}]", PadName(caps.wMid, caps.wPid, caps.szPname), caps.wMid, caps.wPid));
                    n++;
                }
            }
            catch (Exception ex) { Log("Erro ao listar controles: " + ex.Message); }
            if (n == 0) controllers.Items.Add("Nenhum controle conectado (teclado e mouse disponíveis)");
            Log("Controles: " + n + " encontrado(s).");
        }

        // ---------- launch ----------
        // Always pass cvars as --name=value: cvars registered by modules that
        // load after startup (the GPU plugin) ignore the "--name value" form.
        static string Q(string s) { return "\"" + s.Replace("\"", "") + "\""; }

        void Launch()
        {
            if (game != null && !game.HasExited) { Log("O jogo já está aberto."); return; }
            SaveSettings();
            if (!CheckGame()) { Log("Não dá para iniciar: confira a pasta do jogo."); return; }

            string exe = Path.Combine(root, ExeRel);
            string dir = gameDir.Text.Trim();

            if (backup.Checked)
            {
                string b = BackupSave();
                if (b != null) Log("Backup do save: " + b);
            }
            if (clearCache.Checked)
            {
                try
                {
                    string cache = Path.Combine(saveRoot, "cache");
                    if (Directory.Exists(cache)) { Directory.Delete(cache, true); Log("Cache de shaders apagado."); }
                }
                catch (Exception ex) { Log("Não consegui apagar o cache: " + ex.Message); }
            }

            int f = int.Parse(Val(fps));
            var a = new StringBuilder();
            a.Append(Q("--game_data_root=" + dir));
            a.Append(" --log_level=").Append(debugLog.Checked ? "debug" : "info");
            a.Append(" --gpu_allow_invalid_fetch_constants --d3d12_readback_resolve --query_occlusion_fake_sample_count=-1");
            a.Append(" --video_mode_refresh_rate=").Append(f * 2);
            if (Val(resolution) == "1080") a.Append(" --video_mode_width=1920 --video_mode_height=1080");
            a.Append(asyncReadback.Checked ? "" : " --no-d3d12_readback_resolve_async");
            a.Append(fullscreen.Checked ? " --fullscreen" : " --no-fullscreen");
            a.Append(vsync.Checked ? " --vsync" : " --no-vsync");
            a.Append(" --present_effect=").Append(Val(upscaling));
            string gm = Val(gpuMode);
            if (gm == "rov" || gm == "rov_full") a.Append(" --render_target_path_d3d12=rov");
            if (gm == "full" || gm == "rov_full") a.Append(" --readback_resolve=full");
            a.Append(" --user_language=").Append(Val(language));
            if (mute.Checked) a.Append(" --audio_mute");
            if (mnk.Checked)
            {
                a.Append(" --mnk_mode");
                a.Append(mnkMouse.Checked ? " --mnk_mouse" : " --no-mnk_mouse");
                a.Append(" --mnk_sensitivity=").Append(sensitivity.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            a.Append(" --input_backend=").Append(Val(backend));
            string db = Path.Combine(root, @"controllers\gamecontrollerdb.txt");
            if (File.Exists(db)) a.Append(" ").Append(Q("--hid_mappings_file=" + db));

            var psi = new ProcessStartInfo(exe, a.ToString())
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(exe)
            };
            // SDL3 reads hints from the environment (same as tools\controller-env.bat).
            psi.EnvironmentVariables["SDL_JOYSTICK_HIDAPI"] = "1";
            psi.EnvironmentVariables["SDL_JOYSTICK_HIDAPI_PS5"] = "1";
            psi.EnvironmentVariables["SDL_JOYSTICK_HIDAPI_PS4"] = "1";
            psi.EnvironmentVariables["SDL_JOYSTICK_HIDAPI_SWITCH"] = "1";
            psi.EnvironmentVariables["SDL_JOYSTICK_HIDAPI_XBOX"] = "1";
            psi.EnvironmentVariables["SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS"] = "1";
            if (File.Exists(db)) psi.EnvironmentVariables["SDL_GAMECONTROLLERCONFIG_FILE"] = db;

            try
            {
                launchTime = DateTime.Now;
                game = Process.Start(psi);
                game.EnableRaisingEvents = true;
                game.Exited += delegate { BeginInvoke(new Action(OnGameExited)); };
                Log("Iniciando: " + f + " FPS, " + (Val(resolution) == "1080" ? "1080p" : "720p") + ", controle " + Val(backend).ToUpperInvariant());
                play.Enabled = false; play.BackColor = Theme.AccentDark;
                stop.Enabled = true; stop.ForeColor = Theme.Text;
                fpsLabel.Text = "carregando...";
                tailPath = null; tailPos = 0;
                tailTimer.Start();
            }
            catch (Exception ex) { Log("Falha ao abrir o jogo: " + ex.Message); }
        }

        void StopGame()
        {
            try { if (game != null && !game.HasExited) { game.Kill(); Log("Jogo encerrado pelo launcher."); } }
            catch (Exception ex) { Log("Não consegui fechar o jogo: " + ex.Message); }
        }

        void OnGameExited()
        {
            TailLog();
            tailTimer.Stop();
            int code = 0;
            try { code = game.ExitCode; } catch { }
            Log("Jogo fechado (código " + code + ").");
            play.Enabled = true; play.BackColor = Theme.Accent;
            stop.Enabled = false; stop.ForeColor = Theme.Dim;
            fpsLabel.Text = "";
        }

        // Follows the game's log file and shows the useful lines + the FPS.
        static readonly Regex FpsRx = new Regex(@"guest VdSwap: .*= ([\d.]+) fps");
        void TailLog()
        {
            try
            {
                if (tailPath == null)
                {
                    var logs = new DirectoryInfo(Path.Combine(root, @"out\build\win-amd64-release\logs"));
                    if (!logs.Exists) return;
                    var newest = logs.GetFiles("*.log").Where(x => x.LastWriteTime >= launchTime.AddSeconds(-2))
                        .OrderByDescending(x => x.LastWriteTime).FirstOrDefault();
                    if (newest == null) return;
                    tailPath = newest.FullName;
                    tailPos = 0;
                }
                using (var fs = new FileStream(tailPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    if (fs.Length <= tailPos) return;
                    fs.Seek(tailPos, SeekOrigin.Begin);
                    var sr = new StreamReader(fs, Encoding.UTF8);
                    string chunk = sr.ReadToEnd();
                    int lastNl = chunk.LastIndexOf('\n');
                    if (lastNl < 0) return;
                    tailPos += Encoding.UTF8.GetByteCount(chunk.Substring(0, lastNl + 1));
                    foreach (var raw in chunk.Substring(0, lastNl + 1).Split('\n'))
                    {
                        string line = raw.TrimEnd('\r');
                        if (line.Length == 0) continue;
                        var m = FpsRx.Match(line);
                        if (m.Success)
                        {
                            double v;
                            if (double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v))
                            {
                                fpsLabel.Text = v.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " FPS";
                                fpsLabel.ForeColor = v >= int.Parse(Val(fps)) * 0.9 || v >= 55 ? Theme.Good : Theme.Accent;
                            }
                            continue;
                        }
                        if (line.Contains("present:") || line.Contains(" STUB") || line.Contains("[NtCreateFile] FAILED")) continue;
                        if (line.Contains("[error]") || line.Contains("[critical]") || line.Contains("Controller") ||
                            line.Contains("GameControllerDB") || line.Contains("level") || line.Contains("Window closing") ||
                            line.Contains("crash") || line.Contains("Crash"))
                        {
                            int cut = line.IndexOf("] ", line.IndexOf("[t", StringComparison.Ordinal) + 1, StringComparison.Ordinal);
                            Log("jogo> " + (cut > 0 ? line.Substring(cut + 2) : line));
                        }
                    }
                }
            }
            catch { }
        }

        // ---------- DLC ----------
        // Same layout rexglue's ContentManager expects (see tools\install-dlc.py):
        //   <saves>\0000000000000000\5451087B\00000002\<package>\files...
        //   <saves>\0000000000000000\5451087B\Headers\00000002\<package>.header
        const uint TitleId = 0x5451087B;
        string DlcRoot { get { return Path.Combine(saveRoot, @"0000000000000000\5451087B\00000002"); } }
        string DlcHeaderRoot { get { return Path.Combine(saveRoot, @"0000000000000000\5451087B\Headers\00000002"); } }

        List<string> InstalledDlcNames()
        {
            var names = new List<string>();
            try
            {
                if (!Directory.Exists(DlcRoot)) return names;
                foreach (var dir in Directory.GetDirectories(DlcRoot))
                {
                    string file = Path.GetFileName(dir);
                    string header = Path.Combine(DlcHeaderRoot, file + ".header");
                    string name = file;
                    if (File.Exists(header))
                    {
                        byte[] h = File.ReadAllBytes(header);
                        if (h.Length >= 0x108)
                        {
                            string n = Encoding.BigEndianUnicode.GetString(h, 8, 0x100);
                            int z = n.IndexOf('\0');
                            if (z >= 0) n = n.Substring(0, z);
                            if (n.Length > 0) name = n;
                        }
                    }
                    names.Add(name);
                }
            }
            catch { }
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        void UpdateDlcStatus(bool logList)
        {
            var names = InstalledDlcNames();
            dlcStatus.Text = names.Count == 0 ? "nenhuma instalada" : names.Count + " instalada(s)";
            dlcStatus.ForeColor = names.Count == 0 ? Theme.Dim : Theme.Good;
            if (logList && names.Count > 0) Log("DLC: " + string.Join(", ", names.ToArray()));
        }

        void InstallDlcDialog()
        {
            using (var d = new FolderBrowserDialog
            {
                Description = "Escolha a pasta com as DLCs do Xbox 360 (por exemplo a pasta 5451087B ou 00000002)",
                ShowNewFolderButton = false
            })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                string src = d.SelectedPath;
                installDlc.Enabled = false;
                play.Enabled = false;
                Log("Procurando DLCs em " + src + " ...");
                var worker = new System.Threading.Thread(() =>
                {
                    int ok = 0, skipped = 0, failed = 0;
                    try
                    {
                        foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
                        {
                            StfsPackage pkg;
                            try { pkg = StfsPackage.Open(file); }
                            catch { continue; }
                            using (pkg)
                            {
                                string fn = Path.GetFileName(file);
                                if (pkg.TitleId != TitleId || pkg.ContentType != 2)
                                {
                                    skipped++;
                                    continue;
                                }
                                try
                                {
                                    string name = pkg.DisplayName;
                                    UiLog("Instalando: " + name + " ...");
                                    pkg.ExtractTo(Path.Combine(DlcRoot, fn));
                                    Directory.CreateDirectory(DlcHeaderRoot);
                                    File.WriteAllBytes(Path.Combine(DlcHeaderRoot, fn + ".header"),
                                        StfsPackage.AggregateHeader(fn, name, TitleId, 0xFFFFFFFF));
                                    ok++;
                                }
                                catch (Exception ex)
                                {
                                    failed++;
                                    UiLog("Falhou: " + fn + " - " + ex.Message);
                                }
                            }
                        }
                    }
                    catch (Exception ex) { UiLog("Erro ao ler a pasta: " + ex.Message); }
                    BeginInvoke(new Action(() =>
                    {
                        Log(string.Format("DLC: {0} instalada(s), {1} ignorada(s) (não são DLC do Alive, ex.: itens de avatar){2}.",
                            ok, skipped, failed > 0 ? ", " + failed + " com erro" : ""));
                        UpdateDlcStatus(false);
                        installDlc.Enabled = true;
                        play.Enabled = game == null || game.HasExited;
                    }));
                });
                worker.IsBackground = true;
                worker.Start();
            }
        }

        void UiLog(string text)
        {
            try { BeginInvoke(new Action(() => Log(text))); } catch { }
        }

        // ---------- files ----------
        string BackupSave()
        {
            try
            {
                if (!Directory.Exists(saveRoot)) return null;
                var items = new DirectoryInfo(saveRoot).GetFileSystemInfos().Where(x => x.Name != "cache").ToList();
                if (items.Count == 0) return null;
                string parent = Path.GetDirectoryName(saveRoot);
                string dest = Path.Combine(parent, "mx_alive.bak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                Directory.CreateDirectory(dest);
                foreach (var it in items) CopyAny(it.FullName, Path.Combine(dest, it.Name));
                // keep the newest 20 automatic backups
                var rx = new Regex(@"^mx_alive\.bak-\d{8}-\d{6}$");
                foreach (var old in new DirectoryInfo(parent).GetDirectories("mx_alive.bak-*")
                    .Where(x => rx.IsMatch(x.Name)).OrderByDescending(x => x.Name).Skip(20))
                {
                    try { old.Delete(true); } catch { }
                }
                return dest;
            }
            catch (Exception ex) { Log("Backup falhou: " + ex.Message); return null; }
        }

        static void CopyAny(string src, string dst)
        {
            if (File.Exists(src)) { File.Copy(src, dst, true); return; }
            Directory.CreateDirectory(dst);
            foreach (var f in Directory.GetFiles(src)) File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
            foreach (var d in Directory.GetDirectories(src)) CopyAny(d, Path.Combine(dst, Path.GetFileName(d)));
        }

        void OpenFolder(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                Process.Start("explorer.exe", Q(path));
            }
            catch (Exception ex) { Log("Não consegui abrir a pasta: " + ex.Message); }
        }

        // Zip with logs + system info on the Desktop, to send when something breaks.
        void MakeReport()
        {
            try
            {
                SaveSettings();
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string zipPath = Path.Combine(desktop, "MX-Alive-relatorio-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".zip");
                if (File.Exists(zipPath)) File.Delete(zipPath);
                using (var zip = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create))
                {
                    var info = new StringBuilder();
                    info.AppendLine("MX vs. ATV Alive - relatório  " + DateTime.Now);
                    info.AppendLine("Launcher v" + Version + "  |  pasta: " + root);
                    info.Append(Wmi("Win32_OperatingSystem", "Caption,Version,BuildNumber,OSArchitecture"));
                    info.AppendLine("Jogo: " + ReadXexInfo(Path.Combine(gameDir.Text.Trim(), "default.xex")));
                    info.AppendLine();
                    info.Append(Wmi("Win32_VideoController", "Name,DriverVersion,DriverDate,AdapterRAM,CurrentHorizontalResolution,CurrentVerticalResolution,CurrentRefreshRate"));
                    info.Append(Wmi("Win32_Processor", "Name,NumberOfCores,NumberOfLogicalProcessors,MaxClockSpeed"));
                    info.Append(Wmi("Win32_ComputerSystem", "TotalPhysicalMemory"));
                    info.AppendLine("Controles:");
                    foreach (var c in controllers.Items) info.AppendLine("  " + c);
                    info.AppendLine();
                    info.AppendLine("Arquivos do port:");
                    string outDir = Path.Combine(root, @"out\build\win-amd64-release");
                    foreach (var f in new[] { "mx_alive.exe", "rexgpu-xenos.dll", "rexruntime.dll" })
                    {
                        var fi = new FileInfo(Path.Combine(outDir, f));
                        info.AppendLine("  " + f + ": " + (fi.Exists ? fi.Length + " bytes, " + fi.LastWriteTime : "AUSENTE"));
                    }
                    info.AppendLine("  DLC instaladas: " + string.Join(", ", InstalledDlcNames().ToArray()));
                    var e1 = zip.CreateEntry("sistema.txt");
                    using (var w = new StreamWriter(e1.Open(), Encoding.UTF8)) w.Write(info.ToString());

                    var files = new List<string>();
                    var logs = new DirectoryInfo(Path.Combine(outDir, "logs"));
                    if (logs.Exists)
                        files.AddRange(logs.GetFiles("*.log").OrderByDescending(f => f.LastWriteTime).Take(4).Select(f => f.FullName));
                    foreach (var f in new[] { Path.Combine(root, "launcher.ini"), Path.Combine(root, "setup.log") })
                        if (File.Exists(f)) files.Add(f);
                    foreach (var f in files)
                    {
                        var entry = zip.CreateEntry(Path.GetFileName(f), System.IO.Compression.CompressionLevel.Optimal);
                        using (var src = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                        using (var dst = entry.Open()) src.CopyTo(dst);
                    }
                }
                Log("Relatório criado na Área de Trabalho: " + Path.GetFileName(zipPath));
                Process.Start("explorer.exe", "/select,\"" + zipPath + "\"");
            }
            catch (Exception ex) { Log("Não consegui gerar o relatório: " + ex.Message); }
        }

        static string Wmi(string cls, string props)
        {
            var sb = new StringBuilder();
            try
            {
                using (var searcher = new System.Management.ManagementObjectSearcher("SELECT " + props + " FROM " + cls))
                    foreach (System.Management.ManagementObject o in searcher.Get())
                    {
                        sb.AppendLine(cls + ":");
                        foreach (var name in props.Split(','))
                            sb.AppendLine("  " + name + " = " + o[name]);
                    }
            }
            catch (Exception ex) { sb.AppendLine(cls + ": erro " + ex.Message); }
            sb.AppendLine();
            return sb.ToString();
        }

        void CreateShortcut()
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string lnk = Path.Combine(desktop, "MX vs ATV Alive.lnk");
                Type t = Type.GetTypeFromProgID("WScript.Shell");
                object shell = Activator.CreateInstance(t);
                object sc = t.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
                Type st = sc.GetType();
                st.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { Application.ExecutablePath });
                st.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { root });
                st.InvokeMember("IconLocation", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { Application.ExecutablePath + ",0" });
                st.InvokeMember("Description", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { "MX vs. ATV Alive - port nativo" });
                st.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, sc, null);
                Log("Atalho criado: " + lnk);
            }
            catch (Exception ex) { Log("Não consegui criar o atalho: " + ex.Message); }
        }

        void Log(string text)
        {
            log.AppendText((log.TextLength > 0 ? Environment.NewLine : "") + text);
            if (log.TextLength > 60000) log.Text = log.Text.Substring(log.TextLength - 40000);
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            try { Native.SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            // The launcher lives in the repository root, next to tools\ and out\.
            string root = Path.GetDirectoryName(Application.ExecutablePath);
            if (!Directory.Exists(Path.Combine(root, "tools")) && Directory.Exists(Path.Combine(root, @"..\tools")))
                root = Path.GetFullPath(Path.Combine(root, ".."));
            Application.Run(new LauncherForm(root));
        }
    }
}
