using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Delay;
using GongSolutions.Wpf.DragDrop;
using GongSolutions.Wpf.DragDrop.Utilities;
using Marsher.Annotations;
using NuGet;

namespace Marsher
{
    /// <summary>
    /// QaListView.xaml 的交互逻辑
    /// </summary>
    public partial class QaListView
    {
        public bool AllowDropExtended
        {
            get => (bool) GetValue(AllowDropExtendedProperty);
            set => SetValue(AllowDropExtendedProperty, value);
        }

        public bool AllowForeignDrop
        {
            get => (bool) GetValue(AllowForeignDropProperty);
            set => SetValue(AllowForeignDropProperty, value);
        }
        public static readonly DependencyProperty AllowForeignDropProperty =
            DependencyProperty.Register("AllowForeignDrop", typeof(bool), typeof(QaListView),
                new PropertyMetadata(true, AllowForeignDropPropertyChanged));
        public static readonly DependencyProperty AllowDropExtendedProperty =
            DependencyProperty.Register("AllowDropExtended", typeof(bool), typeof(QaListView),
                new PropertyMetadata(true, AllowDropExtendedPropertyChanged));
        public QaListViewViewModel ViewModel { get; }
        public event Action<IList, IEnumerable> OnDelete;
        public event Action ReachBottom;

        public QaListView()
        {
            ViewModel = new QaListViewViewModel(this);
            InitializeComponent();
            GongSolutions.Wpf.DragDrop.DragDrop.SetDropHandler(this, ViewModel);
            GongSolutions.Wpf.DragDrop.DragDrop.SetDragHandler(this, ViewModel);
            GongSolutions.Wpf.DragDrop.DragDrop.SetIsDropTarget(this, AllowDropExtended);
        }

        private static void AllowDropExtendedPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {

        }

        private static void AllowForeignDropPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {

        }

