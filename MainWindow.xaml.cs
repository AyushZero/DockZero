using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace DockZero;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private Storyboard expandAnimation;
    private Storyboard collapseAnimation;

    public MainWindow()
    {
        InitializeComponent();
        expandAnimation = (Storyboard)FindResource("ExpandAnimation");
        collapseAnimation = (Storyboard)FindResource("CollapseAnimation");
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Position window at the top of the screen, centered horizontally
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;
        
        // Center horizontally
        Left = (screenWidth - Width) / 2;
        // Position at the very top
        Top = 0;
    }

    private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        expandAnimation.Begin();
    }

    private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        collapseAnimation.Begin();
    }
}
