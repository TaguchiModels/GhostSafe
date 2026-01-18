using GhostSafe.Dialog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace GhostSafe
{
    /// <summary>
    /// PageLogin.xaml の相互作用ロジック
    /// </summary>
    public partial class PageLogin : Page
    {
        public PageLogin()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Hyperlink
        /// </summary>
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        /// <summary>
        /// Keyborad enter
        /// </summary>
        private async void passwordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (string.IsNullOrEmpty(this.passwordBox.Password)) { return; }

                // パスワードの復号化
                byte[] data = Convert.FromBase64String(Properties.Settings.Default.Password);
                byte[] decrypted = ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
                string inputPassword = Encoding.UTF8.GetString(decrypted);

                Debug.WriteLine("inputPassword:" + inputPassword);

                if (inputPassword != this.passwordBox.Password)
                {
                    await ShowDialog.ShowDialogsAsync(App.GetStringResource("PasswordFailure"));
                    return;
                }

                var parentWindow = MainWindow.GetWindow(this) as MainWindow;

                if (parentWindow != null)
                {
                    parentWindow.ChangeDisplay.Visibility = Visibility.Visible;
                    parentWindow.Settings.IsEnabled = true;
                    parentWindow.Settings.Foreground = Brushes.WhiteSmoke;
                }

                ((MainWindow)Application.Current.MainWindow).MainFrame.Navigate(new Page1());

            }
        }

    }
}
