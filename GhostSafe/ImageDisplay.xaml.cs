using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GhostSafe
{
    /// <summary>
    /// ImageDisplay.xaml の相互作用ロジック
    /// </summary>
    public partial class ImageDisplay : Window
    {
        public ImageDisplay()
        {
            InitializeComponent();
        }

        /// <summary>
        /// ファイルパスまたは pack URI を指定して画像を読み込む
        /// </summary>
        /// <param name="filePath">対象ファイルのパス</param>
        /// <param name="fileNamePath">対象ファイルの復号化ファイル名のパス</param>
        public void LoadImage(string filePath, string fileNamePath)
        {
            try
            {
                BitmapImage bmp = new BitmapImage();
                bmp.BeginInit();

                // ファイルから直接読み込む（OnLoad でメモリへコピーしてファイルロックを解除）
                bmp.UriSource = new Uri(filePath, UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;

                bmp.EndInit();
                bmp.Freeze(); // スレッドセーフにしておく（任意）

                DisplayedImage.Source = bmp;
                this.Title = $"{System.IO.Path.GetFileName(fileNamePath)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"画像の読み込みに失敗しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
