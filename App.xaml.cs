using System.Windows;
using System.Windows.Threading;

namespace DreamLauncher;

public partial class App : System.Windows.Application
{
    private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        System.Windows.MessageBox.Show(
            e.Exception.Message,
            "Dream Launcher error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}
