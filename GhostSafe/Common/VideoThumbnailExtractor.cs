using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace GhostSafe.Common
{
    public static class VideoThumbnailProvider
    {
        private static readonly ConcurrentDictionary<string, Bitmap> _cache = new();
        private static readonly SemaphoreSlim _semaphore = new(4);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SHCreateItemFromParsingName(
            [In][MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            IntPtr pbc,
            [In][MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            [Out][MarshalAs(UnmanagedType.Interface, IidParameterIndex = 2)] out IShellItemImageFactory ppv);

        [ComImport]
        [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItemImageFactory
        {
            void GetImage([In] SIZE size, [In] SIIGBF flags, out IntPtr phbm);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE
        {
            public int cx;
            public int cy;
        }

        [Flags]
        private enum SIIGBF
        {
            ResizeToFit = 0x00,
            BiggerSizeOk = 0x01,
            MemoryOnly = 0x02,
            IconOnly = 0x04,
            ThumbnailOnly = 0x08,
            InCacheOnly = 0x10
        }

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        // HRESULT for "component (codec) not found"
        private const int WINCODEC_ERR_COMPONENTNOTFOUND = unchecked((int)0x88982F50);

        /// <summary>
        /// 指定されたファイルのサムネイル画像を非同期で取得する
        /// </summary>
        /// <remarks>
        /// 本メソッドは、Windows Shell（IShellItemImageFactory）を使用して
        /// ファイルに関連付けられたサムネイルを生成します。
        /// <para>
        /// サムネイルは指定された <paramref name="width"/> および
        /// <paramref name="height"/> に収まるようリサイズされます。
        /// </para>
        /// <para>
        /// <paramref name="useCache"/> が true の場合、
        /// 取得したサムネイルは内部キャッシュに保存され、
        /// 同一ファイル・同一サイズの再取得時には
        /// キャッシュされた画像が返されます。
        /// </para>
        /// <para>
        /// COM コンポーネントを使用するため、
        /// 同時実行数を制限する目的でセマフォを用いて
        /// 排他制御を行っています。
        /// </para>
        /// 取得に失敗した場合は null を返します。
        /// </remarks>
        /// <param name="filePath">サムネイルを取得する対象ファイルのパス</param>
        /// <param name="width">生成するサムネイルの最大幅（ピクセル単位）</param>
        /// <param name="height">生成するサムネイルの最大高さ（ピクセル単位）</param>
        /// <param name="useCache">
        /// サムネイルキャッシュを使用するかどうか。
        /// true の場合はキャッシュを利用します。
        /// </param>
        /// <returns>
        /// 取得されたサムネイルを表す <see cref="Bitmap"/>。
        /// 取得に失敗した場合は null。
        /// </returns>
        public static async Task<Bitmap?> GetThumbnailAsync(string filePath, int width, int height, bool useCache = true)
        {
            if (!File.Exists(filePath)) return null;

            string cacheKey = $"{filePath}|{width}x{height}";

            if (useCache && _cache.TryGetValue(cacheKey, out var cached))
            {
                return (Bitmap)cached.Clone();
            }

            await _semaphore.WaitAsync();
            try
            {
                return await Task.Run(() =>
                {
                    try
                    {
                        var iid = typeof(IShellItemImageFactory).GUID;
                        SHCreateItemFromParsingName(filePath, IntPtr.Zero, iid, out IShellItemImageFactory factory);

                        SIZE size = new() { cx = width, cy = height };
                        factory.GetImage(size, SIIGBF.ResizeToFit | SIIGBF.ThumbnailOnly, out IntPtr hBitmap);

                        if (hBitmap == IntPtr.Zero) return null;

                        var bmp = Image.FromHbitmap(hBitmap);
                        DeleteObject(hBitmap);

                        if (useCache)
                            _cache[cacheKey] = (Bitmap)bmp.Clone();

                        Debug.WriteLine($"Normal end: {Path.GetFileName(filePath)}");
                        return bmp;
                    }
                    catch (Exception ex)
                    {
                        int hr = Marshal.GetHRForException(ex);

                        if (hr == WINCODEC_ERR_COMPONENTNOTFOUND)
                        {
                            Debug.WriteLine($"コーデックが見つかりません: {Path.GetFileName(filePath)}");
                        }
                        else
                        {
                            Debug.WriteLine($"GetThumbnailAsync 失敗: {ex.Message} (HRESULT=0x{hr:X8})");
                        }

                        return null;
                    }
                });
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
