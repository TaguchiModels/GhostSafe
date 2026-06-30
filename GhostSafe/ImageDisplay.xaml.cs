using GhostSafe.Common;
using System;
using System.Collections.Generic;
using System.IO;
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

    /// <summary>
    /// ImageDisplay.xaml の相互作用ロジック
    /// </summary>
    //public partial class ImageDisplay : Window
    //{
    //    private List<string> fileNamePaths = new();
    //    private List<string> imageFiles = new();
    //    private int currentIndex = -1;
    //    private readonly string[] ghostfiles = { ".ghostsafe" };
    //    private readonly string[] extensions =
    //    {
    //        ".bmp", ".jpeg", ".jpg", ".png",
    //        ".gif", ".tiff", ".ico", ".webp"
    //    };

    //    public ImageDisplay()
    //    {
    //        InitializeComponent();

    //        MouseMove += ImageDisplay_MouseMove;
    //        MouseLeave += ImageDisplay_MouseLeave;
    //    }

    //    /// <summary>
    //    /// 左右80px付近にカーソルが来た時だけ表示
    //    /// </summary>
    //    /// <param name="sender"></param>
    //    /// <param name="e"></param>
    //    private void ImageDisplay_MouseMove(object sender, MouseEventArgs e)
    //    {
    //        Point p = e.GetPosition(this);

    //        double area = 80;

    //        PrevButton.Visibility =
    //            (p.X < area) ? Visibility.Visible : Visibility.Collapsed;

    //        NextButton.Visibility =
    //            (p.X > ActualWidth - area) ? Visibility.Visible : Visibility.Collapsed;
    //    }

    //    /// <summary>
    //    /// 矢印ボタンの折りたたみ
    //    /// </summary>
    //    /// <param name="sender"></param>
    //    /// <param name="e"></param>
    //    private void ImageDisplay_MouseLeave(object sender, MouseEventArgs e)
    //    {
    //        PrevButton.Visibility = Visibility.Collapsed;
    //        NextButton.Visibility = Visibility.Collapsed;
    //    }


    //    /// <summary>
    //    /// ファイルパスまたは pack URI を指定して画像を読み込む
    //    /// </summary>
    //    /// <param name="filePath">対象ファイルのパス</param>
    //    /// <param name="fileNamePath">対象ファイルの復号化ファイル名のパス</param>
    //    public void LoadImage(string filePath, string fileNamePath)
    //    {

    //        try
    //        {
    //            string ghostPath = System.IO.Path.GetDirectoryName(fileNamePath);

    //            fileNamePaths = Directory
    //                .GetFiles(ghostPath)
    //                .Where(f => ghostfiles.Contains(
    //                        Path.GetExtension(f).ToLower()))
    //                .OrderBy(f => f)
    //                .ToList();

    //            string folder = Path.GetDirectoryName(filePath);

    //            imageFiles = Directory
    //                .GetFiles(folder)
    //                .Where(f => extensions.Contains(
    //                        Path.GetExtension(f).ToLower()))
    //                .OrderBy(f => f)
    //                .ToList();

    //            currentIndex = imageFiles.FindIndex(x =>
    //                string.Equals(x,
    //                              filePath,
    //                              StringComparison.OrdinalIgnoreCase));

    //            ShowCurrentImage();

    //        }
    //        catch (Exception ex)
    //        {
    //            MessageBox.Show(ex.Message);
    //        }
    //    }

    //    /// <summary>
    //    /// 画像の表示
    //    /// </summary>
    //    private void ShowCurrentImage()
    //    {
    //        if (currentIndex < 0 || currentIndex >= imageFiles.Count)
    //            return;

    //        string file = imageFiles[currentIndex];

    //        if (!File.Exists(file))
    //        {
    //            DisplayedImage.Source = null;
    //            this.Title = "no image";
    //            return;
    //        }

    //        BitmapImage bmp = new BitmapImage();

    //        bmp.BeginInit();
    //        bmp.UriSource = new Uri(file);
    //        bmp.CacheOption = BitmapCacheOption.OnLoad;
    //        bmp.EndInit();
    //        bmp.Freeze();

    //        DisplayedImage.Source = bmp;

    //        string[] names = EncryptorAesGcm.UnprotectText(fileNamePaths[currentIndex]).Split('/');
    //        this.Title = $"{System.IO.Path.GetFileName(names[0])}"; // 実際のファイル名

    //    }

    //    /// <summary>
    //    /// 前へボタン
    //    /// </summary>
    //    /// <param name="sender"></param>
    //    /// <param name="e"></param>
    //    private void PrevButton_Click(object sender, RoutedEventArgs e)
    //    {
    //        if (imageFiles.Count == 0)
    //            return;

    //        currentIndex--;

    //        if (currentIndex < 0)
    //            currentIndex = imageFiles.Count - 1;

    //        ShowCurrentImage();
    //    }

    //    /// <summary>
    //    /// 次へボタン
    //    /// </summary>
    //    /// <param name="sender"></param>
    //    /// <param name="e"></param>
    //    private void NextButton_Click(object sender, RoutedEventArgs e)
    //    {
    //        if (imageFiles.Count == 0)
    //            return;

    //        currentIndex++;

    //        if (currentIndex >= imageFiles.Count)
    //            currentIndex = 0;

    //        ShowCurrentImage();
    //    }

    //}
}
