using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using GhostSafe.Common;
using GhostSafe.Dialog;
using GhostSafe.ViewModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Net;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace GhostSafe
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        static public string AppDataPath = string.Empty;

        static public string AppTempPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GhostSafeTempFiles");

        // パイプを保持しておく
        static public List<VideoChild> PipeWriter = new List<VideoChild>();
        // ファイルの更新記録を保持しておく
        static public List<FileModel> FileUpdates = new List<FileModel>();

        static public string TempUserDataFolder = "";
        static public string SystemLang = "";
        static public byte[] MasterKey;
        static public byte[] AppSalt;

        /// <summary>
        /// スタートアップ
        /// </summary>
        /// <param name="e"></param>
        protected override async void OnStartup(StartupEventArgs e)
        {
            if (string.IsNullOrEmpty(GhostSafe.Properties.Settings.Default.Language))
            {
                // システムの UI 言語を取得
                SystemLang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

                // サポートしている言語かどうか確認（英語と日本語の例）Language
                if (SystemLang != "ja")
                {
                    SystemLang = "en"; // デフォルト
                }
            }
            else
            {
                SystemLang = GhostSafe.Properties.Settings.Default.Language; // 保存された言語
            }

            SetLanguageDictionary(SystemLang);

            base.OnStartup(e);

        }

        /// <summary>
        /// ファイルの読み取り専用属性を解除して削除
        /// </summary>
        /// <param name="path">対象ファイルのパス</param>
        public static void RemoveReadOnlyAttributes(string path)
        {
            // フォルダ自身の属性を解除
            var dirInfo = new DirectoryInfo(path);
            dirInfo.Attributes &= ~FileAttributes.ReadOnly;

            // 中のファイル・サブフォルダも再帰的に解除
            foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
            {
                file.Attributes &= ~FileAttributes.ReadOnly;
            }

            foreach (var dir in dirInfo.GetDirectories("*", SearchOption.AllDirectories))
            {
                dir.Attributes &= ~FileAttributes.ReadOnly;
            }

            try { Directory.Delete(path, true); } catch { }

        }

        /// <summary>
        /// 言語設定
        /// </summary>
        /// <param name="lang">日本語:ja 日本語以外:en</param>
        public static void SetLanguageDictionary(string lang)
        {
            SystemLang = lang;

            // 既存の言語辞書を削除（"Strings." を含む辞書のみ）
            var existing = Application.Current.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Strings."));

            if (existing != null)
            {
                Application.Current.Resources.MergedDictionaries.Remove(existing);
            }

            var dict = new ResourceDictionary
            {
                Source = new Uri($"Resources/Strings.{lang}.xaml", UriKind.Relative)
            };

            Application.Current.Resources.MergedDictionaries.Add(dict);
        }

        /// <summary>
        /// リソース文字列取得のヘルパー
        /// </summary>
        /// <param name="key">メッセージテキストのキー</param>
        /// <returns>メッセージのテキストを返す</returns>
        public static string GetStringResource(string key)
        {
            if (Application.Current.Resources.Contains(key))
            {
                return Application.Current.Resources[key] as string ?? key;
            }
            return key; // キーが無ければキー名そのまま返す
        }

    }

}
