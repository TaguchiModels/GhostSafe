using ControlzEx.Standard;
using GhostSafe.Common;
using GhostSafe.Dialog;
using GhostSafe.ViewModel;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace GhostSafe
{
    /// <summary>
    /// Page0.xaml の相互作用ロジック
    /// </summary>
    public partial class Page0 : Page
    {
        private MainWindow _mainWindow;
        public Page0(MainWindow mainWindow)
        {
            InitializeComponent();

            DownloadFolder.Text = Properties.Settings.Default.DownloadFolder;
            EncryptFolder.Text = Properties.Settings.Default.EncryptFolder;

            _mainWindow = mainWindow;

            if (string.IsNullOrEmpty(DownloadFolder.Text))
            {   // ダウンロードフォルダーがなかったら、保管場所をデスクトップで作成する
                DownloadFolder.Text = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            }

            if (string.IsNullOrEmpty(EncryptFolder.Text))
            {   // 暗号化フォルダーがなかったら、保管場所を Appdata で作成する
                EncryptFolder.Text = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "GhostSafeFiles");
            }

            // パスワードの復号化
            if (!string.IsNullOrEmpty(Properties.Settings.Default.Password))
            {
                byte[] data = Convert.FromBase64String(Properties.Settings.Default.Password);
                byte[] decrypted = ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
                this.passwordBox.Password = Encoding.UTF8.GetString(decrypted);
                this.passwordHeader.Header = App.GetStringResource("PasswordSettingNg");
                this.passwordHeader.IsExpanded = false;
                this.passwordBox.IsEnabled = false;
            }
        }

        /// <summary>
        /// 設定を保存する
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(DownloadFolder.Text))
            {
                await ShowDialog.ShowDialogsAsync(App.GetStringResource("DownloadFolderRequest"));
                return;
            }

            if (!Directory.Exists(DownloadFolder.Text))
            {
                await ShowDialog.ShowDialogsAsync(App.GetStringResource("DownloadFolderNotFound"));
                return;
            }

            if (string.IsNullOrEmpty(EncryptFolder.Text))
            {
                await ShowDialog.ShowDialogsAsync(App.GetStringResource("EncryptFolderRequest"));
                return;
            }

            if (!Directory.Exists(EncryptFolder.Text))
            {
                await ShowDialog.ShowDialogsAsync(App.GetStringResource("EncryptFolderNotFound"));
                return;
            }

            const int MinLength = 4;
            const int MaxLength = 24;

            // パスワード4桁未満 or 24桁超の英数字はエラー
            var pattern = $"^[a-zA-Z0-9]{{{MinLength},{MaxLength}}}$";
            var result = Regex.IsMatch(this.passwordBox.Password, pattern);

            Debug.WriteLine($"result:{result}");

            if (!result)
            {
                await ShowDialog.ShowDialogsAsync(App.GetStringResource("PasswordCharFailure"));
                return;
            }

            // パスワードの復号化
            if (string.IsNullOrEmpty(Properties.Settings.Default.Password))
            {
                string passwordConfirm = App.GetStringResource("PasswordConfirm")
                                       + Environment.NewLine + "\"" + this.passwordBox.Password + "\"";

                var result2 = await DialogHost.Show(
                              new ConfirmDialog { DataContext = new ConfirmDialogViewModel(passwordConfirm) },
                              "RootDialog");

                if ((result2 is bool b && b) || (result2 is string s && s.Equals("True", StringComparison.OrdinalIgnoreCase)))
                {
                    PasswordSet();

                    await ShowDialog.ShowDialogsAsync(App.GetStringResource("SettingSaved"));

                    _mainWindow.MenuEnableOn(); // 親のメニューを活性化

                    ((MainWindow)Application.Current.MainWindow).MainFrame.Navigate(new Page1());
                }
            }
            else
            {
                PasswordSet();

                await ShowDialog.ShowDialogsAsync(App.GetStringResource("SettingSaved"));

                _mainWindow.MenuEnableOn(); // 親のメニューを活性化

                ((MainWindow)Application.Current.MainWindow).MainFrame.Navigate(new Page1());
            }

        }

        /// <summary>
        /// パスワードセット
        /// </summary>
        private void PasswordSet()
        {
            // パスワードの暗号化
            string plainText = this.passwordBox.Password;

            byte[] encrypted = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(plainText),
                null,
                DataProtectionScope.CurrentUser
            );

            string base64 = Convert.ToBase64String(encrypted);
            Properties.Settings.Default.Password = base64;
            Properties.Settings.Default.DownloadFolder = DownloadFolder.Text;
            Properties.Settings.Default.EncryptFolder = EncryptFolder.Text;
            Properties.Settings.Default.InitialSetting = true;
            Properties.Settings.Default.Save();

            App.AppDataPath = Path.Combine(Properties.Settings.Default.EncryptFolder, "GhostSafeFiles");

            if (!Directory.Exists(App.AppDataPath))
                Directory.CreateDirectory(App.AppDataPath);

            if (!Directory.Exists(App.AppTempPath))
                Directory.CreateDirectory(App.AppTempPath);

            App.MasterKey = EncryptorAesGcm.CreateMasterKey(); // 起動時に1回だけマスターキーを作る
        }

        /// <summary>
        /// 暗号化フォルダーを指定する場合 Check off
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EncryptCheck_Click(object sender, RoutedEventArgs e)
        {
            EncryptFolder.IsEnabled = EncryptCheck.IsChecked == true ? false : true;
        }
    }
}
