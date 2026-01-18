using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace GhostSafe.Common
{
    /// <summary>
    /// ファイルモデル
    /// </summary>
    public class FileModel
    {
        public string Name { get; set; } = string.Empty;
        public DateTime Modified { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public string EncryptName { get; set; } = string.Empty;
    }

    /// <summary>
    /// アイコンモデル
    /// </summary>
    public class IconModel
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string CurrentPath { get; set; } = string.Empty;
        public string EncryptName { get; set; } = string.Empty;
        public ImageSource? IconOrThumbnail { get; set; }
    }

}
