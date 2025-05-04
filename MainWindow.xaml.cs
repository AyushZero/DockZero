using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace DockZero;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private Storyboard expandAnimation;
    private Storyboard collapseAnimation;
    public required DispatcherTimer timer;

    public MainWindow()
    {
        InitializeComponent();
        expandAnimation = (Storyboard)FindResource("ExpandAnimation");
        collapseAnimation = (Storyboard)FindResource("CollapseAnimation");
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
        // TODO: Implement previous track functionality
    }

    private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Implement play/pause functionality
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Implement next track functionality
    }
}
