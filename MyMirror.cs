using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

internal sealed class Mirror : Form
{
    static readonly string Engine = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"uxplay-windows\uxplay-windows.exe");
    static readonly string ArgsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"leapbtw\uxplay-windows\arguments.txt");
    static readonly string RecoveryFile = ArgsFile + ".mymirror-recovery";
    static readonly string MachineArgs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"uxplay-windows\arguments.txt");
    static readonly Encoding Utf8 = new UTF8Encoding(false);
    const int Port = 7010;
    readonly TextBox receiver = new TextBox { Text = "My-Mirror", MaxLength = 28, Dock = DockStyle.Fill };
    readonly TextBox pin = new TextBox { ReadOnly = true, AccessibleName = "Code AirPlay", Text = "—", Font = new Font("Segoe UI", 17, FontStyle.Bold) };
    readonly Label status = new Label { AutoSize = true, MaximumSize = new Size(420, 0), Text = "Récepteur arrêté." };
    readonly Button action = new Button { Text = "Démarrer", AutoSize = true };
    readonly Button rename = new Button { Text = "&Appliquer le nom", AutoSize = true, Enabled = false };
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 500 };
    NotifyIcon tray;
    ContextMenuStrip trayMenu;
    Form stream;
    IntPtr renderer;
    bool closingStream;
    bool resumeAfterStream;
    bool exiting;
    Process child;
    string activeName;
    DateTime started;

    [STAThread]
    static void Main(string[] args)
    {
        bool unique;
        using (var mutex = new Mutex(true, @"Local\MyMirrorAirPlay", out unique))
        {
            if (!unique) { MessageBox.Show("My Mirror est déjà ouvert."); return; }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Mirror(Array.IndexOf(args, "--manuel") < 0));
        }
    }

    Mirror(bool autoStart)
    {
        Text = "My Mirror · AirPlay";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        if (!SystemInformation.HighContrast)
        {
            BackColor = Color.FromArgb(16, 16, 16);
            ForeColor = Color.WhiteSmoke;
            receiver.BackColor = Color.FromArgb(33, 33, 33);
            receiver.ForeColor = Color.WhiteSmoke;
            pin.BackColor = receiver.BackColor;
            pin.ForeColor = receiver.ForeColor;
            foreach (Button button in new[] { action, rename })
            {
                button.FlatStyle = FlatStyle.Flat;
                button.BackColor = Color.FromArgb(36, 36, 36);
            }
        }
        Font = new Font("Segoe UI", 10);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(460, 395);
        AutoScroll = true;
        var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(20), ColumnCount = 1, RowCount = 10 };
        var header = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        header.Controls.Add(new PictureBox { Image = Icon.ToBitmap(), Size = new Size(42, 42), SizeMode = PictureBoxSizeMode.Zoom });
        header.Controls.Add(new Label { Text = "My Mirror", AutoSize = true, Font = new Font("Segoe UI", 23, FontStyle.Bold) });
        layout.Controls.Add(header);
        layout.Controls.Add(new Label { Text = "&Nom affiché sur l’iPhone", AutoSize = true, TabIndex = 1 });
        receiver.AccessibleName = "Nom du récepteur AirPlay";
        receiver.TabIndex = 2;
        rename.TabIndex = 3;
        action.TabIndex = 4;
        pin.TabIndex = 6;
        layout.Controls.Add(receiver);
        layout.Controls.Add(rename);
        layout.Controls.Add(action);
        layout.Controls.Add(new Label { Text = "&Code AirPlay", AutoSize = true, TabIndex = 5 });
        layout.Controls.Add(pin);
        layout.Controls.Add(new Label { Text = "Sur l’iPhone : Centre de contrôle → Recopie de l’écran. Choisis le nom ci-dessus puis saisis le code. L’image s’ouvre dans une autre fenêtre.", MaximumSize = new Size(420, 0), AutoSize = true });
        layout.Controls.Add(status);
        layout.Controls.Add(new Label { Text = "Created by 7myo", AutoSize = true });
        for (int i = 0; i < layout.RowCount; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        foreach (Control c in layout.Controls) c.Margin = new Padding(0, 4, 0, 5);
        Controls.Add(layout);
        AcceptButton = action;
        action.Click += delegate { if (child == null) TryStart(); else TryStop(); };
        receiver.TextChanged += delegate { UpdateRename(); };
        rename.Click += delegate
        {
            if (!ValidateName()) return;
            if (TryStop()) TryStart();
        };
        timer.Tick += delegate { Poll(); };
        FormClosing += delegate(object sender, FormClosingEventArgs e)
        {
            exiting = true;
            if (child != null && !TryStop()) { exiting = false; e.Cancel = true; }
        };
        FormClosed += delegate
        {
            if (tray != null) tray.Dispose();
            if (trayMenu != null) trayMenu.Dispose();
        };
        trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Quitter My Mirror", null, delegate { Close(); });
        tray = new NotifyIcon { Icon = Icon, Text = "My Mirror", ContextMenuStrip = trayMenu, Visible = true };
        if (autoStart) Shown += delegate { TryStart(); };
    }

    void TryStart()
    {
        if (!ValidateName()) return;
        bool prepared = false;
        try
        {
            string name = receiver.Text.Trim();
            if (!File.Exists(Engine)) throw new FileNotFoundException("Installe UxPlay Windows x64 pour tous les utilisateurs, puis réessaie.");
            if (File.Exists(MachineArgs)) throw new InvalidOperationException("Une configuration UxPlay est imposée dans ProgramData. Elle doit être gérée par l’administrateur.");
            foreach (var p in Process.GetProcessesByName("uxplay-windows"))
                using (p) { if (!p.HasExited) throw new InvalidOperationException("Quitte UxPlay Windows depuis son icône près de l’horloge, puis réessaie."); }
            Restore();
            using (var svc = new ServiceController("Bonjour Service"))
                if (svc.Status != ServiceControllerStatus.Running) throw new InvalidOperationException("Le service Bonjour doit être démarré.");
            string code = MakeCode();
            Directory.CreateDirectory(Path.GetDirectoryName(ArgsFile));
            string config = "-n " + name + " -nh -pin " + code + " -p " + Port + " -vs d3d11videosink -fps 60 -nofreeze";
            SaveRecovery(config);
            prepared = true;
            WriteFile(ArgsFile, Utf8.GetBytes(config));
            child = Process.Start(new ProcessStartInfo(Engine) { WorkingDirectory = Path.GetDirectoryName(Engine), UseShellExecute = false });
            started = DateTime.UtcNow;
            status.Text = "Démarrage du récepteur…";
            pin.Text = code;
            activeName = name;
            action.Text = "&Arrêter";
            UpdateRename();
            timer.Start();
        }
        catch (Exception e)
        {
            string error = e.Message;
            if (prepared && child == null)
                try { Restore(); } catch (Exception restoreError) { error += " " + restoreError.Message; }
            status.Text = error;
        }
    }

    bool ValidateName()
    {
        if (Regex.IsMatch(receiver.Text.Trim(), @"^[A-Za-z0-9][A-Za-z0-9_-]{0,27}$")) return true;
        status.Text = "Utilise 1 à 28 lettres sans accent, chiffres, tirets ou soulignés.";
        receiver.Focus();
        return false;
    }

    void UpdateRename()
    {
        rename.Enabled = child != null && receiver.Text.Trim() != activeName;
        AcceptButton = child == null ? action : (rename.Enabled ? rename : null);
    }

    static void WriteFile(string path, byte[] bytes)
    {
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temporary, bytes);
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
        finally { File.Delete(temporary); }
    }

    static string MakeCode()
    {
        using (var rng = RandomNumberGenerator.Create())
        {
            var bytes = new byte[2];
            int value;
            do { rng.GetBytes(bytes); value = BitConverter.ToUInt16(bytes, 0); } while (value >= 63000);
            return (1000 + value % 9000).ToString();
        }
    }

    static void SaveRecovery(string config)
    {
        byte[] previous = File.Exists(ArgsFile) ? File.ReadAllBytes(ArgsFile) : null;
        using (var buffer = new MemoryStream())
        using (var writer = new BinaryWriter(buffer, Utf8))
        {
            writer.Write(config);
            writer.Write(previous != null);
            if (previous != null) writer.Write(previous);
            writer.Flush();
            WriteFile(RecoveryFile, buffer.ToArray());
        }
    }

    static void Restore()
    {
        if (!File.Exists(RecoveryFile)) return;
        string config;
        byte[] previous;
        using (var buffer = new MemoryStream(File.ReadAllBytes(RecoveryFile)))
        using (var reader = new BinaryReader(buffer, Utf8))
        {
            config = reader.ReadString();
            previous = reader.ReadBoolean() ? reader.ReadBytes(checked((int)(buffer.Length - buffer.Position))) : null;
        }
        if (File.Exists(ArgsFile) && File.ReadAllText(ArgsFile, Utf8) == config)
        {
            if (previous == null) File.Delete(ArgsFile);
            else WriteFile(ArgsFile, previous);
        }
        File.Delete(RecoveryFile);
    }

    bool TryStop(bool fromStream = false)
    {
        try
        {
            if (child != null && !child.HasExited)
            {
                child.Kill();
                if (!child.WaitForExit(3000)) throw new InvalidOperationException("UxPlay ne s’est pas encore arrêté.");
            }
            Finish("Récepteur arrêté.");
            if (!fromStream) CloseStream();
            return true;
        }
        catch (Exception e) { status.Text = e.Message; return false; }
    }

    void Finish(string message)
    {
        Restore();
        timer.Stop();
        if (child != null) child.Dispose();
        child = null;
        activeName = null;
        action.Text = "Démarrer";
        UpdateRename();
        pin.Text = "—";
        status.Text = message;
    }

    void Poll()
    {
        try
        {
            if (child == null) return;
            if (child.HasExited)
            {
                Finish("Le moteur AirPlay s’est arrêté. Vérifie Bonjour et ferme tout autre récepteur.");
                CloseStream();
                return;
            }
            if (stream != null && (!IsWindow(renderer) || !IsWindowVisible(renderer)))
            {
                CloseStream();
                return;
            }
            if (stream == null)
            {
                IntPtr window = FindRenderer(child.Id);
                if (window != IntPtr.Zero) OpenStream(window);
            }
            if (Listening(child.Id))
                status.Text = "Récepteur à l’écoute : " + activeName + ".";
            else if ((DateTime.UtcNow - started).TotalSeconds > 10)
                status.Text = "UxPlay tourne, mais aucun port AirPlay n’est à l’écoute. Vérifie l’icône UxPlay près de l’horloge.";
        }
        catch (Exception e) { status.Text = e.Message; }
    }

    void CloseStream()
    {
        if (stream == null) return;
        closingStream = true;
        try { stream.Close(); }
        finally { closingStream = false; }
    }

    void OpenStream(IntPtr window)
    {
        var form = new Form {
            Text = "My Mirror · Vidéo",
            Icon = Icon,
            BackColor = Color.Black,
            FormBorderStyle = FormBorderStyle.FixedSingle,
            MaximizeBox = false,
            StartPosition = FormStartPosition.CenterScreen
        };
        Rectangle workArea = Screen.FromControl(this).WorkingArea;
        NativeRect bounds;
        Size source = GetClientRect(window, out bounds) && bounds.Right > 0 && bounds.Bottom > 0
            ? new Size(bounds.Right, bounds.Bottom) : new Size(720, 1280);
        form.ClientSize = FitVideo(source, new Size(Math.Max(1, workArea.Width - 32), Math.Max(1, workArea.Height - 80)));
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(workArea.Left + (workArea.Width - form.Width) / 2, workArea.Top + (workArea.Height - form.Height) / 2);
        DarkCaption(form);
        IntPtr host = form.Handle;
        int style = GetWindowLong(window, -16);
        ShowWindow(window, 0);
        SetWindowLong(window, -16, (style & ~unchecked((int)0x80CF0000)) | 0x40000000);
        SetParent(window, host);
        if (GetParent(window) != host)
        {
            SetWindowLong(window, -16, style);
            ShowWindow(window, 5);
            form.Dispose();
            throw new InvalidOperationException("Impossible d’intégrer la vidéo à la fenêtre My Mirror.");
        }

        renderer = window;
        stream = form;
        form.Resize += delegate { SizeStream(); };
        form.FormClosing += delegate(object sender, FormClosingEventArgs e)
        {
            if (!closingStream)
            {
                string name = activeName;
                e.Cancel = !TryStop(true);
                resumeAfterStream = !e.Cancel;
                if (resumeAfterStream && name != null) receiver.Text = name;
            }
        };
        form.FormClosed += delegate
        {
            stream = null;
            renderer = IntPtr.Zero;
            if (exiting) return;
            Show();
            Activate();
            if (resumeAfterStream) { resumeAfterStream = false; TryStart(); }
        };
        form.Show();
        SizeStream();
        ShowWindow(window, 5);
        Hide();
    }

    static Size FitVideo(Size source, Size available)
    {
        double scale = Math.Min(1.0, Math.Min((double)available.Width / source.Width, (double)available.Height / source.Height));
        return new Size(Math.Max(1, (int)Math.Round(source.Width * scale)), Math.Max(1, (int)Math.Round(source.Height * scale)));
    }

    [StructLayout(LayoutKind.Sequential)]
    struct NativeRect { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr window, out NativeRect rectangle);

    void SizeStream()
    {
        if (stream != null && IsWindow(renderer))
            SetWindowPos(renderer, IntPtr.Zero, 0, 0, stream.ClientSize.Width, stream.ClientSize.Height, 0x34);
    }

    static IntPtr FindRenderer(int pid)
    {
        IntPtr result = IntPtr.Zero;
        EnumWindows(delegate(IntPtr window, IntPtr context)
        {
            int owner;
            GetWindowThreadProcessId(window, out owner);
            if (owner != pid || !IsWindowVisible(window)) return true;
            var title = new StringBuilder(256);
            GetWindowText(window, title, title.Capacity);
            string name = title.ToString();
            if (name.IndexOf("AirPlay Video Stream", StringComparison.OrdinalIgnoreCase) < 0 &&
                !(name.IndexOf("Direct", StringComparison.OrdinalIgnoreCase) >= 0 &&
                  name.IndexOf("enderer", StringComparison.OrdinalIgnoreCase) >= 0)) return true;
            result = window;
            return false;
        }, IntPtr.Zero);
        return result;
    }

    delegate bool WindowCallback(IntPtr window, IntPtr context);
    [DllImport("user32.dll")] static extern bool EnumWindows(WindowCallback callback, IntPtr context);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out int pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr window, StringBuilder text, int capacity);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] static extern IntPtr SetParent(IntPtr window, IntPtr parent);
    [DllImport("user32.dll")] static extern IntPtr GetParent(IntPtr window);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] static extern int GetWindowLong(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] static extern int SetWindowLong(IntPtr window, int index, int value);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        DarkCaption(this);
    }

    static void DarkCaption(Form form)
    {
        int enabled = 1;
        if (!SystemInformation.HighContrast)
            DwmSetWindowAttribute(form.Handle, 20, ref enabled, sizeof(int));
    }

    [System.Runtime.InteropServices.DllImport("iphlpapi.dll")]
    static extern uint GetExtendedTcpTable(IntPtr table, ref int size, bool order, int family, int type, uint reserved);

    static bool Listening(int pid)
    {
        int size = 0;
        GetExtendedTcpTable(IntPtr.Zero, ref size, false, 2, 3, 0);
        if (size < 4) return false;
        IntPtr table = System.Runtime.InteropServices.Marshal.AllocHGlobal(size);
        try
        {
            if (GetExtendedTcpTable(table, ref size, false, 2, 3, 0) != 0) return false;
            int count = System.Runtime.InteropServices.Marshal.ReadInt32(table);
            for (int i = 0; i < count; i++)
            {
                int row = 4 + 24 * i;
                int raw = System.Runtime.InteropServices.Marshal.ReadInt32(table, row + 8);
                int port = ((raw & 255) << 8) | ((raw >> 8) & 255);
                if (port >= Port && port <= Port + 2 && System.Runtime.InteropServices.Marshal.ReadInt32(table, row + 20) == pid) return true;
            }
            return false;
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(table); }
    }
}
