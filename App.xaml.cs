using System.Configuration;
using System.Data;
using System.Windows;

namespace Fun_Dub_Tool_Box
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Global exception handlers
            DispatcherUnhandledException += (s, ex) =>
            {
                Log(ex.Exception);     // write to file/Debug
                MessageBox.Show(ex.Exception.Message, "Unexpected error");
                ex.Handled = true;     // prevent 0xC000041D crash
            };

            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
            {
                Log(ex.ExceptionObject as Exception);
            };

            TaskScheduler.UnobservedTaskException += (s, ex) =>
            {
                Log(ex.Exception);
                ex.SetObserved();
            };

            base.OnStartup(e);
        }

        private static void Log(Exception? ex)
        {
            try
            {
                System.IO.File.AppendAllText("app-errors.log",
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\r\n");
            }
            catch { }
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

}
