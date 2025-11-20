using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TransparentScreenCapture
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public enum CaptureMode { Interval, MouseClick }

    public class MainForm : Form
    {
        private readonly Label lblFolder = new Label { Text = "Destination folder:", AutoSize = true };
        private readonly TextBox txtFolder = new TextBox { ReadOnly = true, Width = 360 };
        private readonly Button btnBrowse = new Button { Text = "Browse..." };
        private readonly GroupBox grpMode = new GroupBox { Text = "Trigger", Width = 430, Height = 90 };
        private readonly RadioButton rbInterval = new RadioButton
        {
            Text = "Interval",
            Checked = true,
            AutoSize = true,
            Appearance = Appearance.Normal,
            UseVisualStyleBackColor = true
        };
        
        private readonly RadioButton rbMouseClick = new RadioButton
        {
            Text = "On Mouse",
            AutoSize = true,
            Appearance = Appearance.Normal,
            UseVisualStyleBackColor = true
        };

        private readonly Label lblSeconds = new Label { Text = "Seconds:", Left = 10, Top = 55, AutoSize = true };
        private readonly TrackBar tbSeconds = new TrackBar { Minimum = 1, Maximum = 120, Value = 5, TickFrequency = 5, Left = 75, Top = 50, Width = 320 };
        private readonly Label lblSecVal = new Label { Text = "5s", Left = 400, Top = 55, AutoSize = true };
        private readonly Button btnStart = new Button { Text = "Start", Width = 100, Height = 36 };

        private readonly NotifyIcon tray = new NotifyIcon();
        private readonly ContextMenuStrip trayMenu = new ContextMenuStrip();
        private readonly ToolStripMenuItem trayShow = new ToolStripMenuItem("Show");
        private readonly ToolStripMenuItem trayStart = new ToolStripMenuItem("Start");
        private readonly ToolStripMenuItem trayStop = new ToolStripMenuItem("Stop");
        private readonly ToolStripMenuItem trayExit = new ToolStripMenuItem("Exit");

        private readonly System.Windows.Forms.Timer intervalTimer = new System.Windows.Forms.Timer();
        private LowLevelMouseHook? mouseHook;

        private string baseFolder = "";
        private CaptureMode mode = CaptureMode.Interval;

        public MainForm()
        {
            Text = "Screen Capture (Transparent & Visible)";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Width = 480;
            Height = 260;

            // Layout
            lblFolder.Left = 15; lblFolder.Top = 15;
            txtFolder.Left = 15; txtFolder.Top = 35;
            btnBrowse.Left = 380; btnBrowse.Top = 33; btnBrowse.Width = 80;

            grpMode.Left = 15; grpMode.Top = 75;
            grpMode.Controls.Add(rbInterval);
            grpMode.Controls.Add(rbMouseClick);
            grpMode.Controls.Add(lblSeconds);
            grpMode.Controls.Add(tbSeconds);
            grpMode.Controls.Add(lblSecVal);

            btnStart.Left = 365; btnStart.Top = 175;

            Controls.Add(lblFolder);
            Controls.Add(txtFolder);
            Controls.Add(btnBrowse);
            Controls.Add(grpMode);
            Controls.Add(btnStart);

            // Events
            btnBrowse.Click += (s, e) =>
            {
                using var fbd = new FolderBrowserDialog();
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    baseFolder = fbd.SelectedPath;
                    txtFolder.Text = baseFolder;
                }
            };

            rbInterval.CheckedChanged += (s, e) => UpdateMode();
            rbMouseClick.CheckedChanged += (s, e) => UpdateMode();
            // ...inside the MainForm constructor, AFTER grpMode is created:
            rbInterval.Location = new Point(10, 25);
            rbMouseClick.Location = new Point(120, 25);
            grpMode.Controls.Add(rbInterval);
            grpMode.Controls.Add(rbMouseClick);
            tbSeconds.Scroll += (s, e) => lblSecVal.Text = tbSeconds.Value + "s";

            btnStart.Click += (s, e) => StartCaptureFromUI();

            // Tray
            tray.Icon = SystemIcons.Application;
            tray.Text = "Screen Capture (Visible)";
            tray.Visible = false;
            tray.ContextMenuStrip = trayMenu;
            tray.DoubleClick += (s, e) => ShowFromTray();

            trayMenu.Items.AddRange(new ToolStripItem[] { trayShow, trayStart, trayStop, new ToolStripSeparator(), trayExit });
            trayShow.Click += (s, e) => ShowFromTray();
            trayStart.Click += (s, e) => StartCapture();
            trayStop.Click += (s, e) => StopCapture();
            trayExit.Click += (s, e) => { StopCapture(); tray.Visible = false; Application.Exit(); };

            // Timers & hooks
            intervalTimer.Tick += (s, e) => CaptureAllDisplays();

            // Initial UI state
            UpdateMode();
        }

        private void UpdateMode()
        {
            mode = rbInterval.Checked ? CaptureMode.Interval : CaptureMode.MouseClick;
            tbSeconds.Enabled = rbInterval.Checked;
        }

        private void StartCaptureFromUI()
        {
            if (string.IsNullOrWhiteSpace(baseFolder))
            {
                MessageBox.Show("Please choose a destination folder first.", "Folder required",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Minimize to tray and start
            Hide();
            ShowInTaskbar = false;
            tray.Visible = true;
            StartCapture();
        }

        private void StartCapture()
        {
            UpdateMode();

            if (mode == CaptureMode.Interval)
            {
                intervalTimer.Interval = Math.Max(1000, tbSeconds.Value * 1000);
                intervalTimer.Start();
                UnhookMouse();
            }
            else
            {
                intervalTimer.Stop();
                HookMouse();
            }

            tray.BalloonTipTitle = "Screen Capture Running";
            tray.BalloonTipText = mode == CaptureMode.Interval
                ? $"Capturing every {tbSeconds.Value} second(s)."
                : "Capturing on mouse clicks.";
            tray.ShowBalloonTip(2000);
        }

        private void StopCapture()
        {
            intervalTimer.Stop();
            UnhookMouse();
            tray.BalloonTipTitle = "Screen Capture Stopped";
            tray.BalloonTipText = "No more screenshots will be taken.";
            tray.ShowBalloonTip(1500);
        }

        private void ShowFromTray()
        {
            Show();
            ShowInTaskbar = true;
            WindowState = FormWindowState.Normal;
            Activate();
        }

        private void HookMouse()
        {
            if (mouseHook == null)
            {
                mouseHook = new LowLevelMouseHook();
                mouseHook.MouseDown += (btn) => CaptureAllDisplays();
                mouseHook.Install();
            }
        }

        private void UnhookMouse()
        {
            if (mouseHook != null)
            {
                mouseHook.Uninstall();
                mouseHook = null;
            }
        }

        private void CaptureAllDisplays()
        {
            try
            {
                var screens = Screen.AllScreens;
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss.fff");

                for (int i = 0; i < screens.Length; i++)
                {
                    var scr = screens[i];
                    var bounds = scr.Bounds;

                    using var bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
                    }

                    string folder = System.IO.Path.Combine(baseFolder, $"Display_{i}");
                    if (!System.IO.Directory.Exists(folder)) System.IO.Directory.CreateDirectory(folder);

                    string file = System.IO.Path.Combine(folder, $"{i}-{timestamp}.png");
                    bmp.Save(file, ImageFormat.Png);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                tray.BalloonTipTitle = "Capture Error";
                tray.BalloonTipText = ex.Message;
                tray.ShowBalloonTip(2000);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Ensure hooks/timers are off and tray is hidden when exiting from UI
            StopCapture();
            tray.Visible = false;
            base.OnFormClosing(e);
        }
    }

    // Low-level mouse hook: fires on button down; ignores movement.
    public class LowLevelMouseHook
    {
        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MBUTTONDOWN = 0x0207;
        private IntPtr _hookId = IntPtr.Zero;
        private HookProc? _proc;

        public event Action<MouseButtons>? MouseDown;

        public void Install()
        {
            _proc = HookCallback;
            using (Process cur = Process.GetCurrentProcess())
            using (ProcessModule mod = cur.MainModule!)
            {
                _hookId = SetWindowsHookEx(WH_MOUSE_LL, _proc!, GetModuleHandle(mod.ModuleName!), 0);
            }
        }

        public void Uninstall()
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                if (msg == WM_LBUTTONDOWN) MouseDown?.Invoke(MouseButtons.Left);
                else if (msg == WM_RBUTTONDOWN) MouseDown?.Invoke(MouseButtons.Right);
                else if (msg == WM_MBUTTONDOWN) MouseDown?.Invoke(MouseButtons.Middle);
            }
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}
