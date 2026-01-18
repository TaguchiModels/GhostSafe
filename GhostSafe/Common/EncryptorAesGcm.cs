using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;

namespace GhostSafe.Common
{
    /// <summary>
    /// 暗号化 復号化クラス
    /// </summary>
    public static class EncryptorAesGcm
    {
        // ファイルヘッダ定義
        // [0..3]  : Magic "FENC"
        // [4]     : Version = 1
        // [5]     : AlgId   = 1 (AES-GCM)
        // [6..13] : EncryptedLength (Int64, little-endian)
        // [14]    : saltLen (byte)
        // [15]    : nonceLen (byte)
        // [16]    : tagLen (byte)
        // [17..]  : salt | nonce | tag | ciphertext | plainTail
        private const string Magic = "FENC";
        private const byte Version = 1;
        private const byte AlgId = 1;

        // PBKDF2 設定
        private const int SaltSize = 16;
        private const int KeySize = 32; // AES-256
        private const int Iterations = 10_000;

        // GCM パラメータ
        private const int NonceSize = 12;  // GCM 推奨 12 バイト
        private const int TagSize = 16;    // 128-bit 認証タグ

        /// <summary>
        /// アプリケーションで使用するマスターキーを生成する
        /// </summary>
        /// <remarks>
        /// 本メソッドは、設定ファイルに保存されたパスワードを
        /// DPAPI（<see cref="ProtectedData"/>）により復号し、
        /// 固定のアプリケーション用ソルトと PBKDF2
        /// （<see cref="Rfc2898DeriveBytes"/>）を使用して
        /// 暗号化処理に用いるマスターキーを導出します。
        /// 生成されるキーは現在のユーザー コンテキストに紐付けられます。
        /// </remarks>
        /// <returns>生成されたマスターキー（バイト配列）</returns>
        public static byte[] CreateMasterKey() 
        {
            byte[] data = Convert.FromBase64String(Properties.Settings.Default.Password);
            byte[] decrypted = ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
            string password = Encoding.UTF8.GetString(decrypted);

            // 固定のアプリ用Saltを使う（保存しておく）
            App.AppSalt = new byte[]
            {
                0x23, 0x7A, 0x1B, 0x44, 0x59, 0xAF, 0x90, 0x11,
                0xC4, 0x6E, 0x78, 0x9A, 0x2D, 0x33, 0xFE, 0x10
            };

            using var pbkdf2 = new Rfc2898DeriveBytes(password, App.AppSalt, Iterations, HashAlgorithmName.SHA256);
            return pbkdf2.GetBytes(KeySize);
        }

        /// <summary>
        /// 指定されたファイルを暗号化し、保護された出力ファイルを生成する
        /// </summary>
        /// <remarks>
        /// 本メソッドは、入力ファイルの先頭から最大 <paramref name="encryptSize"/> バイトを
        /// AES-GCM により暗号化し、残りのデータはそのまま出力ファイルへコピーします。
        /// 暗号化に必要なヘッダー情報（マジック値、バージョン、アルゴリズム ID、
        /// 暗号化データ長、各種パラメータ、ソルト、ノンス、認証タグ）は
        /// 独自定義のバイナリフォーマットでファイル先頭に書き込まれます。
        /// ヘッダー全体は AAD（Additional Authenticated Data）として
        /// 認証タグの計算に含められるため、ヘッダーの改ざんは検出されます。
        /// </remarks>
        /// <param name="inputPath">暗号化対象となる入力ファイルのパス</param>
        /// <param name="outputPath">暗号化後のデータを書き込む出力ファイルのパス</param>
        /// <param name="encryptSize">暗号化する最大バイト数。ファイルサイズがこれより小さい場合は、ファイル全体が暗号化されます：デフォルトは1MB</param>
        public static void ProtectFile(string inputPath, string outputPath, int encryptSize = 1024 * 1024)
        {
            using var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);

            long encLen = Math.Min(encryptSize, input.Length);
            byte[] plainHead = new byte[encLen];
            int readHead = input.Read(plainHead, 0, plainHead.Length);

            // ランダム材料
            byte[] nonce = RandomNumber(NonceSize);

            // ヘッダ書き込み
            WriteHeader(output, encLen, App.AppSalt.Length, nonce.Length, TagSize, App.AppSalt, nonce);

            // ヘッダ全体を AAD にして改ざん検出を強化
            // ただし、今はヘッダを書き終えた後なので、AAD は同じ内容を再構成する
            byte[] aad = BuildAad(encLen, App.AppSalt.Length, nonce.Length, TagSize);

