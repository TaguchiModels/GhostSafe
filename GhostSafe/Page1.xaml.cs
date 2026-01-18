using ControlzEx.Standard;
using MahApps.Metro.Controls.Dialogs;
using MaterialDesignThemes.Wpf;
using GhostSafe.Common;
using GhostSafe.Dialog;
using GhostSafe.ViewModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
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
using static MaterialDesignThemes.Wpf.Theme;

namespace GhostSafe
{
    /// <summary>
    /// Page1.xaml の相互作用ロジック
    /// </summary>
    public partial class Page1 : System.Windows.Controls.Page
    {
        public static readonly DependencyProperty DataGridVisibilityProperty =
            DependencyProperty.Register(
            "DataGridVisibility",
            typeof(Visibility),
            typeof(Page1),
            new PropertyMetadata(Visibility.Visible)
        );

        public Visibility DataGridVisibility
        {
            get { return (Visibility)GetValue(DataGridVisibilityProperty); }
            set { SetValue(DataGridVisibilityProperty, value); }
        }

        public Visibility FileDataGridVisibility
        {
            get { return FileDataGrid.Visibility; }
            set { FileDataGrid.Visibility = value; }
        }
        public Visibility IconDataGridVisibility
        {
            get { return ThumbnailList.Visibility; }
            set { ThumbnailList.Visibility = value; }
        }

        public Page1()
        {
            InitializeComponent();

            FileDataGrid.SetBinding(System.Windows.Controls.DataGrid.VisibilityProperty, new Binding("DataGridVisibility") { Source = this });
            ThumbnailList.SetBinding(System.Windows.Controls.DataGrid.VisibilityProperty, new Binding("DataGridVisibility") { Source = this });

        }

        /// <summary>
        /// 初期ページロード
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(App.AppDataPath))
                Directory.CreateDirectory(App.AppDataPath);

            var root = new TreeViewItem
            {
                Header = "Safe Folders",
                Tag = App.AppDataPath,
                ContextMenu = CreateContextMenuForFolder(isRoot: true)
            };

            // 🔽 ダミーで [+] を表示（Lazy Load）
            root.Items.Add(null);
            root.Expanded += Folder_Expanded;

            FolderTreeView.Items.Add(root);

