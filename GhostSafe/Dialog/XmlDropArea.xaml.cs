using GhostSafe.Common;
using MaterialDesignThemes.Wpf;
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
using System.Windows.Navigation;
using System.Windows.Threading;

namespace GhostSafe.Dialog
{
    /// <summary>
    /// XmlDropArea.xaml の相互作用ロジック
    /// </summary>
    public partial class XmlDropArea : UserControl
    {
        public XmlDropArea()
        {
            InitializeComponent();
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
        /// ファイルがコントロールにドロップされた際に呼び出され、
        /// XML ファイルからフォルダ構造を復元する
        /// </summary>
        /// <remarks>
        /// 本イベントハンドラは、ドラッグ＆ドロップで渡されたファイルのうち、
        /// 最初の 1 件のみを対象として処理します。
        /// <para>
        /// 処理中はプログレスバーを表示し、
        /// フォルダ復元処理（XML → フォルダ変換）を
        /// バックグラウンドスレッドで実行します。
        /// </para>
        /// <para>
        /// UI 要素の更新は <see cref="Dispatcher"/> を介して行い、
        /// 処理完了後はダイアログを自動的に閉じます。
        /// </para>
        /// </remarks>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void UserControl_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                int total = files.Length;

                // プログレスバー初期化
                EncryptionProgressBar.Visibility = Visibility.Visible;

                await Task.Run(() =>
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        EncryptionProgressBar.IsActive = true;
                    }, DispatcherPriority.Background);

                    for (int i = 0; i < total; i++)
                    {
                        string file = files[i];
                        FolderVsXml.XmlToFolder(file);
                        break; // 最初の XML 以外は対象外
                    }
                });

                EncryptionProgressBar.Visibility = Visibility.Collapsed;

                DialogHost.CloseDialogCommand.Execute(null, this);
            }
        }
    }
}
