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
using System.Windows.Shapes;

namespace Marsher
{
    /// <summary>
    /// ReleaseNoteDisplay.xaml 的交互逻辑
    /// </summary>
    public partial class ReleaseNoteDisplay
    {
        public string Text { get; set; }
        public ReleaseNoteDisplay(string markdown)
        {
            Text = markdown;
            DataContext = this;
            InitializeComponent();
        }
    }
}
