using System.Configuration;
using System.Data;
using System.Windows;

namespace VideoPlayer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static string VideoPath = "";
        public static string PipeName = "";
        public static int ProcessId = 0;

        /// <summary>
        /// スタートアップ
        /// </summary>
        /// <param name="e"></param>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (e.Args.Length != 3)
            {
                MessageBox.Show("Usage: VideoPlayer.exe <VideoPath> <PipeName> <ProcessId>");
                Shutdown();
                return;
            }

            VideoPath = e.Args[0];
            PipeName = e.Args[1];
            ProcessId = int.Parse(e.Args[2]);
        }

    }

}
