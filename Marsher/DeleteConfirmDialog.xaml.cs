using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using MahApps.Metro.Controls.Dialogs;
using Unclassified.TxLib;

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

            PromptLabel.Text = Tx.T("dialog.remove_from_all", ItemsCount);
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
