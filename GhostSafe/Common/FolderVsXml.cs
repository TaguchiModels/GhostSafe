using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace GhostSafe.Common
{
    static public class FolderVsXml
    {
        /// <summary>
        /// 指定されたディレクトリ配下の内容を再帰的に走査し、XML 要素として構築する
        /// </summary>
        /// <remarks>
        /// 本メソッドは、指定ディレクトリ内の <c>*.ghostsafe</c> ファイルを対象に、
        /// ファイル名および復号した内容を XML 要素としてまとめます。
        /// 各ファイルは <c>EncryptorAesGcm.UnprotectText</c> により復号され、
        /// その結果に含まれる複数の <c>&lt;String&gt;</c> 要素を解析して
        /// XML ツリーに追加します。
        /// また、サブディレクトリについても再帰的に処理を行い、
        /// ディレクトリ階層を反映した XML 構造を生成します。
        /// ファイルの復号や解析に失敗した場合は例外を捕捉し、
        /// 該当ファイルはスキップされます。
        /// </remarks>
        /// <param name="dir">処理対象となるディレクトリ情報</param>
        /// <returns>ディレクトリ構造およびファイル内容を表す <see cref="XElement"/></returns>
        static XElement CreateFolderElement(DirectoryInfo dir)
        {
            XElement folderElement = new XElement("Folder",
                new XAttribute("name", dir.Name)
            );

            foreach (var file in dir.GetFiles("*.ghostsafe"))
            {
                XElement fileElement = new XElement("File",
                    new XAttribute("name", file.Name)
                );

                try
                {
                    string unencText = EncryptorAesGcm.UnprotectText(file.FullName).Trim();
                    // XMLとして安全なように & をエスケープ
                    string escapedContent = unencText.Replace("&", "&amp;");

                    // 複数の <string>...</string> をパース
                    var tempXml = XElement.Parse("<Root>" + escapedContent + "</Root>");
                    foreach (var stringElement in tempXml.Elements("String"))
                    {
                        fileElement.Add(new XElement("String", stringElement.Value));
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error:" + ex.Message);
                }

                folderElement.Add(fileElement);
            }

            // 再帰的にサブフォルダも処理
            foreach (var subDir in dir.GetDirectories())
            {
                folderElement.Add(CreateFolderElement(subDir));
            }

            return folderElement;
        }

        /// <summary>
        /// XML ファイルからフォルダおよびファイル構造を復元する
        /// </summary>
        /// <remarks>
        /// 本メソッドは、指定された XML ファイルを読み込み、
        /// そのルート要素を基点としてフォルダおよびファイル構造を生成します。
        /// 復元先のルートディレクトリには、現在のユーザーの
        /// アプリケーションデータフォルダ（ApplicationData）が使用されます。
        /// 実際のフォルダおよびファイル生成処理は
        /// <c>CreateFolderFromXml</c> に委譲されます。
        /// </remarks>
        /// <param name="xmlPath">復元元となる XML ファイルのパス</param>
        static public void XmlToFolder(string xmlPath)
        {
            XDocument xmlDoc = XDocument.Load(xmlPath);
            XElement rootElement = xmlDoc.Root;

            string AppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            CreateFolderFromXml(rootElement, AppData);
        }

        /// <summary>
        /// XML 要素に定義されたフォルダおよびファイル構造を再帰的に生成する
        /// </summary>
        /// <remarks>
        /// 本メソッドは、<c>Folder</c> 要素を基点としてフォルダを作成し、
        /// その配下に定義された <c>File</c> 要素から暗号化ファイルを生成します。
        /// 各 <c>File</c> 要素内の複数の <c>String</c> 要素は連結され、
        /// 暗号化対象の文字列として <see cref="EncryptorAesGcm.ProtectText"/> に渡されます。
        /// 既に同名のフォルダまたはファイルが存在する場合は作成をスキップします。
        /// サブフォルダについては再帰的に処理され、
        /// XML 構造と同一のディレクトリ階層が復元されます。
        /// </remarks>
        /// <param name="folderElement">作成対象となるフォルダを表す XML 要素</param>
        /// <param name="currentPath">フォルダを作成する基準パス</param>
        static void CreateFolderFromXml(XElement folderElement, string currentPath)
        {
            string folderName = folderElement.Attribute("name")?.Value;
            if (string.IsNullOrEmpty(folderName)) return;

            string fullPath = Path.Combine(currentPath, folderName);

            // フォルダがなければ作成
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
            }

            // ファイルを作成
            foreach (var fileElement in folderElement.Elements("File"))
            {
                string fileName = fileElement.Attribute("name")?.Value;
                if (string.IsNullOrEmpty(fileName)) continue;

                string filePath = Path.Combine(fullPath, fileName);

                // すでにファイルがあればスキップ
                if (File.Exists(filePath)) continue;

                try
                {                    
                    string xmlstring = "";
                    
                    foreach (var stringElement in fileElement.Elements("String"))
                    {
                        xmlstring += $"<String>{stringElement.Value}</String>";
                    }

                    // XMLのエスケープを元に戻す
                    string escapedContent = xmlstring.Replace("&amp;", "&");

                    EncryptorAesGcm.ProtectText(xmlstring, filePath); // 暗号化
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"ファイル作成エラー: {filePath}, {ex.Message}");
                }
            }

            // サブフォルダの処理（再帰）
            foreach (var subFolder in folderElement.Elements("Folder"))
            {
                CreateFolderFromXml(subFolder, fullPath);
            }
        }
    }
}
