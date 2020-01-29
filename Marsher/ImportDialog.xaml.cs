using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using MahApps.Metro.Controls.Dialogs;
using Unclassified.TxLib;
// ReSharper disable ConvertIfStatementToSwitchStatement

namespace Marsher
{
    public partial class ImportDialog
    {
        private ImportDialogViewModel _viewModel;

        public ImportDialog(string title, string message, string defaultNewName, MainViewModel mainViewModel, Func<string, string> checkValidFunc)
        {
            InitializeComponent();
            Title = title;
            _viewModel = new ImportDialogViewModel(message, mainViewModel, checkValidFunc)
            {
                Input = defaultNewName
            };
            DataContext = _viewModel;
        }

        public Task<Tuple<MessageDialogResult, bool, object>> WaitUntilButton()
        {
            var result = new TaskCompletionSource<Tuple<MessageDialogResult, bool, object>>();
            OKButton.Click += (sender, args) =>
                result.TrySetResult(new Tuple<MessageDialogResult, bool, object>(
                    MessageDialogResult.Affirmative,
                    _viewModel.ImportingToNew,
                    _viewModel.ImportingToNew ? _viewModel.Input : _viewModel.SelectedQaList));
            CancelButton.Click += (sender, args) => result.TrySetResult(new Tuple<MessageDialogResult, bool, object>(MessageDialogResult.Negative, false, null));

            return result.Task;
        }

        private void BaseMetroDialog_Loaded(object sender, RoutedEventArgs e)
        {
            OKButton.Focus();
        }
    }

    public class ImportDialogViewModel : InputWithCheckDialogViewModel
    {
        private readonly MainViewModel _mainViewModel;
        private bool _importingToNew = false;
        private bool _importingToExisting = true;
        private bool _importingAllowed = true;
        private object _selectedQaList;

        public ImportDialogViewModel(string message, MainViewModel mainViewModel, Func<string, string> checkValidFunc) : base(message, checkValidFunc)
        {
            _mainViewModel = mainViewModel;
            _selectedQaList = AllQaItemsHolder[0];
            PropertyChanged += (sender, e) =>
            {
                if ((e.PropertyName == nameof(SelectedQaList)
                     || e.PropertyName == nameof(ImportingToExisting)) && ImportingToExisting)
                    ImportingAllowed = SelectedQaList != null;
                else if ((e.PropertyName == nameof(ValidInput)
                         || e.PropertyName == nameof(ImportingToNew)) && ImportingToNew)
                    ImportingAllowed = ValidInput;
            };
        }

        public bool ImportingToExisting
        {
            get => _importingToExisting;
            set
            {
                _importingToExisting = value;
                OnPropertyChanged();
            }
        }

        public bool ImportingToNew
        {
            get => _importingToNew;
            set
            {
                _importingToNew = value;
                OnPropertyChanged();
            }
        }

        public bool ImportingAllowed
        {
            get => _importingAllowed;
            set
            {
                _importingAllowed = value;
                OnPropertyChanged();
            }
        }

        public object SelectedQaList
        {
            get => _selectedQaList;
            set
            {
                _selectedQaList = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<QaListObservable> AllQaItemsHolder => _mainViewModel.AllQaItemsHolder;
        public ObservableCollection<QaListStubsViewModel> QaListStubs => _mainViewModel.QaListStubs;
    }
}
