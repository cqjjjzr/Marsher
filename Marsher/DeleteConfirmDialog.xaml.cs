using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MahApps.Metro.Controls.Dialogs;

namespace Marsher
{
    /// <summary>
    /// DeleteConfirmDialog.xaml 的交互逻辑
    /// </summary>
    public partial class DeleteConfirmDialog
    {
        private IList<QaItem> Items { get; }
        private int ItemsCount => Items.Count;

        public DeleteConfirmDialog(IList<QaItem> items)
        {
            Items = items;
            InitializeComponent();
            PromptLabel.DataContext = this;
            ItemList.ItemsSource = Items;
        }

        public Task<MessageDialogResult> WaitUntilButton()
        {
            var result = new TaskCompletionSource<MessageDialogResult>();
            OKButton.Click += (sender, args) => result.TrySetResult(MessageDialogResult.Affirmative);
            CancelButton.Click += (sender, args) => result.TrySetResult(MessageDialogResult.Negative);

            return result.Task;
        }

        private void BaseMetroDialog_Loaded(object sender, RoutedEventArgs e)
        {
            OKButton.Focus();
        }
    }
}
