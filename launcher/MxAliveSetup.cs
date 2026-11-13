// MX vs. ATV Alive - one-click installer.
// Downloads this project, installs the toolchain with winget, fetches
// rexglue-sdk, copies the user's OWN game files, builds everything and creates
// a desktop shortcut. No game files are downloaded or distributed.
// C# 5 / WinForms: builds with the csc.exe that ships with Windows
// (launcher\build-launcher.bat).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Reflection;

// File details shown in Windows (Properties > Details). An .exe without them
// is much more likely to be flagged by antivirus heuristics.
[assembly: AssemblyTitle("MX vs. ATV Alive - Setup")]
[assembly: AssemblyDescription("Instalador do port nativo para PC do MX vs. ATV Alive (usa a sua propria copia do jogo)")]
[assembly: AssemblyProduct("MX vs. ATV Alive Recomp")]
[assembly: AssemblyCompany("mx-vs-atv-alive-recomp")]
[assembly: AssemblyCopyright("Codigo aberto - github.com/arthurmacucos-png/mx-vs-atv-alive-recomp")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]

namespace MxAliveSetup
{
    static class Config
    {
        // Repository that holds this project (GitHub "Download ZIP" of the default branch).
        public const string RepoZipUrl = "https://github.com/arthurmacucos-png/mx-vs-atv-alive-recomp/archive/refs/heads/main.zip";
        public const string SdkRepo = "https://github.com/rexglue/rexglue-sdk.git";
        public const string SdkCommit = "f5337cdc947ff6d4c4196737e2c807a48f2a1fc2"; // v0.10.0
        public const string GameSubdir = @"Game\MX vs. ATV Alive (USA Europe)";
    }

    static class Theme
    {
        public static readonly Color Back = Color.FromArgb(18, 18, 20);
        public static readonly Color Panel = Color.FromArgb(30, 30, 33);
        public static readonly Color Field = Color.FromArgb(42, 42, 46);
        public static readonly Color Border = Color.FromArgb(58, 58, 64);
        public static readonly Color Text = Color.FromArgb(232, 232, 232);
        public static readonly Color Dim = Color.FromArgb(150, 150, 156);
        public static readonly Color Accent = Color.FromArgb(222, 228, 46);
        public static readonly Color Good = Color.FromArgb(90, 200, 110);
        public static readonly Color Bad = Color.FromArgb(235, 80, 70);
    }

    class SetupForm : Form
    {
        readonly string[] stepNames = {
            "Conferir a sua cópia do jogo",
            "Baixar o projeto",
            "Instalar as ferramentas (Git, CMake, Ninja, LLVM, Visual Studio)",
            "Baixar o rexglue-sdk",
            "Compilar o rexglue com a correção da água",
            "Recompilar o jogo (30 a 60 min na primeira vez)",
            "Criar o atalho e abrir o launcher"
        };
        readonly List<Label> stepIcons = new List<Label>();
        readonly List<Label> stepLabels = new List<Label>();
        TextBox installDir, gameDir, log;
        Label status;
        ProgressBar bar;
        Button install, updateOnly, browseInstall, browseGame;
        StreamWriter logFile;
        string logPath;

        public SetupForm()
        {
            Text = "MX vs. ATV Alive - instalação";
            BackColor = Theme.Back; ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9.5f);
            // Resizable: on a TV / small screen / high Windows scaling the 760x720
            // design does not fit. Everything is scaled to the window (see Rescale).
            FormBorderStyle = FormBorderStyle.Sizable; MaximizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(760, 720);
            MinimumSize = new Size(420, 400);
            KeyPreview = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            var title = new Label { Text = "MX vs. ATV Alive", Font = new Font("Segoe UI Semibold", 20f), ForeColor = Theme.Accent, AutoSize = true, Location = new Point(20, 14) };
            var sub = new Label { Text = "Port nativo para PC  ·  instalação em um clique", Font = new Font("Segoe UI Semibold", 11f), AutoSize = true, Location = new Point(23, 54) };
            var legal = new Label { Text = "O jogo NÃO é baixado: você escolhe a pasta da SUA cópia de Xbox 360 (com o default.xex).", ForeColor = Theme.Dim, AutoSize = true, Location = new Point(23, 80), Font = new Font("Segoe UI", 8.5f) };
            Controls.AddRange(new Control[] { title, sub, legal });

