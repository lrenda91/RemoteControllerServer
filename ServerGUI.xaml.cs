using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Interop;
using System.ComponentModel;
using System.IO;
using Server.ViewModel;
using System.Windows.Controls;
using System.Collections.Specialized;
using MahApps.Metro.Controls;
using Server.Net;
using System.Net;

namespace Server {

    public partial class ServerGUI : MetroWindow {

        ServerTrayIcon trayIcon = new ServerTrayIcon();
        Graphics.LoginWindow loginWin = new Graphics.LoginWindow();
        Net.MainConnection connection = new Net.MainConnection();
        
        public ServerGUI() {
            InitializeComponent();
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            trayIcon.ExitMenuClick += serverGUI_OnExitMenuItemClicked;
            ShowActivated = true;
            errorLabel.Text = string.Empty;   
        }

        void ServerGUI_onNetworkError(System.Net.Sockets.SocketException ex) {
            trayIcon.ShowErrorMessage(ex.Message);
        }

        private void OnConnectionStarted() {
            loginWin.CancelRequest += forceMainConnectionStop;
            loginWin.Closing -= this.MakeVisible;
        }

        private void OnConnectionStopped() {
            loginWin.CancelRequest -= forceMainConnectionStop;
            loginWin.Closing += this.MakeVisible;
        }

        private void forceMainConnectionStop() {
            connection.Stop();
        }
        
        private void Start_Button_Click(object sender, RoutedEventArgs e) {
            Settings settings = (Settings)DataContext;
            try {
                connection.CheckSettings(settings);
                connection.Start(settings);
                errorLabel.Text = string.Empty;
                WindowState = WindowState.Minimized;
                Visibility = System.Windows.Visibility.Hidden;
            } catch (Exception ex) {
                errorLabel.Text = ex.Message;
            }
        }

        private void MakeVisible(object sender, CancelEventArgs e) {
            Visibility = System.Windows.Visibility.Visible;
            Show();
            Activate();
            WindowState = WindowState.Normal;
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e) {
            if (this.DataContext != null) {
                ((dynamic)this.DataContext).SecurePassword = ((PasswordBox)sender).SecurePassword;
            }
        }

        private void registerCallbacks() {
            connection.ClientConnected += loginWin.OnClientConnected;
            connection.ClientConnected += trayIcon.OnClientConnected;
            connection.ClientAuthenticated += trayIcon.OnClientAuthenticated;
            connection.ClientAuthenticated += loginWin.OnClientAuthenticated;
            connection.ClientDisconnected += loginWin.OnClientDisconnected;
            connection.ClientDisconnected += trayIcon.OnClientDisconnected;
            //connection.NetworkErrorOccured += ServerGUI_onNetworkError;

            connection.Started += this.OnConnectionStarted;
            connection.Started += loginWin.OnConnectionStarted;

            connection.Stopped += this.OnConnectionStopped;
            connection.Stopped += loginWin.OnConnectionStopped;
        }


        public void unregisterCallbacks() {
            loginWin.Closing -= this.MakeVisible;
            loginWin.CancelRequest = null;
            connection.ClientConnected = null;
            connection.ClientAuthenticated = null;
            connection.ClientDisconnected = null;
            //connection.NetworkErrorOccured = null;
            connection.Started = null;
            connection.Stopped = null;
            ClipboardMonitor.ClipboardChanged = null;
        }

        private void ServerGUI_OnClipbardChanged(IDataObject content) {
            string message = "Clipboard Changed: ";
            string[] formats = {
                                   DataFormats.Text,
                                   DataFormats.Bitmap,
                                   DataFormats.FileDrop
                               };
            foreach (string f in formats) {
                if (content.GetDataPresent(f)) {
                    message += f;
                    break;
                }
            }
            trayIcon.ShowSimpleMessage(message);
        }

        private void serverGUI_OnExitMenuItemClicked() {
            Close();
            System.Environment.Exit(0);
        }

        private void Select_Backup_Folder_Button_Click(object sender, RoutedEventArgs e) {
            Settings settings = (Settings)DataContext;
            System.Windows.Forms.FolderBrowserDialog folderDialog = new System.Windows.Forms.FolderBrowserDialog();
            folderDialog.SelectedPath = settings.TempFolderPath;
            System.Windows.Forms.DialogResult result = folderDialog.ShowDialog();
            if (result.ToString() == "OK") {
                settings.TempFolderPath = folderDialog.SelectedPath + '\\';
                pathTextBlock.Text = settings.TempFolderPath;
            }
        }

        void ServerGUI_Loaded(object sender, RoutedEventArgs e) {
            loginWin.IsClosingDisabled = false;
            registerCallbacks();
            ClipboardMonitor.ClipboardViewer = this;
            ClipboardMonitor.EnableCapture();
            ClipboardMonitor.ClipboardChanged += ServerGUI_OnClipbardChanged;
        }

        private void ServerGUI_Unloaded(object sender, RoutedEventArgs e) {
            unregisterCallbacks();
            trayIcon.notifyIcon.Icon = null;
            trayIcon.notifyIcon.Visible = false;
            trayIcon.Dispose();
            loginWin.IsClosingDisabled = false;
            loginWin.Close();
            ClipboardMonitor.DisableCapture();
        }

        public static void runOnUI(Action action) {
            Application.Current.Dispatcher.BeginInvoke(action, null);
        }

        
    }
}