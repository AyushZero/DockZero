using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Media;
using System.Text;
using System.Runtime.InteropServices.ComTypes;
using System.Diagnostics;

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
    private Path? playPauseIcon;
    private DispatcherTimer pomodoroTimer;
    private TimeSpan remainingTime = TimeSpan.FromMinutes(52);
    private bool isTimerRunning = false;
    private bool isLongTimer = true; // true for 52 minutes, false for 17 minutes

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
        if (PlayPauseButton.Content is Path icon)
        {
            playPauseIcon = icon;
        }

        // Initialize pomodoro timer
        pomodoroTimer = new DispatcherTimer();
        pomodoroTimer.Interval = TimeSpan.FromSeconds(1);
        pomodoroTimer.Tick += PomodoroTimer_Tick;
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
            MessageBox.Show($"Error during initialization: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            MessageBox.Show($"Error initializing timer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            MessageBox.Show($"Error updating time: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            MessageBox.Show($"Error during expand animation: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            MessageBox.Show($"Error during collapse animation: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            Application.Current.Shutdown();
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
}