            AddRow("Instalar em", 112, out installDir, out browseInstall);
            installDir.Text = DefaultInstallDir();
            browseInstall.Click += delegate { PickFolder(installDir, "Escolha onde instalar (evite pastas do OneDrive)"); };
            AddRow("Pasta do jogo", 148, out gameDir, out browseGame);
            browseGame.Click += delegate { PickFolder(gameDir, "Escolha a pasta do MX vs. ATV Alive de Xbox 360 (a que tem o default.xex)"); };

            int y = 194;
            for (int i = 0; i < stepNames.Length; i++)
            {
                var icon = new Label { Text = "•", ForeColor = Theme.Dim, Font = new Font("Segoe UI Semibold", 11f), Location = new Point(24, y), Size = new Size(26, 24) };
                var lbl = new Label { Text = stepNames[i], ForeColor = Theme.Dim, Location = new Point(52, y + 2), Size = new Size(680, 22) };
                stepIcons.Add(icon); stepLabels.Add(lbl);
                Controls.Add(icon); Controls.Add(lbl);
                y += 28;
            }

            bar = new ProgressBar { Location = new Point(24, y + 8), Size = new Size(712, 14), Maximum = stepNames.Length * 10 };
            status = new Label { Text = "Escolha a pasta do jogo e clique em INSTALAR.", ForeColor = Theme.Dim, Location = new Point(24, y + 28), Size = new Size(712, 22) };
            Controls.Add(bar); Controls.Add(status);

            log = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.FromArgb(12, 12, 14), ForeColor = Color.FromArgb(190, 190, 190), Font = new Font("Consolas", 8.5f), BorderStyle = BorderStyle.FixedSingle, Location = new Point(24, y + 56), Size = new Size(712, 640 - (y + 56)) };
            Controls.Add(log);

            install = new Button { Text = "INSTALAR", Font = new Font("Segoe UI Semibold", 13f), FlatStyle = FlatStyle.Flat, BackColor = Theme.Accent, ForeColor = Color.FromArgb(18, 20, 24), Location = new Point(24, 656), Size = new Size(200, 46), Cursor = Cursors.Hand };
            install.FlatAppearance.BorderColor = Theme.Accent;
            install.Click += delegate { Start(); };
            Controls.Add(install);
            updateOnly = new Button { Text = "SÓ ATUALIZAR", Font = new Font("Segoe UI Semibold", 11f), FlatStyle = FlatStyle.Flat, BackColor = Theme.Field, ForeColor = Theme.Accent, Location = new Point(236, 656), Size = new Size(170, 46), Cursor = Cursors.Hand };
            updateOnly.FlatAppearance.BorderColor = Theme.Accent;
            updateOnly.Click += delegate { UpdateOnlyClicked(); };
            Controls.Add(updateOnly);
            var note = new Label { Text = "SÓ ATUALIZAR: baixa só o que mudou, sem mexer no jogo e nos saves.\nInternet e ~15 GB livres. F11 = tela cheia.", ForeColor = Theme.Dim, Font = new Font("Segoe UI", 8f), Location = new Point(418, 652), Size = new Size(330, 56) };
            Controls.Add(note);
            AcceptButton = install;

            // Running from inside an extracted project: offer that folder.
            string here = Path.GetDirectoryName(Application.ExecutablePath);
            if (File.Exists(Path.Combine(here, @"tools\build-game.bat"))) installDir.Text = here;
            // Existing install somewhere common: preselect it (button becomes ATUALIZAR).
            if (!IsInstalled(installDir.Text.Trim()))
            {
                string found = FindInstall();
                if (found != null) installDir.Text = found;
            }

