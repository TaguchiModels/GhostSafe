using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GhostSafe.ViewModel
{
    public class ConfirmDialogViewModel
    {
        public string Question { get; set; }
        public ConfirmDialogViewModel(string question) => Question = question;
    }
}
