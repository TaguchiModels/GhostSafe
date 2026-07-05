using GhostSafe.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using System.Windows.Threading;

namespace GhostSafe
{
    /// <summary>
    /// ImageDisplay.xaml の相互作用ロジック　
    /// </summary>
    public partial class ImageDisplay : Window
    {
        private List<KeyValuePair<string, string>> sortedList = new List<KeyValuePair<string, string>>();
        private int currentIndex = -1;
        private string ghostPath = "";
        private string appdataPath = "";　
        private readonly DispatcherTimer slideTimer = new();

        public ImageDisplay()
        {
            InitializeComponent();

            MouseMove += ImageDisplay_MouseMove;
            MouseLeave += ImageDisplay_MouseLeave;

            slideTimer.Tick += SlideTimer_Tick;
        }

        /// <summary>
        /// スライダータイマー
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SlideTimer_Tick(object? sender, EventArgs e)
        {
            NextImage();
        }

        /// <summary>
        /// 左右80px付近にカーソルが来た時だけ表示
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ImageDisplay_MouseMove(object sender, MouseEventArgs e)
        {
            Point p = e.GetPosition(this);

            double area = 80;

            PrevButton.Visibility =
                (p.X < area) ? Visibility.Visible : Visibility.Collapsed;

            NextButton.Visibility =
                (p.X > ActualWidth - area) ? Visibility.Visible : Visibility.Collapsed;

            TimeBorder.Visibility =
                (p.X > ActualWidth - area * 5) ? Visibility.Visible : Visibility.Collapsed;

            TimeStop.Visibility =
                (p.X > ActualWidth - area * 5) ? Visibility.Visible : Visibility.Collapsed;

            Time1s.Visibility =
                (p.X > ActualWidth - area * 5) ? Visibility.Visible : Visibility.Collapsed;

            Time3s.Visibility =
                (p.X > ActualWidth - area * 5) ? Visibility.Visible : Visibility.Collapsed;

            Time5s.Visibility =
                (p.X > ActualWidth - area * 5) ? Visibility.Visible : Visibility.Collapsed;

            Time15s.Visibility =
                (p.X > ActualWidth - area * 5) ? Visibility.Visible : Visibility.Collapsed;

            Time30s.Visibility =
                (p.X > ActualWidth - area * 5) ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// 矢印ボタンの折りたたみ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ImageDisplay_MouseLeave(object sender, MouseEventArgs e)
        {
            PrevButton.Visibility = Visibility.Collapsed;
            NextButton.Visibility = Visibility.Collapsed;
            TimeBorder.Visibility = Visibility.Collapsed;
            TimeStop.Visibility = Visibility.Collapsed;
            Time1s.Visibility = Visibility.Collapsed;
            Time3s.Visibility = Visibility.Collapsed;
            Time5s.Visibility = Visibility.Collapsed;
            Time15s.Visibility = Visibility.Collapsed;
            Time30s.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// ファイルパスまたは pack URI を指定して画像を読み込む
        /// </summary>
        /// <param name="filePath">対象画像ファイルのAppdataパス</param>
        /// <param name="fileNamePath">対象ファイルの.ghostsafeファイルのパス</param>
        public void LoadImage(string filePath, string fileNamePath)
        {
            // 検索する拡張子を HashSet で高速化（大文字小文字を無視）
            var targetExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".bmp", ".jpeg", ".jpg", ".png", ".gif", ".tiff", ".ico", ".webp"
            };

            try
            {
                ghostPath = Path.GetDirectoryName(fileNamePath);
                appdataPath = Path.GetDirectoryName(filePath);

                if (ghostPath == null || appdataPath == null) return;

                // 1. 画像フォルダ内のファイルを列挙し、対象の拡張子のみに絞り込む（この段階ではソートしない）
                var imageFiles = Directory.EnumerateFiles(appdataPath)
                    .Where(f => targetExtensions.Contains(Path.GetExtension(f)));

                var sortFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                // 2. 対応する .ghostsafe ファイルが存在するものだけ処理
                foreach (string imgPath in imageFiles)
                {
                    string baseName = Path.GetFileNameWithoutExtension(imgPath);
                    string ghostFile = Path.Combine(ghostPath, baseName + ".ghostsafe");

                    // .ghostsafe ファイルが存在する場合のみ読み込む
                    if (File.Exists(ghostFile))
                    {
                        string[] names = EncryptorAesGcm.UnprotectText(ghostFile).Split('/');

                        sortFiles.Add(ghostFile, names[0]);
                    }
                }

                // 3. 値（Value）でソート
                sortedList = sortFiles.OrderBy(pair => pair.Value).ToList();

                // ソート結果を出力
                foreach (var pair in sortedList)
                {
                    Debug.WriteLine($"{pair.Key}: {pair.Value}");
                }

                // 4. ソート後のリストから、対象の fileNamePath のインデックスを探す（バグ修正）
                currentIndex = sortedList.FindIndex(pair =>
                    string.Equals(pair.Key, fileNamePath, StringComparison.OrdinalIgnoreCase));

                ShowCurrentImage();

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        /// <summary>
        /// 画像の表示
        /// </summary>
        private void ShowCurrentImage()
        {
            if (currentIndex < 0 || currentIndex >= sortedList.Count)
                return;

            string ext = Path.GetExtension(sortedList[currentIndex].Value);
            string noext = Path.GetFileNameWithoutExtension(sortedList[currentIndex].Key);
            string file = Path.Combine(appdataPath, noext + ext);
            BitmapImage bmp = new BitmapImage();

            if (!File.Exists(file))
            {
                this.Title = "404 not found!";
                bmp.BeginInit();
                // Pack URI スキームを使用します（プロジェクト名が「MyApp」の場合）
                bmp.UriSource = new Uri("pack://application:,,,/images/notFound404.png", UriKind.Absolute);
                // もし上の記述で動かない場合は、以下のような簡略表記も試してください
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                DisplayedImage.Source = bmp;
                return;
            }

            bmp.BeginInit();
            bmp.UriSource = new Uri(file);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();

            DisplayedImage.Source = bmp;

            this.Title = $"{System.IO.Path.GetFileName(sortedList[currentIndex].Value)}"; // 実際のファイル名

        }

        /// <summary>
        /// 前へボタン
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            if (sortedList.Count == 0)
                return;

            currentIndex--;

            if (currentIndex < 0)
                currentIndex = sortedList.Count - 1;

            ShowCurrentImage();
        }

        /// <summary>
        /// 次へボタン
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            NextImage();
        }

        /// <summary>
        /// 次へボタン共通化
        /// </summary>
        private void NextImage()
        {
            if (sortedList.Count == 0)
                return;

            currentIndex++;

            if (currentIndex >= sortedList.Count)
                currentIndex = 0;

            ShowCurrentImage();
        }

        /// <summary>
        /// タイマー停止
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TimeStop_Click(object sender, RoutedEventArgs e)
        {
            slideTimer.Stop();
            return;
        }

        /// <summary>
        /// タイマー 1秒間隔
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Time1s_Click(object sender, RoutedEventArgs e)
        {
            slideTimer.Interval = TimeSpan.FromSeconds(1);

            if (!slideTimer.IsEnabled)
                slideTimer.Start();
        }

        /// <summary>
        /// タイマー 3秒間隔
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Time3s_Click(object sender, RoutedEventArgs e)
        {
            slideTimer.Interval = TimeSpan.FromSeconds(3);

            if (!slideTimer.IsEnabled)
                slideTimer.Start();
        }

        /// <summary>
        /// タイマー 5秒間隔
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Time5s_Click(object sender, RoutedEventArgs e)
        {
            slideTimer.Interval = TimeSpan.FromSeconds(5);

            if (!slideTimer.IsEnabled)
                slideTimer.Start();
        }

        /// <summary>
        /// タイマー 15秒間隔
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Time15s_Click(object sender, RoutedEventArgs e)
        {
            slideTimer.Interval = TimeSpan.FromSeconds(15);

            if (!slideTimer.IsEnabled)
                slideTimer.Start();
        }

        /// <summary>
        /// タイマー 30秒間隔
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Time30s_Click(object sender, RoutedEventArgs e)
        {
            slideTimer.Interval = TimeSpan.FromSeconds(30);

            if (!slideTimer.IsEnabled)
                slideTimer.Start();

        }
    }
}
