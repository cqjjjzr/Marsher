using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using GongSolutions.Wpf.DragDrop;
using GongSolutions.Wpf.DragDrop.Utilities;
using Marsher.Annotations;

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
        private QaListViewViewModel ViewModel { get; }

        public QaListView()
        {
            ViewModel = new QaListViewViewModel(this);
            InitializeComponent();
            GongSolutions.Wpf.DragDrop.DragDrop.SetDropHandler(this, ViewModel);
            GongSolutions.Wpf.DragDrop.DragDrop.SetIsDropTarget(this, AllowDropExtended);
        }

        private static void AllowDropExtendedPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is QaListView view)) return;
            GongSolutions.Wpf.DragDrop.DragDrop.SetIsDropTarget(view, (bool) e.NewValue);
        }

        private static void AllowForeignDropPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {

        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {

        }
    }

    public sealed class QaListViewViewModel: IDropTarget
    {
        private readonly QaListView _view;

        public QaListViewViewModel(QaListView view)
        {
            _view = view;
        }

        public void DragOver(IDropInfo dropInfo)
        {
            if (!CanAcceptData(dropInfo)) return;
            dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
            dropInfo.Effects = ReferenceEquals(dropInfo.DragInfo.SourceCollection, dropInfo.TargetCollection)
                ? DragDropEffects.Move : DragDropEffects.Copy;
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

            if (!(_view.AllowForeignDrop))
                return false;

            if (dropInfo.TargetCollection == null)
                return false;

            if (TestCompatibleTypes(dropInfo.TargetCollection, dropInfo.Data))
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
                return;

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
            var data = ExtractData(dropInfo.Data).OfType<object>().ToList();
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
    }
}
