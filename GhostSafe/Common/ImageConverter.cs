using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GhostSafe.Common
{
    public static class ImageConverter
    {
        /// <summary>
        /// <see cref="System.Drawing.Bitmap"/> を
        /// WPF で使用可能な <see cref="ImageSource"/> に変換します。
        /// </summary>
        /// <remarks>
        /// 本メソッドは、GDI+ の <see cref="System.Drawing.Bitmap"/> を
        /// メモリストリーム経由で PNG 形式に変換し、
        /// WPF の <see cref="BitmapImage"/> として読み込みます。
        /// <para>
        /// <see cref="BitmapCacheOption.OnLoad"/> を使用することで、
        /// ストリーム破棄後も画像を安全に利用できるようにしています。
        /// また、<see cref="BitmapImage.Freeze"/> を呼び出すことで、
        /// UI スレッド以外からでも安全にアクセス可能な
        /// <see cref="ImageSource"/> を返します。
        /// </para>
        /// </remarks>
        /// <param name="bitmap">
        /// 変換元となる <see cref="System.Drawing.Bitmap"/>。
        /// null の場合は null を返します。
        /// </param>
        /// <returns>
        /// 変換された <see cref="ImageSource"/>。
        /// <paramref name="bitmap"/> が null の場合は null。
        /// </returns>
        public static ImageSource? BitmapTaskToImageSource(Bitmap? bitmap)
        {
            if (bitmap == null)
                return null;

            using (var memory = new MemoryStream())
            {
                // PNG形式でストリームに保存（透明も保持）
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                memory.Position = 0;

                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = memory;
                bitmapImage.EndInit();
                bitmapImage.Freeze(); // UIスレッド以外でも安全に使用可能

                return bitmapImage;
            }
        }
    }
}
