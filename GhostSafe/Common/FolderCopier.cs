using GhostSafe.Dialog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace GhostSafe.Common
{
    class FolderCopier
    {
        /// <summary>
        /// フォルダー配下のファイルを復号してダウンロードする
        /// </summary>
        /// <param name="sourceFolderPath">ダウンロード元のフォルダーのパス</param>
        public async static void CopyToDownloads(string sourceFolderPath)
        {
            // コピー先のフォルダー名（元フォルダー名と同じ名前で作成）
            string folderName = new DirectoryInfo(sourceFolderPath).Name;
            string destinationPath = Path.Combine(Properties.Settings.Default.DownloadFolder, folderName);

            try
            {
                CopyDirectoryRecursive(sourceFolderPath, destinationPath);
                // UI スレッドでダイアログ表示
                await Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                   await ShowDialog.ShowDialogsAsync(App.GetStringResource("ProcessComplete"));
                });
            }
            catch (Exception ex)
            {
                // UI スレッドでダイアログ表示
                await Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    await ShowDialog.ShowDialogsAsync(App.GetStringResource("DownloadFailure"));
                });
            }
        }

        /// <summary>
        /// 再帰的にフォルダー配下のファイルを復号する
        /// </summary>
        /// <param name="sourceDir">ダウンロード元のフォルダーのパス</param>
        /// <param name="destinationDir">ダウンロード先のフォルダーのパス</param>
        private static async void CopyDirectoryRecursive(string sourceDir, string destinationDir)
        {
            // フォルダーを作成（存在しない場合）
            Directory.CreateDirectory(destinationDir);

            // .ghostsafeファイル（ファイル名を保存したファイル）だけ取得
            var encNameFiles = Directory.GetFiles(sourceDir, "*.ghostsafe");

            foreach (var encNameFile in encNameFiles)
            {
                try
                {
                    // 親ディレクトリのパスを取得
                    string parentDirectoryPath = Path.GetDirectoryName(encNameFile);
                    string randomName = Path.GetFileNameWithoutExtension(encNameFile);
                    string unencText = EncryptorAesGcm.UnprotectText(encNameFile);
                    string[] spritName = unencText.Trim().Split('/');
                    string originalFileName = spritName[0];
                    string encryptFileName = spritName[1];
                    // 拡張子を元のファイルから復元
                    string originalExtension = Path.GetExtension(originalFileName);
                    string encryptedFileName = randomName + originalExtension;
                    string encryptedFilePath = Path.Combine(sourceDir, encryptedFileName);


                    string download = destinationDir + @"\" + originalFileName;
                    string withoutExtension = originalFileName.Replace(originalExtension, "");

                    for (int i = 1; i <= 100; i++)
                    {
                        if (!File.Exists(download))
                        {
                            break;
                        }

                        download = destinationDir + @"\" + withoutExtension + "(" + i + ")" + originalExtension;
                    }

                    EncryptorAesGcm.UnprotectFile(encryptedFilePath, download);
                }
                catch (Exception ex)
                {
                    // ログなどに出力（オプション）
                    Debug.WriteLine($"Error loading file info from {encNameFile}: {ex.Message}");
                    // UI スレッドでダイアログ表示
                    await Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                       await ShowDialog.ShowDialogsAsync(App.GetStringResource("DownloadFailure"));
                    });

                    return;
                }
            }

            // サブディレクトリを再帰的にコピー
            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string subDirName = Path.GetFileName(subDir);
                string destSubDir = Path.Combine(destinationDir, subDirName);

                CopyDirectoryRecursive(subDir, destSubDir);
            }
        }
    }
}
