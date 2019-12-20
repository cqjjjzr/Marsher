using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using GongSolutions.Wpf.DragDrop;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using MahApps.Metro.IconPacks;
using Marsher.Annotations;
using Microsoft.EntityFrameworkCore;
using static Unclassified.TxLib.Tx;
using TimeSpan = System.TimeSpan;

namespace Marsher
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow
    {
        private readonly MarsherUpdateManager _updateManager;

        private readonly MainViewModel _viewModel;

        private readonly MarshmallowService _marshmallowService;
        private readonly PeingService _peingService;
        private readonly QaDataContext _database = new QaDataContext();
        private readonly LocalListPersistence _localListPersistence;
        private readonly DisplayCommunication _displayCommunication;

        private readonly DelayAction _saveDatabaseAction = new DelayAction();
        private readonly DelayAction _saveListAction = new DelayAction();
        private Task _currentTask;
        public MainWindow()
        {
            LoadFromXmlFile(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly()?.Location ?? "Marsher.exe"), "Resources", "dictionary.txd"));
            _updateManager = new MarsherUpdateManager();
            _updateManager.CheckUpdate();

            try
            {
                IEHelper.EnsureBrowserEmulationEnabled(AppDomain.CurrentDomain.FriendlyName);
            }
            catch (IeVersionTooOldException)
            {
                MessageBox.Show(
                    T("dialog.no_ie"),
                    T("dialog.header.warning"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                System.Diagnostics.Process.Start("https://support.microsoft.com/en-us/help/18520/download-internet-explorer-11-offline-installer");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    T("dialog.ie_fail",
                        "exception", ex.ToString()),
                    T("dialog.header.warning"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            InitializeComponent();
            _localListPersistence = new LocalListPersistence();
            _viewModel = new MainViewModel(_database, _localListPersistence, DialogCoordinator.Instance)
            {
                Version = "v" + _updateManager.GetCurrentVersion()
            };
            DataContext = _viewModel;
            _database.Database.EnsureCreatedAsync();
            //_database.Items.LoadAsync();
            //_viewModel.LoadNextPage();

            QaListSelector.SelectedIndex = 0;
            //QaList.ItemsSource = _viewModel.ActiveQaList;
            _peingService = new PeingService();
            _peingService.OnLoginStatusChanged += (sender, status) =>
                Dispatcher?.Invoke(() =>
                {
                    if (sender != _peingService) return;
                    _viewModel.UpdatePeingStatus(status);
                    if (status == ServiceStatus.Available)
                        _viewModel.StatusText = T("status.logged_in.peing");
                    else if (status == ServiceStatus.NotLoggedIn) _viewModel.StatusText = T("status.dropped.peing");
                });
            _marshmallowService = new MarshmallowService();
            _marshmallowService.OnLoginStatusChanged += (sender, status) =>
                Dispatcher?.Invoke(() =>
                {
                    if (sender != _marshmallowService) return;
                    _viewModel.UpdateMarshmallowStatus(status);
                    if (status == ServiceStatus.Available)
                        _viewModel.StatusText = T("status.logged_in.marshmallow");
                    else if (status == ServiceStatus.NotLoggedIn) _viewModel.StatusText = T("status.dropped.marshmallow");
                });

            _viewModel.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName != nameof(_viewModel.ActiveQaList)) return;
                var list = _viewModel.ActiveQaList;
                switch (list)
                {
                    case QaListStubsViewModel stubs:
                        _viewModel.ActiveQaItems = stubs.PopulatedItems;
                        break;
                    case QaList qaList:
                        var coll2 = new ObservableCollection<QaItem>(qaList.Items);
                        _viewModel.ActiveQaItems = coll2;
                        break;
                    case QaListObservable qaListObservable:
                        _viewModel.ActiveQaItems = qaListObservable.Items;
                        break;
                }
            };

            foreach (var stubs in _localListPersistence.GetAllStubs())
            {
                var vm = new QaListStubsViewModel(stubs, _database);
                vm.CollectionChanged += OnQaListModified;
                vm.PropertyChanged += OnQaListLockStatusChanged;
                _viewModel.QaListStubs.Add(vm);
            }
            _localListPersistence.OnListModified += (sender, args) =>
            {
                Dispatcher?.InvokeAsync(() =>
                {
                    switch (args.Action)
                    {
                        case NotifyCollectionChangedAction.Add:
                            var vm = new QaListStubsViewModel((QaListStubs) args.NewItems[0], _database);
                            vm.PropertyChanged += OnQaListLockStatusChanged;
                            vm.CollectionChanged += OnQaListModified;
                            _viewModel.QaListStubs.Add(vm);

                            QaListSelector.SelectedItem = vm;
                            OpenCollectorFor(vm);
                            break;
                        case NotifyCollectionChangedAction.Remove:
                            _viewModel.QaListStubs.Remove(_viewModel.QaListStubs.FirstOrDefault(it => (QaListStubs)args.OldItems[0] == it.Underlying));
                            break;
                        case NotifyCollectionChangedAction.Replace:
                            _viewModel.QaListStubs.FirstOrDefault(it => (QaListStubs)args.NewItems[0] == it.Underlying)?.OnPropertyChanged(nameof(QaListStubsViewModel.Name));
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                });
            };

            _displayCommunication = new DisplayCommunication();
            try
            {
                _displayCommunication.Start();
                _viewModel.ServerStatusText = T("status.display.running", "port", DisplayCommunication.DisplayWSPort.ToString());
            }
            catch (Exception)
            {
                _viewModel.ServerStatusText = T("status.display.failed");
            }

            if (File.Exists("resources/index_preview.html"))
                PreviewBrowser.NavigateToStream(File.Open("resources/index_preview.html", FileMode.Open, FileAccess.Read));

            QaList.OnDelete += OnDelete;
            QaList.ViewModel.OnRequestingImport += OnRequestingImport;
        }

        private void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_updateManager.Updated) return;
            Task.Delay(TimeSpan.FromSeconds(3)).ContinueWith(t =>
            {
                var notes = File.ReadAllText(System.IO.Path.Combine(
                    // ReSharper disable once AssignNullToNotNullAttribute
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly()?.Location ??
                                                    "Marsher.exe"), "Resources", "RELEASES.md"));
                Dispatcher?.Invoke(() =>
                {
                    var relNoteDisplay = new ReleaseNoteDisplay(notes);
                    relNoteDisplay.Show();
                    relNoteDisplay.Focus();
                });
            });
        }

        #region Login

        private void LoginCommand_Click(object sender, RoutedEventArgs e)
        {
            LoginContextMenu.PlacementTarget = (UIElement)sender;
            LoginContextMenu.IsOpen = true;
        }


        private void LoginToMarshmallowContextMenuItem_Click(object sender, RoutedEventArgs e)
        {
            OpenLoginWindow(
                "https://marshmallow-qa.com",
                "https://marshmallow-qa.com/messages/personal",
                T("ui.commands.login.marshmallow"), _marshmallowService);
        }

        private void LoginToPeingContextMenuItem_Click(object sender, RoutedEventArgs e)
        {
            OpenLoginWindow(
                "https://peing.net/",
                "https://peing.net/ja/stg",
                T("ui.commands.login.peing"), _peingService);
        }

        private void OpenLoginWindow(string url, string cookieUrl, string title, Service service)
        {
            var window = new ServiceLoginWindow();
            window.Initialize(new Uri(url), new Uri(cookieUrl), title);

            window.ShowDialog();
            if (window.ResultContainer != null)
                service.UpdateCookie(window.ResultContainer);
        }

        private void LogoutContextMenuItem_Click(object sender, RoutedEventArgs e)
        {
            _marshmallowService.ClearCookie();
        }

        #endregion

        private MetroDialogSettings CreateMetroDialogSettings()
        {
            return new MetroDialogSettings()
            {
                DefaultText = "",
                AffirmativeButtonText = T("dialog.ok"),
                NegativeButtonText = T("dialog.cancel")
            };
        }

        #region List Operations
        private async void ListRenameButton_Click(object sender, RoutedEventArgs e)
        {
            var objList = _viewModel.ActiveQaList;
            if (objList == _viewModel.AllQaItemsList) return;
            var list = ((QaListStubsViewModel)objList).Underlying;
            var oldName = list.Name;

            _viewModel.FixAirspace = true;

            var dialog = new InputWithCheckDialog(T("dialog.header.rename"), T("dialog.rename_list", "oldName", oldName),
                oldName, s =>
                {
                    if (!_localListPersistence.CheckValidListName(s)) return T("dialog.invalid_list_name", "name", s);
                    if (_localListPersistence.CheckDuplicateListName(s, list)) return T("dialog.list_name_already_exists", "name", s);
                    return null;
                });
            await this.ShowMetroDialogAsync(dialog, CreateMetroDialogSettings());
            var (result, name) = await dialog.WaitUntilButton();
            await this.HideMetroDialogAsync(dialog);
            if (result != MessageDialogResult.Affirmative) return;
            try
            {
                list.Name = name;
                _localListPersistence.UpdateList(list, true);
            }
            catch (Exception ex)
            {
                await this.ShowMessageAsync(T("dialog.header.error"), T("dialog.rename_failed", "name", name, "exception", ex.ToString()), MessageDialogStyle.Affirmative, CreateMetroDialogSettings());
                list.Name = oldName;
            }

            _viewModel.FixAirspace = false;
        }

        private async void ListRemoveButton_Click(object sender, RoutedEventArgs e)
        {
            var objList = _viewModel.ActiveQaList;
            if (objList == _viewModel.AllQaItemsList) return;
            var list = ((QaListStubsViewModel)objList).Underlying;

            _viewModel.FixAirspace = true;
            var result = await this.ShowMessageAsync(T("dialog.header.confirm"), T("dialog.remove_confirm", "name", list.Name), MessageDialogStyle.AffirmativeAndNegative, CreateMetroDialogSettings());
            _viewModel.FixAirspace = false;
            if (result != MessageDialogResult.Affirmative) return;
            _localListPersistence.RemoveList(list);
            QaListSelector.SelectedIndex = 0;
        }

        private async void ListCreateButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.FixAirspace = true;
            var dialog = new InputWithCheckDialog(T("dialog.header.new"), T("dialog.create_list"),
                DateTime.Now.ToLongDateString(), s =>
            {
                if (!_localListPersistence.CheckValidListName(s)) return T("dialog.invalid_list_name", "name", s);
                if (_localListPersistence.CheckDuplicateListName(s)) return T("dialog.list_name_already_exists", "name", s);
                return null;
            });
            await this.ShowMetroDialogAsync(dialog, CreateMetroDialogSettings());
            var (result, name) = await dialog.WaitUntilButton();
            await this.HideMetroDialogAsync(dialog);
            if (result != MessageDialogResult.Affirmative) return;
            try
            {
                _localListPersistence.CreateList(name);
            }
            catch (Exception ex)
            {
                await this.ShowMessageAsync(T("dialog.header.error"), T("dialog.create_failed", "name", name, "exception", ex.ToString()), MessageDialogStyle.Affirmative, CreateMetroDialogSettings());
            }
            _viewModel.FixAirspace = false;
        }

        private void ListEditButton_Click(object sender, RoutedEventArgs e)
        {
            var objList = _viewModel.ActiveQaList;
            if (objList == _viewModel.AllQaItemsList) return;

            OpenCollectorFor((QaListStubsViewModel)objList);
        }

        #endregion

        private void OpenCollectorFor(QaListStubsViewModel vm)
        {
            var window = new CollectorWindow(vm, OnDelete) {Left = Left + Width, Top = Top};
            window.Show();
            ShiftWindowOntoScreenHelper.ShiftWindowOntoScreen(window);
        }

        private async void OnDelete(IList items, IEnumerable src)
        {
            await _viewModel.DeleteItemsFrom(src, items);
        }
        private async void OnRequestingImport(string filename)
        {
            try
            {
                var session = new ImportSession(filename, _database, _localListPersistence);
                if (session.ItemsCount == 0) return; // WTF?
                var dialog = new ImportDialog(T("dialog.import"), "", DateTime.Now.ToLongDateString(), _viewModel, s =>
                {
                    if (!_localListPersistence.CheckValidListName(s)) return T("dialog.invalid_list_name", "name", s);
                    if (_localListPersistence.CheckDuplicateListName(s)) return T("dialog.list_name_already_exists", "name", s);
                    return null;
                });
                _viewModel.FixAirspace = true;
                await this.ShowMetroDialogAsync(dialog);
                var (result, needToCreate, filenameOrList) = await dialog.WaitUntilButton();
                await this.HideMetroDialogAsync(dialog);
                _viewModel.FixAirspace = false;

                if (result != MessageDialogResult.Affirmative) return;
                if (needToCreate)
                    session.ImportToNew((string)filenameOrList);
                else if (filenameOrList == _viewModel.AllQaItemsList)
                    session.ImportToExisting(null);
                else
                {
                    session.ImportToExisting((QaListStubsViewModel)filenameOrList);
                }
                _viewModel.StatusText = T("status.imported", session.ItemsCount);
            }
            catch (IOException ex)
            {
                _viewModel.StatusText = T("dialog.import.error_io", "name", filename, "exception", ex.ToString());
            }
            catch (Exception)
            {
                _viewModel.StatusText = T("dialog.import.error_format", "name", filename);
            }
        }

        private void OnQaListModified(object sender, NotifyCollectionChangedEventArgs args)
        {
            if (!(sender is QaListStubsViewModel)) return;

            _saveListAction.Debounce(2000, null, () => _localListPersistence.UpdateList(((QaListStubsViewModel)sender).Underlying));
        }

        private void OnQaListLockStatusChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(QaListStubsViewModel.Locked) &&
                _viewModel.ActiveQaList == sender)
                _viewModel.UpdateActiveListEditable();
        }

        private void ContentTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            dynamic document = PreviewBrowser.Document;
            document.getElementById("text").innerText = ContentTextBox.Text;

            if (_viewModel.UiPersistingLocked || _viewModel.ActiveQaItem == null) return;
            using (var transaction = _database.Database.BeginTransaction())
            {
                _database.Items.Update(_viewModel.ActiveQaItem);
                transaction.Commit();
            }
            _saveDatabaseAction.Debounce(2000, null, () => _database.SaveChanges());
        }

        private async void FetchCommand_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTask != null && !_currentTask.IsCanceled && !_currentTask.IsCompleted) return;

            _viewModel.ProgressBarVisibility = Visibility.Visible;
            _currentTask = Task.Run(() =>
            {
                UpdateStatusText(T("status.fetching"));
                FetchService(T("service.marshmallow"), _marshmallowService, out var marshmallowCount, out var marshmallowPageCount);
                FetchService(T("service.peing"), _peingService, out var peingCount, out var peingPageCount);

                UpdateStatusText(T("status.fetched", "items", (marshmallowCount + peingCount).ToString(), "pages", (marshmallowPageCount + peingPageCount).ToString()));
            });
            try
            {
                await _currentTask;
            }
            catch (Exception exception)
            {
                _viewModel.StatusText = T("status.fetch_failed", "exception", exception.ToString());
            }
            _viewModel.ProgressBarVisibility = Visibility.Collapsed;
        }

        private void FetchService(string displayName, Service service, out int count, out int pageCount)
        {
            var localCount = 0;
            var localPageCount = 0;
            var allFetchedItems = new List<QaItem>();
            service.Fetch(items =>
            {
                var flag = true;
                foreach (var qaItem in items)
                    if (_database.Items.Find(qaItem.Id) != null) flag = false;
                    else
                    {
                        allFetchedItems.Add(qaItem);
                        localCount++;
                    }
                localPageCount++;
                UpdateStatusText(T("status.fetch_update",
                    "items", localCount.ToString(),
                    "pages", localPageCount.ToString(),
                    "service", displayName));
                return flag;
            });
            count = localCount;
            pageCount = localPageCount;

            allFetchedItems.Reverse();
            Dispatcher?.Invoke(() =>
            {
                using (var transaction = _database.Database.BeginTransaction())
                {
                    _database.Items.AddRange(allFetchedItems);
                    transaction.Commit();
                }
                _database.SaveChangesAsync();
            });
        }

        private void UpdateStatusText(string text)
        {
            Dispatcher?.InvokeAsync(() => _viewModel.StatusText = text);
        }

        private void DisplayButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_viewModel.ActiveQaItem != null)
                    _displayCommunication.UpdateText(_viewModel.ActiveQaItem, () =>
                    {
                        UpdateStatusText(T("status.display_updated"));
                    });
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private void MetroWindow_Closing(object sender, CancelEventArgs e)
        {
            _database.SaveChanges();
            _updateManager.Dispose();
        }

        private void QaList_OnReachBottom()
        {
            _viewModel.LoadNextPage();
        }
    }

    [SuppressMessage("ReSharper", "RedundantDefaultMemberInitializer")]
    public class MainViewModel : INotifyPropertyChanged, IDropTarget
    {
        private readonly Dictionary<ServiceStatus, PackIconMaterialKind> _statusDictionary = new Dictionary<ServiceStatus, PackIconMaterialKind>()
        {
            { ServiceStatus.Available, PackIconMaterialKind.CheckCircleOutline },
            { ServiceStatus.Error, PackIconMaterialKind.AlertOutline },
            { ServiceStatus.NotLoggedIn, PackIconMaterialKind.CloseCircleOutline },
            { ServiceStatus.Unknown, PackIconMaterialKind.HelpCircleOutline }
        };

        private readonly QaDataContext _dataContext;
        private readonly LocalListPersistence _persistence;
        private readonly IDialogCoordinator _dialogCoordinator;

        #region List & Item Properties

        public readonly QaListObservable AllQaItemsList;
        public ObservableCollection<QaListObservable> AllQaItemsHolder { get; set; } = new ObservableCollection<QaListObservable>();
        public ObservableCollection<QaListStubsViewModel> QaListStubs { get; set; } = new ObservableCollection<QaListStubsViewModel>();
        private ObservableCollection<QaItem> _activeQaItems;
        public ObservableCollection<QaItem> ActiveQaItems
        {
            get => _activeQaItems;
            set
            {
                if (_activeQaItems != null)
                    _activeQaItems.CollectionChanged -= OnActiveListModified;
                _activeQaItems = value;

                UpdateEmptyListIndicator();
                _activeQaItems.CollectionChanged += OnActiveListModified;
                FireOnPropertyChanged();
                ResetPaging();
            }
        }

        private object _activeList;
        public object ActiveQaList
        {
            get => _activeList;
            set
            {
                _activeList = value;
                FireOnPropertyChanged();
                UpdateActiveListEditable();
                ResetPaging();
            }
        }

        private void OnActiveListModified(object sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateEmptyListIndicator();
        }

        private bool _activeQaListEditable = false;
        public bool ActiveQaListEditable
        {
            get => _activeQaListEditable;
            set
            {
                _activeQaListEditable = value;
                FireOnPropertyChanged();
            }
        }

        private QaItem _activeQaItem = null;

        public QaItem ActiveQaItem
        {
            get => _activeQaItem;
            set
            {
                UiPersistingLocked = true;
                _activeQaItem = value;
                FireOnPropertyChanged();
                UiPersistingLocked = false;
            }
        }

        #endregion

        private string _version = "";
        public string Version
        {
            get => _version;
            set
            {
                _version = value;
                FireOnPropertyChanged();
            } }

        #region Status Properties

        private string _serverStatusText = T("status.display.not_running");
        public string ServerStatusText
        {
            get => _serverStatusText;
            set
            {
                _serverStatusText = value;
                FireOnPropertyChanged();
            }
        }

        private string _statusText = "";
        public string StatusText
        {
            get => _statusText;
            set
            {
                _statusText = value;
                FireOnPropertyChanged();
            }
        }

        private string _emptyListIndicatorText = "";
        public string EmptyListIndicatorText
        {
            get => _emptyListIndicatorText;
            set
            {
                _emptyListIndicatorText = value;
                FireOnPropertyChanged();
            }
        }

        private Visibility _emptyListIndicatorVisibility = Visibility.Collapsed;
        private Visibility _progressBarVisibility = Visibility.Collapsed;

        public Visibility EmptyListIndicatorVisibility
        {
            get => _emptyListIndicatorVisibility;
            set
            {
                _emptyListIndicatorVisibility = value;
                FireOnPropertyChanged();
            }
        }

        public Visibility ProgressBarVisibility
        {
            get => _progressBarVisibility;
            set
            {
                _progressBarVisibility = value;
                FireOnPropertyChanged();
            }
        }

        private bool _fixAirspace = false;

        public bool FixAirspace
        {
            get => _fixAirspace;
            set
            {
                _fixAirspace = value;
                FireOnPropertyChanged();
            }
        }

        public volatile bool UiPersistingLocked = false;

        public PackIconMaterialKind MarshmallowStatus { get; set; } = PackIconMaterialKind.HelpCircleOutline;
        public PackIconMaterialKind PeingStatus { get; set; } = PackIconMaterialKind.HelpCircleOutline;

        #endregion

        #region Lazy Loading

        private int _pageIndex = 0;
        private bool _isLastPage = false;
        private const int PageSize = 10;

        private void ResetPaging()
        {
            _pageIndex = 0;
            _isLastPage = false;
            LoadNextPage();
        }

        public void LoadNextPage()
        {
            if (_isLastPage) return;
            var qaItems = _dataContext.Items.Skip(PageSize * (_pageIndex)).Take(PageSize).ToArray();
            if (qaItems.Length == 0)
            {
                _isLastPage = true;
                return;
            }
            foreach (QaItem qaItem in qaItems)
            {
                if (_activeQaItems.Where((item, i) => item.Id == qaItem.Id).Any()) continue;
                _activeQaItems.Add(qaItem);
            }
            _pageIndex++;
        }

        #endregion

        public MainViewModel(QaDataContext dbContext, LocalListPersistence persistence, IDialogCoordinator dialogCoordinator)
        {
            _dataContext = dbContext;
            _persistence = persistence;
            var localDbSet = dbContext.Items.Local.ToObservableCollection();

            AllQaItemsList = new QaListObservable()
                {Name = T("ui.list_all"), Items = localDbSet };
            ActiveQaItems = localDbSet;
            AllQaItemsHolder.Add(AllQaItemsList);
            ActiveQaList = AllQaItemsList;

            _dialogCoordinator = dialogCoordinator;
        }

        private void UpdateEmptyListIndicator()
        {
            if (_activeQaItems.Count != 0)
            {
                EmptyListIndicatorVisibility = Visibility.Collapsed;
                return;
            }

            EmptyListIndicatorText = T(ReferenceEquals(_activeQaItems, AllQaItemsList.Items) ? "ui.list.empty_all" : "ui.list.empty_list");
            EmptyListIndicatorVisibility = Visibility.Visible;
        }

        internal void UpdateMarshmallowStatus(ServiceStatus status)
        {
            MarshmallowStatus = _statusDictionary[status];
            FireOnPropertyChanged(nameof(MarshmallowStatus));
        }

        internal void UpdatePeingStatus(ServiceStatus status)
        {
            PeingStatus = _statusDictionary[status];
            FireOnPropertyChanged(nameof(PeingStatus));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void FireOnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void UpdateActiveListEditable()
        {
            ActiveQaListEditable = !ReferenceEquals(_activeList, AllQaItemsList)
                                   && (!(_activeList is QaListStubs) || !((QaListStubs)_activeList).Locked)
                                   && (!(_activeList is QaListStubsViewModel) || !((QaListStubsViewModel)_activeList).Locked);
        }

        #region Drop and Delete Support

        public void DragOver(IDropInfo dropInfo)
        {
            if (!CanAcceptDataForDelete(dropInfo)) return;
            dropInfo.DropTargetAdorner = typeof(RemoveDropTargetAdorner);
            dropInfo.Effects = ReferenceEquals(dropInfo.DragInfo.SourceCollection, dropInfo.TargetCollection)
                ? DragDropEffects.Move : DragDropEffects.Copy;
        }

        private static bool CanAcceptDataForDelete(IDropInfo dropInfo)
        {
            if (!(dropInfo.DragInfo?.SourceCollection is ObservableCollection<QaItem>)) return false;
            var gargs = dropInfo.DragInfo?.SourceCollection?.GetType().GetGenericArguments();
            return gargs.Length >= 1 && gargs[0].IsAssignableFrom(typeof(QaItem));
        }

        [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
        public async void Drop(IDropInfo dropInfo)
        {
            // deleting
            var src = dropInfo.DragInfo.SourceCollection;
            var data = ExtractData(dropInfo.DragInfo.Data);

            await DeleteItemsFrom(src, data);
        }

        public async Task DeleteItemsFrom(IEnumerable src, IEnumerable data)
        {
            var items = new List<QaItem>();
            items.AddRange(data.Cast<QaItem>());
            if (items.Count == 0) return;

            var fromAllList = ReferenceEquals(src, AllQaItemsList.Items);
            if (fromAllList)
            {
                FixAirspace = true;
                var confirmDialog = new DeleteConfirmDialog(items);
                await _dialogCoordinator.ShowMetroDialogAsync(this, confirmDialog, new MetroDialogSettings()
                {
                    AnimateHide = false,
                    AnimateShow = false
                });
                var result = await confirmDialog.WaitUntilButton();
                await _dialogCoordinator.HideMetroDialogAsync(this, confirmDialog);
                FixAirspace = false;
                if (result != MessageDialogResult.Affirmative) return;
            }

            if (fromAllList)
            {
                using (var transaction = _dataContext.Database.BeginTransaction())
                {
                    _dataContext.Items.RemoveRange(items);
                    transaction.Commit();
                }

                await _dataContext.SaveChangesAsync();
            }
            else
            {
                if (!(src is ObservableCollection<QaItem> srcObs)) return;
                foreach (var item in items)
                    srcObs.Remove(item);
            }
        }

        private static IEnumerable ExtractData(object data)
        {
            if (data is IEnumerable enumerable && !(enumerable is string))
                return enumerable;
            return Enumerable.Repeat(data, 1);
        }

        private class RemoveDropTargetAdorner : DropTargetAdorner
        {
            private readonly Brush RemovingBrush = new SolidColorBrush(Color.FromArgb(128, 255, 0, 0));
            public RemoveDropTargetAdorner(UIElement adornedElement, DropInfo dropInfo) : base(adornedElement, dropInfo)
            {
            }

            protected override void OnRender(DrawingContext drawingContext)
            {
                if (DropInfo.VisualTarget == null) return;
                var rects = DropInfo.VisualTarget.FindChildren<Rectangle>().ToArray();
                foreach (var rectangle in rects)
                    if (rectangle.Name == "RemoveRectangle")
                    {
                        var geom = rectangle.RenderedGeometry.Clone();
                        geom.Transform = new TranslateTransform(0.0, 3.0);
                        drawingContext.DrawGeometry(RemovingBrush, null, geom);
                    }
            }
        }
#endregion
    }

    public class QaListObservable
    {
        public string Name { get; set; }
        public ObservableCollection<QaItem> Items { get; set; }
    }

    public class QaListStubsViewModel : INotifyPropertyChanged, INotifyCollectionChanged
    {
        public QaListStubsViewModel(QaListStubs underlying, QaDataContext database)
        {
            Underlying = underlying;

            PopulatedItems = new ObservableCollection<QaItem>(database.LoadStubs(underlying).Items);
            PopulatedItems.CollectionChanged += (sender, args) =>
            {
                // sync the changes back to the underlying id list.
                switch (args.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        if (args.NewItems != null && args.NewItems.Count > 0)
                             Underlying.Items.Insert(args.NewStartingIndex, ((QaItem) args.NewItems[0]).Id);
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        Underlying.Items.RemoveAt(args.OldStartingIndex);
                        break;
                    case NotifyCollectionChangedAction.Reset:
                        Underlying.Items.Clear();
                        break;
                    case NotifyCollectionChangedAction.Replace:
                        if (args.NewItems != null && args.NewItems.Count > 0 && args.NewStartingIndex != -1)
                            Underlying.Items[args.NewStartingIndex] = ((QaItem)args.NewItems[0]).Id;
                        break;
                    case NotifyCollectionChangedAction.Move:
                        if (args.NewItems != null && args.NewItems.Count > 0 && args.NewStartingIndex != -1)
                        {
                            var obj = Underlying.Items[args.OldStartingIndex];
                            Underlying.Items.RemoveAt(args.OldStartingIndex);
                            Underlying.Items.Insert(args.NewStartingIndex, obj);
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                CollectionChanged?.Invoke(this, args);
            };
        }

        public QaListStubs Underlying { get; }
        public ObservableCollection<QaItem> PopulatedItems { get; }

        public string Name
        {
            get => Underlying.Name;
            set => Underlying.Name = value;
        }

        public bool Locked
        {
            get => Underlying.Locked;
            set
            {
                Underlying.Locked = value;
                OnPropertyChanged();
            }
        }

        public List<string> Items => Underlying.Items;

        public event PropertyChangedEventHandler PropertyChanged;
        [NotifyPropertyChangedInvocator]
        public void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public event NotifyCollectionChangedEventHandler CollectionChanged;
    }

    public class QaIconConverter : IValueConverter
    {
        private readonly Dictionary<QaService, ImageSource> _sources = new Dictionary<QaService, ImageSource>();

        public QaIconConverter()
        {
            foreach (var service in Enum.GetValues(typeof(QaService)))
            {
                _sources[(QaService) service] = new BitmapImage(new Uri($"/Resources/Icons/{service}.png", UriKind.Relative));
            }
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is QaService)) return null;
            var service = (QaService) value;
            return _sources.ContainsKey(service)
                ? _sources[service] : null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
