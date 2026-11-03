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
    static readonly string MachineArgs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"uxplay-windows\arguments.txt");
    static readonly Encoding Utf8 = new UTF8Encoding(false);
    const int Port = 7010;
    readonly TextBox receiver = new TextBox { Text = "My-Mirror", MaxLength = 28, Dock = DockStyle.Fill };
    readonly Label pin = new Label { AutoSize = true, Text = "Code : —", Font = new Font("Segoe UI", 17, FontStyle.Bold) };
    readonly Label status = new Label { AutoSize = true, MaximumSize = new Size(420, 0), Text = "Récepteur arrêté." };
    readonly Button action = new Button { Text = "Démarrer", AutoSize = true };
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 500 };
    Form stream;
    IntPtr renderer;
    bool closingStream;
    bool resumeAfterStream;
    Process child;
    byte[] previous;
    string applied;
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
        BackColor = Color.FromArgb(16, 16, 16);
        ForeColor = Color.WhiteSmoke;
        receiver.BackColor = Color.FromArgb(33, 33, 33);
        receiver.ForeColor = Color.WhiteSmoke;
        action.FlatStyle = FlatStyle.Flat;
        action.BackColor = Color.FromArgb(36, 36, 36);
        Font = new Font("Segoe UI", 10);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(460, 330);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 7 };
        layout.Controls.Add(new Label { Text = "My Mirror", AutoSize = true, Font = new Font("Segoe UI", 23, FontStyle.Bold) });
        layout.Controls.Add(new Label { Text = "Nom affiché sur l’iPhone", AutoSize = true });
        layout.Controls.Add(receiver);
        layout.Controls.Add(action);
        layout.Controls.Add(pin);
        layout.Controls.Add(new Label { Text = "Sur l’iPhone : Centre de contrôle → Recopie de l’écran. Choisis le nom ci-dessus puis saisis le code. L’image s’ouvre dans une autre fenêtre.", MaximumSize = new Size(420, 0), AutoSize = true });
        layout.Controls.Add(status);
        for (int i = 0; i < 7; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        foreach (Control c in layout.Controls) c.Margin = new Padding(0, 4, 0, 5);
        Controls.Add(layout);
        AcceptButton = action;
        action.Click += delegate { if (child == null) TryStart(); else TryStop(); };
        timer.Tick += delegate { Poll(); };
        FormClosing += delegate(object sender, FormClosingEventArgs e) { if (child != null && !TryStop()) e.Cancel = true; };
        if (autoStart) Shown += delegate { TryStart(); };
    }

    void TryStart()
    {
        try
        {
            string name = receiver.Text.Trim();
            if (!Regex.IsMatch(name, @"^[A-Za-z0-9][A-Za-z0-9_-]{0,27}$"))
                throw new InvalidOperationException("Utilise 1 à 28 lettres sans accent, chiffres, tirets ou soulignés.");
            if (!File.Exists(Engine)) throw new FileNotFoundException("Installe UxPlay Windows x64 pour tous les utilisateurs, puis réessaie.");
            if (File.Exists(MachineArgs)) throw new InvalidOperationException("Une configuration UxPlay est imposée dans ProgramData. Elle doit être gérée par l’administrateur.");
            foreach (var p in Process.GetProcessesByName("uxplay-windows"))
                using (p) { if (!p.HasExited) throw new InvalidOperationException("Quitte UxPlay Windows depuis son icône près de l’horloge, puis réessaie."); }
            using (var svc = new ServiceController("Bonjour Service"))
                if (svc.Status != ServiceControllerStatus.Running) throw new InvalidOperationException("Le service Bonjour doit être démarré.");
            string code = MakeCode();
            Directory.CreateDirectory(Path.GetDirectoryName(ArgsFile));
            previous = File.Exists(ArgsFile) ? File.ReadAllBytes(ArgsFile) : null;
            string config = "-n " + name + " -nh -pin " + code + " -p " + Port + " -vs d3d11videosink -nofreeze";
            File.WriteAllText(ArgsFile, config, Utf8);
            applied = config;
            child = Process.Start(new ProcessStartInfo(Engine) { WorkingDirectory = Path.GetDirectoryName(Engine), UseShellExecute = false });
            started = DateTime.UtcNow;
            status.Text = "Démarrage du récepteur…";
            pin.Text = "Code : " + code;
            receiver.Enabled = false;
            action.Text = "Arrêter";
            timer.Start();
        }
        catch (Exception e)
        {
            string error = e.Message;
            try { Restore(); } catch (Exception restoreError) { error += " " + restoreError.Message; }
            status.Text = error;
        }
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

    void Restore()
    {
        if (applied == null) return;
        if (File.Exists(ArgsFile) && File.ReadAllText(ArgsFile, Utf8) == applied)
        {
            if (previous == null) File.Delete(ArgsFile);
            else File.WriteAllBytes(ArgsFile, previous);
        }
        applied = null;
        previous = null;
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
        receiver.Enabled = true;
        action.Text = "Démarrer";
        pin.Text = "Code : —";
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
                status.Text = "Récepteur à l’écoute. Ouvre Recopie de l’écran sur l’iPhone.";
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
            ClientSize = new Size(640, 640),
            MinimumSize = new Size(320, 260),
            StartPosition = FormStartPosition.CenterScreen
        };
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
                e.Cancel = !TryStop(true);
                resumeAfterStream = !e.Cancel;
            }
        };
        form.FormClosed += delegate { stream = null; renderer = IntPtr.Zero; Show(); Activate(); if (resumeAfterStream) { resumeAfterStream = false; TryStart(); } };
        form.Show();
        SizeStream();
        ShowWindow(window, 5);
        Hide();
    }

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