            // GCM で暗号化
            byte[] ciphertext = new byte[readHead];
            byte[] tag = new byte[TagSize];
            using (var gcm = new AesGcm(App.MasterKey.AsSpan(), tagSizeInBytes: 16))
            {
                gcm.Encrypt(nonce, plainHead.AsSpan(0, readHead), ciphertext, tag, aad);
            }

            // tag を書き込み（ヘッダ直後の固定領域に配置）
            output.Write(tag, 0, tag.Length);
            // 暗号化済み先頭データ
            output.Write(ciphertext, 0, ciphertext.Length);

            // 残りをそのままコピー
            input.CopyTo(output);
        }

        /// <summary>
        /// 暗号化されたファイルを復号し、元のファイルを生成する
        /// </summary>
        /// <remarks>
        /// 本メソッドは <see cref="ProtectFile(string, string, int)"/> により生成された
        /// 保護ファイルを読み込み、ファイル先頭のヘッダーを検証した上で、
        /// AES-GCM により暗号化された先頭データを復号します。
        /// ヘッダー情報から再構築した AAD（Additional Authenticated Data）を
        /// 認証に使用するため、ファイル内容またはヘッダーが改ざんされている場合、
        /// 復号処理は失敗します。
        /// 復号後は、暗号化されていない残りのデータをそのまま出力ファイルへコピーします。
        /// </remarks>
        /// <param name="inputPath">復号対象となる暗号化ファイルのパス</param>
        /// <param name="outputPath">復号後のデータを書き込む出力ファイルのパス</param>
        /// <param name="encryptSize">
        /// 暗号化されている最大バイト数。
        /// 本パラメータはフォーマット互換性のために定義されていますが、
        /// 実際の復号範囲はファイルヘッダー内の情報に従います。
        /// </param>
        /// <exception cref="InvalidDataException">ヘッダー形式が不正、または認証タグ長が想定と異なる場合</exception>
        /// <exception cref="UnauthorizedAccessException">復号に失敗しました（パスワード誤り、またはファイル改ざんの可能性）</exception>
        public static void UnprotectFile(string inputPath, string outputPath, int encryptSize = 1024 * 1024)
        {
            using var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);

            // ヘッダ読み込み
            ReadHeader(input, out long encLen, out byte saltLen, out byte nonceLen, out byte tagLen, out byte[] salt, out byte[] nonce);

            if (tagLen != TagSize) throw new InvalidDataException("Unexpected GCM tag length.");

            // AAD を再構築
            byte[] aad = BuildAad(encLen, saltLen, nonceLen, tagLen);

            // 認証タグ
            byte[] tag = new byte[tagLen];
            ReadExact(input, tag, 0, tag.Length);

            // ciphertext 読み込み（encLen と同じ長さ）
            byte[] ciphertext = new byte[encLen];
            ReadExact(input, ciphertext, 0, ciphertext.Length);

            byte[] plainHead = new byte[encLen];
            try
            {
                using var gcm = new AesGcm(App.MasterKey.AsSpan(), tagSizeInBytes: 16);
                gcm.Decrypt(nonce, ciphertext, tag, plainHead, aad);
            }
            catch (CryptographicException)
            {
                throw new UnauthorizedAccessException("復号に失敗しました（パスワード誤り、またはファイル改ざんの可能性）。");
            }

            // 復号した先頭部分を書き出し
            output.Write(plainHead, 0, plainHead.Length);

