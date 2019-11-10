using System;
using System.Collections.ObjectModel;

namespace Marsher
{
    /// <summary>
    /// CollectorWindow.xaml 的交互逻辑
    /// </summary>
    public partial class CollectorWindow
    {
        private CollectorViewModel _viewModel;

        public CollectorWindow(QaListStubsViewModel listViewModel)
        {
            InitializeComponent();

            _viewModel = new CollectorViewModel(listViewModel);
            DataContext = _viewModel;
            listViewModel.Locked = true;

            Title = $"Collector for {listViewModel.Name}";
        }

        private void MetroWindow_Closed(object sender, EventArgs e)
        {
            _viewModel.StubsViewModel.Locked = false;
        }
    }

    public class CollectorViewModel
    {
        public ObservableCollection<QaItem> ActiveQaItems { get; }
        public QaListStubsViewModel StubsViewModel { get; }

        public CollectorViewModel(QaListStubsViewModel stubs)
        {
            StubsViewModel = stubs;
            ActiveQaItems = stubs.PopulatedItems;
        }
    }
}