        private void ListView_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                OnDelete?.Invoke(SelectedItems, ItemsSource);
            }
        }

        private void QaListView_OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (Math.Abs(e.VerticalOffset - e.ExtentHeight) < 10)
                ReachBottom?.Invoke();
        }
    }

    public sealed class QaListViewViewModel: IDropTarget, IDragSource
    {
        private readonly QaListView _view;
        public event Action<string> OnRequestingImport;

        public QaListViewViewModel(QaListView view)
        {
            _view = view;
        }

        public void DragOver(IDropInfo dropInfo)
        {
            if (CanAcceptData(dropInfo))
            {
                if (!_view.AllowForeignDrop)
                    return;
                if (!_view.AllowDropExtended)
                    return;
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
                dropInfo.Effects = ReferenceEquals(dropInfo.DragInfo.SourceCollection, dropInfo.TargetCollection)
                    ? DragDropEffects.Move : DragDropEffects.Copy;
                return;
            }
            if (CanAcceptDataForImport(dropInfo))
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
                dropInfo.Effects = DragDropEffects.Copy;
            }
        }

        private static bool CanAcceptDataForImport(IDropInfo dropInfo)
        {
            if (!(dropInfo.Data is DataObject dataObj)) return false;
            if (!dataObj.ContainsFileDropList()) return false;
            var dragFileList = dataObj.GetFileDropList().Cast<string>();
            return dragFileList.Count() == 1;
        }

        private bool CanAcceptData(IDropInfo dropInfo)
        {
            if (dropInfo?.DragInfo == null)
                return false;

            if (!dropInfo.IsSameDragDropContextAsSource)
                return false;

            if (ReferenceEquals(dropInfo.DragInfo.SourceCollection, dropInfo.TargetCollection))
            {
                var targetList = dropInfo.TargetCollection.TryGetList();
                return targetList != null;
            }

            if (dropInfo.TargetCollection == null)
                return false;

            if (TestCompatibleTypes(dropInfo.TargetCollection, dropInfo.DragInfo.Data))
            {
                var isChildOf = IsChildOf(dropInfo.VisualTargetItem, dropInfo.DragInfo.VisualSourceItem);
                return !isChildOf;
            }
            return false;
        }

        private static IEnumerable ExtractData(object data)
        {
            if (data is IEnumerable enumerable && !(enumerable is string))
                return enumerable;
            return Enumerable.Repeat(data, 1);
        }

        private static void SelectDroppedItems([NotNull] IDropInfo dropInfo, [NotNull] IEnumerable items, bool applyTemplate = true, bool focusVisualTarget = true)
        {
            if (dropInfo == null) throw new ArgumentNullException(nameof(dropInfo));
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (!(dropInfo.VisualTarget is ItemsControl itemsControl)) return;
            var tvItem = dropInfo.VisualTargetItem as TreeViewItem;
            var tvItemIsExpanded = tvItem != null && tvItem.HasHeader && tvItem.HasItems && tvItem.IsExpanded;

            var itemsParent = tvItemIsExpanded ? tvItem : (dropInfo.VisualTargetItem != null ? ItemsControl.ItemsControlFromItemContainer(dropInfo.VisualTargetItem) : itemsControl);
            itemsParent = itemsParent ?? itemsControl;

            itemsParent.ClearSelectedItems();

            foreach (var obj in items)
            {
                if (applyTemplate)
                {
                    // call ApplyTemplate for TabItem in TabControl to avoid this error:
                    //
                    // System.Windows.Data Error: 4 : Cannot find source for binding with reference
                    var container = itemsParent.ItemContainerGenerator.ContainerFromItem(obj) as FrameworkElement;
                    container?.ApplyTemplate();
                }
                itemsParent.SetItemSelected(obj, true);
            }

            if (focusVisualTarget)
                itemsControl.Focus();
        }

        /// <summary>
        /// Determines whether the data of the drag drop action should be copied otherwise moved.
        /// </summary>
        /// <param name="dropInfo">The DropInfo with a valid DragInfo.</param>
        private static bool ShouldCopyData(IDropInfo dropInfo)
        {
            if (dropInfo?.DragInfo == null)
                return true;

            return !ReferenceEquals(dropInfo.TargetCollection, dropInfo.DragInfo.SourceCollection);
        }

        /// <inheritdoc />
        public void Drop(IDropInfo dropInfo)
        {
            if (dropInfo?.DragInfo == null)
            {
                if (CanAcceptDataForImport(dropInfo))
                    OnRequestingImport?.Invoke(((DataObject)dropInfo.Data).GetFileDropList()[0]);
                return;
            }

            var insertIndex = dropInfo.UnfilteredInsertIndex;

            var itemsControl = dropInfo.VisualTarget as ItemsControl;
            if (itemsControl != null)
            {
                if (itemsControl.Items is IEditableCollectionView editableItems)
                {
                    var newItemPlaceholderPosition = editableItems.NewItemPlaceholderPosition;
                    if (newItemPlaceholderPosition == NewItemPlaceholderPosition.AtBeginning && insertIndex == 0)
                        ++insertIndex;
                    else if (newItemPlaceholderPosition == NewItemPlaceholderPosition.AtEnd && insertIndex == itemsControl.Items.Count)
                        --insertIndex;
                }
            }

            var destinationList = dropInfo.TargetCollection.TryGetList();
            var data = ExtractData(dropInfo.DragInfo.Data).OfType<object>().ToList();
            var sourceList = dropInfo.DragInfo.SourceCollection.TryGetList();
            var selfToSelf = ReferenceEquals(sourceList, destinationList);

            var copyData = ShouldCopyData(dropInfo);
            if (!copyData && sourceList != null)
                foreach (var o in data)
                {
                    var index = sourceList.IndexOf(o);
                    if (index == -1) continue;
                    sourceList.RemoveAt(index);
                    // so, is the source list the destination list too ?
                    if (destinationList != null && selfToSelf && index < insertIndex)
                        --insertIndex;
                }

            if (destinationList == null) return;
            var objects2Insert = new List<object>();

            foreach (var o in data)
            {
                var index = destinationList.IndexOf(o);
                if (index != -1)
                {
                    destinationList.RemoveAt(index);
                    if (index < insertIndex)
                        --insertIndex;
                }

                objects2Insert.Add(o);
                destinationList.Insert(insertIndex++, o);
            }

            var selectDroppedItems = itemsControl is TabControl ||
                                     (itemsControl != null && GongSolutions.Wpf.DragDrop.DragDrop.GetSelectDroppedItems(itemsControl));
            if (selectDroppedItems)
                SelectDroppedItems(dropInfo, objects2Insert);
        }

        private static bool IsChildOf(UIElement targetItem, UIElement sourceItem)
        {
            var parent = ItemsControl.ItemsControlFromItemContainer(targetItem);
            while (parent != null)
            {
                if (parent == sourceItem)
                    return true;
                parent = ItemsControl.ItemsControlFromItemContainer(parent);
            }

            return false;
        }

        private static bool TestCompatibleTypes(IEnumerable target, object data)
        {
            bool Filter(Type t, object o)
            {
                return (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            }

            var enumerableInterfaces = target.GetType().FindInterfaces(Filter, null);
            var enumerableTypes = from i in enumerableInterfaces
                                  select i.GetGenericArguments().Single();

            var enumerable = enumerableTypes.ToList();
            if (!enumerable.Any()) return target is IList;
            var dataType = TypeUtilities.GetCommonBaseClass(ExtractData(data));
            return enumerable.Any(t => t.IsAssignableFrom(dataType));
        }

        public void StartDrag(IDragInfo dragInfo)
        {
            /*var items = TypeUtilities.CreateDynamicallyTypedList(dragInfo.SourceItems).Cast<object>().ToList();
            if (items.Count > 1)
            {
                dragInfo.Data = items;
            }
            else
            {
                // special case: if the single item is an enumerable then we can not drop it as single item
                var singleItem = items.FirstOrDefault();
                if (singleItem is IEnumerable && !(singleItem is string))
                {
                    dragInfo.Data = items;
                }
                else
                {
                    dragInfo.Data = singleItem;
                }
            }*/
            var items = TypeUtilities.CreateDynamicallyTypedList(dragInfo.SourceItems).Cast<QaItem>()
                .ToList();
            var sess = new ExportSession(items);
            var filename = Path.Combine(Path.GetTempPath(),
                DateTime.Now.ToString("yyyy-M-d HH_mm_ss") + ".marsher");
            var virtualFileDataObject = new VirtualFileDataObject();
            virtualFileDataObject.SetData(new VirtualFileDataObject.FileDescriptor[]
            {
                new VirtualFileDataObject.FileDescriptor
                {
                    Name = DateTime.Now.ToString("yyyy-M-d HH_mm_ss") + ".marsher",
                    StreamContents = stream =>
                    {
                        var bytes = Encoding.UTF8.GetBytes(sess.Json);
                        stream.Write(bytes, 0, bytes.Length);
                    }
                },
            });
            virtualFileDataObject.SetData(
                (short)(DataFormats.GetDataFormat(DataFormats.Text).Id),
                Encoding.Default.GetBytes(sess.Json + "\0"));
            dragInfo.DataObject = virtualFileDataObject;
            dragInfo.Data = items;

            dragInfo.Effects = !items.IsEmpty() ? DragDropEffects.Move | DragDropEffects.Copy : DragDropEffects.None;
        }

        /// <inheritdoc />
        public bool CanStartDrag(IDragInfo dragInfo)
        {
            return true;
        }

        /// <inheritdoc />
        public void Dropped(IDropInfo dropInfo)
        {

        }

        /// <inheritdoc />
        public void DragDropOperationFinished(DragDropEffects operationResult, IDragInfo dragInfo)
        {

        }

        /// <inheritdoc />
        public void DragCancelled()
        {

        }

        /// <inheritdoc />
        public bool TryCatchOccurredException(Exception exception)
        {
            return false;
        }
    }
}
