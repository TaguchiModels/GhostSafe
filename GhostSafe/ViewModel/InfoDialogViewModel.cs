using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace GhostSafe.ViewModel
{
    public class InfoDialogViewModel
    {
        public string Message { get; }
        public Brush BackgroundBrush { get; }

        public InfoDialogViewModel(string message)
        {
            Message = message;

            if (message.Contains("処理は完了しました") ||
                message.Contains("Processing completed"))
            {
                BackgroundBrush = Brushes.MediumPurple;
            }
            else
            {
                BackgroundBrush = Brushes.MediumVioletRed;
            }
        }
    }
}
