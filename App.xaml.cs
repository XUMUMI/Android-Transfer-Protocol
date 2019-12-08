using System.Windows;

namespace Android_Transfer_Protocol
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        private void Application_Exit(object sender, ExitEventArgs e) => Adb.Stop();
    }
}
