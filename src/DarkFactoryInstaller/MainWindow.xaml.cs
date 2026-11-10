using System;
using System.Collections.Generic;
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
    private readonly ProvisionViewModel _provisionVm = new(new StreamingProcessRunner());

    public MainWindow()
    {
        InitializeComponent();
        StepList.ItemsSource = _provisionVm.Steps;
        _provisionVm.PropertyChanged += OnProvisionChanged;
    }

    private void OnProvisionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProvisionViewModel.Banner)) BannerTextBlock.Text = _provisionVm.Banner;
        else if (e.PropertyName == nameof(ProvisionViewModel.BannerVisible)) BannerTextBlock.Visibility = _provisionVm.BannerVisible ? Visibility.Visible : Visibility.Collapsed;
        else if (e.PropertyName == nameof(ProvisionViewModel.BannerIsSuccess)) BannerTextBlock.Foreground = _provisionVm.BannerIsSuccess ? Brushes.Green : Brushes.Red;
        else if (e.PropertyName == nameof(ProvisionViewModel.Log)) LogTextBox.Text = _provisionVm.Log;
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
            if (!statuses.Any(s => s.BlocksProvision)) ProvisionBtn.IsEnabled = true;
        }
        finally
        {
            VerifyBinariesBtn.IsEnabled = true;
        }
    }

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

            // The headless CLI is an npm script; the installer invokes it via npm. Build the canonical
            // command, then wrap it so the real process runs `npm run df:sandbox-up -- <flags>`.
            var command = CliRunner.BuildCommand(new SandboxRunRequest(RepoBox.Text.Trim(), issue));
            var tokens = command.Split(' ');
            string exe;
            IReadOnlyList<string> args;
            if (tokens[0] == "df:sandbox-up")
            {
                exe = "npm";
                args = new List<string> { "run", "df:sandbox-up", "--" }.Concat(tokens.Skip(1)).ToList();
            }
            else
            {
                exe = tokens[0];
                args = tokens.Skip(1).ToList();
            }

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
}