            // 起動時にルートを展開（Expanded イベントが発火）
            root.IsExpanded = true;

        }

        /// <summary>
        /// 左ペイン：右クリックメニュー作成
        /// </summary>
        /// <param name="isRoot">
        /// true:ルートフォルダの時
        /// false:ルートフォルダ以外の時
        /// </param>
        /// <returns>メニューを返す</returns>
        private ContextMenu CreateContextMenuForFolder(bool isRoot = false)
        {
            var menu = new ContextMenu();

            var createMenu = new MenuItem { Header = App.GetStringResource("FolderCreate") };
            createMenu.Click += CreateFolder_Click;
            menu.Items.Add(createMenu);

            var encryptMenu = new MenuItem { Header = App.GetStringResource("Encryption") };
            encryptMenu.Click += EncryptMenu_Click;
            menu.Items.Add(encryptMenu);

            if (!isRoot)
            {
                var renameMenu = new MenuItem { Header = App.GetStringResource("Rename") };
                renameMenu.Click += RenameMenu_Click;
                menu.Items.Add(renameMenu);

                var deleteMenu = new MenuItem { Header = App.GetStringResource("FolderDelete") };
                deleteMenu.Click += DeleteMenu_Click;
                menu.Items.Add(deleteMenu);
            }

            var downloadMenu = new MenuItem { Header = App.GetStringResource("Download") };
            downloadMenu.Click += DownloadMenu_Click;
            menu.Items.Add(downloadMenu);

            return menu;
        }

        /// <summary>
        /// 展開時にサブフォルダーを読み込む
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Folder_Expanded(object sender, RoutedEventArgs e)
        {
            var item = sender as TreeViewItem;
            if (item == null) return;

            // 既に展開済みなら何もしない
            if (item.Items.Count == 1 && item.Items[0] == null)
            {
                item.Items.Clear();

                string path = item.Tag as string;
                if (path == null) return;

                // 子ディレクトリ
                foreach (var dir in Directory.GetDirectories(path))
                {
                    var dirInfo = new DirectoryInfo(dir);
                    var childItem = new TreeViewItem
                    {
                        Header = dirInfo.Name,
                        Tag = dirInfo.FullName,
                        ContextMenu = CreateContextMenuForFolder(isRoot: false)
                    };

                    // Lazy Load 用のダミー
                    if (Directory.GetDirectories(dir).Length > 0 || Directory.GetFiles(dir).Length > 0)
                        childItem.Items.Add(null);

                    childItem.Expanded += Folder_Expanded;
                    item.Items.Add(childItem);

                    // 起動時のみ、1階層目だけ自動展開
                    if (item.Parent == null)
                        childItem.IsExpanded = true;
                }

            }

            e.Handled = true;
        }

        /// <summary>
        /// 左ペイン：サブフォルダーロード
        /// </summary>
        /// <param name="parentItem">ツリービューアイテム</param>
        /// <param name="path">フォルダーパス</param>
        private void LoadSubfolders(TreeViewItem parentItem, string path)
        {
            try
            {
                foreach (var dir in Directory.GetDirectories(path))
                {
                    var subItem = new TreeViewItem
                    {
                        Header = Path.GetFileName(dir),
                        Tag = dir,
                        IsExpanded = false,
                        AllowDrop = true,
                        ContextMenu = CreateContextMenuForFolder() // isRoot: false がデフォルト
                    };

                    parentItem.Items.Add(subItem);

                    // ✅ 再帰的にサブフォルダも読み込み
                    LoadSubfolders(subItem, dir);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                // アクセス拒否などは無視（空行を追加しない）
                Debug.WriteLine("LoadSubfolders UnauthorizedAccessException:" + ex.Message);
            }
            catch (Exception ex)
            {
                // 予期しないエラーも空行は作らない
                Debug.WriteLine("LoadSubfolders Exception:" + ex.Message);
            }
        }

        /// <summary>
        /// 共通：TreeViewItem を返す
        /// </summary>
        /// <param name="sender"></param>
        /// <returns>TreeViewItem</returns>
        private TreeViewItem GetTargetTreeViewItem(object sender)
        {
            return (sender as MenuItem)?.Parent is ContextMenu cm ? cm.PlacementTarget as TreeViewItem : null;
        }

        /// <summary>
        /// フォルダーの作成
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void CreateFolder_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = GetTargetTreeViewItem(sender);
            if (selectedItem == null) return;

            string parentPath = selectedItem.Tag as string;
            string newFolderName = "";

            var inputVM = new InputDialogViewModel(App.GetStringResource("NewFolderRequest"));
            var resultInput = await DialogHost.Show(new InputDialog { DataContext = inputVM }, "RootDialog") as InputDialogViewModel;
            if (resultInput != null)
            {
                newFolderName = resultInput.Input.Trim();
            }

            if (string.IsNullOrWhiteSpace(newFolderName)) return;

            string newFolderPath = Path.Combine(parentPath, newFolderName);

            try
            {
                if (!Directory.Exists(newFolderPath))
                {
                    Directory.CreateDirectory(newFolderPath);

                    // 再読み込みしてツリー更新（手動追加せず再構成）
                    selectedItem.Items.Clear();
                    LoadSubfolders(selectedItem, parentPath);
                    selectedItem.IsExpanded = true;

                    // 🔽 作成したフォルダーの TreeViewItem を探して選択
                    await Dispatcher.InvokeAsync(() =>
                    {
                        var createdItem = FindTreeViewItemByPath(selectedItem.Items, newFolderPath);
                        if (createdItem != null)
                        {
                            createdItem.IsSelected = true;
                            createdItem.Focus(); // 必要に応じてフォーカス
                        }
                    }, DispatcherPriority.Background);

                }
                else
                {
                    await ShowDialog.ShowDialogsAsync(App.GetStringResource("SameFolderExists"));
                }
            }
            catch (Exception ex)
            {
                await ShowDialog.ShowDialogsAsync(App.GetStringResource("CreateFailure"));
            }
        }

        /// <summary>
        /// フォルダー名の変更
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void RenameMenu_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = GetTargetTreeViewItem(sender);
            if (selectedItem == null) return;

            string currentPath = selectedItem.Tag as string;
            string parentPath = Path.GetDirectoryName(currentPath);
            string currentName = Path.GetFileName(currentPath);
            string newFolderName = "";

            var inputVM = new InputDialogViewModel(App.GetStringResource("NewFolderRequest"));
            inputVM.Input = currentName;

            var resultInput = await DialogHost.Show(new InputDialog { DataContext = inputVM }, "RootDialog") as InputDialogViewModel;
            if (resultInput != null)
            {
                newFolderName = resultInput.Input.Trim();

                // 使用できない文字を調べる
                if (!ValidFolderName.IsValidFolderName(newFolderName))
                {
                    await ShowDialog.ShowDialogsAsync(App.GetStringResource("invalidChars"));
                    return;
                }
            }

            if (newFolderName == currentName) return;

            string oldFolderPath = Path.Combine(parentPath, currentName);
            string newFolderPath = Path.Combine(parentPath, newFolderName);

            try
            {
                if (!Directory.Exists(newFolderPath))
                {
                    Directory.Move(oldFolderPath, newFolderPath);

                    // TreeVoew Item の更新
                    selectedItem.Header = newFolderName;
                    selectedItem.Tag = newFolderPath;
                }
                else
                {
                    await ShowDialog.ShowDialogsAsync(App.GetStringResource("SameFolderExists"));
                }
            }
            catch (Exception ex)
            {
                await ShowDialog.ShowDialogsAsync(App.GetStringResource("CreateFailure"));
            }
        }

        /// <summary>
        /// フォルダーの削除
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void DeleteMenu_Click(object sender, RoutedEventArgs e)
        {
            var tvi = GetTargetTreeViewItem(sender);
            if (tvi == null) return;

            string folderPath = tvi.Tag as string;

            var result = await DialogHost.Show(
                new ConfirmDialog { DataContext = new ConfirmDialogViewModel(App.GetStringResource("ConfirmDelete")) },
                "RootDialog");

            if ((result is bool b && b) || (result is string s && s.Equals("True", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    Directory.Delete(folderPath, true);
                    if (tvi.Parent is TreeViewItem parentItem)
                        parentItem.Items.Remove(tvi);
                    else
                        FolderTreeView.Items.Remove(tvi);
                }
                catch (Exception ex)
                {
                    await ShowDialog.ShowDialogsAsync(App.GetStringResource("DeleteFailure"));
                }
            }
        }

        /// <summary>
        /// フォルダー配下のファイルダウンロード（再帰）
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void DownloadMenu_Click(object sender, RoutedEventArgs e)
        {
            var tvi = GetTargetTreeViewItem(sender);
            if (tvi == null) return;

            string folderPath = tvi.Tag as string;

            if (!Directory.Exists(Properties.Settings.Default.DownloadFolder))
            {
                await ShowDialog.ShowDialogsAsync(App.GetStringResource("DownloadFolderSetPrease"));
                return;
            }

            // プログレスバー初期化
            OverlayLayer.Visibility = Visibility.Visible;

            await Task.Run(() =>
            {
                FolderCopier.CopyToDownloads(folderPath);
            });

            OverlayLayer.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// ファイルを暗号化する
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void EncryptMenu_Click(object sender, RoutedEventArgs e)
        {
            var tvi = GetTargetTreeViewItem(sender);
            if (tvi == null) return;

            string folderPath = tvi.Tag as string;
            if (!string.IsNullOrEmpty(folderPath))
            {
                var encryptDropWindow = new EncryptDropWindow(folderPath);
                encryptDropWindow.ShowDialog();

                if (encryptDropWindow.Cancel)
                {
                    // キャンセルされた
                    await ShowDialog.ShowDialogsAsync(App.GetStringResource("CancelEncrypt"));
                }
                else
                {
                    await ShowDialog.ShowDialogsAsync(App.GetStringResource("ProcessComplete"));
                }

                // 自分自身の TreeViewItem を取得
                var item = FindTreeViewItemByPath(FolderTreeView.Items, folderPath);
                if (item != null)
                {
                    item.Items.Clear(); // サブフォルダーをリセット
                    LoadSubfolders(item, folderPath); // 再読み込み
                    item.IsExpanded = true;
                }

                // ファイル一覧の再表示（選択されている場合）
                if (FolderTreeView.SelectedItem is TreeViewItem selectedItem &&
                    (selectedItem.Tag as string) == folderPath)
                {
                    LoadFileList(folderPath);
                }

            }
        }

        /// <summary>
        /// 左ペイン：ツリー表示クリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FolderTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewItem selectedItem && selectedItem.Tag is string folderPath)
            {
                LoadFileList(folderPath);
            }
        }

        /// <summary>
        /// 右ペイン：ファイルリストを作成する
        /// </summary>
        /// <param name="folderPath"></param>
        private async void LoadFileList(string folderPath)
        {
            var files = new List<FileModel>();
            var icons = new List<IconModel>();

            // .ghostsafeファイル（ファイル名を保存したファイル）だけ取得
            var encNameFiles = Directory.GetFiles(folderPath, "*.ghostsafe");

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
                    string encryptedFilePath = Path.Combine(folderPath, encryptedFileName);

                    if (File.Exists(encryptedFilePath))
                    {
                        var info = new FileInfo(encryptedFilePath);

                        files.Add(new FileModel
                        {
                            Name = originalFileName,
                            Modified = info.LastWriteTime,
                            Type = originalExtension,
                            Size = FormatSize(info.Length),
                            EncryptName = encryptFileName
                        });

                        icons.Add(new IconModel
                        {
                            Name = originalFileName,
                            Type = originalExtension,
                            CurrentPath = folderPath,
                            EncryptName = encryptFileName,
                            IconOrThumbnail = await IconExtractor.GetThumbnailOrIcon(encryptedFilePath)
                        });
                    }
                }
                catch (Exception ex)
                {
                    // ログなどに出力（オプション）
                    Debug.WriteLine($"Error loading file info from {encNameFile}: {ex.Message}");
                    await ShowDialog.ShowDialogsAsync(App.GetStringResource("SystemFailure"));
                    break;
                }
            }

            FileDataGrid.ItemsSource = files;
            ThumbnailList.ItemsSource = icons;
        }

        /// <summary>
        /// 右ペイン：ファイルサイズを計算する
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns>ファイルサイズを返す（バイト単位）</returns>
        private string FormatSize(long bytes)
        {
            if (bytes >= 1 << 30)
                return $"{bytes / (1 << 30):F1} GB";
            if (bytes >= 1 << 20)
                return $"{bytes / (1 << 20):F1} MB";
            if (bytes >= 1 << 10)
                return $"{bytes / (1 << 10):F1} KB";
            return $"{bytes} B";
        }

        /// <summary>
        /// 左ペイン：ツリービューを検索する
        /// </summary>
        /// <param name="items"></param>
        /// <param name="targetPath"></param>
        /// <returns>ツリービューを返す</returns>
        private TreeViewItem? FindTreeViewItemByPath(ItemCollection items, string targetPath)
        {
            foreach (var obj in items)
            {
                if (obj is not TreeViewItem item)
                    continue; // TreeViewItem でないものはスキップ

                var tagPath = item.Tag as string;
                if (!string.IsNullOrEmpty(tagPath) && tagPath.Equals(targetPath, StringComparison.OrdinalIgnoreCase))
                    return item;

                if (item.Items.Count > 0)
                {
                    var found = FindTreeViewItemByPath(item.Items, targetPath);
                    if (found != null)
                        return found;
                }
            }
            return null;
        }

        Point _startPoint; // マウスのスタート位置

        /// <summary>
        /// 右ペイン：DataGrid アイテム 左ボタンクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FileDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(null); // ドラッグ開始位置を記録
        }

        /// <summary>
        /// 右ペイン：DataGrid アイテム ドラッグ＆ドロップ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void TreeViewItem_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            // マウス座標を TreeView 基準で取得
            var point = e.GetPosition(FolderTreeView);
            var hit = VisualTreeHelper.HitTest(FolderTreeView, point);
            if (hit == null) return;

            // ヒットした要素から TreeViewItem を遡って探す
            var targetItem = FindAncestor<TreeViewItem>(hit.VisualHit);
            if (targetItem == null) return;

            string targetFolder = targetItem.Tag as string;
            if (string.IsNullOrEmpty(targetFolder)) return;

            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            foreach (var file in files)
            {
                string dest = Path.Combine(targetFolder, Path.GetFileName(file));

                try
                {
                    File.Move(file, dest);
                }
                catch (Exception ex)
                {
                    await ShowDialog.ShowDialogsAsync(App.GetStringResource("MoveFailure"));
                    break;
                }
            }

        }

        /// <summary>
        /// 指定された要素からビジュアルツリーを遡り、
        /// 最初に見つかった指定型の親要素を取得する
        /// </summary>
        /// <remarks>
        /// 本メソッドは、WPF の <see cref="VisualTreeHelper"/> を使用して
        /// 現在の <see cref="DependencyObject"/> から親要素を順に辿り、
        /// 型 <typeparamref name="T"/> に一致する要素を検索します。
        /// <para>
        /// 一致する要素が見つからない場合は null を返します。
        /// 主に、イベントハンドラやコントロール内から
        /// 上位コンテナ（例: Window、UserControl、ListBoxItem 等）を
        /// 取得したい場合に使用されます。
        /// </para>
        /// </remarks>
        /// <typeparam name="T">検索対象となる親要素の型</typeparam>
        /// <param name="current">検索を開始する基点となる要素</param>
        /// <returns>
        /// 見つかった最初の <typeparamref name="T"/> 型の親要素。
        /// 見つからない場合は null。
        /// </returns>
        private T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T)
                    return (T)current;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        /// <summary>
        /// 右ペイン：DataGrid アイテム右クリック
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FileDataGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var dep = (DependencyObject)e.OriginalSource;

            // 行の取得
            while (dep != null && !(dep is DataGridRow))
                dep = VisualTreeHelper.GetParent(dep);

            if (dep is DataGridRow row)
            {
                row.IsSelected = true;

                // 動的にコンテキストメニューを作る
                var contextMenu = new ContextMenu();

                // 削除メニュー
                var deleteMenuItem = new MenuItem { Header = App.GetStringResource("Delete") };
                contextMenu.Items.Add(deleteMenuItem);

                // リネームメニュー
                var renameMenuItem = new MenuItem { Header = App.GetStringResource("Rename") };
                contextMenu.Items.Add(renameMenuItem);

                // ダウンロードメニュー
                var downloadMenuItem = new MenuItem { Header = App.GetStringResource("Download") };
                contextMenu.Items.Add(downloadMenuItem);

                // メニューを開く
                row.ContextMenu = contextMenu;
                contextMenu.IsOpen = true;

                e.Handled = true;

            }
        }

        /// <summary>
        /// 右ペイン：共通 ファイルダウンロード
        /// </summary>
        /// <param name="Folder">対象のフォルダー名</param>
        /// <param name="EncryptName">暗号化ファイル名</param>
        private async void CommonDownloadFile_Click(string Folder, string EncryptName)
        {
            if (!Directory.Exists(Properties.Settings.Default.DownloadFolder))
            {
                await ShowDialog.ShowDialogsAsync(App.GetStringResource("DownloadFolderSetPrease"));
                return;
            }

            // プログレスバー初期化
            OverlayLayer.Visibility = Visibility.Visible;

            await Task.Run(() =>
            {
                // ファイルの内容を読み込む
                string ghostPath = Path.Combine(Folder, EncryptName.Substring(0, 20) + ".ghostsafe");
                string[] names = EncryptorAesGcm.UnprotectText(ghostPath).Split('/');
                string filePath = Path.Combine(Folder, names[1]);
                string download = Path.Combine(Properties.Settings.Default.DownloadFolder, names[0]);

                Debug.WriteLine("ghostPath:" + ghostPath);
                Debug.WriteLine("download:" + download);

                EncryptorAesGcm.UnprotectFile(filePath, download);

            });

            OverlayLayer.Visibility = Visibility.Collapsed;

            await ShowDialog.ShowDialogsAsync(App.GetStringResource("ProcessComplete"));
        }

        /// <summary>
        /// 右ペイン：共通 ファイル名の変更
        /// </summary>
        /// <param name="Folder">対象のフォルダー名</param>
        /// <param name="EncryptName">暗号化ファイル名</param>
        /// <param name="Name">変更後のファイル名</param>
        private async void CommonRenameFile_Click(string Folder, string EncryptName, string Name)
        {
            var inputVM = new InputDialogViewModel(App.GetStringResource("RenameRequest"));
            inputVM.Input = Name;

            var resultInput = await DialogHost.Show(new InputDialog { DataContext = inputVM }, "RootDialog") as InputDialogViewModel;
            if (resultInput == null || string.IsNullOrWhiteSpace(resultInput.Input))
                return;

            string newName = resultInput.Input.Trim();
            if (newName == Name) return;

            // 使用できない文字を調べる
            if (!ValidFolderName.IsValidFolderName(newName))
            {
                await ShowDialog.ShowDialogsAsync(App.GetStringResource("invalidChars"));
                return;
            }

            // ファイルの内容を読み込む
            string ghostPath = Path.Combine(Folder, EncryptName.Substring(0, 20) + ".ghostsafe");
            string[] names = EncryptorAesGcm.UnprotectText(ghostPath).Split('/');
            names[0] = newName;

            EncryptorAesGcm.ProtectText(names[0] + "/" + names[1], ghostPath);

            LoadFileList(Folder); // 一覧を更新

        }

        /// <summary>
        /// 右ペイン：DataGrid ダブルクリック時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FileDataGrid_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var row = ItemsControl.ContainerFromElement(FileDataGrid, e.OriginalSource as DependencyObject) as DataGridRow;
            if (row == null) return;

            var selectedFile = row.Item as FileModel;
            if (selectedFile == null) return;

            string selectedFolder = (FolderTreeView.SelectedItem as TreeViewItem)?.Tag as string;
            if (string.IsNullOrEmpty(selectedFolder))
                return;

            // 共通のダブルクリック処理
            Common_PreviewMouseDoubleClick(selectedFile.Type, selectedFolder, selectedFile.EncryptName);

        }

        /// <summary>
        /// 右ペイン：共通 ダブルクリック時
        /// </summary>
        /// <param name="Type">拡張子</param>
        /// <param name="Folder">対象のフォルダー名</param>
        /// <param name="EncryptName">暗号化ファイル名</param>
        private async void Common_PreviewMouseDoubleClick(string Type, String Folder, string EncryptName)
        {
            if (!Directory.Exists(App.AppTempPath))
                Directory.CreateDirectory(App.AppTempPath);

            bool iVideo = false;
            bool iPhoto = false;

            // 動画：MP4, MKV, AVI, MOV, MPEG, WMV, FLV, WebM, 3GP, M4V, ASF, MKV
            // 音声：MP3, AAC, WAV, FLAC, OGG, WMA, M4A
            if (Type.ToLower() == ".mp4" || Type.ToLower() == ".avi" ||
               Type.ToLower() == ".wmv" || Type.ToLower() == ".flv" ||
               Type.ToLower() == ".webm" || Type.ToLower() == ".mov" ||
               Type.ToLower() == ".mpeg" || Type.ToLower() == ".mpg" ||
               Type.ToLower() == ".3gp" || Type.ToLower() == ".m4v" ||
               Type.ToLower() == ".asf" || Type.ToLower() == ".mkv" ||
               Type.ToLower() == ".mp3" || Type.ToLower() == ".aac" ||
               Type.ToLower() == ".wav" || Type.ToLower() == ".flak" ||
               Type.ToLower() == ".ogg" || Type.ToLower() == ".wma" ||
               Type.ToLower() == ".m4a" || Type.ToLower() == ".mkv")
            {
                iVideo = true;
            }
            else if (Type.ToLower() == ".bmp" || Type.ToLower() == "jpeg" ||
               Type.ToLower() == ".jpg" || Type.ToLower() == ".png" ||
               Type.ToLower() == ".gif" || Type.ToLower() == ".tiff" ||
               Type.ToLower() == ".ico")
            {
                iPhoto = true;
            }

            // プログレスバー初期化
            OverlayLayer.Visibility = Visibility.Visible;

            string tempFile = "";
            string[] names = [];

            await Task.Run(() =>
            {
                // ファイルの内容を読み込む
                string ghostPath = Path.Combine(Folder, EncryptName.Substring(0, 20) + ".ghostsafe");
                names = EncryptorAesGcm.UnprotectText(ghostPath).Split('/');
                string filePath = Path.Combine(Folder, names[1]);
                tempFile = iVideo ? App.AppTempPath + @"\" + names[0] : App.AppTempPath + @"\" + names[1];

                if (!File.Exists(tempFile))
                {
                    Debug.WriteLine("ghostPath:" + ghostPath);
                    Debug.WriteLine("tempFile:" + tempFile);

                    EncryptorAesGcm.UnprotectFile(filePath, tempFile);
                }
            });

            if (iVideo)
            {
                LaunchVideoWindow(tempFile); // VideoWindow 起動
            }
            else if (iPhoto)
            {
                var imgWin = new ImageDisplay();
                imgWin.Owner = null; // 所有者を設定すると親ウィンドウの前後関係が保たれる
                imgWin.LoadImage(tempFile, names[0]);
                imgWin.Show(); // モーダレス（ShowDialogではない）

                // 親ウィンドウ（PageをホストしているWindow）を取得
                var parentWindow = Window.GetWindow(this);

                if (parentWindow != null)
                {
                    // 親が閉じたら子も閉じる
                    parentWindow.Closed += (s, ev) =>
                    {
                        if (imgWin.IsLoaded)
                            imgWin.Close();
                    };

                    // 親の最小化／復元に追従させる
                    parentWindow.StateChanged += (s, ev) =>
                    {
                        if (!imgWin.IsLoaded)
                            return;

                        if (parentWindow.WindowState == WindowState.Minimized)
                        {
                            imgWin.WindowState = WindowState.Minimized;
                        }
                        else
                        {
                            // 親が通常 or 最大化に戻ったら子も戻す場合
                            if (imgWin.WindowState == WindowState.Minimized)
                            {
                                imgWin.WindowState = parentWindow.WindowState;
                            }
                        }
                    };
                }
            }
            else
            {
                Process.Start(new ProcessStartInfo(tempFile)
                {
                    UseShellExecute = true  // ★これが必要！
                });

                // 更新されているかを取っておく
                FileInfo fileInfo = new FileInfo(tempFile);
                FileModel fileModel = new FileModel();

                fileModel.Name = tempFile;
                fileModel.Modified = fileInfo.LastWriteTime; // 更新日時
                fileModel.Size = fileInfo.Length.ToString(); // サイズ（バイト単位）
                fileModel.Type = fileInfo.Extension;
                fileModel.EncryptName = Path.Combine(Folder, EncryptName);

                App.FileUpdates.Add(fileModel);
            }

            OverlayLayer.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Video Player 起動
        /// </summary>
        /// <param name="videoPath">動画ファイルのパス</param>
        private void LaunchVideoWindow(string videoPath)
        {
            string pipeName = Guid.NewGuid().ToString();
            int parentPid = Process.GetCurrentProcess().Id; //親プロセスID

            string videoPlayerExe = Path.Combine(AppContext.BaseDirectory, "VideoPlayer", "VideoPlayer.exe");

            var startInfo = new ProcessStartInfo
            {
                FileName = videoPlayerExe,
                Arguments = $"\"{videoPath}\" \"{pipeName}\" {parentPid}",
                UseShellExecute = false
            };

            var process = Process.Start(startInfo);

            var client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
            client.Connect();

            var writer = new StreamWriter(client) { AutoFlush = true };
            writer.WriteLine("volume 50");  // 初期音量設定

            App.PipeWriter.Add(new VideoChild
            {
                PipeName = pipeName,
                Writer = writer,
                Process = process,
                VideoPath = videoPath
            });
        }

        /// <summary>
        /// サムネイルアイテムのダブルクリック
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ThumbnailList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ThumbnailList.SelectedItem is IconModel file)
            {
                // 共通のダブルクリック処理
                Common_PreviewMouseDoubleClick(file.Type, file.CurrentPath, file.EncryptName);
            }
        }

        /// <summary>
        /// サムネイルアイテムの名前変更
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RenameMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (ThumbnailList.SelectedItem is IconModel file)
            {
                // 共通のファイル名変更
                CommonRenameFile_Click(file.CurrentPath, file.EncryptName, file.Name);
            }
        }

        /// <summary>
        /// サムネイルアイテムのダウンロード
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DownloadMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (ThumbnailList.SelectedItem is IconModel file)
            {
                // 共通のダウンロード処理
                CommonDownloadFile_Click(file.CurrentPath, file.EncryptName);
            }
        }

        /// <summary>
        /// サムネイルアイテムの削除
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is IconModel file)
            {
                string filePath = Path.Combine(file.CurrentPath, file.Name);

                var result = await DialogHost.Show(
                    new ConfirmDialog
                    {
                        DataContext = new ConfirmDialogViewModel(App.GetStringResource("ConfirmDelete") + $"\n{file.Name}")
                    },
                    "RootDialog");

                if ((result is bool b && b) || (result is string s && s.Equals("True", StringComparison.OrdinalIgnoreCase)))
                {
                    try
                    {
                        filePath = Path.Combine(file.CurrentPath, file.EncryptName.Substring(0, 20) + ".ghostsafe");
                        File.Delete(filePath);
                        filePath = Path.Combine(file.CurrentPath, file.EncryptName);
                        File.Delete(filePath);

                        // 動画のアイコンがあるなら削除する
                        filePath = Path.Combine(file.CurrentPath, file.EncryptName.Substring(0, 20) + ".jpg");
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                        }

                        LoadFileList(file.CurrentPath); // 一覧を更新
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("DeleteMenuItem_Click ex:" + ex.Message);
                        await ShowDialog.ShowDialogsAsync(App.GetStringResource("DeleteFailure"));
                    }
                }
            }
        }

        private Point _dragStartPoint;

        /// <summary>
        /// サムネイル左クリック時にドラッグ開始位置を記録
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ThumbnailList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        /// <summary>
        /// サムネイル左クリック時にマウス移動でドラッグ判定
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ThumbnailList_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point mousePos = e.GetPosition(null);
                Vector diff = _dragStartPoint - mousePos;

                // 一定距離動いたらドラッグ開始
                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    if (ThumbnailList.SelectedItems.Count > 0)
                    {
                        // 選択中のファイルのフルパスを収集
                        var paths = new List<string>();
                        foreach (var item in ThumbnailList.SelectedItems)
                        {
                            if (item is IconModel file)
                            {
                                paths.Add(Path.Combine(file.CurrentPath, file.EncryptName));
                                paths.Add(Path.Combine(file.CurrentPath, file.EncryptName.Substring(0, 20) + ".ghostsafe"));

                                string extension = Path.GetExtension(file.EncryptName);
                                if (extension == ".mp4" || extension == ".avi" ||
                                   extension == ".wmv" || extension == ".flv" ||
                                   extension == ".mov" || extension == ".webm" ||
                                   extension == ".m4v" || extension == ".3gp" ||
                                   extension == ".asf" || extension == ".mkv")
                                {
                                    // アイコンがあるなら移動する
                                    string extRemove = Path.Combine(file.CurrentPath, file.EncryptName.Substring(0, 20) + ".jpg");
                                    if (File.Exists(extRemove))
                                    {
                                        paths.Add(extRemove);
                                    }
                                }
                            }
                        }

                        if (paths.Count > 0)
                        {
                            DataObject data = new DataObject(DataFormats.FileDrop, paths.ToArray());
                            DragDrop.DoDragDrop(ThumbnailList, data, DragDropEffects.Copy | DragDropEffects.Move);

                            LoadFileList(Path.GetDirectoryName(paths[0]));

                        }
                    }
                }
            }
        }
    }
}