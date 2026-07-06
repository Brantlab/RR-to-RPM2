using System.Windows;

namespace RrRpm2;

public partial class TelemetryPrivacyWindow : Window
{
    private readonly bool _isFirstRun;

    public TelemetryPrivacyWindow(bool isFirstRun, bool currentValue)
    {
        InitializeComponent();
        _isFirstRun = isFirstRun;
        SelectedEnabled = currentValue;

        if (!isFirstRun)
        {
            HeadingText.Text = "Privacy & Usage Statistics";
            EnabledCheckBox.Visibility = Visibility.Visible;
            EnabledCheckBox.IsChecked = currentValue;
            SecondaryButton.Content = "Cancel";
            PrimaryButton.Content = "Save";
            PrimaryButton.MinWidth = 100;
        }
    }

    public bool SelectedEnabled { get; private set; }

    private void PrimaryButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedEnabled = _isFirstRun || EnabledCheckBox.IsChecked == true;
        DialogResult = true;
    }

    private void SecondaryButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isFirstRun)
        {
            SelectedEnabled = false;
            DialogResult = true;
            return;
        }

        DialogResult = false;
    }
}
