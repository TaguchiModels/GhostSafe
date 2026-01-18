using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GhostSafe.Common
{
    /// <summary>
    /// ビデオファイルのクラス
    /// </summary>
    public class VideoChild
    {
        public string PipeName { get; set; }
        public StreamWriter Writer { get; set; }
        public Process Process { get; set; }
        public string VideoPath { get; set; }

        public bool IsAlive => Process != null && !Process.HasExited;
    }
}