            // 残りはそのままコピー
            input.CopyTo(output);
        }

        /// <summary>
        /// 暗号学的に安全な乱数バイト列を生成する
        /// </summary>
        /// <param name="len">生成するバイト配列の長さ（バイト数）</param>
        /// <returns>指定された長さの乱数バイト配列</returns>
        private static byte[] RandomNumber(int len)
        {
            byte[] b = new byte[len];
            RandomNumberGenerator.Fill(b);
            return b;
        }

        /// <summary>
        /// 暗号化データのヘッダー情報を指定されたストリームに書き込む
        /// </summary>
        /// <remarks>
        /// 本メソッドは、マジック値・バージョン・アルゴリズム ID・
        /// 暗号化後データ長・各種パラメータ長・ソルトおよびノンスを
        /// 所定のバイナリフォーマットで出力します
        /// 認証タグ（tag）は本メソッドでは書き込まず、後続処理で出力されます
        /// </remarks>
        /// <param name="output">ヘッダーを書き込む出力先ストリーム</param>
        /// <param name="encLen">暗号化後データの長さ（バイト数）</param>
        /// <param name="saltLen">ソルトの長さ（バイト数）</param>
        /// <param name="nonceLen">ノンスの長さ（バイト数）</param>
        /// <param name="tagLen">認証タグの長さ（バイト数）</param>
        /// <param name="salt">鍵導出に使用するソルト</param>
        /// <param name="nonce">暗号化に使用するノンス</param>
        private static void WriteHeader(Stream output, long encLen, int saltLen, int nonceLen, int tagLen, byte[] salt, byte[] nonce)
        {
            // Magic + Version + AlgId
            var magicBytes = Encoding.ASCII.GetBytes(Magic);
            output.Write(magicBytes, 0, magicBytes.Length);
            output.WriteByte(Version);
            output.WriteByte(AlgId);

            // EncryptedLength (Int64 LE)
            Span<byte> lenBuf = stackalloc byte[8];
            BinaryPrimitives.WriteInt64LittleEndian(lenBuf, encLen);
            output.Write(lenBuf);

            // lens
            output.WriteByte((byte)saltLen);
            output.WriteByte((byte)nonceLen);
            output.WriteByte((byte)tagLen);

            // salt, nonce
            output.Write(salt, 0, saltLen);
            output.Write(nonce, 0, nonceLen);
            // tag はこの後に書く
        }

        /// <summary>
        /// 認証付き暗号（AEAD）で使用する AAD（Additional Authenticated Data）を構築する
        /// </summary>
        /// <remarks>
        /// 本メソッドは、暗号化ヘッダーのうち改ざん検知の対象とするフィールド
        /// （マジック値、バージョン、アルゴリズム ID、暗号化後データ長、
        /// ソルト長、ノンス長、認証タグ長）を連結し、
        /// AAD として使用するバイト配列を生成します。
        /// AAD は暗号化対象には含まれませんが、認証タグの計算に含まれるため、
        /// ヘッダー改ざんの検出に使用されます。
        /// </remarks>
        /// <param name="encLen">暗号化後データの長さ（バイト数）</param>
        /// <param name="saltLen">ソルトの長さ（バイト数）</param>
        /// <param name="nonceLen">ノンスの長さ（バイト数）</param>
        /// <param name="tagLen">認証タグの長さ（バイト数）</param>
        /// <returns>AAD として使用するバイト配列</returns>
        private static byte[] BuildAad(long encLen, int saltLen, int nonceLen, int tagLen)
        {
            // Magic, Version, AlgId, EncryptedLength, saltLen, nonceLen, tagLen を AAD に
            byte[] aad = new byte[4 + 1 + 1 + 8 + 1 + 1 + 1];
            int off = 0;
            // Span版での安全な書き込み
            Encoding.ASCII.GetBytes(Magic.AsSpan(), aad.AsSpan(off));
            off += 4;
            aad[off++] = Version;
            aad[off++] = AlgId;
            BinaryPrimitives.WriteInt64LittleEndian(aad.AsSpan(off, 8), encLen); off += 8;
            aad[off++] = (byte)saltLen;
            aad[off++] = (byte)nonceLen;
            aad[off++] = (byte)tagLen;
            return aad;
        }

        /// <summary>
        /// 入力ストリームから暗号化データのヘッダーを読み取り、内容を検証する
        /// </summary>
        /// <remarks>
        /// 本メソッドは、マジック値・バージョン・アルゴリズム ID を検証した上で、
        /// 暗号化後データ長、各種パラメータ長（ソルト／ノンス／認証タグ）、
        /// およびソルトとノンスを読み込みます。
        /// フォーマットが想定と異なる場合は例外を送出します。
        /// </remarks>
        /// <param name="input">ヘッダーを読み取る入力元ストリーム</param>
        /// <param name="encLen">読み取った暗号化後データの長さ（バイト数）</param>
        /// <param name="saltLen">読み取ったソルトの長さ（バイト数）</param>
        /// <param name="nonceLen">読み取ったノンスの長さ（バイト数）</param>
        /// <param name="tagLen">読み取った認証タグの長さ（バイト数）</param>
        /// <param name="salt">読み取ったソルト</param>
        /// <param name="nonce">読み取ったノンス</param>
        /// <exception cref="InvalidDataException">マジック値、バージョン、またはアルゴリズム ID が想定と一致しない場合</exception>
        private static void ReadHeader(Stream input, out long encLen, out byte saltLen, out byte nonceLen, out byte tagLen, out byte[] salt, out byte[] nonce)
        {
            // Magic
            byte[] magic = new byte[4];
            ReadExact(input, magic, 0, 4);
            if (Encoding.ASCII.GetString(magic) != Magic) throw new InvalidDataException("Magic mismatch.");

            int ver = input.ReadByte();
            if (ver != Version) throw new InvalidDataException($"Unsupported version: {ver}");

            int alg = input.ReadByte();
            if (alg != AlgId) throw new InvalidDataException($"Unsupported algorithm id: {alg}");

            Span<byte> lenBuf = stackalloc byte[8];
            ReadExact(input, lenBuf);
            encLen = BinaryPrimitives.ReadInt64LittleEndian(lenBuf);

            saltLen = (byte)input.ReadByte();
            nonceLen = (byte)input.ReadByte();
            tagLen = (byte)input.ReadByte();

            salt = new byte[saltLen];
            nonce = new byte[nonceLen];
            ReadExact(input, salt, 0, saltLen);
            ReadExact(input, nonce, 0, nonceLen);
        }

        /// <summary>
        /// 指定された長さのデータを、ストリームから必ず読み取る
        /// </summary>
        /// <remarks>
        /// 本メソッドは、<paramref name="buffer"/> が満たされるまで
        /// ストリームから読み取りを繰り返します。
        /// 読み取り途中でストリームの終端に達した場合は例外を送出します。
        /// </remarks>
        /// <param name="s">読み取り元のストリーム</param>
        /// <param name="buffer">読み取ったデータを書き込むバッファ</param>
        /// <exception cref="EndOfStreamException"><paramref name="buffer"/> を満たす前にストリームの終端に達した場合</exception>
        private static void ReadExact(Stream s, Span<byte> buffer)
        {
            int total = 0;
            while (total < buffer.Length)
            {
                int read = s.Read(buffer.Slice(total));
                if (read <= 0) throw new EndOfStreamException();
                total += read;
            }
        }

        /// <summary>
        /// 指定したバイト配列の範囲を、ストリームから必ず読み取る
        /// </summary>
        /// <remarks>
        /// 本メソッドは <see cref="ReadExact(Stream, Span{byte})"/> の配列向けラッパーです。
        /// <paramref name="buffer"/> の <paramref name="offset"/> から
        /// <paramref name="count"/> バイト分が満たされるまで読み取りを行います。
        /// </remarks>
        /// <param name="s">読み取り元のストリーム</param>
        /// <param name="buffer">読み取ったデータを書き込むバイト配列</param>
        /// <param name="offset">書き込みを開始する配列内の位置</param>
        /// <param name="count">読み取るバイト数</param>
        private static void ReadExact(Stream s, byte[] buffer, int offset, int count)
            => ReadExact(s, new Span<byte>(buffer, offset, count));

        /// <summary>
        /// 指定された文字列を暗号化し、暗号化ファイルとして保存する
        /// </summary>
        /// <remarks>
        /// 本メソッドは、入力された文字列を UTF-8 バイト列に変換し、
        /// 先頭から最大 <paramref name="encryptSize"/> バイトを AES-GCM により暗号化します。
        /// 残りのデータは暗号化せず、そのまま出力ファイルに書き込まれます。
        /// 暗号化ファイルには、マジック値、バージョン、アルゴリズム ID、
        /// 暗号化データ長、各種パラメータ長、ソルト、ノンス、認証タグが
        /// 独自定義のバイナリフォーマットで格納されます。
        /// ヘッダー情報は AAD（Additional Authenticated Data）として
        /// 認証タグの計算に含まれるため、改ざん検出が可能です。
        /// </remarks>
        /// <param name="inputText">暗号化対象となる文字列（UTF-8 としてエンコードされます）</param>
        /// <param name="outputPath">暗号化後のデータを書き込む出力ファイルのパス</param>
        /// <param name="encryptSize">暗号化する最大バイト数。文字列のバイト長がこれより小さい場合は、全体が暗号化されます：デフォルトは1MB</param>
        public static void ProtectText(string inputText, string outputPath, int encryptSize = 1024 * 1024)
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(inputText);
            long encLen = Math.Min(encryptSize, plainBytes.Length);

            byte[] nonce = RandomNumber(NonceSize);
            byte[] aad = BuildAad(encLen, App.AppSalt.Length, nonce.Length, TagSize);

            byte[] cipherHead = new byte[encLen];
            byte[] tag = new byte[TagSize];

            using (var gcm = new AesGcm(App.MasterKey.AsSpan(), tagSizeInBytes: 16))
            {
                gcm.Encrypt(nonce, plainBytes.AsSpan(0, (int)encLen), cipherHead, tag, aad);
            }

            using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);

            // ヘッダ
            fs.Write(Encoding.ASCII.GetBytes(Magic));
            fs.WriteByte(Version);
            fs.WriteByte(AlgId);

            Span<byte> lenBuf = stackalloc byte[8];
            BinaryPrimitives.WriteInt64LittleEndian(lenBuf, encLen);
            fs.Write(lenBuf);

            fs.WriteByte((byte)App.AppSalt.Length);
            fs.WriteByte((byte)nonce.Length);
            fs.WriteByte((byte)tag.Length);

            // salt, nonce, tag
            fs.Write(App.AppSalt, 0, App.AppSalt.Length);
            fs.Write(nonce, 0, nonce.Length);
            fs.Write(tag, 0, tag.Length);

            // 暗号化した先頭部分
            fs.Write(cipherHead, 0, cipherHead.Length);

            // 残りは平文でそのまま
            if (plainBytes.Length > encLen)
            {
                fs.Write(plainBytes, (int)encLen, plainBytes.Length - (int)encLen);
            }
        }

        /// <summary>
        /// 指定された暗号化ファイルを読み込み、復号して文字列として返す
        /// </summary>
        /// <remarks>
        /// 本メソッドは、独自定義された暗号化フォーマットに基づき、
        /// ファイル先頭のヘッダー（マジック値、バージョン、アルゴリズム ID、
        /// 暗号化データ長、各種パラメータ）を検証した後、
        /// AES-GCM により暗号化された本文を復号します。
        /// ヘッダー情報から生成した AAD（Additional Authenticated Data）を用いて
        /// 認証を行うため、ファイル内容が改ざんされている場合は復号に失敗します。
        /// </remarks>
        /// <param name="encryptedFilePath">復号対象となる暗号化ファイルのパス</param>
        /// <returns>復号された UTF-8 文字列</returns>
        /// <exception cref="InvalidOperationException">マジック値、バージョン、またはアルゴリズム ID が想定と一致しない場合</exception>
        public static string UnprotectText(string encryptedFilePath)
        {
            byte[] encryptedData = File.ReadAllBytes(encryptedFilePath);
            int off = 0;

            if (Encoding.ASCII.GetString(encryptedData, off, 4) != Magic) throw new InvalidOperationException("Magic mismatch");
            off += 4;

            byte ver = encryptedData[off++];
            if (ver != Version) throw new InvalidOperationException($"Unsupported version {ver}");

            byte alg = encryptedData[off++];
            if (alg != AlgId) throw new InvalidOperationException($"Unsupported algorithm {alg}");

            long encLen = BinaryPrimitives.ReadInt64LittleEndian(encryptedData.AsSpan(off, 8));
            off += 8;

            byte saltLen = encryptedData[off++];
            byte nonceLen = encryptedData[off++];
            byte tagLen = encryptedData[off++];

            byte[] salt = encryptedData.AsSpan(off, saltLen).ToArray(); off += saltLen;
            byte[] nonce = encryptedData.AsSpan(off, nonceLen).ToArray(); off += nonceLen;
            byte[] tag = encryptedData.AsSpan(off, tagLen).ToArray(); off += tagLen;

            byte[] cipherHead = encryptedData.AsSpan(off, (int)encLen).ToArray(); off += (int)encLen;
            byte[] tail = encryptedData.AsSpan(off).ToArray();

            byte[] aad = BuildAad(encLen, saltLen, nonceLen, tagLen);

            byte[] plainHead = new byte[encLen];
            using (var gcm = new AesGcm(App.MasterKey.AsSpan(), tagSizeInBytes: 16))
            {
                gcm.Decrypt(nonce, cipherHead, tag, plainHead, aad);
            }

            byte[] allBytes = new byte[plainHead.Length + tail.Length];
            plainHead.CopyTo(allBytes, 0);
            tail.CopyTo(allBytes, plainHead.Length);

            return Encoding.UTF8.GetString(allBytes);
        }

        /// <summary>
        /// ランダムな英数字を生成（大文字・小文字含む）
        /// </summary>
        /// <remarks>暗号化ファイルのファイル名として利用しています</remarks>
        /// <param name="length">生成文字数を指定します</param>
        /// <returns>ランダム英数文字列を返します</returns>
        public static string GenerateRandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

    }
}
