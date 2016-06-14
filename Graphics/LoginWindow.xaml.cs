using Server.ViewModel;
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
using System.Windows.Threading;
using System.Threading;
using System.Net;
using MahApps.Metro.Controls;

namespace Server.Graphics {

    public partial class LoginWindow : MetroWindow {

        public Net.ConnectionEventHandler CancelRequest;

        public bool IsClosingDisabled {
            get;
            set;
        }

        public LoginWindow() {
            InitializeComponent();            
            messageLabel.Content = "Waiting for a client";
            IsClosingDisabled = false;
            Visibility = System.Windows.Visibility.Hidden;
        }

        public void OnConnectionStarted() {
            Dispatcher.BeginInvoke(new Action(() => {
                IsClosingDisabled = true;
                Show();
                Activate();
            }), 
            null);
        }
        
        public void OnClientConnected(IPEndPoint client) {
        }
        
        public void OnClientAuthenticated() {
            Dispatcher.BeginInvoke(new Action(() => {
                Close();
            }),
            null);
        }

        public void OnClientDisconnected(Exception exception) {
            Dispatcher.BeginInvoke(new Action(() => {
                Show();
                Activate();
                messageLabel.Content = "Waiting for a client";
            }),
            null);
        }


        public void OnConnectionStopped() {
            Dispatcher.BeginInvoke(new Action(() => {
                Close();
            }),
            null);
        }


        private void Button_Click(object sender, RoutedEventArgs e) {
            if (CancelRequest != null) {
                CancelRequest();
            }
        }

        private void LoginWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            if (IsClosingDisabled) {
                e.Cancel = true;
                Hide();
            }
        }

    }
}
