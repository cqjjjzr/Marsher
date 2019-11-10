using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
