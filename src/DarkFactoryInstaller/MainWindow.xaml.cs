using System.IO;
using System.Linq;
using System.Windows;
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

    public MainWindow()
    {
        InitializeComponent();
        ProvisionBtn.IsEnabled = false;
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
            ProvisionBtn.IsEnabled = !statuses.Any(s => s.BlocksProvision);
        }
        finally
        {
            VerifyBinariesBtn.IsEnabled = true;
        }
    }

    private void ProvisionBtn_Click(object sender, RoutedEventArgs e)
    {
        // Provisioning is driven by #7 (orchestration). The button is gated on prerequisites + binaries here.
        MessageBox.Show("Provisioning is implemented in workstream #7 (orchestration).",
            "Not yet available");
    }
}
