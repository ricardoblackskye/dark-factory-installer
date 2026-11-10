using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using DarkFactoryInstaller.Domain;
using DarkFactoryInstaller.Orchestration;

namespace DarkFactoryInstaller;

public partial class MainWindow : Window
{
    private readonly PrerequisiteChecker _checker = new(new ProcessCliRunner());
    private readonly BinaryAcquisitionService _binarySvc = new(
        new FileBinaryManifestSource(Path.Combine(AppContext.BaseDirectory, "resources", "artifacts.lock.json")),
        new SystemProcessRunner(),
        new SystemFileDownloader());
    private readonly ProvisionViewModel _provisionVm = new(new StreamingProcessRunner(), new FileProvisionStateStore(), new CliVersionGate(new SystemProcessRunner(), InstallerContract.MinimumCliVersion));

    public MainWindow()
    {
        InitializeComponent();
        StepList.ItemsSource = _provisionVm.Steps;
        _provisionVm.PropertyChanged += OnProvisionChanged;
        // AC2 (#9): show the last known progress for the default target on launch.
        _provisionVm.SetTarget($"{RepoBox.Text.Trim()}/{IssueBox.Text.Trim()}");
    }

    private void OnProvisionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProvisionViewModel.Banner)) BannerTextBlock.Text = _provisionVm.Banner;
        else if (e.PropertyName == nameof(ProvisionViewModel.BannerVisible)) BannerTextBlock.Visibility = _provisionVm.BannerVisible ? Visibility.Visible : Visibility.Collapsed;
        else if (e.PropertyName == nameof(ProvisionViewModel.BannerIsSuccess)) BannerTextBlock.Foreground = _provisionVm.BannerIsSuccess ? Brushes.Green : Brushes.Red;
        else if (e.PropertyName == nameof(ProvisionViewModel.Log)) LogTextBox.Text = _provisionVm.Log;
        else if (e.PropertyName == nameof(ProvisionViewModel.Diagnostics)) // AC3/AC5 (#10)
        {
            var has = !string.IsNullOrEmpty(_provisionVm.Diagnostics);
            DiagnosticsTextBox.Text = _provisionVm.Diagnostics ?? string.Empty;
            DiagnosticsTextBox.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
            DiagnosticsHeader.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private async void CheckBtn_Click(object sender, RoutedEventArgs e)
    {
        CheckBtn.IsEnabled = false;
        try
        {
            var result = await _checker.CheckAsync();
            PrereqList.ItemsSource = result.Statuses.Select(s =>
                $"{(s.Present ? "[OK]      " : "[MISSING]  ")}{s.Name}{(s.Message is null ? "" : $" — {s.Message}")}");
            ProvisionBtn.IsEnabled = result.Ready;
            ResetBtn.IsEnabled = result.Ready;
        }
        finally
        {
            CheckBtn.IsEnabled = true;
        }
    }

    private async void VerifyBinariesBtn_Click(object sender, RoutedEventArgs e)
    {
        VerifyBinariesBtn.IsEnabled = false;
        try
        {
            var statuses = await _binarySvc.AcquireAsync();
            BinaryList.ItemsSource = statuses.Select(s =>
                $"{(s.State is BinaryState.PresentVerified or BinaryState.Acquired ? "[OK]      " : "[PROBLEM] ")}{s.Name} ({s.State}){(s.Message is null ? "" : $" — {s.Message}")}");
            if (!statuses.Any(s => s.BlocksProvision))
            {
                ProvisionBtn.IsEnabled = true;
                ResetBtn.IsEnabled = true;
            }
        }
        finally
        {
            VerifyBinariesBtn.IsEnabled = true;
        }
    }

    private (string TargetId, string WorkspaceDir) Target()
    {
        var repo = RepoBox.Text.Trim();
        var issue = IssueBox.Text.Trim();
        var targetId = $"{repo}/{issue}";
        // The CLI's idempotent skip state lives under --workspace-dir; the installer owns a stable,
        // per-target dir so re-runs resume and resets cleanly (AC3).
        var safe = targetId.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
        var ws = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DarkFactoryInstaller", "workspaces", safe);
        return (targetId, ws);
    }

    /// <summary>Resume-capable run: re-invoking df:sandbox-up lets the CLI skip stages it already completed.</summary>
    private async void ProvisionBtn_Click(object sender, RoutedEventArgs e)
    {
        ProvisionBtn.IsEnabled = false;
        try
        {
            if (!int.TryParse(IssueBox.Text, out var issue))
            {
                MessageBox.Show("Issue must be a number.", "Invalid input");
                return;
            }

            var (targetId, ws) = Target();
            _provisionVm.SetTarget(targetId); // reflect last progress before the run
            var (exe, args) = CliCommands.Up(RepoBox.Text.Trim(), issue, ws);
            await _provisionVm.RunProvisionAsync(exe, args);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Provision failed: {ex.Message}", "Error");
        }
        finally
        {
            ProvisionBtn.IsEnabled = true;
        }
    }

    /// <summary>AC3 (#9): tear the cluster down fully, then re-provision clean.</summary>
    private async void ResetBtn_Click(object sender, RoutedEventArgs e)
    {
        ResetBtn.IsEnabled = false;
        ProvisionBtn.IsEnabled = false;
        try
        {
            if (!int.TryParse(IssueBox.Text, out var issue))
            {
                MessageBox.Show("Issue must be a number.", "Invalid input");
                return;
            }

            var (targetId, ws) = Target();
            await _provisionVm.ResetAndProvisionAsync(targetId, RepoBox.Text.Trim(), issue, ws);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Reset failed: {ex.Message}", "Error");
        }
        finally
        {
            ResetBtn.IsEnabled = true;
            ProvisionBtn.IsEnabled = true;
        }
    }
}
