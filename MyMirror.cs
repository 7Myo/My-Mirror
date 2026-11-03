using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32.SafeHandles;

internal sealed class Mirror : Form
{
    static readonly string Engine = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"uxplay-windows\uxplay-windows.exe");
    static readonly string ArgsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"leapbtw\uxplay-windows\arguments.txt");
    static readonly string RecoveryFile = ArgsFile + ".mymirror-recovery";
    static readonly string MachineArgs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"uxplay-windows\arguments.txt");
    static readonly Encoding Utf8 = new UTF8Encoding(false);
    const int Port = 7010;
    const string ReceiverName = "My-Mirror";
    readonly TextBox pin = new TextBox { ReadOnly = true, AccessibleName = "Code AirPlay", Text = "—", Font = new Font("Segoe UI", 32, FontStyle.Bold), TextAlign = HorizontalAlignment.Center, Dock = DockStyle.Fill };
    readonly Label status = new Label { AutoSize = true, MaximumSize = new Size(420, 0), Text = "Récepteur arrêté." };
    readonly CheckBox airPlayAudio = new CheckBox { Text = "&Son AirPlay", AutoSize = true, Checked = true };
    readonly Button retry = new Button { Text = "&Réessayer", AutoSize = true, Visible = false };
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 500 };
    NotifyIcon tray;
    ContextMenuStrip trayMenu;
    Form stream;
    IntPtr renderer;
    bool closingStream;
    bool exiting;
    Process child;
    EngineJob job;
    readonly Stopwatch startup = new Stopwatch();
    bool ready;
    IntPtr rejectedRenderer;

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
        Text = "My Mirror";
        Icon = Icon.ExtractAssociatedIcon(typeof(Mirror).Assembly.Location);
        bool highContrast = SystemInformation.HighContrast;
        Color secondary = highContrast ? SystemColors.WindowText : Color.FromArgb(82, 88, 96);
        if (!highContrast)
        {
            BackColor = Color.White;
            ForeColor = Color.FromArgb(26, 28, 31);
            pin.BackColor = Color.FromArgb(231, 240, 254);
            pin.ForeColor = ForeColor;
            status.TextChanged += delegate
            {
                status.ForeColor = status.Text.StartsWith("Prêt à se connecter", StringComparison.Ordinal)
                    ? Color.FromArgb(24, 112, 52) : secondary;
            };
        }
        Font = new Font("Segoe UI", 10);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(420, 410);
        AutoScroll = true;
        var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(24), ColumnCount = 1, RowCount = 8 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var header = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 2, RowCount = 2 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var mark = new PictureBox { Image = Icon.ToBitmap(), Size = new Size(54, 54), SizeMode = PictureBoxSizeMode.Zoom, Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 14, 0) };
        header.Controls.Add(mark, 0, 0);
        header.SetRowSpan(mark, 2);
        header.Controls.Add(new Label { Text = "My Mirror", AutoSize = true, Font = new Font("Segoe UI", 23, FontStyle.Bold) }, 1, 0);
        status.MaximumSize = new Size(270, 0);
        status.ForeColor = secondary;
        header.Controls.Add(status, 1, 1);
        layout.Controls.Add(header);
        layout.Controls.Add(new Label { Text = ReceiverName, AutoSize = true, Font = new Font("Segoe UI", 14, FontStyle.Bold) });
        layout.Controls.Add(new Label { Text = "&Code AirPlay", AutoSize = true, TabIndex = 3 });
        pin.TabIndex = 4;
        layout.Controls.Add(pin);
        airPlayAudio.TabIndex = 5;
        layout.Controls.Add(airPlayAudio);
        layout.Controls.Add(new Label { Text = "Sur l’iPhone, ouvre Recopie de l’écran puis choisis " + ReceiverName + ".", MaximumSize = new Size(350, 0), AutoSize = true, ForeColor = secondary });
        retry.TabIndex = 6;
        layout.Controls.Add(retry);
        layout.Controls.Add(new Label { Text = "Created by 7myo", AutoSize = true, ForeColor = secondary });
        for (int i = 0; i < layout.RowCount; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        foreach (Control c in layout.Controls) c.Margin = new Padding(0, 4, 0, 5);
        header.Margin = new Padding(0, 0, 0, 14);
        Controls.Add(layout);
        AcceptButton = retry;
        retry.Click += delegate { TryStart(); };
        airPlayAudio.Click += delegate
        {
            if (child == null) return;
            if (TryStop()) TryStart();
            else airPlayAudio.Checked = !airPlayAudio.Checked;
        };
        timer.Tick += delegate { Poll(); };
        FormClosing += delegate(object sender, FormClosingEventArgs e)
        {
            exiting = true;
            if (child == null) CloseStream();
            else if (!TryStop()) { exiting = false; e.Cancel = true; }
        };
        FormClosed += delegate
        {
            timer.Dispose();
            if (tray != null) tray.Dispose();
            if (trayMenu != null) trayMenu.Dispose();
        };
        trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Quitter My Mirror", null, delegate { Close(); });
        tray = new NotifyIcon { Icon = Icon, Text = "My Mirror", ContextMenuStrip = trayMenu, Visible = true };
        if (autoStart) Shown += delegate { TryStart(); };
        else retry.Visible = true;
    }

    void TryStart()
    {
        if (child != null) return;
        bool prepared = false;
        try
        {
            if (!File.Exists(Engine)) throw new FileNotFoundException("Installe UxPlay Windows x64 pour tous les utilisateurs, puis réessaie.");
            if (File.Exists(MachineArgs)) throw new InvalidOperationException("Une configuration UxPlay est imposée dans ProgramData. Elle doit être gérée par l’administrateur.");
            var engines = Process.GetProcessesByName("uxplay-windows");
            try
            {
                if (engines.Any(p => !p.HasExited))
                    throw new InvalidOperationException("UxPlay tourne déjà. Fais un clic droit sur son icône près de l’horloge, choisis Quitter, puis clique sur Réessayer.");
            }
            finally { foreach (var engine in engines) engine.Dispose(); }
            Restore();
            using (var svc = new ServiceController("Bonjour Service"))
                if (svc.Status != ServiceControllerStatus.Running) throw new InvalidOperationException("Le service Bonjour doit être démarré.");
            string code = MakeCode();
            Directory.CreateDirectory(Path.GetDirectoryName(ArgsFile));
            string config = EngineArguments(code, airPlayAudio.Checked);
            SaveRecovery(config);
            prepared = true;
            WriteFile(ArgsFile, Utf8.GetBytes(config));
            job = new EngineJob();
            child = Process.Start(new ProcessStartInfo(Engine) { WorkingDirectory = Path.GetDirectoryName(Engine), UseShellExecute = false });
            job.Attach(child);
            startup.Restart();
            ready = false;
            rejectedRenderer = IntPtr.Zero;
            status.Text = "Démarrage du récepteur…";
            pin.Text = code;
            retry.Visible = false;
            timer.Start();
        }
        catch (Exception e)
        {
            string error = e.Message;
            if (prepared)
            {
                if (child != null)
                {
                    if (TryStop()) status.Text = error;
                    return;
                }
                ReleaseJob();
                try { Restore(); } catch (Exception restoreError) { error += " " + restoreError.Message; }
            }
            status.Text = error;
            retry.Visible = child == null;
        }
    }

    static string EngineArguments(string code, bool withAudio)
    {
        string options = "-n " + ReceiverName + " -nh -pin " + code + " -p " + Port + " -vs d3d11videosink -fps 60 -nofreeze";
        return withAudio ? options : options + " -as 0";
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
        if (File.Exists(ArgsFile) && File.ReadAllBytes(ArgsFile).SequenceEqual(Utf8.GetBytes(config)))
        {
            if (previous == null) File.Delete(ArgsFile);
            else WriteFile(ArgsFile, previous);
        }
        File.Delete(RecoveryFile);
    }

    bool TryStop(bool fromStream = false, string message = "Récepteur arrêté.")
    {
        try
        {
            if (child != null && !child.HasExited)
            {
                bool attached = job != null && job.Attached;
                ReleaseJob();
                if (!attached)
                {
                    try { child.Kill(); }
                    catch (InvalidOperationException) { if (!child.HasExited) throw; }
                }
                if (!child.WaitForExit(3000)) throw new InvalidOperationException("UxPlay ne s’est pas encore arrêté.");
            }
            Finish(message);
            if (!fromStream) CloseStream();
            return true;
        }
        catch (Exception e) { status.Text = e.Message; return false; }
    }

    void Finish(string message)
    {
        timer.Stop();
        startup.Reset();
        ReleaseJob();
        if (child != null) child.Dispose();
        child = null;
        retry.Visible = !exiting;
        pin.Text = "—";
        try { Restore(); }
        catch (Exception e) { message += " Restauration à réessayer au prochain démarrage : " + e.Message; }
        status.Text = message;
    }

    void ReleaseJob()
    {
        if (job == null) return;
        job.Dispose();
        job = null;
    }

    void Poll()
    {
        try
        {
            if (child == null) return;
            if (child.HasExited)
            {
                Finish("Le moteur AirPlay s’est arrêté. Vérifie Bonjour et ferme tout autre récepteur.");
                if (stream != null) Close();
                return;
            }
            if (stream != null && (!IsWindow(renderer) || !IsWindowVisible(renderer)))
            {
                Close();
                return;
            }
            if (!ready && Listening(child.Id))
            {
                ready = true;
                status.Text = "Prêt à se connecter";
            }
            else if (!ready && startup.Elapsed.TotalSeconds >= 15)
            {
                TryStop(false, "Aucun port AirPlay ne répond. Ferme les autres récepteurs et clique sur Réessayer.");
                return;
            }
            if (stream == null)
            {
                IntPtr window = FindRenderer(child.Id);
                if (window != IntPtr.Zero && window != rejectedRenderer)
                {
                    try { OpenStream(window); }
                    catch { rejectedRenderer = window; throw; }
                }
            }
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
        UseLightCaption(form);
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
            }
        };
        form.FormClosed += delegate
        {
            stream = null;
            renderer = IntPtr.Zero;
            if (!exiting) Close();
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
        UseLightCaption(this);
    }

    static void UseLightCaption(Form form)
    {
        int enabled = 0;
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
            int count = Marshal.ReadInt32(table);
            if (count < 0 || count > (size - 4) / 24) return false;
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

    sealed class EngineJob : IDisposable
    {
        const int ExtendedLimitInformation = 9;
        const uint KillOnClose = 0x2000;
        readonly SafeFileHandle handle;
        public bool Attached { get; private set; }

        public EngineJob()
        {
            handle = CreateJobObject(IntPtr.Zero, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error();
                handle.Dispose();
                throw new Win32Exception(error);
            }

            var limits = new JobLimits();
            limits.Basic.Flags = KillOnClose;
            if (!SetInformationJobObject(handle, ExtendedLimitInformation, ref limits, Marshal.SizeOf(typeof(JobLimits))))
            {
                int error = Marshal.GetLastWin32Error();
                handle.Dispose();
                throw new Win32Exception(error);
            }
        }

        public void Attach(Process process)
        {
            if (!AssignProcessToJobObject(handle, process.Handle))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            Attached = true;
        }

        public void Dispose() { handle.Dispose(); }

        [StructLayout(LayoutKind.Sequential)]
        struct BasicLimits
        {
            public long ProcessTime, JobTime;
            public uint Flags;
            public UIntPtr MinimumWorkingSet, MaximumWorkingSet;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass, SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct JobLimits
        {
            public BasicLimits Basic;
            public ulong ReadOperations, WriteOperations, OtherOperations;
            public ulong ReadBytes, WriteBytes, OtherBytes;
            public UIntPtr ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory;
        }

        [DllImport("kernel32.dll", EntryPoint = "CreateJobObjectW", SetLastError = true)]
        static extern SafeFileHandle CreateJobObject(IntPtr attributes, IntPtr name);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetInformationJobObject(SafeFileHandle job, int informationClass, ref JobLimits limits, int length);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);
    }
}
