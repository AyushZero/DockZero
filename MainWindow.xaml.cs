using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Media;
using System.Text;
using System.Diagnostics;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;
using System.ComponentModel;

namespace DockZero;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private Storyboard expandAnimation;
    private Storyboard collapseAnimation;
    public required DispatcherTimer timer;
    private bool isPlaying = false;
    private System.Windows.Shapes.Path? playPauseIcon;
    private DispatcherTimer pomodoroTimer;
    private TimeSpan remainingTime = TimeSpan.FromMinutes(52);
    private bool isTimerRunning = false;
    private bool isLongTimer = true; // true for 52 minutes, false for 17 minutes
    private NotifyIcon? trayIcon;
    private bool isMinimizedToTray = false;
    private List<string> notes = new List<string>();
    private const string NOTES_FILE = "notes.txt";

    // Win32 API imports for window styles
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    private const byte VK_MEDIA_PLAY_PAUSE = 0xB3;
    private const byte VK_MEDIA_NEXT_TRACK = 0xB0;
    private const byte VK_MEDIA_PREV_TRACK = 0xB1;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    // SVG path data for play and pause icons
    private const string PLAY_PATH = "M8 5v14l11-7z";
    private const string PAUSE_PATH = "M6 19h4V5H6v14zm8-14v14h4V5h-4z";

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    internal class MMDeviceEnumerator
    {
    }

    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioEndpointVolume
    {
        int NotImpl1();
        int NotImpl2();
        int GetMasterVolumeLevelScalar(out float level);
        int SetMasterVolumeLevelScalar(float level, Guid guid);
        int NotImpl3();
        int NotImpl4();
        int GetMute(out bool mute);
        int SetMute(bool mute, Guid guid);
    }

    [ComImport]
    [Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
    internal class MediaSessionManager
    {
    }

    [Guid("F8672877-5A68-4B52-8F6C-0F4A6D8F0D4E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMediaSessionManager
    {
        int GetSessionEnumerator(out IMediaSessionEnumerator sessionEnumerator);
    }

    [Guid("E2F5BB1E-9527-4D5A-9E5A-3F0E4D7B5B8A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMediaSessionEnumerator
    {
        int GetCount(out int count);
        int GetSession(int index, out IMediaSession session);
    }

    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMediaSession
    {
        int GetDisplayName(out string name);
        int GetArtist(out string artist);
        int GetTitle(out string title);
    }

    public MainWindow()
    {
        InitializeComponent();
        expandAnimation = (Storyboard)FindResource("ExpandAnimation");
        collapseAnimation = (Storyboard)FindResource("CollapseAnimation");
        
        // Get reference to the play/pause icon
        if (PlayPauseButton.Content is System.Windows.Shapes.Path icon)
        {
            playPauseIcon = icon;
        }

        // Initialize pomodoro timer
        pomodoroTimer = new DispatcherTimer();
        pomodoroTimer.Interval = TimeSpan.FromSeconds(1);
        pomodoroTimer.Tick += PomodoroTimer_Tick;

        // Initialize system tray icon
        InitializeTrayIcon();

        // Load saved notes
        LoadNotes();

        Loaded += (s, e) =>
        {
            // Set WS_EX_TOOLWINDOW extended window style
            var helper = new WindowInteropHelper(this);
            var exStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE);
            SetWindowLong(helper.Handle, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);
        };
    }

    private void InitializeTrayIcon()
    {
        trayIcon = new NotifyIcon();
        trayIcon.Icon = new Icon("icons/icon256.ico");
        trayIcon.Text = "DockZero";
        trayIcon.Visible = true;

        // Create context menu
        var contextMenu = new System.Windows.Forms.ContextMenuStrip();
        var showItem = new System.Windows.Forms.ToolStripMenuItem("Show");
        var exitItem = new System.Windows.Forms.ToolStripMenuItem("Exit");

        showItem.Click += (s, e) => ShowWindow();
        exitItem.Click += (s, e) => ExitApplication();

        contextMenu.Items.Add(showItem);
        contextMenu.Items.Add(exitItem);

        trayIcon.ContextMenuStrip = contextMenu;
        trayIcon.DoubleClick += (s, e) => ShowWindow();
    }

    private void ShowWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        isMinimizedToTray = false;
    }

    private void ExitApplication()
    {
        trayIcon?.Dispose();
        System.Windows.Application.Current.Shutdown();
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
            isMinimizedToTray = true;
        }
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (!isMinimizedToTray)
        {
            e.Cancel = true;
            Hide();
            isMinimizedToTray = true;
        }
        else
        {
            trayIcon?.Dispose();
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Position window at the top of the screen, centered horizontally
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            
            // Center horizontally
            Left = (screenWidth - Width) / 2;
            // Position at the very top
            Top = 0;

            // Initialize timer after window is loaded
            InitializeTimer();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error during initialization: {ex}");
        }
    }

    private void InitializeTimer()
    {
        try
        {
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;
            timer.Start();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error initializing timer: {ex}");
        }
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        try
        {
            if (TimeText != null && DateText != null)
            {
                var now = DateTime.Now;
                TimeText.Text = now.ToString("HH:mm");
                DateText.Text = GetFormattedDate(now);
            }
        }
        catch (Exception ex)
        {
            timer.Stop();
            System.Diagnostics.Debug.WriteLine($"Error updating time: {ex}");
        }
    }

    private static string GetFormattedDate(DateTime date)
    {
        int day = date.Day;
        string suffix = GetDaySuffix(day);
        string month = date.ToString("MMMM");
        string year = date.ToString("''yy");
        return $"{day}{suffix} {month} '{year}";
    }

    private static string GetDaySuffix(int day)
    {
        if (day >= 11 && day <= 13) return "th";
        switch (day % 10)
        {
            case 1: return "st";
            case 2: return "nd";
            case 3: return "rd";
            default: return "th";
        }
    }

    private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        try
        {
            expandAnimation.Begin();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error during expand animation: {ex}");
        }
    }

    private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        try
        {
            collapseAnimation.Begin();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error during collapse animation: {ex}");
        }
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            System.Windows.Application.Current.Shutdown();
        }
    }

    private void PreviousButton_Click(object sender, RoutedEventArgs e)
    {
        keybd_event(VK_MEDIA_PREV_TRACK, 0, KEYEVENTF_EXTENDEDKEY, 0);
        keybd_event(VK_MEDIA_PREV_TRACK, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, 0);
        
        // Reset play state when changing tracks
        isPlaying = false;
        if (playPauseIcon != null)
        {
            playPauseIcon.Data = Geometry.Parse(PLAY_PATH);
        }
    }

    private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
        keybd_event(VK_MEDIA_PLAY_PAUSE, 0, KEYEVENTF_EXTENDEDKEY, 0);
        keybd_event(VK_MEDIA_PLAY_PAUSE, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, 0);
        
        // Toggle play state and update icon
        isPlaying = !isPlaying;
        if (playPauseIcon != null)
        {
            playPauseIcon.Data = Geometry.Parse(isPlaying ? PLAY_PATH : PAUSE_PATH);
        }
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        keybd_event(VK_MEDIA_NEXT_TRACK, 0, KEYEVENTF_EXTENDEDKEY, 0);
        keybd_event(VK_MEDIA_NEXT_TRACK, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, 0);
        
        // Reset play state when changing tracks
        isPlaying = false;
        if (playPauseIcon != null)
        {
            playPauseIcon.Data = Geometry.Parse(PLAY_PATH);
        }
    }

    private void TimerBorder_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (isTimerRunning)
        {
            pomodoroTimer.Stop();
        }
        else
        {
            pomodoroTimer.Start();
        }
        isTimerRunning = !isTimerRunning;
    }

    private void SwitchTimerButton_Click(object sender, RoutedEventArgs e)
    {
        isLongTimer = !isLongTimer;
        remainingTime = TimeSpan.FromMinutes(isLongTimer ? 52 : 17);
        if (TimerText != null)
        {
            TimerText.Text = remainingTime.ToString(@"mm\:ss");
        }
        pomodoroTimer.Stop();
        isTimerRunning = false;
    }

    private void PomodoroTimer_Tick(object? sender, EventArgs e)
    {
        if (remainingTime > TimeSpan.Zero)
        {
            remainingTime = remainingTime.Subtract(TimeSpan.FromSeconds(1));
            if (TimerText != null)
            {
                TimerText.Text = remainingTime.ToString(@"mm\:ss");
            }
        }
        else
        {
            pomodoroTimer.Stop();
            isTimerRunning = false;
            remainingTime = TimeSpan.FromMinutes(isLongTimer ? 52 : 17);
            if (TimerText != null)
            {
                TimerText.Text = remainingTime.ToString(@"mm\:ss");
            }
        }
    }

    private void ResetTimerButton_Click(object sender, RoutedEventArgs e)
    {
        remainingTime = TimeSpan.FromMinutes(isLongTimer ? 52 : 17);
        if (TimerText != null)
        {
            TimerText.Text = remainingTime.ToString(@"mm\:ss");
        }
        pomodoroTimer.Stop();
        isTimerRunning = false;
    }

    private void NotesButton_Click(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("NotesButton_Click started");
        try
        {
            if (NotesViewPanel == null || NotesViewText == null)
            {
                System.Diagnostics.Debug.WriteLine("ERROR: Required components are null");
                return;
            }

            if (notes == null)
            {
                notes = new List<string>();
            }

            if (NotesViewPanel.Visibility == Visibility.Visible)
            {
                System.Diagnostics.Debug.WriteLine("Hiding notes panel");
                NotesViewPanel.Visibility = Visibility.Collapsed;
                NotesAreaBackground.Visibility = Visibility.Collapsed;
                Height = 18;
                return;
            }

            System.Diagnostics.Debug.WriteLine("Showing notes panel");
            try
            {
                NotesAreaBackground.Margin = new Thickness(0, 18, 0, 0);
                NotesAreaBackground.Visibility = Visibility.Visible;
                NotesViewPanel.Visibility = Visibility.Visible;
                Height = 238;

                if (notes.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Formatting {notes.Count} notes for display");
                    var formattedNotes = new List<string>();
                    for (int i = 0; i < notes.Count; i++)
                    {
                        if (notes[i] != null)
                        {
                            formattedNotes.Add($"{i + 1}. {notes[i]}");
                        }
                    }
                    NotesViewText.Text = string.Join(Environment.NewLine + Environment.NewLine, formattedNotes);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("No notes to display");
                    NotesViewText.Text = string.Empty;
                }

                NotesViewText.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error displaying notes: {ex}");
                NotesViewPanel.Visibility = Visibility.Collapsed;
                NotesAreaBackground.Visibility = Visibility.Collapsed;
                Height = 18;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CRITICAL ERROR in NotesButton_Click: {ex}");
            if (NotesViewPanel != null)
            {
                NotesViewPanel.Visibility = Visibility.Collapsed;
                NotesAreaBackground.Visibility = Visibility.Collapsed;
            }
            Height = 18;
        }
    }

    private void CloseNotesViewButton_Click(object sender, RoutedEventArgs e)
    {
        NotesViewPanel.Visibility = Visibility.Collapsed;
        NotesAreaBackground.Visibility = Visibility.Collapsed;
        Height = 18;
    }

    private void LoadNotes()
    {
        System.Diagnostics.Debug.WriteLine("LoadNotes started");
        try
        {
            // Initialize notes list if needed
            if (notes == null)
            {
                notes = new List<string>();
            }

            // Try to load notes from file
            if (File.Exists(NOTES_FILE))
            {
                try
                {
                    var loadedNotes = File.ReadAllLines(NOTES_FILE)
                        .Where(line => !string.IsNullOrWhiteSpace(line))
                        .ToList();
                    
                    notes.Clear();
                    notes.AddRange(loadedNotes);
                    System.Diagnostics.Debug.WriteLine($"Successfully loaded {notes.Count} notes from file");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error reading notes file: {ex}");
                    // Keep existing notes if any
                }
            }

            // Update view if visible
            if (NotesViewPanel != null && NotesViewPanel.Visibility == Visibility.Visible && NotesViewText != null)
            {
                try
                {
                    var formattedNotes = notes.Select((note, index) => $"{index + 1}. {note}").ToList();
                    NotesViewText.Text = string.Join(Environment.NewLine + Environment.NewLine, formattedNotes);
                    NotesViewText.Visibility = Visibility.Visible;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error updating notes view: {ex}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in LoadNotes: {ex}");
            // Ensure notes list exists
            if (notes == null)
            {
                notes = new List<string>();
            }
        }
    }

    private void SaveNotes()
    {
        System.Diagnostics.Debug.WriteLine("SaveNotes started");
        try
        {
            if (notes == null)
            {
                notes = new List<string>();
            }

            // Filter out any null or empty notes
            var validNotes = notes.Where(note => !string.IsNullOrWhiteSpace(note)).ToList();
            
            try
            {
                File.WriteAllLines(NOTES_FILE, validNotes);
                System.Diagnostics.Debug.WriteLine($"Saved {validNotes.Count} notes to file");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error writing to notes file: {ex}");
            }

            // Update view if visible
            if (NotesViewPanel != null && NotesViewPanel.Visibility == Visibility.Visible && NotesViewText != null)
            {
                try
                {
                    var formattedNotes = validNotes.Select((note, index) => $"{index + 1}. {note}").ToList();
                    NotesViewText.Text = string.Join(Environment.NewLine + Environment.NewLine, formattedNotes);
                    NotesViewText.Visibility = Visibility.Visible;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error updating notes view: {ex}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in SaveNotes: {ex}");
        }
    }

    private void ClearNotesButton_Click(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("ClearNotesButton_Click started");
        try
        {
            if (notes == null)
            {
                notes = new List<string>();
            }

            notes.Clear();
            SaveNotes();

            // Hide both panels and background
            NotesViewPanel.Visibility = Visibility.Collapsed;
            NotesPanel.Visibility = Visibility.Collapsed;
            NotesAreaBackground.Visibility = Visibility.Collapsed;
            Height = 18;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in ClearNotesButton_Click: {ex}");
            // Ensure all panels are hidden
            NotesViewPanel.Visibility = Visibility.Collapsed;
            NotesPanel.Visibility = Visibility.Collapsed;
            NotesAreaBackground.Visibility = Visibility.Collapsed;
            Height = 18;
        }
    }

    private void AddNoteButton_Click(object sender, RoutedEventArgs e)
    {
        NotesAreaBackground.Margin = new Thickness(0, 18, 0, 0);
        NotesAreaBackground.Visibility = Visibility.Visible;
        NotesPanel.Visibility = Visibility.Visible;
        NotesViewPanel.Visibility = Visibility.Collapsed;
        NotesTextBox.Clear();
        NotesTextBox.Focus();
        
        Height = 63;
    }

    private void NotesTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            string note = NotesTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(note))
            {
                notes.Add(note);
                SaveNotes();
            }
            NotesPanel.Visibility = Visibility.Collapsed;
            NotesAreaBackground.Visibility = Visibility.Collapsed;
            NotesTextBox.Clear();
            
            Height = 18;
        }
        else if (e.Key == System.Windows.Input.Key.Escape)
        {
            NotesPanel.Visibility = Visibility.Collapsed;
            NotesAreaBackground.Visibility = Visibility.Collapsed;
            NotesTextBox.Clear();
            
            Height = 18;
        }
    }

    protected override void OnMouseDown(System.Windows.Input.MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (NotesViewPanel.Visibility == Visibility.Visible)
        {
            var mousePosition = e.GetPosition(NotesViewPanel);
            if (mousePosition.X < 0 || mousePosition.X > NotesViewPanel.ActualWidth ||
                mousePosition.Y < 0 || mousePosition.Y > NotesViewPanel.ActualHeight)
            {
                NotesViewPanel.Visibility = Visibility.Collapsed;
                NotesAreaBackground.Visibility = Visibility.Collapsed;
                Height = 18;
            }
        }
    }

    private void ExplorerButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start("explorer.exe");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error launching Explorer: {ex}");
        }
    }

    private void OperaGXButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string operaPath = @"C:\Users\ayush\AppData\Local\Programs\Opera GX\opera.exe";
            if (File.Exists(operaPath))
            {
                System.Diagnostics.Process.Start(operaPath);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Opera GX not found at specified path");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error launching Opera GX: {ex}");
        }
    }

    private void WhatsAppButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start("explorer.exe", "shell:AppsFolder\\5319275A.WhatsAppDesktop_cv1g1gvanyjgm!App");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error launching WhatsApp: {ex}");
            // Fallback to web version if desktop app fails
            try
            {
                System.Diagnostics.Process.Start(new ProcessStartInfo
                {
                    FileName = "https://web.whatsapp.com",
                    UseShellExecute = true
                });
            }
            catch (Exception webEx)
            {
                System.Diagnostics.Debug.WriteLine($"Error launching WhatsApp Web: {webEx}");
            }
        }
    }

    private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        DragMove();
    }
}

