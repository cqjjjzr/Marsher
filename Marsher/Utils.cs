using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Marsher
{
    public class TrulyObservableCollection<T> : ObservableCollection<T>
    {
        public TrulyObservableCollection()
            : base()
        {}

        public void FireChangedItem(T item)
        {
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, item, item));
        }
    }
}
