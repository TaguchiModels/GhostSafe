using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GhostSafe.Common
{
    public static class ValidFolderName
    {
        /// <summary>
        /// 指定された名前が、Windows ファイルシステムで使用できない予約名かどうかを判定する
        /// </summary>
        /// <remarks>
        /// 本メソッドは、Windows においてファイル名またはフォルダ名として
        /// 使用できないデバイス予約名（例: CON, PRN, AUX, NUL, COM1～COM9, LPT1～LPT9）
        /// に該当するかどうかを判定します。
        /// 大文字・小文字は区別せずに比較されます。
        /// </remarks>
        /// <param name="name">判定対象となるファイル名またはフォルダ名</param>
        /// <returns>
        /// <paramref name="name"/> が Windows の予約名である場合は true、
        /// それ以外の場合は false。
        /// </returns>
        private static bool IsReservedName(string name)
        {
            string[] reserved =
            {
                "CON","PRN","AUX","NUL",
                "COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9",
                "LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9"
            };

            return reserved.Contains(name.ToUpperInvariant());
        }

        /// <summary>
        /// 指定された文字列が、Windows 環境で有効なファイル名またはフォルダ名かどうかを判定する
        /// </summary>
        /// <remarks>
        /// 本メソッドは、以下の条件をすべて満たす場合にのみ
        /// 有効なフォルダ名であると判断します。
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// null、空文字、または空白のみで構成されていないこと
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// Windows で使用できない文字（<see cref="Path.GetInvalidFileNameChars"/>）を含まないこと
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// Windows のデバイス予約名（CON, PRN, AUX, NUL, COM1～COM9, LPT1～LPT9）
        /// に該当しないこと
        /// </description>
        /// </item>
        /// </list>
        /// </remarks>
        /// <param name="name">検証対象となるファイル名またはフォルダ名</param>
        /// <returns>
        /// <paramref name="name"/> が有効なファイル名またはフォルダ名である場合は true、
        /// 無効な場合は false。
        /// </returns>
        public static bool IsValidFolderName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return false;

            if (IsReservedName(name))
                return false;

            return true;
        }

    }
}
