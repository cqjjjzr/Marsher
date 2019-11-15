using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Marsher
{
    /// <summary>
    /// EmptyListIndicator.xaml 的交互逻辑
    /// </summary>
    public partial class EmptyListIndicator : UserControl
    {
        public static readonly DependencyProperty PrimaryStringProperty =
            DependencyProperty.Register("PrimaryString", typeof(string), typeof(EmptyListIndicator),
                new PropertyMetadata("", StringChanged));
        public static readonly DependencyProperty SecondaryStringProperty =
            DependencyProperty.Register("SecondaryString", typeof(string), typeof(EmptyListIndicator),
                new PropertyMetadata("", StringChanged));

        public string PrimaryString
        {
            get => (string) GetValue(PrimaryStringProperty);
            set => SetValue(PrimaryStringProperty, value);
        }

        public string SecondaryString
        {
            get => (string)GetValue(SecondaryStringProperty);
            set => SetValue(SecondaryStringProperty, value);
        }

        private static void StringChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {

        }

        public EmptyListIndicator()
        {
            InitializeComponent();
        }
    }
}
