using ControlzEx.Standard;
using DirectShowLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GhostSafe.Common
{
    public static class IconExtractor
    {
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(
            string pszPath,
            uint dwFileAttributes,
            ref SHFILEINFO psfi,
            uint cbFileInfo,
            uint uFlags);

        private const uint SHGFI_ICON = 0x100;
        private const uint SHGFI_LARGEICON = 0x0;    // 大きいアイコン
        private const uint SHGFI_SMALLICON = 0x1;    // 小さいアイコン

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        /// <summary>
        /// 指定されたファイルまたはフォルダのアイコンを ImageSource として取得します。
        /// </summary>
        /// <param name="path">
        /// アイコンを取得したいファイルまたはフォルダのフルパス。
        /// </param>
        /// <param name="small">
        /// true の場合は小さいアイコン（16x16）を取得します。
        /// false の場合は大きいアイコン（通常 32x32）を取得します。
        /// </param>
        /// <returns>
        /// 取得したアイコンを表す ImageSource。
        /// アイコンを取得できなかった場合は null を返します。
        /// </returns>
        public static ImageSource GetFileIcon(string path, bool small = true)
        {
            SHFILEINFO shinfo = new SHFILEINFO();
            uint flags = SHGFI_ICON | (small ? SHGFI_SMALLICON : SHGFI_LARGEICON);
            SHGetFileInfo(path, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);

            if (shinfo.hIcon != IntPtr.Zero)
            {
                var icon = System.Drawing.Icon.FromHandle(shinfo.hIcon);
                var img = Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle,
                    System.Windows.Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                icon.Dispose();
                return img;
            }
            return null;
        }

        /// <summary>
        /// 指定された暗号化ファイルから、サムネイル画像またはファイルアイコンを取得する
        /// </summary>
        /// <remarks>
        /// 本メソッドは、ファイル拡張子に応じて以下の処理を行います。
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// 画像ファイル（jpg, png 等）の場合：
        /// 一時フォルダに復号した後、画像を読み込みサムネイルを生成します。
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// 動画ファイル（mp4, avi 等）の場合：
        /// 対応するサムネイル画像（jpg）が存在すればそれを使用し、
        /// 存在しない場合は動画ファイルからサムネイルを生成します。
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// 上記以外のファイルの場合：
        /// OS のファイルアイコンを取得して返します。
        /// </description>
        /// </item>
        /// </list>
        /// 暗号化されたファイルは一時ディレクトリに復号され、
        /// その内容を元にサムネイルまたはアイコンが生成されます。
        /// 例外が発生した場合は null を返します。
        /// </remarks>
        /// <param name="path">サムネイルまたはアイコンを取得する対象となる暗号化ファイルのパス</param>
        /// <returns>
        /// サムネイル画像、またはファイルアイコンを表す <see cref="ImageSource"/>。
        /// 取得に失敗した場合は null。
        /// </returns>
        public async static Task<ImageSource> GetThumbnailOrIcon(string path)
        {
            try
            {
                string ext = Path.GetExtension(path).ToLower();
                if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" ||
                    ext == ".bmp" || ext == ".gif" || ext == ".tiff")
                {
                    string tempFile = Path.Combine(App.AppTempPath, Path.GetFileName(path));

                    EncryptorAesGcm.UnprotectFile(path, tempFile);

                    Image img = Image.FromFile(tempFile);
                    Bitmap imgbitmap = new Bitmap(img);
                    return await ResizeImage(imgbitmap);
                }
                else if (ext == ".mp4" || ext == ".avi" ||
                       ext == ".wmv" || ext == ".flv" ||
                       ext == ".mov" || ext == ".webm" ||
                       ext == ".m4v" || ext == ".3gp" ||
                       ext == ".asf" || ext == ".mkv")
                {
                    // アイコンがあるなら表示する
                    string jpgFile = Path.Combine(Path.GetDirectoryName(path), Path.GetFileName(path).Replace(ext, ".jpg"));

                    if (File.Exists(jpgFile))
                    {
                        string unprotectFile = Path.Combine(App.AppTempPath, Path.GetFileName(jpgFile));
                        EncryptorAesGcm.UnprotectFile(jpgFile, unprotectFile);
                        return await LoadImageAsync(unprotectFile);
                    }

                    string tempFile = Path.Combine(App.AppTempPath, Path.GetFileName(path));
                    EncryptorAesGcm.UnprotectFile(path, tempFile);

                    Debug.WriteLine("tempFile:" + tempFile);

                    var rtc = PresenceVideoCodecs.GetPresenceVideoCodecs(tempFile);
                    if (rtc)
                    {
                        // ストップウォッチを作成・開始
                        Stopwatch sw = new Stopwatch();
                        sw.Start();

                        var bmp = await VideoThumbnailProvider.GetThumbnailAsync(tempFile, 256, 256);

                        // 計測終了
                        sw.Stop();

                        // 結果をDebug出力
                        Debug.WriteLine($"処理時間: {sw.ElapsedMilliseconds} ms");

                        return ImageConverter.BitmapTaskToImageSource(bmp);
                    }
                    else
                    {
                        return GetFileIcon(path, small: false);
                    }

                }
                else
                {
                    return GetFileIcon(path, small: false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("GetThumbnailOrIcon ex:" + ex.Message);
                return null;
            }

        }

        /// <summary>
        /// 指定された画像ファイルを非同期で読み込み、<see cref="ImageSource"/> として返す
        /// </summary>
        /// <remarks>
        /// 本メソッドは、ファイル I/O をバックグラウンドスレッドで実行し、
        /// 読み込んだ画像を <see cref="BitmapImage"/> として生成します。
        /// <see cref="BitmapCacheOption.OnLoad"/> を使用することで、
        /// ストリームを閉じた後も画像を使用できるようにしています。
        /// また、<see cref="BitmapImage.Freeze"/> を呼び出すことで、
        /// UI スレッド以外からでも安全に利用可能な <see cref="ImageSource"/> を返します。
        /// </remarks>
        /// <param name="filePath">読み込む画像ファイルのパス</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">
        /// <paramref name="filePath"/> が null、空文字、または空白のみの場合。
        /// </exception>
        /// <exception cref="FileNotFoundException">
        /// 指定された画像ファイルが存在しない場合。
        /// </exception>
        public static async Task<ImageSource> LoadImageAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("ファイルパスが無効です。", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException("指定されたファイルが見つかりません。", filePath);

            return await Task.Run(async () =>
            {
                // FileStream を非同期で開く
                await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad; // ストリームを閉じても使用できるようにする
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze(); // スレッドセーフにする

                return (ImageSource)bitmap;
            });

        }

        /// <summary>
        /// 指定された画像を、縦横比を維持したまま指定サイズ以内に縮小し、
        /// <see cref="ImageSource"/> として返します。
        /// </summary>
        /// <remarks>
        /// 本メソッドは、元画像の縦横比を維持しつつ、
        /// 幅または高さが <paramref name="maxSize"/> を超えないようにリサイズします。
        /// 縮小処理には高品質な補間アルゴリズムを使用し、
        /// サムネイル表示やプレビュー用途に適した画像を生成します。
        /// 生成された画像は WPF で利用可能な <see cref="ImageSource"/> に変換され、
        /// 非同期で返されます。
        /// </remarks>
        /// <param name="image">リサイズ対象となる元画像</param>
        /// <param name="maxSize">縮小後の画像における最大の幅または高さ（ピクセル単位）。既定値は 128</param>
        /// <returns>
        /// リサイズ後の画像を表す <see cref="ImageSource"/>
        /// </returns>
        public async static Task<ImageSource> ResizeImage(Image image, int maxSize = 128)
        {
            // 元のサイズ
            int originalWidth = image.Width;
            int originalHeight = image.Height;

            // 縦横比を維持して縮小後サイズを計算
            double ratio = Math.Min((double)maxSize / originalWidth, (double)maxSize / originalHeight);
            int newWidth = (int)(originalWidth * ratio);
            int newHeight = (int)(originalHeight * ratio);

            var destinationRect = new Rectangle(0, 0, newWidth, newHeight);
            var destinationImage = new Bitmap(newWidth, newHeight);

            destinationImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destinationImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destinationRect, 0, 0, image.Width, image.Height,
                                       GraphicsUnit.Pixel, wrapMode);
                }
            }

            // Image → ImageSource に変換
            return await ConvertToImageSource(destinationImage);
        }

        /// <summary>
        /// <see cref="System.Drawing.Image"/> を非同期で
        /// WPF 用の <see cref="BitmapImage"/> に変換します。
        /// </summary>
        /// <remarks>
        /// 本メソッドは、GDI+ の <see cref="System.Drawing.Image"/> を
        /// メモリストリーム経由で PNG 形式に変換し、
        /// WPF で利用可能な <see cref="BitmapImage"/> を生成します。
        /// 画像のエンコード処理は比較的高コストなため、
        /// UI スレッドをブロックしないよう <see cref="Task.Run"/> 内で実行されます。
        /// <para>
        /// 生成された <see cref="BitmapImage"/> は
        /// <see cref="BitmapImage.CacheOption"/> に
        /// <see cref="BitmapCacheOption.OnLoad"/> を指定し、
        /// さらに <see cref="BitmapImage.Freeze"/> を呼び出すことで、
        /// ストリーム破棄後も安全に利用でき、
        /// スレッド間での使用も可能となります。
        /// </para>
        /// </remarks>
        /// <param name="image">変換元となる <see cref="System.Drawing.Image"/></param>
        /// <returns>変換された <see cref="BitmapImage"/></returns>
        private async static Task<BitmapImage> ConvertToImageSource(Image image)
        {
            return await Task.Run(() =>
            {
                using (var ms = new MemoryStream())
                {
                    // System.Drawing.Image を PNG に保存（重い処理なので Task.Run 内で実行）
                    image.Save(ms, ImageFormat.Png);
                    ms.Position = 0;

                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze(); // スレッドセーフにすることで UI スレッドで利用可能

                    return bitmap;
                }
            });

        }

    }
}
