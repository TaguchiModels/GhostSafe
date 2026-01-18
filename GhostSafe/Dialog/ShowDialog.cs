using MaterialDesignThemes.Wpf;
using GhostSafe.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GhostSafe.Dialog
{
    public class ShowDialog
    {
        /// <summary>
        /// 処理完了のダイアログ表示
        /// </summary>
        /// <param name="message"></param>
        /// <returns>ダイアログの表示</returns>
        public async static Task ShowDialogsAsync(string message)
        {
            var dialog = new InfoDialog
            {
                DataContext = new InfoDialogViewModel(message)
            };

            await DialogHost.Show(dialog, "RootDialog");
        }

    }


}
