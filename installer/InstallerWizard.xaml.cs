using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace KillerMCP.Setup
{
    public partial class InstallerWizard : Window
    {
        private readonly string _destination;
        private readonly bool _isolated;
        private bool _installed;

        private InstallerWizard(string destination, bool isolated)
        {
            _destination = destination;
            _isolated = isolated;
            InitializeComponent();
            VersionLabel.Text = "SETUP " + Program.CurrentVersion;
            InstallLocation.Text = destination;
            SetRuntimeStatus();
        }

        internal static int Run(string destination, bool isolated)
        {
            var application = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var wizard = new InstallerWizard(destination, isolated);
            bool? result = wizard.ShowDialog();
            application.Shutdown();
            return result == true ? 0 : 1;
        }

        private void SetRuntimeStatus()
        {
            bool ready = Program.HasRuntime10(_isolated);
            RuntimeStatus.Text = ready ? ".NET 10 runtime detected" : ".NET 10 runtime required before installation";
            RuntimeStatus.Foreground = new SolidColorBrush(ready ? System.Windows.Media.Color.FromRgb(30, 165, 76) : System.Windows.Media.Color.FromRgb(255, 190, 80));
            RuntimeLink.Visibility = ready ? Visibility.Collapsed : Visibility.Visible;
            InstallButton.IsEnabled = ready;
        }

        private async void Install_Click(object sender, RoutedEventArgs e)
        {
            if (_installed) { DialogResult = true; return; }
            if (!Program.HasRuntime10(_isolated)) { SetRuntimeStatus(); return; }
            try
            {
                InstallButton.IsEnabled = false;
                Heading.Text = "Installing KillerMCP";
                Status.Text = "Verifying the native runtime and connecting compatible agent clients...";
                await Task.WhenAll(Task.Run(() => Program.Install(_destination)), Task.Delay(700));
                _installed = true;
                Heading.Text = "KillerMCP is ready";
                Status.Text = "Installed and connected. Open a new agent chat to use the tools.";
                Status.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 165, 76));
                InstallButton.Content = "Done";
                InstallButton.IsEnabled = true;
            }
            catch (Exception exception)
            {
                Status.Text = exception.Message;
                Status.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(227, 93, 106));
                InstallButton.IsEnabled = true;
            }
        }

        private void RuntimeLink_Click(object sender, MouseButtonEventArgs e) => Process.Start(new ProcessStartInfo("https://dotnet.microsoft.com/en-us/download/dotnet/10.0") { UseShellExecute = true });
        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
    }
}
