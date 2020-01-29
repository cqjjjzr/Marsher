using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using Marsher.Annotations;
using static Unclassified.TxLib.Tx;

namespace Marsher
{
    /// <summary>
    /// CollectorWindow.xaml 的交互逻辑
    /// </summary>
    public partial class CollectorWindow
    {
        private CollectorViewModel _viewModel;

        public CollectorWindow(QaListStubsViewModel listViewModel, Action<IList, IEnumerable> onDelete)
        {
            _viewModel = new CollectorViewModel(listViewModel);
            DataContext = _viewModel;
            InitializeComponent();
            QaList.OnDelete += onDelete;

            listViewModel.Locked = true;

            Title = T("ui.collector", "name", listViewModel.Name);
        }

        private void MetroWindow_Closed(object sender, EventArgs e)
        {
            _viewModel.StubsViewModel.Locked = false;
        }
    }

    public class CollectorViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<QaItem> ActiveQaItems { get; }
        public QaListStubsViewModel StubsViewModel { get; }

        public CollectorViewModel(QaListStubsViewModel stubs)
        {
            StubsViewModel = stubs;
            ActiveQaItems = stubs.PopulatedItems;

            WeakEventManager<ObservableCollection<QaItem>, NotifyCollectionChangedEventArgs>.AddHandler(ActiveQaItems, "CollectionChanged",
                (sender, args) =>
                {
                    EmptyListIndicatorVisibility = ActiveQaItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                });
            EmptyListIndicatorVisibility = ActiveQaItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private Visibility _emptyListIndicatorVisibility = Visibility.Collapsed;

        public Visibility EmptyListIndicatorVisibility
        {
            get => _emptyListIndicatorVisibility;
            set
            {
                _emptyListIndicatorVisibility = value;
                FireOnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void FireOnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
