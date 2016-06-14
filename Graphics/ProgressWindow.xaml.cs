using MahApps.Metro.Controls;

namespace Server.Graphics {
    /// <summary>
    /// Interaction logic for TransferWindow.xaml
    /// </summary>
    public partial class ProgressWindow : MetroWindow {

        public ProgressWindow(string file, long totLength) {
            InitializeComponent();
        }

        public string Message {
            get {
                return titleLabel.Content.ToString();
            }
            set {
                titleLabel.Content = value;
            }
        }

    }
}
