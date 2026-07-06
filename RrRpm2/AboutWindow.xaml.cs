using System.Windows;
using System.Windows.Navigation;

namespace RrRpm2;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = $"Version {AppInfo.DisplayVersion}";
    }

    private void Hyperlink_RequestNavigate(
        object sender,
        RequestNavigateEventArgs e)
    {
        AppInfo.OpenUrl(e.Uri.AbsoluteUri);
        e.Handled = true;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
