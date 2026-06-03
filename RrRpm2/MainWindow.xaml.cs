using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Data;
using Microsoft.Win32;
using RrRpm2.Models;
using RrRpm2.Services;

namespace RrRpm2;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<Talkgroup> _talkgroups = [];
    private readonly ObservableCollection<TrunkedSystem> _systems = [];
    private readonly ObservableCollection<TrunkedSite> _sites = [];
    private readonly ObservableCollection<RadioReferenceState> _states = [];
    private readonly ObservableCollection<RadioReferenceCounty> _counties = [];
    private readonly ICollectionView _talkgroupView;
    private readonly RadioReferenceClient _radioReferenceClient = new();
    private bool _statesLoaded;

    public MainWindow()
    {
        InitializeComponent();
        TalkgroupsGrid.ItemsSource = _talkgroups;
        SystemResultsBox.ItemsSource = _systems;
        SitesBox.ItemsSource = _sites;
        StateSearchBox.ItemsSource = _states;
        CountySearchBox.ItemsSource = _counties;
        LoadSavedCredentials();
        _talkgroupView = CollectionViewSource.GetDefaultView(_talkgroups);
        _talkgroupView.Filter = FilterTalkgroup;
    }

    private async void TestConnectionButton_Click(object sender, RoutedEventArgs e)
    {
        await RunGuardedAsync("Logging in to RadioReference...", async () =>
        {
            var user = await _radioReferenceClient.GetUserDataAsync(GetAuth());
            await LoadStatesAsync();
            SaveCredentialsIfRequested();
            SetStatus(string.IsNullOrWhiteSpace(user)
                ? "Login succeeded."
                : $"Login succeeded: {user}");
        });
    }

    private void ForgetCredentialsButton_Click(object sender, RoutedEventArgs e)
    {
        CredentialStorage.Delete();
        SaveCredentialsBox.IsChecked = false;
        ForgetCredentialsButton.IsEnabled = false;
        SetStatus("Saved credentials removed.");
    }

    private async void LookupButton_Click(object sender, RoutedEventArgs e)
    {
        var sysId = SysIdBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(sysId))
        {
            SetStatus("Enter a system SysID to look up.");
            return;
        }

        await RunGuardedAsync("Looking up systems...", async () =>
        {
            _systems.Clear();
            foreach (var system in await _radioReferenceClient.GetTrsBySysIdAsync(sysId, GetAuth()))
            {
                _systems.Add(system);
            }

            if (_systems.Count > 0)
            {
                SystemResultsBox.SelectedIndex = 0;
                SetStatus($"Found {_systems.Count} system(s).");
            }
            else
            {
                SetStatus("No systems matched that SysID.");
            }
        });
    }

    private async void CountySearchButton_Click(object sender, RoutedEventArgs e)
    {
        if (StateSearchBox.SelectedItem is not RadioReferenceState state)
        {
            SetStatus("Select a state.");
            return;
        }

        await RunGuardedAsync("Searching systems by state/county...", async () =>
        {
            var county = CountySearchBox.SelectedItem as RadioReferenceCounty;
            _systems.Clear();
            var systems = county is null
                ? await _radioReferenceClient.GetTrsByStateAsync(state.Id, GetAuth())
                : await _radioReferenceClient.GetTrsByCountyAsync(county.Id, GetAuth());

            foreach (var system in systems)
            {
                _systems.Add(system);
            }

            if (_systems.Count > 0)
            {
                SystemResultsBox.SelectedIndex = 0;
                SetStatus($"Found {_systems.Count} system(s).");
            }
            else
            {
                SetStatus("No systems matched that state/county search.");
            }
        });
    }

    private async void StateSearchBox_DropDownOpened(object sender, EventArgs e)
    {
        if (_statesLoaded)
        {
            return;
        }

        await RunGuardedAsync("Loading states...", LoadStatesAsync);
    }

    private async void StateSearchBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (StateSearchBox.SelectedItem is not RadioReferenceState state)
        {
            _counties.Clear();
            return;
        }

        await RunGuardedAsync($"Loading counties for {state.Code}...", async () =>
        {
            _counties.Clear();
            foreach (var county in await _radioReferenceClient.GetCountiesForStateAsync(state.Id, GetAuth()))
            {
                _counties.Add(county);
            }

            CountySearchBox.SelectedIndex = -1;
            SetStatus($"Loaded {_counties.Count} county/counties for {state.DisplayName}.");
        });
    }

    private async void LoadButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(SidBox.Text.Trim(), out var sid) || sid <= 0)
        {
            SetStatus("Enter a valid RadioReference SID.");
            return;
        }

        await RunGuardedAsync("Downloading talkgroups...", async () =>
        {
            var auth = GetAuth();
            var details = await _radioReferenceClient.GetTrsDetailsAsync(sid, auth);
            if (!string.IsNullOrWhiteSpace(details?.Name))
            {
                GroupSetNameBox.Text = Rpm2CsvExporter.CleanName(details.Name, 8);
            }

            var groups = await _radioReferenceClient.GetTrsTalkgroupsAsync(sid, auth);
            var sites = await _radioReferenceClient.GetTrsSitesAsync(sid, auth);

            _talkgroups.Clear();
            foreach (var talkgroup in groups.OrderBy(t => t.CategorySort).ThenBy(t => t.Sort).ThenBy(t => t.DecimalId))
            {
                _talkgroups.Add(talkgroup);
            }

            _sites.Clear();
            foreach (var site in sites.Where(s => s.ControlFrequencies.Count > 0))
            {
                _sites.Add(site);
            }

            RefreshCountyFilterOptions();
            ExportButton.IsEnabled = _talkgroups.Count > 0;
            ExportSitesButton.IsEnabled = _sites.Count > 0;
            _talkgroupView.Refresh();
            SetStatus($"Loaded {_talkgroups.Count} talkgroup(s) and {_sites.Count} site(s).");
        });
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        var visibleTalkgroups = _talkgroupView.Cast<Talkgroup>().ToList();
        if (visibleTalkgroups.Count == 0)
        {
            SetStatus("There are no visible talkgroups to export.");
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Export RPM2 Talkgroup CSV",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            FileName = $"{Rpm2CsvExporter.CleanName(GroupSetNameBox.Text, 8)}.csv"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var groupSetName = Rpm2CsvExporter.CleanName(GroupSetNameBox.Text, 8);
        GroupSetNameBox.Text = groupSetName;

        var options = new Rpm2ExportOptions(
            groupSetName,
            string.IsNullOrWhiteSpace(RpmSystemIdBox.Text) ? "0x0" : RpmSystemIdBox.Text.Trim(),
            ((VisualColorBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString()) ?? "White",
            ((LongNameSourceBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString()) ?? "Smart",
            ConventionalFallbackBox.IsChecked == true,
            UseGroupIdZeroBox.IsChecked == true,
            SuppressKeyNumberBox.IsChecked == true,
            EncryptCallParametersBox.IsChecked == true,
            TransmitBox.IsChecked == true,
            ReceiveBox.IsChecked == true,
            CallsBox.IsChecked == true,
            ScanBox.IsChecked == true,
            AlertTonesBox.IsChecked == true,
            BacklightBox.IsChecked == true,
            ScanListMemberBox.IsChecked == true,
            MandownBox.IsChecked == true);

        Rpm2CsvExporter.Write(dialog.FileName, visibleTalkgroups, options);
        SetStatus($"Exported {visibleTalkgroups.Count} talkgroup(s) to {dialog.FileName}.");
    }

    private void ExportSitesButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var selectedSites = SitesBox.SelectedItems.Cast<TrunkedSite>().ToList();
            var sitesToExport = selectedSites.Count > 0 || ExportAllSitesIfNoneSelectedBox.IsChecked != true
                ? selectedSites
                : _sites.ToList();

            if (sitesToExport.Count == 0)
            {
                SetStatus("Select one or more sites to export.");
                return;
            }

            var name = Rpm2CsvExporter.CleanName(GroupSetNameBox.Text, 8);
            GroupSetNameBox.Text = name;
            var transmitPlaceholder = ReadSiteTransmitPlaceholder();

            var dialog = new SaveFileDialog
            {
                Title = "Export RPM2 Site Frequency CSV",
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                FileName = $"{name}_SITES.csv"
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            Rpm2SiteCsvExporter.Write(dialog.FileName, sitesToExport, name, transmitPlaceholder);
            SetStatus($"Exported {sitesToExport.Count} site(s) to {dialog.FileName}.");
        }
        catch (Exception ex)
        {
            App.LogException(ex);
            SetStatus(ex.Message);
            MessageBox.Show(this, ex.Message, "RadioReference to RPM2", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SystemResultsBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (SystemResultsBox.SelectedItem is TrunkedSystem system)
        {
            SidBox.Text = system.Sid.ToString();
            GroupSetNameBox.Text = Rpm2CsvExporter.CleanName(system.Name, 8);
        }
    }

    private void FilterBox_TextChanged(object sender, RoutedEventArgs e)
    {
        _talkgroupView?.Refresh();
    }

    private void CountyFilterBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        _talkgroupView?.Refresh();
    }

    private bool FilterTalkgroup(object item)
    {
        if (item is not Talkgroup talkgroup)
        {
            return false;
        }

        if (IncludeEncryptedBox.IsChecked != true && talkgroup.IsEncrypted)
        {
            return false;
        }

        var selectedCounties = SelectedCountyFilters();
        if (selectedCounties.Count > 0 && !selectedCounties.Contains(talkgroup.CategoryName))
        {
            return false;
        }

        var filter = FilterBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        return talkgroup.DecimalId.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase)
            || talkgroup.Alpha.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || talkgroup.Description.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || talkgroup.CategoryName.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || talkgroup.TagSummary.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshCountyFilterOptions()
    {
        var selected = SelectedCountyFilters();
        var options = _talkgroups
            .Select(t => t.CategoryName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        CountyFilterBox.ItemsSource = options;
        CountyFilterBox.SelectedItems.Clear();
        foreach (var option in options.Where(selected.Contains))
        {
            CountyFilterBox.SelectedItems.Add(option);
        }
    }

    private HashSet<string> SelectedCountyFilters()
    {
        return CountyFilterBox.SelectedItems
            .Cast<object>()
            .Select(x => x.ToString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase)!;
    }

    private decimal ReadSiteTransmitPlaceholder()
    {
        if (!decimal.TryParse(SiteTransmitPlaceholderBox.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            throw new InvalidOperationException("Site TX placeholder must be a valid frequency.");
        }

        if (!IsValidSiteTransmitPlaceholder(value))
        {
            throw new InvalidOperationException("Site TX placeholder must be between 763-776, 793-805.99375, or 825-896 MHz.");
        }

        return value;
    }

    private static bool IsValidSiteTransmitPlaceholder(decimal value)
    {
        return value is >= 763.00000m and <= 776.00000m
            || value is >= 793.00000m and <= 805.99375m
            || value is >= 825.00000m and <= 896.00000m;
    }

    private async Task LoadStatesAsync()
    {
        if (_statesLoaded)
        {
            return;
        }

        _states.Clear();
        foreach (var state in await _radioReferenceClient.GetUsStatesAsync(GetAuth()))
        {
            _states.Add(state);
        }

        _statesLoaded = true;
    }

    private void LoadSavedCredentials()
    {
        try
        {
            var credentials = CredentialStorage.Load();
            if (credentials is null)
            {
                ForgetCredentialsButton.IsEnabled = false;
                return;
            }

            UsernameBox.Text = credentials.Username;
            PasswordBox.Password = credentials.Password;
            AppKeyBox.Password = credentials.AppKey;
            SaveCredentialsBox.IsChecked = true;
            ForgetCredentialsButton.IsEnabled = true;
            SetStatus("Saved credentials loaded.");
        }
        catch (Exception ex)
        {
            App.LogException(ex);
            ForgetCredentialsButton.IsEnabled = CredentialStorage.Exists;
            SetStatus("Saved credentials could not be loaded.");
        }
    }

    private void SaveCredentialsIfRequested()
    {
        if (SaveCredentialsBox.IsChecked != true)
        {
            return;
        }

        CredentialStorage.Save(new SavedCredentials
        {
            Username = UsernameBox.Text.Trim(),
            Password = PasswordBox.Password,
            AppKey = AppKeyBox.Password.Trim()
        });
        ForgetCredentialsButton.IsEnabled = true;
    }

    private RadioReferenceAuth GetAuth()
    {
        return new RadioReferenceAuth(
            UsernameBox.Text.Trim(),
            PasswordBox.Password,
            DecodeAppKey(AppKeyBox.Password.Trim()),
            "latest");
    }

    private static string DecodeAppKey(string appKey)
    {
        const string prefix = "base64:";
        if (!appKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return appKey;
        }

        try
        {
            var encodedValue = appKey[prefix.Length..].Trim();
            return Encoding.UTF8.GetString(Convert.FromBase64String(encodedValue));
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("The encoded RadioReference API app key is not valid base64.", ex);
        }
    }

    private async Task RunGuardedAsync(string busyText, Func<Task> action)
    {
        SetBusy(true);
        SetStatus(busyText);

        try
        {
            await action();
        }
        catch (Exception ex)
        {
            App.LogException(ex);
            SetStatus(ex.Message);
            MessageBox.Show(this, ex.Message, "RadioReference to RPM2", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool isBusy)
    {
        TestConnectionButton.IsEnabled = !isBusy;
        LookupButton.IsEnabled = !isBusy;
        CountySearchButton.IsEnabled = !isBusy;
        LoadButton.IsEnabled = !isBusy;
        ExportButton.IsEnabled = !isBusy && _talkgroups.Count > 0;
        ExportSitesButton.IsEnabled = !isBusy && _sites.Count > 0;
    }

    private void SetStatus(string message)
    {
        StatusText.Text = message;
    }
}
