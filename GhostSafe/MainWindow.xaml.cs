using ControlzEx.Standard;
using MahApps.Metro.Controls; // 追加
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using GhostSafe.Common;
using GhostSafe.Dialog;
using GhostSafe.ViewModel;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;

namespace GhostSafe
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public MainWindow()
        {
            InitializeComponent();

            EnsureDotNet8(); // .Net8 インストールの確認

            if (string.IsNullOrEmpty(Properties.Settings.Default.EncryptFolder))
            {   // 暗号化フォルダーがなかったら、保管場所を Appdata で作成する
                Properties.Settings.Default.EncryptFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                Properties.Settings.Default.Save();
            }

            App.AppDataPath = Path.Combine(Properties.Settings.Default.EncryptFolder, "GhostSafeFiles");

            if (!Directory.Exists(App.AppDataPath))
                Directory.CreateDirectory(App.AppDataPath);

            if (!Directory.Exists(App.AppTempPath))
                Directory.CreateDirectory(App.AppTempPath);

            // 前回の位置とサイズを復元
            this.Top = Properties.Settings.Default.WindowTop;
            this.Left = Properties.Settings.Default.WindowLeft;
            this.Width = Properties.Settings.Default.WindowWidth;
            this.Height = Properties.Settings.Default.WindowHeight;

            // 画面内に収まっているか確認（必要に応じて）
            this.Loaded += (s, e) => EnsureWindowInScreen();

            if (Properties.Settings.Default.InitialSetting == false)
            {   // 初期処理未済の時
                MainFrame.Navigate(new Page0(this)); // 初期設定のページ
            }
            else
            {
                MainFrame.Navigate(new PageLogin()); // ログインのページ
                App.MasterKey = EncryptorAesGcm.CreateMasterKey(); // 起動時に1回だけマスターキーを作る
            }
        }

        /// <summary>
        /// .Net8 のインストールの確認
        /// </summary>
        static void EnsureDotNet8()
        {
            // .NET 8 以降なら true
            if (Environment.Version.Major >= 8)
                return;

            var result = MessageBox.Show(
                App.GetStringResource("Dotnet8NotFound") + Environment.NewLine +
                App.GetStringResource("OpenDownload"),
                "Lack of execution environment",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://dotnet.microsoft.com/ja-jp/download/dotnet/8.0",
                    UseShellExecute = true
                });
            }

            Environment.Exit(1);
        }

        private bool _isConfirmedClose = false;

        /// <summary>
        /// アプリケーション終了時に位置とサイズを保存
        /// </summary>
        /// <param name="e"></param>
        protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // すでに確認済みなら通常通り閉じる
            if (_isConfirmedClose)
            {
                base.OnClosing(e);
                return;
            }

            bool isUpdate = false;

            foreach (FileModel file in App.FileUpdates)
            {
                if (IsFileLocked(file.Name))
                {
                    e.Cancel = true; // 一旦閉じるのをキャンセル

                    await DialogHost.Show(
                        new InfoDialog
                        {
                            DataContext = new InfoDialogViewModel(App.GetStringResource("LockedFile"))
                        },
                    "RootDialog");

                    return;
                }

                FileInfo fileInfo = new FileInfo(file.Name);

                if (fileInfo.LastWriteTime != file.Modified ||
                    fileInfo.Length.ToString() != file.Size)
                {
                    isUpdate = true;

                    break;
                }
            }

            if (isUpdate)
            {
                e.Cancel = true; // 一旦閉じるのをキャンセル

                var result = await DialogHost.Show(
                    new ConfirmDialog
                    {
                        DataContext = new ConfirmDialogViewModel(App.GetStringResource("UpdateFileEncrypt"))
                    },
                "RootDialog");

                if ((result is bool b && b) || (result is string s && s.Equals("True", StringComparison.OrdinalIgnoreCase)))
                {
                    Debug.WriteLine("yes update!");

                    foreach (FileModel file in App.FileUpdates)
                    {
                        FileInfo fileInfo = new FileInfo(file.Name);

                        if (fileInfo.LastWriteTime != file.Modified ||
                            fileInfo.Length.ToString() != file.Size)
                        {
                            // 更新されたファイルを暗号化して戻す処理
                            EncryptorAesGcm.ProtectFile(file.Name, file.EncryptName);
                        }
                    }
                }
                else
                {
                    Debug.WriteLine("no update");
                }

                // ここでフラグを立てる
                _isConfirmedClose = true;

                // Dispatcher 経由で安全に閉じる
                await Dispatcher.BeginInvoke(new Action(() =>
                {
                    this.Close();
                }), System.Windows.Threading.DispatcherPriority.Background);

            }

            if (this.WindowState == WindowState.Normal)
            {
                Properties.Settings.Default.WindowTop = this.Top;
                Properties.Settings.Default.WindowLeft = this.Left;
                Properties.Settings.Default.WindowWidth = this.Width;
                Properties.Settings.Default.WindowHeight = this.Height;
                Properties.Settings.Default.Save();
            }

            // パイプ書き込みは並列で処理
            await Task.WhenAll(App.PipeWriter.Select(child =>
                Task.Run(() =>
                {
                    try { child.Writer.WriteLine("close"); } catch { }
                })
            ));

            App.PipeWriter.Clear();

            Debug.WriteLine("Onclosing end!");
            // 一時フォルダー削除
            App.RemoveReadOnlyAttributes(App.AppTempPath);

            base.OnClosing(e);
        }

        /// <summary>
        /// ファイルが使用中かを判断する
        /// </summary>
        /// <param name="path">対象ファイルのパス</param>
        /// <returns>true:他プロセスがロック中 false:ロックなし</returns>
        private static bool IsFileLocked(string path)
        {
            FileStream? stream = null;
            try
            {
                stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.None);

                return false; // 開けた＝ロックなし
            }
            catch (IOException)
            {
                return true;  // IOException = 他プロセスがロック中
            }
            catch (UnauthorizedAccessException)
            {
                // これはパーミッション不足の場合もある
                return true;
            }
            finally
            {
                stream?.Close();
            }
        }

        /// <summary>
        /// ウィンドウが画面外に出ている場合は調整
        /// </summary>
        private void EnsureWindowInScreen()
        {
            var screenWidth = SystemParameters.VirtualScreenWidth;
            var screenHeight = SystemParameters.VirtualScreenHeight;

            if (Left < 0 || Top < 0 || Left + Width > screenWidth || Top + Height > screenHeight)
            {
                Left = 100;
                Top = 100;
            }
        }

        /// <summary>
        /// 表示言語の設定(英語)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnSelectEnglish(object sender, RoutedEventArgs e)
        {
            SetLanguage("en");
        }

        /// <summary>
        /// 表示言語の設定(日本語)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnSelectJapanese(object sender, RoutedEventArgs e)
        {
            SetLanguage("ja");
        }

        /// <summary>
        /// アイコンリストの表示
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnSelectIcons(object sender, RoutedEventArgs e)
        {
            var page = MainFrame.Content as Page1;
            if (page != null)
            {
                page.IconDataGridVisibility = Visibility.Visible;
                page.FileDataGridVisibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// ファイルリストの表示
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnSelectDetails(object sender, RoutedEventArgs e)
        {
            var page = MainFrame.Content as Page1;
            if (page != null)
            {
                page.IconDataGridVisibility = Visibility.Collapsed;
                page.FileDataGridVisibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// 表示言語の設定
        /// </summary>
        /// <param name="lang">日本語:ja 英語:en</param>
        private void SetLanguage(string lang)
        {
            App.SetLanguageDictionary(lang);

            Properties.Settings.Default.Language = lang;
            Properties.Settings.Default.Save();

            // ページを再読み込みしてリソース再適用
            var currentPage = MainFrame.Content?.GetType();
            if (currentPage != null)
            {
                if (currentPage.Name == "Page0")
                {
                    MainFrame.Navigate(Activator.CreateInstance(currentPage, this));
                }
                else
                {
                    MainFrame.Navigate(Activator.CreateInstance(currentPage));
                }
            }
        }

        /// <summary>
        /// メニューボタン活性化
        /// </summary>
        public void MenuEnableOn()
        {
            this.ChangeDisplay.Visibility = Visibility.Visible;
            this.Settings.IsEnabled = true;
            this.Settings.Foreground = Brushes.WhiteSmoke;
        }

        /// <summary>
        /// 設定ウィンドウ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnSettings(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("OnSettings!");

            MainFrame.Navigate(new Page0(this)); // 設定ページ             
        }

        /// <summary>
        /// メインウィンドウの表示
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnTopWindow(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new Page1()); // 暗号化ウィンドウ         
        }

        /// <summary>
        /// 親ウィンドウからの指令処理
        /// </summary>
        /// <param name="command">minimize:ウインドウの最小化 restore:ウインドウの再表示</param>
        void BroadcastCommand(string command)
        {
            foreach (var child in App.PipeWriter.ToList()) // ToListで列挙中に削除OK
            {
                if (!child.IsAlive)
                {
                    App.PipeWriter.Remove(child);
                    continue;
                }

                try
                {
                    child.Writer.WriteLine(command);
                }
                catch (ObjectDisposedException)
                {
                    Debug.WriteLine($"Writer for {child.VideoPath} already closed.");
                    App.PipeWriter.Remove(child);
                }
                catch (IOException ioEx)
                {
                    Debug.WriteLine($"Pipe error for {child.VideoPath}: {ioEx.Message}");
                    App.PipeWriter.Remove(child);
                }
            }
        }

        /// <summary>
        /// ウインドウのリサイズ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MetroWindow_StateChanged(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Minimized)
            {
                BroadcastCommand("minimize");
            }
            else if (this.WindowState == WindowState.Normal)
            {
                BroadcastCommand("restore");
            }
        }

    }
}