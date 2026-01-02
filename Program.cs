using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
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
        private readonly GroupBox grpMode = new GroupBox { Text = "Trigger", Width = 430, Height = 200 };
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
        private readonly Button btnAddHotkey = new Button { Text = "Add", Width = 50 };
        private readonly Button btnRemoveHotkey = new Button { Text = "Remove", Width = 70 };
        private readonly Button btnClearHotkey = new Button { Text = "Clear All", Width = 70 };
        private readonly ListBox lstHotkeys = new ListBox { Height = 70, Width = 360 };

        private readonly Label lblSeconds = new Label { Text = "Seconds:", Left = 10, Top = 55, AutoSize = true };
        private readonly TrackBar tbSeconds = new TrackBar { Minimum = 1, Maximum = 120, Value = 5, TickFrequency = 5, Left = 75, Top = 50, Width = 320 };
        private readonly Label lblSecVal = new Label { Text = "5s", Left = 400, Top = 55, AutoSize = true };
        private readonly Button btnStart = new Button { Text = "Start", Width = 100, Height = 36 };

        private readonly GroupBox grpTarget = new GroupBox { Text = "Capture Target", Width = 430, Height = 170 };
        private readonly RadioButton rbTargetAll = new RadioButton { Text = "All screens", Checked = true, AutoSize = true };
        private readonly RadioButton rbTargetScreen = new RadioButton { Text = "Specific screen", AutoSize = true };
        private readonly RadioButton rbTargetWindow = new RadioButton { Text = "Window", AutoSize = true };
        private readonly ComboBox cbScreens = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false, Width = 280 };
        private readonly ComboBox cbWindows = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false, Width = 280, DrawMode = DrawMode.OwnerDrawFixed };
        private readonly Button btnRefreshWindows = new Button { Text = "Refresh", Enabled = false, Width = 70 };
        private readonly Button btnPickWindow = new Button { Text = "Pick...", Enabled = false, Width = 60 };

        private readonly NotifyIcon tray = new NotifyIcon();
        private readonly ContextMenuStrip trayMenu = new ContextMenuStrip();
        private readonly ToolStripMenuItem trayShow = new ToolStripMenuItem("Show");
        private readonly ToolStripMenuItem trayStart = new ToolStripMenuItem("Start");
        private readonly ToolStripMenuItem trayStop = new ToolStripMenuItem("Stop");
        private readonly ToolStripMenuItem trayExit = new ToolStripMenuItem("Exit");

        private readonly System.Windows.Forms.Timer intervalTimer = new System.Windows.Forms.Timer();
        private LowLevelMouseHook? mouseHook;
        private LowLevelKeyboardHook? keyboardHook;
        private LowLevelMouseHook? pickHook;

        private string baseFolder = "";
        private CaptureMode mode = CaptureMode.Interval;
        private CaptureTargetType targetType = CaptureTargetType.AllScreens;
        private HotkeyBinding? pendingHotkey;
        private readonly List<HotkeyBinding> hotkeys = new();
        private bool isPickingWindow;
        private int windowSkipCount;
        private DateTime lastWindowCaptureSuccess = DateTime.UtcNow;
        private IntPtr lastHoverHandle = IntPtr.Zero;
        private Rectangle lastHoverRect = Rectangle.Empty;

        public MainForm()
        {
            Text = "Screen Capture (Transparent & Visible)";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Width = 480;
            Height = 540;

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
            grpMode.Controls.Add(btnAddHotkey);
            grpMode.Controls.Add(btnRemoveHotkey);
            grpMode.Controls.Add(btnClearHotkey);
            grpMode.Controls.Add(lstHotkeys);

            grpTarget.Left = 15; grpTarget.Top = 190;
            grpTarget.Controls.Add(rbTargetAll);
            grpTarget.Controls.Add(rbTargetScreen);
            grpTarget.Controls.Add(rbTargetWindow);
            grpTarget.Controls.Add(cbScreens);
            grpTarget.Controls.Add(cbWindows);
            grpTarget.Controls.Add(btnRefreshWindows);
            grpTarget.Controls.Add(btnPickWindow);

            btnStart.Left = 365; btnStart.Top = 430;

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
            lblHotkey.Left = 10; lblHotkey.Top = 60;
            txtHotkey.Left = 65; txtHotkey.Top = 55;
            btnAddHotkey.Left = 275; btnAddHotkey.Top = 55;
            btnRemoveHotkey.Left = 330; btnRemoveHotkey.Top = 55;
            btnClearHotkey.Left = 330; btnClearHotkey.Top = 85;
            lstHotkeys.Left = 65; lstHotkeys.Top = 90;
            txtHotkey.KeyDown += Hotkey_KeyDown;
            txtHotkey.GotFocus += (s, e) => txtHotkey.Text = "Press keys...";
            txtHotkey.LostFocus += (s, e) => RefreshHotkeyText();
            btnAddHotkey.Click += (s, e) => AddPendingHotkey();
            btnRemoveHotkey.Click += (s, e) => RemoveSelectedHotkey();
            btnClearHotkey.Click += (s, e) => { hotkeys.Clear(); pendingHotkey = null; RefreshHotkeyText(); RefreshHotkeyList(); };
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
            btnPickWindow.Click += (s, e) => BeginWindowPick();
            cbScreens.SelectedIndexChanged += (s, e) => UpdateTargetUI();
            cbWindows.SelectedIndexChanged += (s, e) => { UpdateTargetUI(); ClearHoverHighlight(); };
            cbScreens.DropDown += (s, e) => RefreshScreens();
            cbWindows.DropDown += (s, e) => RefreshWindows();
            cbWindows.DrawItem += CbWindows_DrawItem;
            cbWindows.MouseMove += CbWindows_MouseMove;
            cbWindows.MouseLeave += (s, e) => ClearHoverHighlight();
            cbWindows.DropDownClosed += (s, e) => ClearHoverHighlight();
            cbWindows.DropDownWidth = 350;

            LayoutTargetControls();
            RefreshScreens();
            RefreshWindows();

            // Initial UI state
            hotkeys.Add(new HotkeyBinding { Key = Keys.S, Control = true, Alt = false, Shift = true });
            UpdateMode();
            RefreshHotkeyText();
            RefreshHotkeyList();
        }

        private void UpdateMode()
        {
            mode = rbInterval.Checked ? CaptureMode.Interval
                : rbMouseClick.Checked ? CaptureMode.MouseClick
                : CaptureMode.Keyboard;
            bool interval = mode == CaptureMode.Interval;
            bool keyboard = mode == CaptureMode.Keyboard;

            lblSeconds.Visible = interval;
            tbSeconds.Visible = interval;
            lblSecVal.Visible = interval;
            tbSeconds.Enabled = interval;

            lblHotkey.Visible = keyboard;
            txtHotkey.Visible = keyboard;
            btnAddHotkey.Visible = keyboard;
            btnRemoveHotkey.Visible = keyboard;
            btnClearHotkey.Visible = keyboard;
            lstHotkeys.Visible = keyboard;
            txtHotkey.Enabled = keyboard;
            btnAddHotkey.Enabled = keyboard;
            btnRemoveHotkey.Enabled = keyboard;
            btnClearHotkey.Enabled = keyboard;
        }

        private void LayoutTargetControls()
        {
            rbTargetAll.Location = new Point(10, 25);
            rbTargetScreen.Location = new Point(120, 25);
            rbTargetWindow.Location = new Point(260, 25);

            cbScreens.Left = 25; cbScreens.Top = 60;

            cbWindows.Left = 25; cbWindows.Top = 60;
            btnPickWindow.Left = 315; btnPickWindow.Top = 60;
            btnRefreshWindows.Left = 315; btnRefreshWindows.Top = 90;
        }

        private void UpdateTargetUI()
        {
            targetType = rbTargetAll.Checked ? CaptureTargetType.AllScreens
                : rbTargetScreen.Checked ? CaptureTargetType.SingleScreen
                : CaptureTargetType.Window;

            cbScreens.Enabled = rbTargetScreen.Checked;
            cbWindows.Enabled = rbTargetWindow.Checked;
            btnRefreshWindows.Enabled = rbTargetWindow.Checked;
            btnPickWindow.Enabled = rbTargetWindow.Checked;

            cbScreens.Visible = rbTargetScreen.Checked;
            cbWindows.Visible = rbTargetWindow.Checked;
            btnRefreshWindows.Visible = rbTargetWindow.Checked;
            btnPickWindow.Visible = rbTargetWindow.Checked;

            if (!rbTargetWindow.Checked)
            {
                ClearHoverHighlight();
            }
        }

        private void StartCaptureFromUI()
        {
            if (string.IsNullOrWhiteSpace(baseFolder))
            {
                MessageBox.Show("Please choose a destination folder first.", "Folder required",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (targetType == CaptureTargetType.SingleScreen)
            {
                RefreshScreens();
            }

            if (targetType == CaptureTargetType.Window)
            {
                RefreshWindows();
            }

            if (mode == CaptureMode.Keyboard && hotkeys.Count == 0)
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
            EnsureValidScreenSelection(fallback: true);

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

            lastWindowCaptureSuccess = DateTime.UtcNow;
            windowSkipCount = 0;
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
            if (hotkeys.Any(h => h.Matches(key, ctrl, alt, shift)))
            {
                CaptureTargets();
            }
        }

        private void CaptureTargets()
        {
            if (isPickingWindow) return;
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
            if (!EnsureValidScreenSelection(fallback: false))
            {
                StopCapture();
                tray.BalloonTipTitle = "Capture stopped";
                tray.BalloonTipText = "Selected screen is unavailable.";
                tray.ShowBalloonTip(2000);
                return;
            }

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

            if (IsIconic(win.Handle))
            {
                windowSkipCount++;
                var elapsed = DateTime.UtcNow - lastWindowCaptureSuccess;
                if (windowSkipCount >= 3 && elapsed > TimeSpan.FromMinutes(5))
                {
                    StopCapture();
                    tray.BalloonTipTitle = "Capture stopped";
                    tray.BalloonTipText = "Window minimized for 5+ minutes. Stopping capture.";
                    tray.ShowBalloonTip(2000);
                    Application.Exit();
                }
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
            lastWindowCaptureSuccess = DateTime.UtcNow;
            windowSkipCount = 0;
        }

        private void RefreshScreens()
        {
            int currentIndex = (cbScreens.SelectedItem as ScreenItem)?.Index ?? -1;
            cbScreens.Items.Clear();
            var screens = Screen.AllScreens;
            for (int i = 0; i < screens.Length; i++)
            {
                cbScreens.Items.Add(new ScreenItem(i, screens[i]));
            }
            if (currentIndex >= 0 && currentIndex < screens.Length)
            {
                cbScreens.SelectedIndex = currentIndex;
            }
            else
            {
                EnsureValidScreenSelection(fallback: true);
            }
        }

        private void RefreshWindows()
        {
            ClearHoverHighlight();
            IntPtr currentHandle = (cbWindows.SelectedItem as WindowItem)?.Handle ?? IntPtr.Zero;
            cbWindows.Items.Clear();
            foreach (var win in EnumerateWindows())
            {
                cbWindows.Items.Add(win);
            }
            if (cbWindows.Items.Count > 0)
            {
                if (currentHandle != IntPtr.Zero)
                {
                    for (int i = 0; i < cbWindows.Items.Count; i++)
                    {
                        if (cbWindows.Items[i] is WindowItem wi && wi.Handle == currentHandle)
                        {
                            cbWindows.SelectedIndex = i;
                            return;
                        }
                    }
                }
                cbWindows.SelectedIndex = 0;
            }
        }

        private IEnumerable<WindowItem> EnumerateWindows()
        {
            var list = new List<WindowItem>();
            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd)) return true;
                if (IsToolWindow(hWnd)) return true;
                var title = GetWindowTitle(hWnd);
                if (string.IsNullOrWhiteSpace(title)) return true;
                var processName = GetProcessNameForWindow(hWnd);
                if (IsIrrelevantWindow(title, processName)) return true;
                list.Add(new WindowItem(title, processName, hWnd));
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

            pendingHotkey = new HotkeyBinding
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
            txtHotkey.Text = pendingHotkey?.ToString() ?? "No hotkey";
        }

        private static string SanitizeName(string input)
        {
            foreach (var ch in System.IO.Path.GetInvalidFileNameChars())
            {
                input = input.Replace(ch, '_');
            }
            return string.IsNullOrWhiteSpace(input) ? "Window" : input;
        }

        private void AddPendingHotkey()
        {
            if (pendingHotkey == null) return;
            if (hotkeys.Any(h => h.Equals(pendingHotkey))) return;
            hotkeys.Add(pendingHotkey);
            pendingHotkey = null;
            RefreshHotkeyText();
            RefreshHotkeyList();
        }

        private void RemoveSelectedHotkey()
        {
            if (lstHotkeys.SelectedItem is HotkeyBinding hb)
            {
                hotkeys.RemoveAll(h => h.Equals(hb));
                RefreshHotkeyList();
            }
        }

        private void RefreshHotkeyList()
        {
            lstHotkeys.Items.Clear();
            foreach (var hk in hotkeys)
            {
                lstHotkeys.Items.Add(hk);
            }
        }

        private bool EnsureValidScreenSelection(bool fallback)
        {
            var screens = Screen.AllScreens;
            if (cbScreens.SelectedItem is ScreenItem item)
            {
                if (item.Index < screens.Length) return true;
            }

            if (screens.Length == 0) return false;

            if (fallback)
            {
                cbScreens.SelectedIndex = 0;
                return true;
            }

            return false;
        }

        private void BeginWindowPick()
        {
            if (isPickingWindow) return;
            isPickingWindow = true;
            ClearHoverHighlight();
            Hide();
            Cursor = Cursors.Cross;
            pickHook = new LowLevelMouseHook();
            pickHook.MouseDown += OnPickMouseDown;
            pickHook.Install();
        }

        private void OnPickMouseDown(MouseButtons button)
        {
            if (!isPickingWindow) return;
            isPickingWindow = false;
            Cursor = Cursors.Default;
            pickHook?.Uninstall();
            pickHook = null;
            Show();
            ShowInTaskbar = true;
            Activate();

            if (!GetCursorPos(out POINT pt)) return;
            IntPtr hWnd = WindowFromPoint(pt);
            hWnd = GetAncestor(hWnd, GA_ROOT);
            if (hWnd == IntPtr.Zero || !IsWindow(hWnd) || !IsWindowVisible(hWnd)) return;
            if (hWnd == this.Handle) return;

            var title = GetWindowTitle(hWnd);
            var processName = GetProcessNameForWindow(hWnd);
            if (IsIrrelevantWindow(title, processName)) return;

            var item = new WindowItem(title, processName, hWnd);
            // Add or select in list
            for (int i = 0; i < cbWindows.Items.Count; i++)
            {
                if (cbWindows.Items[i] is WindowItem wi && wi.Handle == hWnd)
                {
                    cbWindows.SelectedIndex = i;
                    return;
                }
            }
            cbWindows.Items.Add(item);
            cbWindows.SelectedItem = item;
        }

        private static bool IsIrrelevantWindow(string title, string processName)
        {
            if (string.IsNullOrWhiteSpace(title)) return true;

            string t = title.Trim();
            if (t.StartsWith("#") && int.TryParse(t.TrimStart('#'), out _)) return true;

            string[] ignoreTitles = { "Settings", "MainWindow", "Windows Input Experience" };
            foreach (var it in ignoreTitles)
            {
                if (string.Equals(t, it, StringComparison.OrdinalIgnoreCase)) return true;
            }

            string[] ignoreProcesses = { "ApplicationFrameHost", "ShellExperienceHost", "TextInputHost", "SystemSettings" };
            foreach (var p in ignoreProcesses)
            {
                if (string.Equals(processName, p, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        private static bool IsToolWindow(IntPtr hWnd)
        {
            int style = GetWindowLong(hWnd, GWL_EXSTYLE);
            return (style & WS_EX_TOOLWINDOW) == WS_EX_TOOLWINDOW;
        }

        private static string GetProcessNameForWindow(IntPtr hWnd)
        {
            _ = GetWindowThreadProcessId(hWnd, out uint pid);
            try
            {
                return Process.GetProcessById((int)pid).ProcessName;
            }
            catch
            {
                return string.Empty;
            }
        }

        private void CbWindows_DrawItem(object? sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index < 0) return;
            if (cbWindows.Items[e.Index] is WindowItem item)
            {
                string text = item.ToString();
                TextRenderer.DrawText(e.Graphics, text, e.Font ?? Font, e.Bounds, e.ForeColor);
            }
            e.DrawFocusRectangle();
        }

        private void CbWindows_MouseMove(object? sender, MouseEventArgs e)
        {
            int index = GetDropdownIndexUnderCursor();
            if (index >= 0 && index < cbWindows.Items.Count && cbWindows.Items[index] is WindowItem item)
            {
                ShowHoverHighlight(item.Handle);
            }
            else
            {
                ClearHoverHighlight();
            }
        }

        private void ShowHoverHighlight(IntPtr handle)
        {
            if (handle == IntPtr.Zero || handle == lastHoverHandle) return;
            ClearHoverHighlight();
            if (!IsWindow(handle) || !GetWindowRect(handle, out RECT rect)) return;
            var bounds = new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
            ControlPaint.DrawReversibleFrame(bounds, Color.Yellow, FrameStyle.Dashed);
            lastHoverHandle = handle;
            lastHoverRect = bounds;
        }

        private void ClearHoverHighlight()
        {
            if (lastHoverHandle != IntPtr.Zero && lastHoverRect != Rectangle.Empty)
            {
                ControlPaint.DrawReversibleFrame(lastHoverRect, Color.Yellow, FrameStyle.Dashed);
            }
            lastHoverHandle = IntPtr.Zero;
            lastHoverRect = Rectangle.Empty;
        }

        private int GetDropdownIndexUnderCursor()
        {
            if (!cbWindows.DroppedDown) return -1;
            var info = new COMBOBOXINFO { cbSize = Marshal.SizeOf<COMBOBOXINFO>() };
            if (!GetComboBoxInfo(cbWindows.Handle, out info)) return -1;
            if (info.hwndList == IntPtr.Zero) return -1;

            if (!GetCursorPos(out POINT pt)) return -1;
            POINT clientPt = pt;
            if (!ScreenToClient(info.hwndList, ref clientPt)) return -1;

            int lParam = (clientPt.Y << 16) | (clientPt.X & 0xFFFF);
            int result = (int)SendMessage(info.hwndList, LB_ITEMFROMPOINT, IntPtr.Zero, new IntPtr(lParam));
            int index = result & 0xFFFF;
            int outside = (result >> 16) & 0xFFFF;
            return outside != 0 ? -1 : index;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Ensure hooks/timers are off and tray is hidden when exiting from UI
            StopCapture();
            ClearHoverHighlight();
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

    public record HotkeyBinding
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

    public record WindowItem(string Title, string ProcessName, IntPtr Handle)
    {
        public override string ToString()
        {
            if (string.IsNullOrWhiteSpace(ProcessName)) return Title;
            return $"{ProcessName} — {Title}";
        }
    }

    internal static class NativeWindowInterop
    {
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const uint GA_ROOT = 2;
        public const int LB_ITEMFROMPOINT = 0x01A9;

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

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern IntPtr WindowFromPoint(POINT Point);

        [DllImport("user32.dll")]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetComboBoxInfo(IntPtr hwndCombo, out COMBOBOXINFO info);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct COMBOBOXINFO
    {
        public int cbSize;
        public RECT rcItem;
        public RECT rcButton;
        public int stateButton;
        public IntPtr hwndCombo;
        public IntPtr hwndItem;
        public IntPtr hwndList;
    }
}
