using DirectShowLib;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace GhostSafe.Common
{
    public static class PresenceVideoCodecs
    {
        [DllImport("Mfplat.dll", ExactSpelling = true)]
        static extern int MFStartup(int version, int flags = 0);

        [DllImport("Mfplat.dll", ExactSpelling = true)]
        static extern int MFShutdown();

        [DllImport("Mfreadwrite.dll", ExactSpelling = true)]
        static extern int MFCreateSourceReaderFromURL(
            [MarshalAs(UnmanagedType.LPWStr)] string pwszURL,
            IntPtr pAttributes,
            out IntPtr ppSourceReader);

        /// <summary>
        /// 指定された動画ファイルが、現在の実行環境で再生可能かどうかを判定する
        /// </summary>
        /// <remarks>
        /// 本メソッドは、まず Media Foundation を使用して
        /// 動画ファイルの読み込み可否を確認します。
        /// Media Foundation で再生不可と判定された場合は、
        /// フォールバックとして DirectShow を用いた再生可否チェックを行います。
        /// <para>
        /// いずれかの方式で再生可能と判断された場合は true を返し、
        /// 両方で失敗した場合は false を返します。
        /// </para>
        /// <para>
        /// 主に、動画サムネイル生成や再生前チェックなど、
        /// 「コーデックの有無」を事前に確認したい用途を想定しています。
        /// </para>
        /// </remarks>
        /// <param name="path">再生可否を判定する動画ファイルのパス</param>
        /// <returns>
        /// 動画ファイルが再生可能な場合は true、
        /// 再生不可またはエラーが発生した場合は false。
        /// </returns>
        public static bool GetPresenceVideoCodecs(string path)
        {
            int hr = MFStartup(0x20070); // Windows10以降のMFバージョン
            if (hr != 0)
            {
                Debug.WriteLine($"MFStartup failed: 0x{hr:X8}");
                return false;
            }

            Debug.WriteLine($"Testing: {path}");

            if (!File.Exists(path))
            {
                Debug.WriteLine($"NG:ファイルなし");
                return false;
            }

            hr = MFCreateSourceReaderFromURL(path, IntPtr.Zero, out IntPtr reader);
            if (hr == 0)
            {
                Debug.WriteLine($"OK:再生可能");
                Marshal.Release(reader);
            }
            else
            {
                Debug.WriteLine($"NG:再生不可 (HRESULT=0x{hr:X8})");
                Debug.WriteLine($"DirectShowで検査！");

                try
                {
                    IGraphBuilder graphBuilder = (IGraphBuilder)new FilterGraph();
                    int hr2 = graphBuilder.RenderFile(path, null);

                    if (hr2 == 0)
                    {
                        Debug.WriteLine($"{path} : OK DirectShowで再生可能");
                    }
                    else
                    {
                        Debug.WriteLine($"{path} : NG DirectShow再生不可 (HRESULT=0x{hr2:X8})");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"例外: {ex.Message}");
                    return false;
                }

            }

            return true;

        }
    }
}
