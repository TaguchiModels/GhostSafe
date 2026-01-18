using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GhostSafe.ViewModel
{
    public class InputDialogViewModel : INotifyPropertyChanged
    {
        public string Prompt { get; set; }
        private string _input;
        public string Input
        {
            get => _input;
            set
            {
                _input = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Input)));
            }
        }

        public InputDialogViewModel(string prompt, string input = "")
        {
            Prompt = prompt;
            Input = input;
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
