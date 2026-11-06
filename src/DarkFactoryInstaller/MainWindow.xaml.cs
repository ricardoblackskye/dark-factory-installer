using System.Linq;
using System.Windows;
using DarkFactoryInstaller.Orchestration;

namespace DarkFactoryInstaller;

public partial class MainWindow : Window
{
    private readonly PrerequisiteChecker _checker = new(new ProcessCliRunner());

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

    private void ProvisionBtn_Click(object sender, RoutedEventArgs e)
    {
        // Provisioning is driven by #7 (orchestration). The button is gated on prerequisites here.
        MessageBox.Show("Provisioning is implemented in workstream #7 (orchestration).",
            "Not yet available");
    }
}
