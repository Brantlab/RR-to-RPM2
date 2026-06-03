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
    private const string NoTagFilterName = "(No tag)";
    private readonly ObservableCollection<Talkgroup> _talkgroups = [];
    private readonly ObservableCollection<TrunkedSystem> _systems = [];
    private readonly ObservableCollection<TrunkedSite> _sites = [];
    private readonly ObservableCollection<RadioReferenceState> _states = [];
    private readonly ObservableCollection<RadioReferenceCounty> _counties = [];
    private readonly ObservableCollection<FilterOption> _categoryFilters = [];
    private readonly ObservableCollection<FilterOption> _tagFilters = [];
    private readonly ObservableCollection<FilterOption> _siteCountyFilters = [];
    private readonly ICollectionView _talkgroupView;
    private readonly ICollectionView _siteView;
    private readonly RadioReferenceClient _radioReferenceClient = new();
    private bool _statesLoaded;
    private bool _savedCredentialsLoaded;

    public MainWindow()
    {
        InitializeComponent();
        TalkgroupsGrid.ItemsSource = _talkgroups;
        SystemResultsBox.ItemsSource = _systems;
        SitesBox.ItemsSource = _sites;
        StateSearchBox.ItemsSource = _states;
        CountySearchBox.ItemsSource = _counties;
        CountyFilterBox.ItemsSource = _categoryFilters;
        TagFilterBox.ItemsSource = _tagFilters;
        SiteCountyFilterBox.ItemsSource = _siteCountyFilters;
        _savedCredentialsLoaded = LoadSavedCredentials();
        _talkgroupView = CollectionViewSource.GetDefaultView(_talkgroups);
        _talkgroupView.Filter = FilterTalkgroup;
        _siteView = CollectionViewSource.GetDefaultView(_sites);
        _siteView.Filter = FilterSite;
        Loaded += MainWindow_Loaded;
    }

    private async void TestConnectionButton_Click(object sender, RoutedEventArgs e)
    {
        await LoginAsync("Logging in to RadioReference...");
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_savedCredentialsLoaded)
        {
            return;
        }

        await LoginAsync("Auto-connecting to RadioReference...");
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
            BeginProgress(6);
            var auth = GetAuth();
            SetStatus("Loading system details...");
            var details = await _radioReferenceClient.GetTrsDetailsAsync(sid, auth);
            StepProgress();
            if (!string.IsNullOrWhiteSpace(details?.Name))
            {
                GroupSetNameBox.Text = Rpm2CsvExporter.CleanName(details.Name, 8);
            }

            SetStatus("Downloading talkgroups...");
            var groups = await _radioReferenceClient.GetTrsTalkgroupsAsync(sid, auth);
            StepProgress();
            SetStatus("Downloading sites...");
            var sites = await _radioReferenceClient.GetTrsSitesAsync(sid, auth);
            StepProgress();

            SetStatus("Populating talkgroups...");
            _talkgroups.Clear();
            foreach (var talkgroup in groups.OrderBy(t => t.CategorySort).ThenBy(t => t.Sort).ThenBy(t => t.DecimalId))
            {
                _talkgroups.Add(talkgroup);
            }
            StepProgress();

            SetStatus("Populating sites...");
            _sites.Clear();
            foreach (var site in sites)
            {
                _sites.Add(site);
            }
            StepProgress();

            SetStatus("Refreshing filters...");
            RefreshFilterOptions();
            RefreshSiteCountyFilterOptions();
            ExportButton.IsEnabled = _talkgroups.Count > 0;
            ExportSitesButton.IsEnabled = _sites.Count > 0;
            ExportSiteAliasesButton.IsEnabled = _sites.Count > 0;
            _talkgroupView.Refresh();
            _siteView.Refresh();
            StepProgress();
            SetStatus($"Loaded {_talkgroups.Count} talkgroup(s) and {_sites.Count} site(s).");
        });
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        var visibleTalkgroups = _talkgroupView.Cast<Talkgroup>()
            .Where(t => t.IsSelected)
            .ToList();
        if (visibleTalkgroups.Count == 0)
        {
            SetStatus("There are no selected visible talkgroups to export.");
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
            var sitesToExport = SelectedSitesForExport();

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

    private void ExportSiteAliasesButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var sitesToExport = SelectedSitesForExport();

            if (sitesToExport.Count == 0)
            {
                SetStatus("Select one or more sites to export.");
                return;
            }

            var options = ReadSiteAliasOptions();
            var dialog = new SaveFileDialog
            {
                Title = "Export RPM2 Site Alias CSV",
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                FileName = $"{options.Name}_SITE_ALIAS.csv"
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            Rpm2SiteAliasCsvExporter.Write(dialog.FileName, sitesToExport, options);
            SetStatus($"Exported {sitesToExport.Count} site alias row(s) to {dialog.FileName}.");
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
            if (string.IsNullOrWhiteSpace(SiteAliasListNameBox.Text) || SiteAliasListNameBox.Text.Equals("STEL0001", StringComparison.OrdinalIgnoreCase))
            {
                SiteAliasListNameBox.Text = Rpm2CsvExporter.CleanName(system.Name, 8);
            }
        }
    }

    private void FilterBox_TextChanged(object sender, RoutedEventArgs e)
    {
        _talkgroupView?.Refresh();
    }

    private void CategoryFilterCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _talkgroupView?.Refresh();
    }

    private void TagFilterCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _talkgroupView?.Refresh();
    }

    private void SelectAllCategoriesButton_Click(object sender, RoutedEventArgs e)
    {
        SetAllCategoriesSelected(true);
    }

    private void ClearCategoriesButton_Click(object sender, RoutedEventArgs e)
    {
        SetAllCategoriesSelected(false);
    }

    private void SelectAllTagsButton_Click(object sender, RoutedEventArgs e)
    {
        SetAllTagsSelected(true);
    }

    private void ClearTagsButton_Click(object sender, RoutedEventArgs e)
    {
        SetAllTagsSelected(false);
    }

    private void SelectVisibleTalkgroupsButton_Click(object sender, RoutedEventArgs e)
    {
        SetVisibleTalkgroupsSelected(true);
    }

    private void ClearVisibleTalkgroupsButton_Click(object sender, RoutedEventArgs e)
    {
        SetVisibleTalkgroupsSelected(false);
    }

    private void SelectVisibleSitesButton_Click(object sender, RoutedEventArgs e)
    {
        SetVisibleSitesSelected(true);
    }

    private void ClearSitesButton_Click(object sender, RoutedEventArgs e)
    {
        SetAllSitesSelected(false);
    }

    private void SiteCountyFilterCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _siteView?.Refresh();
    }

    private void SelectAllSiteCountiesButton_Click(object sender, RoutedEventArgs e)
    {
        SetAllSiteCountyFiltersSelected(true);
    }

    private void ClearSiteCountiesButton_Click(object sender, RoutedEventArgs e)
    {
        SetAllSiteCountyFiltersSelected(false);
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

        var selectedCategories = SelectedCategoryFilters();
        if (!selectedCategories.Contains(talkgroup.CategoryName))
        {
            return false;
        }

        var selectedTags = SelectedTagFilters();
        if (!TalkgroupTags(talkgroup).Any(selectedTags.Contains))
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

    private bool FilterSite(object item)
    {
        if (item is not TrunkedSite site)
        {
            return false;
        }

        return SelectedSiteCountyFilters().Contains(SiteCountyName(site));
    }

    private void RefreshFilterOptions()
    {
        var previousSelections = _categoryFilters.ToDictionary(
            x => x.Name,
            x => x.IsSelected,
            StringComparer.OrdinalIgnoreCase);
        var options = _talkgroups
            .Select(t => t.CategoryName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        _categoryFilters.Clear();
        foreach (var option in options)
        {
            _categoryFilters.Add(new FilterOption(
                option,
                !previousSelections.TryGetValue(option, out var isSelected) || isSelected));
        }

        RefreshTagFilterOptions();
    }

    private void RefreshSiteCountyFilterOptions()
    {
        var previousSelections = _siteCountyFilters.ToDictionary(
            x => x.Name,
            x => x.IsSelected,
            StringComparer.OrdinalIgnoreCase);
        var options = _sites
            .Select(SiteCountyName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x.Equals("(No county)", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(x => x)
            .ToList();

        _siteCountyFilters.Clear();
        foreach (var option in options)
        {
            _siteCountyFilters.Add(new FilterOption(
                option,
                !previousSelections.TryGetValue(option, out var isSelected) || isSelected));
        }
    }

    private HashSet<string> SelectedCategoryFilters()
    {
        return _categoryFilters
            .Where(x => x.IsSelected)
            .Select(x => x.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase)!;
    }

    private void RefreshTagFilterOptions()
    {
        var previousSelections = _tagFilters.ToDictionary(
            x => x.Name,
            x => x.IsSelected,
            StringComparer.OrdinalIgnoreCase);
        var options = _talkgroups
            .SelectMany(TalkgroupTags)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x.Equals(NoTagFilterName, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(x => x)
            .ToList();

        _tagFilters.Clear();
        foreach (var option in options)
        {
            _tagFilters.Add(new FilterOption(
                option,
                !previousSelections.TryGetValue(option, out var isSelected) || isSelected));
        }
    }

    private HashSet<string> SelectedTagFilters()
    {
        return _tagFilters
            .Where(x => x.IsSelected)
            .Select(x => x.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase)!;
    }

    private HashSet<string> SelectedSiteCountyFilters()
    {
        return _siteCountyFilters
            .Where(x => x.IsSelected)
            .Select(x => x.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase)!;
    }

    private void SetAllCategoriesSelected(bool isSelected)
    {
        foreach (var category in _categoryFilters)
        {
            category.IsSelected = isSelected;
        }

        CountyFilterBox.Items.Refresh();
        _talkgroupView.Refresh();
    }

    private void SetAllTagsSelected(bool isSelected)
    {
        foreach (var tag in _tagFilters)
        {
            tag.IsSelected = isSelected;
        }

        TagFilterBox.Items.Refresh();
        _talkgroupView.Refresh();
    }

    private void SetAllSiteCountyFiltersSelected(bool isSelected)
    {
        foreach (var county in _siteCountyFilters)
        {
            county.IsSelected = isSelected;
        }

        SiteCountyFilterBox.Items.Refresh();
        _siteView.Refresh();
    }

    private void SetVisibleTalkgroupsSelected(bool isSelected)
    {
        var count = 0;
        foreach (var talkgroup in _talkgroupView.Cast<Talkgroup>())
        {
            talkgroup.IsSelected = isSelected;
            count++;
        }

        TalkgroupsGrid.Items.Refresh();
        SetStatus($"{(isSelected ? "Selected" : "Cleared")} {count} visible talkgroup(s).");
    }

    private void SetAllSitesSelected(bool isSelected)
    {
        foreach (var site in _sites)
        {
            site.IsSelected = isSelected;
        }

        SitesBox.Items.Refresh();
        SetStatus($"{(isSelected ? "Selected" : "Cleared")} {_sites.Count} site(s).");
    }

    private void SetVisibleSitesSelected(bool isSelected)
    {
        var count = 0;
        foreach (var site in _siteView.Cast<TrunkedSite>())
        {
            site.IsSelected = isSelected;
            count++;
        }

        SitesBox.Items.Refresh();
        SetStatus($"{(isSelected ? "Selected" : "Cleared")} {count} visible site(s).");
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

    private List<TrunkedSite> SelectedSitesForExport()
    {
        return _sites.Where(s => s.IsSelected).ToList();
    }

    private Rpm2SiteAliasOptions ReadSiteAliasOptions()
    {
        var name = Rpm2SiteAliasCsvExporter.CleanAliasName(SiteAliasListNameBox.Text);
        var wanList = SiteAliasWanListBox.Text.Trim();
        var network = SiteAliasNetworkBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Site alias list name must contain at least one alphanumeric character.");
        }

        if (string.IsNullOrWhiteSpace(wanList))
        {
            throw new InvalidOperationException("P25 WAN list is required.");
        }

        if (string.IsNullOrWhiteSpace(network))
        {
            throw new InvalidOperationException("WA network is required.");
        }

        SiteAliasListNameBox.Text = name;
        SiteAliasWanListBox.Text = wanList;
        SiteAliasNetworkBox.Text = network;

        return new Rpm2SiteAliasOptions(name, wanList, network);
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

    private async Task LoginAsync(string busyText)
    {
        await RunGuardedAsync(busyText, async () =>
        {
            var user = await _radioReferenceClient.GetUserDataAsync(GetAuth());
            await LoadStatesAsync();
            SaveCredentialsIfRequested();
            SetStatus(string.IsNullOrWhiteSpace(user)
                ? "Login succeeded."
                : $"Login succeeded: {user}");
        });
    }

    private bool LoadSavedCredentials()
    {
        try
        {
            var credentials = CredentialStorage.Load();
            if (credentials is null)
            {
                ForgetCredentialsButton.IsEnabled = false;
                return false;
            }

            UsernameBox.Text = credentials.Username;
            PasswordBox.Password = credentials.Password;
            AppKeyBox.Password = credentials.AppKey;
            SaveCredentialsBox.IsChecked = true;
            ForgetCredentialsButton.IsEnabled = true;
            SetStatus("Saved credentials loaded.");
            return true;
        }
        catch (Exception ex)
        {
            App.LogException(ex);
            ForgetCredentialsButton.IsEnabled = CredentialStorage.Exists;
            SetStatus("Saved credentials could not be loaded.");
            return false;
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
        BeginIndeterminateProgress();

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
            EndProgress();
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
        ExportSiteAliasesButton.IsEnabled = !isBusy && _sites.Count > 0;
    }

    private void SetStatus(string message)
    {
        StatusText.Text = message;
    }

    private void BeginIndeterminateProgress()
    {
        BusyProgressBar.Visibility = Visibility.Visible;
        BusyProgressBar.IsIndeterminate = true;
        BusyProgressBar.Value = 0;
    }

    private void BeginProgress(int steps)
    {
        BusyProgressBar.Visibility = Visibility.Visible;
        BusyProgressBar.IsIndeterminate = false;
        BusyProgressBar.Minimum = 0;
        BusyProgressBar.Maximum = Math.Max(steps, 1);
        BusyProgressBar.Value = 0;
    }

    private void StepProgress()
    {
        BusyProgressBar.IsIndeterminate = false;
        BusyProgressBar.Value = Math.Min(BusyProgressBar.Value + 1, BusyProgressBar.Maximum);
    }

    private void EndProgress()
    {
        BusyProgressBar.IsIndeterminate = false;
        BusyProgressBar.Value = 0;
        BusyProgressBar.Visibility = Visibility.Collapsed;
    }

    private static string SiteCountyName(TrunkedSite site)
    {
        var location = site.Location.Trim();
        return string.IsNullOrWhiteSpace(location) ? "(No county)" : location;
    }

    private static IReadOnlyList<string> TalkgroupTags(Talkgroup talkgroup)
    {
        if (string.IsNullOrWhiteSpace(talkgroup.TagSummary))
        {
            return [NoTagFilterName];
        }

        var tags = talkgroup.TagSummary
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return tags.Count > 0 ? tags : [NoTagFilterName];
    }

    private sealed class FilterOption(string name, bool isSelected)
    {
        public string Name { get; } = name;
        public bool IsSelected { get; set; } = isSelected;
    }
}
