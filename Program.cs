using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using static TransparentScreenCapture.NativeWindowInterop;

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

    public enum CaptureMode { Interval, MouseClick, Keyboard }
    public enum CaptureTargetType { AllScreens, SingleScreen, Window }

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
        private readonly RadioButton rbKeyboard = new RadioButton
        {
            Text = "On Keystroke",
            AutoSize = true,
            Appearance = Appearance.Normal,
            UseVisualStyleBackColor = true
        };
        private readonly Label lblHotkey = new Label { Text = "Hotkey:", AutoSize = true };
        private readonly TextBox txtHotkey = new TextBox { ReadOnly = true, Width = 200 };
        private readonly Button btnClearHotkey = new Button { Text = "Clear", Width = 60 };

        private readonly Label lblSeconds = new Label { Text = "Seconds:", Left = 10, Top = 55, AutoSize = true };
        private readonly TrackBar tbSeconds = new TrackBar { Minimum = 1, Maximum = 120, Value = 5, TickFrequency = 5, Left = 75, Top = 50, Width = 320 };
        private readonly Label lblSecVal = new Label { Text = "5s", Left = 400, Top = 55, AutoSize = true };
        private readonly Button btnStart = new Button { Text = "Start", Width = 100, Height = 36 };

        private readonly GroupBox grpTarget = new GroupBox { Text = "Capture Target", Width = 430, Height = 120 };
        private readonly RadioButton rbTargetAll = new RadioButton { Text = "All screens", Checked = true, AutoSize = true };
        private readonly RadioButton rbTargetScreen = new RadioButton { Text = "Specific screen", AutoSize = true };
        private readonly RadioButton rbTargetWindow = new RadioButton { Text = "Window", AutoSize = true };
        private readonly ComboBox cbScreens = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false, Width = 280 };
        private readonly ComboBox cbWindows = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false, Width = 280 };
        private readonly Button btnRefreshWindows = new Button { Text = "Refresh", Enabled = false, Width = 70 };

        private readonly NotifyIcon tray = new NotifyIcon();
        private readonly ContextMenuStrip trayMenu = new ContextMenuStrip();
        private readonly ToolStripMenuItem trayShow = new ToolStripMenuItem("Show");
        private readonly ToolStripMenuItem trayStart = new ToolStripMenuItem("Start");
        private readonly ToolStripMenuItem trayStop = new ToolStripMenuItem("Stop");
        private readonly ToolStripMenuItem trayExit = new ToolStripMenuItem("Exit");

        private readonly System.Windows.Forms.Timer intervalTimer = new System.Windows.Forms.Timer();
        private LowLevelMouseHook? mouseHook;
        private LowLevelKeyboardHook? keyboardHook;

        private string baseFolder = "";
        private CaptureMode mode = CaptureMode.Interval;
        private CaptureTargetType targetType = CaptureTargetType.AllScreens;
        private HotkeyBinding? hotkey;

        public MainForm()
        {
            Text = "Screen Capture (Transparent & Visible)";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Width = 480;
            Height = 420;

            // Layout
            lblFolder.Left = 15; lblFolder.Top = 15;
            txtFolder.Left = 15; txtFolder.Top = 35;
            btnBrowse.Left = 380; btnBrowse.Top = 33; btnBrowse.Width = 80;

            grpMode.Left = 15; grpMode.Top = 75;
            grpMode.Controls.Add(rbInterval);
            grpMode.Controls.Add(rbMouseClick);
            grpMode.Controls.Add(rbKeyboard);
            grpMode.Controls.Add(lblSeconds);
            grpMode.Controls.Add(tbSeconds);
            grpMode.Controls.Add(lblSecVal);
            grpMode.Controls.Add(lblHotkey);
            grpMode.Controls.Add(txtHotkey);
            grpMode.Controls.Add(btnClearHotkey);

            grpTarget.Left = 15; grpTarget.Top = 190;
            grpTarget.Controls.Add(rbTargetAll);
            grpTarget.Controls.Add(rbTargetScreen);
            grpTarget.Controls.Add(rbTargetWindow);
            grpTarget.Controls.Add(cbScreens);
            grpTarget.Controls.Add(cbWindows);
            grpTarget.Controls.Add(btnRefreshWindows);

            btnStart.Left = 365; btnStart.Top = 330;

            Controls.Add(lblFolder);
            Controls.Add(txtFolder);
            Controls.Add(btnBrowse);
            Controls.Add(grpMode);
            Controls.Add(grpTarget);
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
            rbKeyboard.CheckedChanged += (s, e) => UpdateMode();
            rbInterval.Location = new Point(10, 25);
            rbMouseClick.Location = new Point(120, 25);
            rbKeyboard.Location = new Point(220, 25);
            lblHotkey.Left = 10; lblHotkey.Top = 55;
            txtHotkey.Left = 65; txtHotkey.Top = 50;
            btnClearHotkey.Left = 275; btnClearHotkey.Top = 50;
            txtHotkey.KeyDown += Hotkey_KeyDown;
            txtHotkey.GotFocus += (s, e) => txtHotkey.Text = "Press keys...";
            txtHotkey.LostFocus += (s, e) => RefreshHotkeyText();
            btnClearHotkey.Click += (s, e) => { hotkey = null; RefreshHotkeyText(); };
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
            intervalTimer.Tick += (s, e) => CaptureTargets();

            // Target UI
            rbTargetAll.CheckedChanged += (s, e) => UpdateTargetUI();
            rbTargetScreen.CheckedChanged += (s, e) => UpdateTargetUI();
            rbTargetWindow.CheckedChanged += (s, e) => UpdateTargetUI();
            btnRefreshWindows.Click += (s, e) => RefreshWindows();
            cbScreens.SelectedIndexChanged += (s, e) => UpdateTargetUI();
            cbWindows.SelectedIndexChanged += (s, e) => UpdateTargetUI();

            LayoutTargetControls();
            RefreshScreens();
            RefreshWindows();

            // Initial UI state
            hotkey = new HotkeyBinding { Key = Keys.S, Control = true, Alt = false, Shift = true };
            UpdateMode();
            RefreshHotkeyText();
        }

        private void UpdateMode()
        {
            mode = rbInterval.Checked ? CaptureMode.Interval
                : rbMouseClick.Checked ? CaptureMode.MouseClick
                : CaptureMode.Keyboard;
            tbSeconds.Enabled = mode == CaptureMode.Interval;
            txtHotkey.Enabled = mode == CaptureMode.Keyboard;
            btnClearHotkey.Enabled = mode == CaptureMode.Keyboard;
        }

        private void LayoutTargetControls()
        {
            rbTargetAll.Location = new Point(10, 25);
            rbTargetScreen.Location = new Point(120, 25);
            cbScreens.Left = 25; cbScreens.Top = 55;
            rbTargetWindow.Location = new Point(10, 85);
            cbWindows.Left = 120; cbWindows.Top = 83;
            btnRefreshWindows.Left = 320; btnRefreshWindows.Top = 83;
        }

        private void UpdateTargetUI()
        {
            targetType = rbTargetAll.Checked ? CaptureTargetType.AllScreens
                : rbTargetScreen.Checked ? CaptureTargetType.SingleScreen
                : CaptureTargetType.Window;

            cbScreens.Enabled = rbTargetScreen.Checked;
            cbWindows.Enabled = rbTargetWindow.Checked;
            btnRefreshWindows.Enabled = rbTargetWindow.Checked;
        }

        private void StartCaptureFromUI()
        {
            if (string.IsNullOrWhiteSpace(baseFolder))
            {
                MessageBox.Show("Please choose a destination folder first.", "Folder required",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (mode == CaptureMode.Keyboard && hotkey == null)
            {
                MessageBox.Show("Set a hotkey for keystroke capture first.", "Hotkey required",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (targetType == CaptureTargetType.SingleScreen && cbScreens.SelectedIndex < 0)
            {
                MessageBox.Show("Select which screen to capture.", "Screen required",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (targetType == CaptureTargetType.Window && cbWindows.SelectedItem is not WindowItem)
            {
                MessageBox.Show("Select a window to capture.", "Window required",
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
                UnhookKeyboard();
            }
            else
            {
                intervalTimer.Stop();
                UnhookMouse();
                UnhookKeyboard();
                if (mode == CaptureMode.MouseClick) HookMouse();
                else if (mode == CaptureMode.Keyboard) HookKeyboard();
            }

            tray.BalloonTipTitle = "Screen Capture Running";
            tray.BalloonTipText = mode == CaptureMode.Interval
                ? $"Capturing every {tbSeconds.Value} second(s)."
                : mode == CaptureMode.MouseClick ? "Capturing on mouse clicks."
                : "Capturing on configured keystrokes.";
            tray.ShowBalloonTip(2000);
        }

        private void StopCapture()
        {
            intervalTimer.Stop();
            UnhookMouse();
            UnhookKeyboard();
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
                mouseHook.MouseDown += (btn) => CaptureTargets();
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

        private void HookKeyboard()
        {
            if (keyboardHook == null)
            {
                keyboardHook = new LowLevelKeyboardHook();
                keyboardHook.KeyPressed += OnGlobalKeyPressed;
                keyboardHook.Install();
            }
        }

        private void UnhookKeyboard()
        {
            if (keyboardHook != null)
            {
                keyboardHook.Uninstall();
                keyboardHook.KeyPressed -= OnGlobalKeyPressed;
                keyboardHook = null;
            }
        }

        private void OnGlobalKeyPressed(Keys key, bool ctrl, bool alt, bool shift)
        {
            if (hotkey != null && hotkey.Matches(key, ctrl, alt, shift))
            {
                CaptureTargets();
            }
        }

        private void CaptureTargets()
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss.fff");
                switch (targetType)
                {
                    case CaptureTargetType.AllScreens:
                        CaptureAllScreens(timestamp);
                        break;
                    case CaptureTargetType.SingleScreen:
                        CaptureSelectedScreen(timestamp);
                        break;
                    case CaptureTargetType.Window:
                        CaptureSelectedWindow(timestamp);
                        break;
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

        private void CaptureAllScreens(string timestamp)
        {
            var screens = Screen.AllScreens;
            for (int i = 0; i < screens.Length; i++)
            {
                CaptureScreen(screens[i], i, timestamp);
            }
        }

        private void CaptureSelectedScreen(string timestamp)
        {
            if (cbScreens.SelectedIndex < 0) return;
            var selected = cbScreens.SelectedItem as ScreenItem;
            if (selected == null) return;
            CaptureScreen(selected.Screen, selected.Index, timestamp);
        }

        private void CaptureScreen(Screen scr, int index, string timestamp)
        {
            var bounds = scr.Bounds;
            using var bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
            }

            string folder = System.IO.Path.Combine(baseFolder, $"Display_{index}");
            if (!System.IO.Directory.Exists(folder)) System.IO.Directory.CreateDirectory(folder);

            string file = System.IO.Path.Combine(folder, $"{index}-{timestamp}.png");
            bmp.Save(file, ImageFormat.Png);
        }

        private void CaptureSelectedWindow(string timestamp)
        {
            if (cbWindows.SelectedItem is not WindowItem win) return;

            if (!IsWindow(win.Handle) || !IsWindowVisible(win.Handle))
            {
                tray.BalloonTipTitle = "Capture Error";
                tray.BalloonTipText = "Selected window is no longer available.";
                tray.ShowBalloonTip(2000);
                return;
            }

            string folderName = $"Window_{SanitizeName(win.Title)}";
            var rect = new RECT();
            if (!GetWindowRect(win.Handle, out rect))
            {
                tray.BalloonTipTitle = "Capture Error";
                tray.BalloonTipText = "Unable to read window bounds.";
                tray.ShowBalloonTip(2000);
                return;
            }

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;
            if (width <= 0 || height <= 0) return;

            using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            bool captured = false;
            try
            {
                using var g = Graphics.FromImage(bmp);
                IntPtr hdc = g.GetHdc();
                try
                {
                    captured = PrintWindow(win.Handle, hdc, 0x00000002);
                }
                finally
                {
                    g.ReleaseHdc(hdc);
                }

                if (!captured)
                {
                    g.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
                    captured = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                captured = false;
            }

            if (!captured)
            {
                tray.BalloonTipTitle = "Capture Error";
                tray.BalloonTipText = "Failed to capture window.";
                tray.ShowBalloonTip(2000);
                return;
            }

            string folder = System.IO.Path.Combine(baseFolder, folderName);
            if (!System.IO.Directory.Exists(folder)) System.IO.Directory.CreateDirectory(folder);
            string file = System.IO.Path.Combine(folder, $"{timestamp}.png");
            bmp.Save(file, ImageFormat.Png);
        }

        private void RefreshScreens()
        {
            cbScreens.Items.Clear();
            var screens = Screen.AllScreens;
            for (int i = 0; i < screens.Length; i++)
            {
                cbScreens.Items.Add(new ScreenItem(i, screens[i]));
            }
            if (cbScreens.Items.Count > 0)
            {
                cbScreens.SelectedIndex = 0;
            }
        }

        private void RefreshWindows()
        {
            cbWindows.Items.Clear();
            foreach (var win in EnumerateWindows())
            {
                cbWindows.Items.Add(win);
            }
            if (cbWindows.Items.Count > 0)
            {
                cbWindows.SelectedIndex = 0;
            }
        }

        private IEnumerable<WindowItem> EnumerateWindows()
        {
            var list = new List<WindowItem>();
            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd)) return true;
                var title = GetWindowTitle(hWnd);
                if (string.IsNullOrWhiteSpace(title)) return true;
                list.Add(new WindowItem(title, hWnd));
                return true;
            }, IntPtr.Zero);
            return list;
        }

        private static string GetWindowTitle(IntPtr hWnd)
        {
            int length = GetWindowTextLength(hWnd);
            if (length == 0) return string.Empty;
            var sb = new StringBuilder(length + 1);
            _ = NativeWindowInterop.GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        private void Hotkey_KeyDown(object? sender, KeyEventArgs e)
        {
            e.SuppressKeyPress = true;
            if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.Menu)
            {
                return;
            }

            hotkey = new HotkeyBinding
            {
                Key = e.KeyCode,
                Control = e.Control,
                Alt = e.Alt,
                Shift = e.Shift
            };
            RefreshHotkeyText();
        }

        private void RefreshHotkeyText()
        {
            txtHotkey.Text = hotkey?.ToString() ?? "No hotkey";
        }

        private static string SanitizeName(string input)
        {
            foreach (var ch in System.IO.Path.GetInvalidFileNameChars())
            {
                input = input.Replace(ch, '_');
            }
            return string.IsNullOrWhiteSpace(input) ? "Window" : input;
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

    // Low-level keyboard hook for global hotkey capture without swallowing keystrokes.
    public class LowLevelKeyboardHook
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private IntPtr _hookId = IntPtr.Zero;
        private HookProc? _proc;

        public event Action<Keys, bool, bool, bool>? KeyPressed;

        public void Install()
        {
            _proc = HookCallback;
            using (Process cur = Process.GetCurrentProcess())
            using (ProcessModule mod = cur.MainModule!)
            {
                _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc!, GetModuleHandle(mod.ModuleName!), 0);
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
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                var key = (Keys)vkCode;
                bool ctrl = (GetKeyState(Keys.ControlKey) & 0x8000) != 0;
                bool alt = (GetKeyState(Keys.Menu) & 0x8000) != 0;
                bool shift = (GetKeyState(Keys.ShiftKey) & 0x8000) != 0;
                KeyPressed?.Invoke(key, ctrl, alt, shift);
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

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern short GetKeyState(Keys nVirtKey);
    }

    public class HotkeyBinding
    {
        public Keys Key { get; set; }
        public bool Control { get; set; }
        public bool Alt { get; set; }
        public bool Shift { get; set; }

        public bool Matches(Keys key, bool ctrl, bool alt, bool shift)
        {
            return key == Key && ctrl == Control && alt == Alt && shift == Shift;
        }

        public override string ToString()
        {
            var parts = new List<string>();
            if (Control) parts.Add("Ctrl");
            if (Alt) parts.Add("Alt");
            if (Shift) parts.Add("Shift");
            parts.Add(Key.ToString());
            return string.Join("+", parts);
        }
    }

    public record ScreenItem(int Index, Screen Screen)
    {
        public override string ToString() => $"Display {Index} ({Screen.Bounds.Width}x{Screen.Bounds.Height})";
    }

    public record WindowItem(string Title, IntPtr Handle)
    {
        public override string ToString() => Title;
    }

    internal static class NativeWindowInterop
    {
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, int nFlags);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