            installDir.TextChanged += delegate { RefreshMode(); };
            RefreshMode();
            WrapInScalableContent();
            Load += delegate { FitToScreen(); };
        }

        // ---------- scaling (any window size / TV / fullscreen) ----------
        // The layout is designed at 760x720. Everything lives in one panel that is
        // scaled uniformly to fit the window and kept centered (same as the launcher).
        Panel content;
        float currentScale = 1f, zoom = 1f;
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
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.F11) { ToggleFullscreen(); e.Handled = true; }
                else if (e.Control && (e.KeyCode == Keys.Oemplus || e.KeyCode == Keys.Add)) { SetZoom(zoom + 0.1f); e.Handled = true; }
                else if (e.Control && (e.KeyCode == Keys.OemMinus || e.KeyCode == Keys.Subtract)) { SetZoom(zoom - 0.1f); e.Handled = true; }
                else if (e.Control && (e.KeyCode == Keys.D0 || e.KeyCode == Keys.NumPad0)) { SetZoom(1f); e.Handled = true; }
            };
        }

        void RememberFonts(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                baseFontSizes[c] = c.Font.Size;
                RememberFonts(c);
            }
        }

        // Start no bigger than the screen (taskbar excluded), keeping the proportions.
        void FitToScreen()
        {
            Rectangle wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            Size chrome = Size - ClientSize;
            int maxW = (int)(wa.Width * 0.95f) - chrome.Width, maxH = (int)(wa.Height * 0.95f) - chrome.Height;
            if (ClientSize.Width <= maxW && ClientSize.Height <= maxH) return;
            float k = Math.Min(maxW / (float)designSize.Width, maxH / (float)designSize.Height);
            MinimumSize = new Size(Math.Min(MinimumSize.Width, wa.Width), Math.Min(MinimumSize.Height, wa.Height));
            ClientSize = new Size(Math.Max(200, (int)(designSize.Width * k)), Math.Max(200, (int)(designSize.Height * k)));
            Location = new Point(wa.Left + (wa.Width - Width) / 2, wa.Top + (wa.Height - Height) / 2);
        }

        void SetZoom(float z)
        {
            zoom = Math.Max(0.5f, Math.Min(1.5f, z));
            Rescale();
        }

        void Rescale()
        {
            if (content == null || ClientSize.Width < 50 || ClientSize.Height < 50) return;
            float target = Math.Min(ClientSize.Width / (float)designSize.Width,
                                    ClientSize.Height / (float)designSize.Height) * zoom;
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
            // Zoomed in past the window: scroll instead of cutting things off.
            AutoScroll = content.Width > ClientSize.Width || content.Height > ClientSize.Height;
            content.Location = new Point(Math.Max(0, (ClientSize.Width - content.Width) / 2) + AutoScrollPosition.X,
                                         Math.Max(0, (ClientSize.Height - content.Height) / 2) + AutoScrollPosition.Y);
        }

        FormWindowState stateBeforeFullscreen = FormWindowState.Normal;
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

        // ---------- update mode ----------
        bool updateMode;
        readonly string[] updateStepNames = {
            "Baixar a atualização do GitHub",
            "Preparar o rexglue para as novas correções",
            "Recompilar o módulo gráfico",
            "Instalar as DLLs novas no jogo",
            "Atualizar o atalho"
        };

        // Looks for an existing install in the usual places (shallow, fast).
        static string FindInstall()
        {
            var roots = new List<string>();
            roots.Add(Path.GetDirectoryName(Application.ExecutablePath));
            foreach (var d in DriveInfo.GetDrives())
            {
                try { if (d.DriveType == DriveType.Fixed && d.IsReady) roots.Add(Path.Combine(d.RootDirectory.FullName, "Jogos")); } catch { }
            }
            string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            roots.Add(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            roots.Add(Path.Combine(user, "Desktop"));
            roots.Add(Path.Combine(user, @"OneDrive\Desktop"));
            roots.Add(Path.Combine(user, @"OneDrive\Área de Trabalho"));
            roots.Add(Path.Combine(user, "Downloads"));
            roots.Add(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            foreach (var r in roots.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    if (!Directory.Exists(r)) continue;
                    if (IsInstalled(r)) return r;
                    foreach (var d1 in Directory.GetDirectories(r))
                    {
                        if (IsInstalled(d1)) return d1;
                        try { foreach (var d2 in Directory.GetDirectories(d1)) if (IsInstalled(d2)) return d2; } catch { }
                    }
                }
                catch { }
            }
            return null;
        }

        void UpdateOnlyClicked()
        {
            string root = installDir.Text.Trim();
            if (!IsInstalled(root))
            {
                string found = FindInstall();
                if (found == null)
                {
                    using (var d = new FolderBrowserDialog { Description = "Escolha a pasta onde o MX vs. ATV Alive já está instalado (a que tem o MX Alive Launcher.exe)" })
                    {
                        if (d.ShowDialog(this) != DialogResult.OK) return;
                        found = d.SelectedPath;
                    }
                }
                if (!IsInstalled(found))
                {
                    MessageBox.Show(this, "Não encontrei uma instalação completa em:\n" + found +
                        "\n\nPara atualizar, a pasta precisa ter o jogo já compilado (out\\build\\win-amd64-release\\mx_alive.exe). " +
                        "Se a instalação nunca terminou, use INSTALAR.", "Atualizar", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                installDir.Text = found;
            }
            Start();
        }

        static bool IsInstalled(string root)
        {
            return File.Exists(Path.Combine(root, @"tools\build-game.bat")) &&
                   File.Exists(Path.Combine(root, @"out\build\win-amd64-release\mx_alive.exe"));
        }

        void RefreshMode()
        {
            if (install == null) return;
            updateMode = IsInstalled(installDir.Text.Trim());
            install.Text = updateMode ? "ATUALIZAR" : "INSTALAR";
            if (updateOnly != null) updateOnly.Visible = !updateMode;
            gameDir.Enabled = browseGame.Enabled = !updateMode;
            var names = updateMode ? updateStepNames : stepNames;
            for (int i = 0; i < stepLabels.Count; i++)
            {
                stepLabels[i].Visible = stepIcons[i].Visible = i < names.Length;
                if (i < names.Length)
                {
                    stepLabels[i].Text = names[i]; stepLabels[i].ForeColor = Theme.Dim;
                    stepIcons[i].Text = "•"; stepIcons[i].ForeColor = Theme.Dim;
                }
            }
            status.Text = updateMode
                ? "Instalação encontrada. Clique em ATUALIZAR (o jogo e os saves não são alterados)."
                : "Escolha a pasta do jogo e clique em INSTALAR.";
        }

        static string DefaultInstallDir()
        {
            string drive = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
            return Path.Combine(drive, @"Jogos\MX vs ATV Alive");
        }

        void AddRow(string label, int y, out TextBox box, out Button button)
        {
            Controls.Add(new Label { Text = label, Location = new Point(24, y + 4), Size = new Size(110, 22) });
            box = new TextBox { Location = new Point(136, y), Size = new Size(500, 26), BackColor = Theme.Field, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle };
            button = new Button { Text = "Procurar...", Location = new Point(644, y - 1), Size = new Size(92, 28), FlatStyle = FlatStyle.Flat, BackColor = Theme.Field, ForeColor = Theme.Text };
            button.FlatAppearance.BorderColor = Theme.Border;
            Controls.Add(box); Controls.Add(button);
        }

        void PickFolder(TextBox box, string description)
        {
            using (var d = new FolderBrowserDialog { Description = description, SelectedPath = box.Text })
                if (d.ShowDialog(this) == DialogResult.OK) box.Text = d.SelectedPath;
        }

        // ---------- UI helpers (thread safe) ----------
        void Ui(Action a) { if (InvokeRequired) BeginInvoke(a); else a(); }

        void Log(string line)
        {
            if (line == null) return;
            lock (this) { if (logFile != null) { logFile.WriteLine(line); logFile.Flush(); } }
            Ui(() =>
            {
                log.AppendText(line + Environment.NewLine);
                if (log.TextLength > 200000) log.Text = log.Text.Substring(log.TextLength - 120000);
            });
        }

        void SetStep(int i, string state)
        {
            Ui(() =>
            {
                var icon = stepIcons[i]; var lbl = stepLabels[i];
                if (state == "run") { icon.Text = "►"; icon.ForeColor = Theme.Accent; lbl.ForeColor = Theme.Text; status.Text = lbl.Text + "..."; bar.Value = Math.Min(bar.Maximum, i * 10 + 2); }
                else if (state == "ok") { icon.Text = "✓"; icon.ForeColor = Theme.Good; lbl.ForeColor = Theme.Text; bar.Value = Math.Min(bar.Maximum, (i + 1) * 10); }
                else if (state == "skip") { icon.Text = "✓"; icon.ForeColor = Theme.Dim; lbl.Text = lbl.Text + "  (já estava pronto)"; bar.Value = Math.Min(bar.Maximum, (i + 1) * 10); }
                else if (state == "fail") { icon.Text = "✗"; icon.ForeColor = Theme.Bad; lbl.ForeColor = Theme.Bad; }
            });
        }

        class StepFailed : Exception { public StepFailed(string m) : base(m) { } }

        // ---------- process runner ----------
        // Process.Start resolves a bare name with THIS process's PATH, which is
        // stale right after winget installs something. Resolve it ourselves.
        internal static string Which(string file)
        {
            if (Path.IsPathRooted(file)) return file;
            foreach (var dir in FreshPath().Split(';'))
            {
                string d = dir.Trim();
                if (d.Length == 0) continue;
                foreach (var ext in new[] { "", ".exe", ".cmd", ".bat" })
                {
                    try
                    {
                        string full = Path.Combine(Environment.ExpandEnvironmentVariables(d), file + ext);
                        if (ext.Length > 0 || Path.HasExtension(file))
                            if (File.Exists(full)) return full;
                    }
                    catch { }
                }
            }
            return file;
        }

        int Run(string file, string args, string workDir)
        {
            file = Which(file);
            Log("> " + file + " " + args);
            var psi = new ProcessStartInfo(file, args)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                WorkingDirectory = workDir ?? Environment.CurrentDirectory,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            psi.EnvironmentVariables["PATH"] = FreshPath();
            using (var p = new Process { StartInfo = psi })
            {
                p.OutputDataReceived += (s, e) => Log(e.Data);
                p.ErrorDataReceived += (s, e) => Log(e.Data);
                p.Start(); p.BeginOutputReadLine(); p.BeginErrorReadLine();
                p.WaitForExit();
                return p.ExitCode;
            }
        }

        static string FreshPath()
        {
            return @"C:\Program Files\LLVM\bin;C:\Program Files\Git\cmd;" +
                Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Machine) + ";" +
                Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) + ";" +
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\WinGet\Links");
        }

        static bool Have(string exe)
        {
            foreach (var dir in FreshPath().Split(';'))
            {
                try { if (dir.Trim().Length > 0 && File.Exists(Path.Combine(Environment.ExpandEnvironmentVariables(dir.Trim()), exe + ".exe"))) return true; } catch { }
            }
            return false;
        }

        // ---------- install ----------
        void Start()
        {
            string root = installDir.Text.Trim(), game = gameDir.Text.Trim();
            updateMode = IsInstalled(root);
            if (!updateMode && (game.Length == 0 || !File.Exists(Path.Combine(game, "default.xex"))))
            {
                MessageBox.Show(this, "Escolha a pasta do jogo que contém o default.xex.", "Pasta do jogo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (root.IndexOf("OneDrive", StringComparison.OrdinalIgnoreCase) >= 0 &&
                MessageBox.Show(this, "Instalar dentro do OneDrive vai sincronizar uns 8 GB com a nuvem. Continuar mesmo assim?", "OneDrive", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            // Another setup window (e.g. one stuck waiting for a UAC prompt) still
            // holds setup.log and may be installing the same things.
            var me = Process.GetCurrentProcess();
            var others = Process.GetProcessesByName(me.ProcessName).Where(p => p.Id != me.Id).ToList();
            if (others.Count > 0)
            {
                var answer = MessageBox.Show(this,
                    "Outra janela do instalador ainda está aberta (ela pode estar esperando uma permissão do Windows na barra de tarefas).\n\n" +
                    "Fechar a outra janela e continuar aqui?", "Instalador já aberto",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (answer != DialogResult.Yes) return;
                foreach (var p in others)
                {
                    try { p.Kill(); p.WaitForExit(5000); } catch { }
                }
            }

            install.Enabled = browseGame.Enabled = browseInstall.Enabled = false;
            updateOnly.Visible = false;
            installDir.ReadOnly = gameDir.ReadOnly = true;
            Directory.CreateDirectory(root);
            logPath = Path.Combine(root, "setup.log");
            try
            {
                logFile = new StreamWriter(new FileStream(logPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite), Encoding.UTF8);
            }
            catch (IOException)
            {
                logPath = Path.Combine(root, "setup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log");
                logFile = new StreamWriter(new FileStream(logPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite), Encoding.UTF8);
            }
            Log("MX vs. ATV Alive setup  " + DateTime.Now + "  |  Windows " + Environment.OSVersion.Version);

            bar.Maximum = (updateMode ? updateStepNames.Length : stepNames.Length) * 10;
            var t = updateMode ? new Thread(() => Update(root)) { IsBackground = true }
                               : new Thread(() => Install(root, game)) { IsBackground = true };
            t.Start();
        }

        void Install(string root, string game)
        {
            int step = 0;
            try
            {
                // 0. Game check.
                SetStep(step, "run");
                string info = CheckXex(Path.Combine(game, "default.xex"));
                Log("default.xex: " + info);
                if (!info.Contains("5451087B") || !info.Contains("5306E1D5"))
                    throw new StepFailed("Esta cópia não é a versão suportada (Xbox 360 USA/Europa, Title 5451087B, Media 5306E1D5).");
                SetStep(step++, "ok");

                // 1. Project.
                SetStep(step, "run");
                if (File.Exists(Path.Combine(root, @"tools\build-game.bat"))) SetStep(step++, "skip");
                else { DownloadProject(root); SetStep(step++, "ok"); }

                // 2. Toolchain.
                SetStep(step, "run");
                bool any = false;
                if (!Have("git")) { any = true; Winget("Git.Git", null); }
                if (!Have("cmake")) { any = true; Winget("Kitware.CMake", null); }
                if (!Have("ninja")) { any = true; Winget("Ninja-build.Ninja", null); }
                if (!Have("clang")) { any = true; Winget("LLVM.LLVM", null); }
                if (!HaveVisualStudio())
                {
                    any = true;
                    Log("Instalando Visual Studio 2022 Build Tools (C++). Pode levar 10-20 minutos.");
                    Winget("Microsoft.VisualStudio.2022.BuildTools", "--override \"--wait --passive --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended\"");
                }
                foreach (var tool in new[] { "git", "cmake", "ninja", "clang" })
                    if (!Have(tool)) throw new StepFailed(tool + " não ficou disponível. Reinicie o PC e rode o instalador de novo.");
                if (!HaveVisualStudio()) throw new StepFailed("Visual Studio Build Tools (C++) não foi instalado. Reinicie o PC e rode de novo.");
                SetStep(step++, any ? "ok" : "skip");

                // 3. rexglue-sdk.
                SetStep(step, "run");
                string sdk = Path.Combine(root, @"third_party\rexglue-sdk");
                if (File.Exists(Path.Combine(sdk, "CMakeLists.txt"))) SetStep(step++, "skip");
                else
                {
                    if (Directory.Exists(sdk)) Directory.Delete(sdk, true);
                    if (Run("git", "clone " + Config.SdkRepo + " \"" + sdk + "\"", root) != 0 ||
                        Run("git", "-C \"" + sdk + "\" checkout " + Config.SdkCommit, root) != 0 ||
                        Run("git", "-C \"" + sdk + "\" submodule update --init --recursive", root) != 0)
                        throw new StepFailed("Falha ao baixar o rexglue-sdk (confira a internet).");
                    SetStep(step++, "ok");
                }

                // Copy game files (needed before the game build).
                string dest = Path.Combine(root, Config.GameSubdir);
                if (!File.Exists(Path.Combine(dest, "default.xex")))
                {
                    Log("Copiando os arquivos do jogo para " + dest + " ...");
                    if (Run("robocopy", "\"" + game + "\" \"" + dest + "\" /E /NFL /NDL /NJH /NJS /NP", root) >= 8)
                        throw new StepFailed("Falha ao copiar os arquivos do jogo.");
                }

                // 4. rexglue.
                SetStep(step, "run");
                if (File.Exists(Path.Combine(sdk, @"out\install\win-amd64\bin\rexglue.exe"))) SetStep(step++, "skip");
                else
                {
                    if (Run("cmd.exe", "/c \"\"" + Path.Combine(root, @"tools\build-rexglue.bat") + "\"\"", root) != 0)
                        throw new StepFailed("Falha ao compilar o rexglue. Veja o log.");
                    SetStep(step++, "ok");
                }

                // 5. Game.
                SetStep(step, "run");
                if (Run("cmd.exe", "/c \"\"" + Path.Combine(root, @"tools\build-game.bat") + "\"\"", root) != 0)
                    throw new StepFailed("Falha ao recompilar o jogo. Veja o log.");
                SetStep(step++, "ok");

                // 6. Shortcut + launcher.
                SetStep(step, "run");
                string launcher = Path.Combine(root, "MX Alive Launcher.exe");
                if (!File.Exists(launcher))
                    Run("cmd.exe", "/c \"\"" + Path.Combine(root, @"launcher\build-launcher.bat") + "\"\"", root);
                CreateShortcut(launcher, root);
                SetStep(step++, "ok");

                Log("PRONTO!");
                Ui(() =>
                {
                    status.Text = "Pronto! Atalho \"MX vs ATV Alive\" criado na Área de Trabalho.";
                    status.ForeColor = Theme.Good;
                    install.Text = "JOGAR"; install.Enabled = true;
                    install.Click += delegate { Process.Start(new ProcessStartInfo(launcher) { WorkingDirectory = root }); Close(); };
                });
                Process.Start(new ProcessStartInfo(launcher) { WorkingDirectory = root });
            }
            catch (Exception ex)
            {
                string msg = ex is StepFailed ? ex.Message : ex.ToString();
                Log("ERRO: " + msg);
                SetStep(Math.Min(step, stepNames.Length - 1), "fail");
                Ui(() =>
                {
                    status.Text = "Erro: " + ex.Message;
                    status.ForeColor = Theme.Bad;
                    install.Text = "TENTAR DE NOVO"; install.Enabled = true;
                    browseGame.Enabled = browseInstall.Enabled = true;
                    installDir.ReadOnly = gameDir.ReadOnly = false;
                    MessageBox.Show(this, ex.Message + "\n\nO log completo foi salvo em:\n" + logPath +
                        "\n\nMande esse arquivo para quem estiver te ajudando.", "Erro na instalação",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                });
            }
            finally
            {
                lock (this) { if (logFile != null) { logFile.Dispose(); logFile = null; } }
            }
        }

        void DownloadProject(string root) { DownloadProject(root, false); }

        void Update(string root)
        {
            int step = 0;
            try
            {
                foreach (var name in new[] { "mx_alive", "MX Alive Launcher" })
                    if (Process.GetProcessesByName(name).Length > 0)
                        throw new StepFailed("Feche o jogo e o launcher antes de atualizar.");
                foreach (var tool in new[] { "git", "cmake", "ninja", "clang" })
                    if (!Have(tool)) throw new StepFailed(tool + " não foi encontrado. Faça a instalação completa em outra pasta.");

                SetStep(step, "run");
                DownloadProject(root, true);
                SetStep(step++, "ok");

                // Reset the SDK sources so build-rexglue.bat re-applies every patch
                // (the author's local patch + ours) on clean files. Build output is
                // kept, so only what changed gets recompiled.
                SetStep(step, "run");
                string sdk = Path.Combine(root, @"third_party\rexglue-sdk");
                if (!File.Exists(Path.Combine(sdk, "CMakeLists.txt")))
                    throw new StepFailed("rexglue-sdk não encontrado em " + sdk + ".");
                if (Run("git", "-C \"" + sdk + "\" checkout -- src include", root) != 0)
                    throw new StepFailed("Falha ao preparar o rexglue-sdk.");
                SetStep(step++, "ok");

                SetStep(step, "run");
                if (Run("cmd.exe", "/c \"\"" + Path.Combine(root, @"tools\build-rexglue.bat") + "\"\"", root) != 0)
                    throw new StepFailed("Falha ao recompilar o rexglue. Veja o log.");
                SetStep(step++, "ok");

                SetStep(step, "run");
                string bin = Path.Combine(sdk, @"out\install\win-amd64\bin");
                string gameOut = Path.Combine(root, @"out\build\win-amd64-release");
                foreach (var dll in Directory.GetFiles(bin, "*.dll"))
                {
                    File.Copy(dll, Path.Combine(gameOut, Path.GetFileName(dll)), true);
                    Log("Atualizado: " + Path.GetFileName(dll));
                }
                SetStep(step++, "ok");

                SetStep(step, "run");
                string launcher = Path.Combine(root, "MX Alive Launcher.exe");
                CreateShortcut(launcher, root);
                SetStep(step++, "ok");

                Log("ATUALIZADO!");
                Ui(() =>
                {
                    status.Text = "Atualizado! Pode abrir o jogo pelo atalho.";
                    status.ForeColor = Theme.Good;
                    install.Text = "JOGAR"; install.Enabled = true;
                    install.Click += delegate { Process.Start(new ProcessStartInfo(launcher) { WorkingDirectory = root }); Close(); };
                });
            }
            catch (Exception ex)
            {
                string msg = ex is StepFailed ? ex.Message : ex.ToString();
                Log("ERRO: " + msg);
                SetStep(Math.Min(step, updateStepNames.Length - 1), "fail");
                Ui(() =>
                {
                    status.Text = "Erro: " + ex.Message;
                    status.ForeColor = Theme.Bad;
                    install.Text = "TENTAR DE NOVO"; install.Enabled = true;
                    browseInstall.Enabled = true; installDir.ReadOnly = false;
                    MessageBox.Show(this, ex.Message + "\n\nO log completo foi salvo em:\n" + logPath +
                        "\n\nMande esse arquivo para quem estiver te ajudando.", "Erro na atualização",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                });
            }
            finally
            {
                lock (this) { if (logFile != null) { logFile.Dispose(); logFile = null; } }
            }
        }

        void DownloadProject(string root, bool update)
        {
            if (Config.RepoZipUrl.Length == 0)
                throw new StepFailed("Este instalador ainda não tem o endereço do repositório configurado. Baixe o projeto em ZIP, extraia e rode o instalador de dentro da pasta.");
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; // TLS 1.2
            string zip = Path.Combine(Path.GetTempPath(), "mx-alive-project.zip");
            Log("Baixando " + Config.RepoZipUrl);
            using (var wc = new WebClient())
            {
                wc.Headers.Add("User-Agent", "MxAliveSetup");
                wc.DownloadFile(Config.RepoZipUrl, zip);
            }
            string tmp = Path.Combine(Path.GetTempPath(), "mx-alive-project");
            if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
            ZipFile.ExtractToDirectory(zip, tmp);
            // GitHub zips contain a single top-level folder. Use the folder that
            // holds this project's files (it may also sit in a subfolder).
            string top = Directory.GetDirectories(tmp).First();
            var marker = Directory.GetFiles(top, "rexglue-mx-fixes.patch", SearchOption.AllDirectories).FirstOrDefault();
            if (marker == null) throw new StepFailed("O ZIP baixado não tem a correção da água (patches\\rexglue-mx-fixes.patch).");
            top = Path.GetDirectoryName(Path.GetDirectoryName(marker));
            Log("Usando a pasta do projeto: " + top.Substring(tmp.Length));
            string self = Path.GetFullPath(Application.ExecutablePath);
            foreach (var f in Directory.GetFiles(top, "*", SearchOption.AllDirectories))
            {
                string rel = f.Substring(top.Length + 1);
                string target = Path.Combine(root, rel);
                if (update)
                {
                    string low = rel.ToLowerInvariant();
                    if (low.StartsWith(@"game\") || low.StartsWith(@"out\") || low.StartsWith(@"third_party\") ||
                        low == "launcher.ini" || low == "setup.log") continue;
                }
                if (string.Equals(Path.GetFullPath(target), self, StringComparison.OrdinalIgnoreCase)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                try { File.Copy(f, target, true); }
                catch (IOException)
                {
                    if (rel.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        throw new StepFailed("Feche o launcher e o jogo antes de atualizar (" + rel + " está em uso).");
                    throw;
                }
            }
            Directory.Delete(tmp, true); File.Delete(zip);
            Log("Projeto extraído em " + root);
        }

        void Winget(string id, string extra)
        {
            Log("Instalando " + id + " ...");
            Log("  Se aparecer um pedido de permissão do Windows (escudo azul/amarelo), clique em SIM.");
            Log("  Ele pode abrir escondido: confira a barra de tarefas se nada acontecer.");
            Ui(() => status.Text = "Instalando " + id + "... (se o Windows pedir permissão, clique em Sim)");
            // Visible console window, no output redirection: winget can stall when its
            // output is piped, and the user sees progress and any prompt.
            string args = "install --id " + id + " -e --accept-package-agreements --accept-source-agreements --disable-interactivity " +
                (extra ?? "--silent");
            var psi = new ProcessStartInfo(Which("winget"), args)
            {
                UseShellExecute = false, CreateNoWindow = false, WindowStyle = ProcessWindowStyle.Normal
            };
            psi.EnvironmentVariables["PATH"] = FreshPath();
            int code = -1;
            using (var p = Process.Start(psi))
            {
                if (!p.WaitForExit(15 * 60 * 1000))
                {
                    Log("  " + id + " está demorando mais de 15 minutos. Continuando a aguardar...");
                    Ui(() => status.Text = id + " ainda instalando. Verifique se há um pedido de permissão na barra de tarefas.");
                    p.WaitForExit();
                }
                code = p.ExitCode;
            }
            // winget returns non-zero for "already installed"; availability is checked afterwards.
            Log("winget terminou com código " + code);
        }

        static bool HaveVisualStudio()
        {
            string vswhere = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft Visual Studio\Installer\vswhere.exe");
            if (!File.Exists(vswhere)) return false;
            try
            {
                var p = Process.Start(new ProcessStartInfo(vswhere, "-latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath")
                { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true });
                string o = p.StandardOutput.ReadToEnd(); p.WaitForExit();
                return o.Trim().Length > 0;
            }
            catch { return false; }
        }

        static uint BE32(byte[] d, int o) { return (uint)(d[o] << 24 | d[o + 1] << 16 | d[o + 2] << 8 | d[o + 3]); }

        static string CheckXex(string path)
        {
            byte[] d = new byte[0x4000];
            using (var f = File.OpenRead(path)) f.Read(d, 0, d.Length);
            if (d[0] != 'X' || d[1] != 'E' || d[2] != 'X' || d[3] != '2') return "inválido";
            uint count = BE32(d, 0x14);
            for (int i = 0; i < count && 0x20 + i * 8 <= d.Length; i++)
                if (BE32(d, 0x18 + i * 8) == 0x00040006)
                {
                    int o = (int)BE32(d, 0x1C + i * 8);
                    return string.Format("Title {0:X8} Media {1:X8}", BE32(d, o + 12), BE32(d, o));
                }
            return "sem informações";
        }

        void CreateShortcut(string target, string workDir)
        {
            string lnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "MX vs ATV Alive.lnk");
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(t);
            object sc = t.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
            Type st = sc.GetType();
            st.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { target });
            st.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { workDir });
            st.InvokeMember("IconLocation", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { target + ",0" });
            st.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, sc, null);
            Log("Atalho criado: " + lnk);
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm());
        }
    }
}
