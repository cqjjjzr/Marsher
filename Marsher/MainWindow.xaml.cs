using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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

namespace Marsher
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow
    {
        private MainViewModel _viewModel;

        private MarshmallowService _marshmallowService;
        private PeingService _peingService;
        private QaDataContext _database = new QaDataContext();
        private LocalListPersistence _localListPersistence;
        private readonly DisplayCommunication _displayCommunication;

        private Task _currentTask = null;
        public MainWindow()
        {
            LoadFromXmlFile("Resources/dictionary.txd");

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
            _viewModel = new MainViewModel(_database, DialogCoordinator.Instance);
            DataContext = _viewModel;
            _database.Database.EnsureCreatedAsync();
            _database.Items.LoadAsync();
            _localListPersistence = new LocalListPersistence(_database);

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

            PreviewBrowser.NavigateToStream(File.Open("resources/index_preview.html", FileMode.Open, FileAccess.Read));
        }

        private void LoginCommand_Click(object sender, RoutedEventArgs e)
        {
            LoginContextMenu.PlacementTarget = (UIElement)sender;
            LoginContextMenu.IsOpen = true;
        }



        private void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {

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
            string oldName = list.Name;
            string name;
            _viewModel.FixAirspace = true;
            do
            {
                name = await this.ShowInputAsync(T("dialog.header.rename"), T("dialog.rename_list", "oldName", oldName), CreateMetroDialogSettings());
                if (name == null) break;
                try
                {
                    list.Name = name;
                    _localListPersistence.UpdateList(list, true);
                }
                catch (IllegalListNameException)
                {
                    await this.ShowMessageAsync(T("dialog.header.error"), T("dialog.invalid_list_name", "name", name), MessageDialogStyle.Affirmative, CreateMetroDialogSettings());
                    name = null;
                    list.Name = oldName;
                }
                catch (DuplicateListNameException)
                {
                    await this.ShowMessageAsync(T("dialog.header.error"), T("dialog.list_name_already_exists", "name", name), MessageDialogStyle.Affirmative, CreateMetroDialogSettings());
                    name = null;
                    list.Name = oldName;
                }
                catch (Exception ex)
                {
                    await this.ShowMessageAsync(T("dialog.header.error"), T("dialog.rename_failed", "name", name, "exception", ex.ToString()), MessageDialogStyle.Affirmative, CreateMetroDialogSettings());
                }
            } while (name == null);

            _viewModel.FixAirspace = false;
        }

        private async void ListRemoveButton_Click(object sender, RoutedEventArgs e)
        {
            var objList = _viewModel.ActiveQaList;
            if (objList == _viewModel.AllQaItemsList) return;
            var list = ((QaListStubsViewModel)objList).Underlying;

            //this.ShowModalInputExternal()
            _viewModel.FixAirspace = true;
            var result = await this.ShowMessageAsync(T("dialog.header.confirm"), T("dialog.remove_confirm", "name", list.Name), MessageDialogStyle.AffirmativeAndNegative, CreateMetroDialogSettings());
            _viewModel.FixAirspace = false;
            if (result != MessageDialogResult.Affirmative) return;
            _localListPersistence.RemoveList(list);
            QaListSelector.SelectedIndex = 0;
        }

        private async void ListCreateButton_Click(object sender, RoutedEventArgs e)
        {
            string name;
            _viewModel.FixAirspace = true;
            do
            {
                name = await this.ShowInputAsync(T("dialog.header.new"), T("dialog.create_list"),
                    CreateMetroDialogSettings());
                if (name == null) break;
                try
                {
                    _localListPersistence.CreateList(name);
                }
                catch (IllegalListNameException)
                {
                    await this.ShowMessageAsync(T("dialog.header.error"), T("dialog.invalid_list_name", "name", name), MessageDialogStyle.Affirmative, CreateMetroDialogSettings());
                    name = null;
                }
                catch (ArgumentException)
                {
                    await this.ShowMessageAsync(T("dialog.header.error"), T("dialog.list_name_already_exists", "name", name), MessageDialogStyle.Affirmative, CreateMetroDialogSettings());
                    name = null;
                }
                catch (Exception ex)
                {
                    await this.ShowMessageAsync(T("dialog.header.error"), T("dialog.create_failed", "name", name, "exception", ex.ToString()), MessageDialogStyle.Affirmative, CreateMetroDialogSettings());
                }
            } while (name == null);
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
            var window = new CollectorWindow(vm) {Left = Left + Width, Top = Top};
            window.Show();
            ShiftWindowOntoScreenHelper.ShiftWindowOntoScreen(window);
        }

        private void OnQaListModified(object sender, NotifyCollectionChangedEventArgs args)
        {
            if (!(sender is QaListStubsViewModel)) return;

            _localListPersistence.UpdateList(((QaListStubsViewModel) sender).Underlying);
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

            if (_viewModel.ActiveQaItem == null) return;
            using (var transaction = _database.Database.BeginTransaction())
            {
                _database.Items.Update(_viewModel.ActiveQaItem);
                transaction.Commit();
            }
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
                    _displayCommunication.UpdateText(_viewModel.ActiveQaItem.Content, () =>
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
        }
    }

    internal class MainViewModel : INotifyPropertyChanged, IDropTarget
    {
        private Dictionary<ServiceStatus, PackIconMaterialKind> _statusDictionary = new Dictionary<ServiceStatus, PackIconMaterialKind>()
        {
            { ServiceStatus.Available, PackIconMaterialKind.CheckCircleOutline },
            { ServiceStatus.Error, PackIconMaterialKind.AlertOutline },
            { ServiceStatus.NotLoggedIn, PackIconMaterialKind.CloseCircleOutline },
            { ServiceStatus.Unknown, PackIconMaterialKind.HelpCircleOutline }
        };

        private readonly IDialogCoordinator _dialogCoordinator;

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
                _activeQaItem = value;
                FireOnPropertyChanged();
            }
        }

        private readonly QaDataContext _dataContext;

        public MainViewModel(QaDataContext dbContext, IDialogCoordinator dialogCoordinator)
        {
            _dataContext = dbContext;
            var localDbSet = dbContext.Items.Local.ToObservableCollection();

            AllQaItemsList = new QaListObservable()
                {Name = T("ui.list_all"), Items = localDbSet };
            ActiveQaItems = localDbSet;
            AllQaItemsHolder.Add(AllQaItemsList);
            ActiveQaList = AllQaItemsList;

            _dialogCoordinator = dialogCoordinator;
        }

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

        private void UpdateEmptyListIndicator()
        {
            if (_activeQaItems.Count != 0)
            {
                EmptyListIndicatorVisibility = Visibility.Collapsed;
                return;
            }

            EmptyListIndicatorText = T(ReferenceEquals(_activeList, AllQaItemsList) ? "ui.list.empty_all" : "ui.list.empty_list");
            EmptyListIndicatorVisibility = Visibility.Visible;
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

        public PackIconMaterialKind MarshmallowStatus { get; set; } = PackIconMaterialKind.HelpCircleOutline;
        internal void UpdateMarshmallowStatus(ServiceStatus status)
        {
            MarshmallowStatus = _statusDictionary[status];
            FireOnPropertyChanged(nameof(MarshmallowStatus));
        }

        public PackIconMaterialKind PeingStatus { get; set; } = PackIconMaterialKind.HelpCircleOutline;
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
            if (!CanAcceptData(dropInfo)) return;
            dropInfo.DropTargetAdorner = typeof(RemoveDropTargetAdorner);
            dropInfo.Effects = ReferenceEquals(dropInfo.DragInfo.SourceCollection, dropInfo.TargetCollection)
                ? DragDropEffects.Move : DragDropEffects.Copy;
        }

        private bool CanAcceptData(IDropInfo dropInfo)
        {
            if (!(dropInfo.DragInfo?.SourceCollection is ObservableCollection<QaItem>)) return false;
            var gargs = dropInfo?.DragInfo?.SourceCollection?.GetType()?.GetGenericArguments();
            if (gargs == null || gargs.Length < 1) return false;
            return gargs[0].IsAssignableFrom(typeof(QaItem));
        }

        public async void Drop(IDropInfo dropInfo)
        {
            var src = dropInfo.DragInfo.SourceCollection;
            var fromAllList = ReferenceEquals(src, AllQaItemsList.Items);
            var data = ExtractData(dropInfo.DragInfo.Data).Cast<QaItem>().ToList();
            if (data.Count == 0) return;

            if (fromAllList)
            {
                FixAirspace = true;
                var confirmDialog = new DeleteConfirmDialog(data);
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
                    if (!(src is ObservableCollection<QaItem> srcObs)) return;
                    foreach (var item in data)
                        srcObs.Remove(item);
                    transaction.Commit();
                }

                await _dataContext.SaveChangesAsync();
            }
            else
            {
                if (!(src is ObservableCollection<QaItem> srcObs)) return;
                foreach (var item in data)
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
        private QaDataContext _database;
        public QaListStubsViewModel(QaListStubs underlying, QaDataContext database)
        {
            _underlying = underlying;
            _database = database;

            _populatedItems = new ObservableCollection<QaItem>(database.LoadStubs(underlying).Items);
            _populatedItems.CollectionChanged += (sender, args) =>
            {
                // sync the changes back to the underlying id list.
                switch (args.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        if (args.NewItems != null && args.NewItems.Count > 0)
                             _underlying.Items.Insert(args.NewStartingIndex, ((QaItem) args.NewItems[0]).Id);
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        _underlying.Items.RemoveAt(args.OldStartingIndex);
                        break;
                    case NotifyCollectionChangedAction.Reset:
                        _underlying.Items.Clear();
                        break;
                    case NotifyCollectionChangedAction.Replace:
                        if (args.NewItems != null && args.NewItems.Count > 0 && args.NewStartingIndex != -1)
                            _underlying.Items[args.NewStartingIndex] = ((QaItem)args.NewItems[0]).Id;
                        break;
                    case NotifyCollectionChangedAction.Move:
                        if (args.NewItems != null && args.NewItems.Count > 0 && args.NewStartingIndex != -1)
                        {
                            var obj = _underlying.Items[args.OldStartingIndex];
                            _underlying.Items.RemoveAt(args.OldStartingIndex);
                            _underlying.Items.Insert(args.NewStartingIndex, obj);
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                CollectionChanged?.Invoke(this, args);
            };
        }

        private QaListStubs _underlying;
        private ObservableCollection<QaItem> _populatedItems;

        public QaListStubs Underlying => _underlying;
        public ObservableCollection<QaItem> PopulatedItems => _populatedItems;

        public string Name
        {
            get => _underlying.Name;
            set => _underlying.Name = Name;
        }

        public bool Locked
        {
            get => _underlying.Locked;
            set
            {
                _underlying.Locked = value;
                OnPropertyChanged();
            }
        }

        public List<string> Items => _underlying.Items;

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
        private Dictionary<QaService, ImageSource> _sources = new Dictionary<QaService, ImageSource>();

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
            var serv = (QaService) value;
            if (_sources.ContainsKey(serv)) return _sources[serv];
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
