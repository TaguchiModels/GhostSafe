using GhostSafe.Common;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
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
using System.Windows.Navigation;
using System.Windows.Threading;

namespace GhostSafe.Dialog
{
    /// <summary>
    /// EncryptDropWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class EncryptDropWindow : Window
    {
        public bool Cancel = false;
        private string TargetFolder = "";

        /// <summary>
        /// 初期ロード
        /// </summary>
        /// <param name="targetFolder">対象フォルダーのパス</param>
        public EncryptDropWindow(string targetFolder)
        {
            InitializeComponent();
            TargetFolder = targetFolder;
        }

        /// <summary>
        /// ドラッグ操作がコントロール領域に入った際に呼び出され、
        /// ドロップ可能なデータかどうかを判定する
        /// </summary>
        /// <remarks>
        /// 本イベントハンドラは、ドラッグされているデータに
        /// ファイル（<see cref="DataFormats.FileDrop"/>）が含まれている場合のみ、
        /// コピー操作としてドロップ可能であることを示します。
        /// それ以外のデータ形式の場合は、ドロップ不可として扱います。
        /// </remarks>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UserControl_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
        }

        /// <summary>
        /// フォルダーまたはファイルがドラッグ＆ドロップされたら
        /// 暗号化を行う。フォルダーの場合は再帰的に暗号化を行う
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void UserControl_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop);

                int count = 0;
                foreach (string path in paths)
                {
                    if (Directory.Exists(path))
                        count += Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Count();
                    else if (File.Exists(path))
                        ++count;
                }

                EncryptionProgressBar.Visibility = Visibility.Visible;
                EncryptionProgressBar.IsActive = true;

                int ctr = 0;

                await Task.Run(() =>
                {
                    foreach (string path in paths)
                    {
                        if (Cancel) return;

                        if (Directory.Exists(path))
                            EncryptFolderRecursive(path, TargetFolder, ref ctr, count);
                        else if (File.Exists(path))
                            EncryptSingleFileWithUpdate(path, TargetFolder, ref ctr, count);
                    }
                });

                Dispatcher.Invoke(() =>
                {
                    EncryptionProgressBar.Visibility = Visibility.Collapsed;
                    Infomation.Text = "..completed!";
                    this.Close();
                });
            }
        }

        /// <summary>
        /// 指定されたフォルダ配下のファイルおよびサブフォルダを、
        /// フォルダ構造を維持したまま再帰的に暗号化します。
        /// </summary>
        /// <remarks>
        /// 本メソッドは、<paramref name="sourceDir"/> の内容を走査し、
        /// 対応する出力先フォルダを <paramref name="destRoot"/> 配下に作成したうえで、
        /// 各ファイルを暗号化します。
        /// <para>
        /// 処理の進捗管理のため、暗号化済みファイル数は
        /// <paramref name="ctr"/> によって呼び出し元と共有されます。
        /// </para>
        /// <para>
        /// <see cref="Cancel"/> フラグが true の場合は、
        /// 処理を途中で中断し、以降のファイルおよびフォルダの処理は行われません。
        /// </para>
        /// </remarks>
        /// <param name="sourceDir">暗号化対象となる元フォルダのパス</param>
        /// <param name="destRoot">暗号化後のフォルダを作成するルートディレクトリのパス</param>
        /// <param name="ctr">
        /// 現在までに暗号化が完了したファイル数を表すカウンタ。
        /// 参照渡しで更新されます。
        /// </param>
        /// <param name="totalCount">
        /// 暗号化対象となる総ファイル数。
        /// 主に進捗表示用に使用されます。
        /// </param>
        private void EncryptFolderRecursive(string sourceDir, string destRoot, ref int ctr, int totalCount)
        {
            if (Cancel) return;

            string folderName = Path.GetFileName(sourceDir);
            string destDir = Path.Combine(destRoot, folderName);
            Directory.CreateDirectory(destDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                if (Cancel) return;

                EncryptSingleFileWithUpdate(file, destDir, ref ctr, totalCount);
            }

            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                if (Cancel) return;

                EncryptFolderRecursive(subDir, destDir, ref ctr, totalCount);
            }
        }

        /// <summary>
        /// 単一ファイルを暗号化し、進捗カウンタおよび UI 表示を更新する
        /// </summary>
        /// <remarks>
        /// 本メソッドは、指定されたファイルを暗号化した後、
        /// スレッドセーフに進捗カウンタをインクリメントし、
        /// 現在の処理状況を UI に反映します。
        /// <para>
        /// カウンタ更新には <see cref="Interlocked.Increment(ref int)"/> を使用し、
        /// 並列処理環境においても正確な進捗管理を行います。
        /// </para>
        /// <para>
        /// UI の更新は <see cref="Dispatcher.Invoke"/> を介して行われ、
        /// バックグラウンドスレッドから安全に UI スレッドを更新します。
        /// </para>
        /// </remarks>
        /// <param name="sourceFile">暗号化対象となる元ファイルのパス</param>
        /// <param name="destFolder">暗号化後のファイルを保存するフォルダのパス</param>
        /// <param name="ctr">
        /// 処理済みファイル数を表す進捗カウンタ。
        /// 参照渡しで更新されます。
        /// </param>
        /// <param name="totalCount">
        /// 処理対象となる総ファイル数。
        /// 主に進捗表示用に使用されます。
        /// </param>
        private void EncryptSingleFileWithUpdate(string sourceFile, string destFolder, ref int ctr, int totalCount)
        {
            EncryptSingleFile(sourceFile, destFolder);

            // カウンター更新
            int newValue = Interlocked.Increment(ref ctr);

            // UI更新はDispatcherで
            Dispatcher.Invoke(() =>
            {
                Infomation.Text = $"{newValue} / {totalCount} is running...";
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        /// <summary>
        /// キャンセルボタン クリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            Cancel = true;
            this.Close();
        }

        /// <summary>
        /// 単一ファイルを暗号化し、対応するメタ情報および必要に応じて
        /// 動画サムネイルを生成・暗号化する
        /// </summary>
        /// <remarks>
        /// 本メソッドは、指定されたファイルを暗号化し、
        /// ランダムに生成したファイル名で出力先フォルダに保存します。
        /// <para>
        /// 元のファイル名と暗号化後ファイル名の対応関係は、
        /// 専用のメタ情報ファイル（.GhostSafe）として別途暗号化して保存されます。
        /// </para>
        /// <para>
        /// 対象ファイルが動画形式（mp4, avi 等）の場合は、
        /// 再生可能であることを確認したうえでサムネイル画像を生成し、
        /// そのサムネイルも暗号化して保存します。
        /// </para>
        /// <para>
        /// <see cref="Cancel"/> フラグが true の場合は、
        /// 処理を即座に中断します。
        /// </para>
        /// <para>
        /// 本メソッドは非同期メソッド（async void）であり、
        /// 主にイベントハンドラや fire-and-forget の用途を想定しています。
        /// </para>
        /// </remarks>
        /// <param name="sourceFile">暗号化対象となる元ファイルのパス</param>
        /// <param name="destFolder">暗号化後のファイルおよび関連情報を保存するフォルダのパス</param>
        private async void EncryptSingleFile(string sourceFile, string destFolder)
        {
            if (Cancel) { return; }

            string originalFileName = Path.GetFileName(sourceFile);
            string extension = Path.GetExtension(sourceFile);
            string randomString = EncryptorAesGcm.GenerateRandomString(20);
            string encryptedFileName = randomString + extension;
            string encryptedFilePath = Path.Combine(destFolder, encryptedFileName);

            EncryptorAesGcm.ProtectFile(sourceFile, encryptedFilePath);

            // 元のファイル名と対応情報を保存
            string nameFilePath = Path.Combine(destFolder, randomString + ".GhostSafe");
            EncryptorAesGcm.ProtectText(originalFileName + "/" + randomString + extension, nameFilePath);


            if (extension == ".mp4" || extension == ".avi" ||
               extension == ".wmv" || extension == ".flv" ||
               extension == ".mov" || extension == ".webm" ||
               extension == ".m4v" || extension == ".3gp" ||
               extension == ".asf" || extension == ".mkv")
            {
                var rtc = PresenceVideoCodecs.GetPresenceVideoCodecs(sourceFile);
                if (rtc)
                {
                    string rondomstr = EncryptorAesGcm.GenerateRandomString(20);
                    var bmp = await VideoThumbnailProvider.GetThumbnailAsync(sourceFile, 256, 256);
                    string workPath = Path.Combine(destFolder, "workImage_" + rondomstr + ".jpg");
                    bmp.Save(workPath, ImageFormat.Jpeg);

                    string encryptedJpgPath = Path.Combine(destFolder, randomString + ".jpg");
                    EncryptorAesGcm.ProtectFile(workPath, encryptedJpgPath);

                    File.Delete(workPath);

                }

            }
        }

    }
}
