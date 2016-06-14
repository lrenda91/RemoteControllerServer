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
        Graphics.LoginWindow loginWin;
        Net.MainConnection connection = new Net.MainConnection();

        public ServerGUI() {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            trayIcon.SettingsMenuClick += serverGUI_OnSettingsMenuItemClicked;
            trayIcon.ExitMenuClick += serverGUI_OnExitMenuItemClicked;
            ShowActivated = true;

            //loginWin = new Graphics.LoginWindow();
            //loginWin.Visibility = System.Windows.Visibility.Hidden;
            
            //loginWin.Closing += loginWin_Closing;
            connection.ConnectionStarted += ServerGUI_loginWin_onConnectionStarted;
            connection.ConnectionStopped += ServerGUI_loginWin_onConnectionStopped;
        }

        private void ServerGUI_loginWin_onConnectionStarted() {
            
            /*loginWin.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate() {
                loginWin.CancelRequest += loginWin_CancelRequest;
                loginWin.Show();
            }));*/
            runOnUI(() => {
                loginWin = new Graphics.LoginWindow();
                registerCallbacks();
                
                //loginWin.Visibility = System.Windows.Visibility.Hidden;
                
                loginWin.Show();
                loginWin.WindowState = System.Windows.WindowState.Normal;
                loginWin.Activate();
            });
        }

        private void ServerGUI_loginWin_onConnectionStopped() {
            /*loginWin.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate() {
                loginWin.Close();
            }));*/
            runOnUI(() => {
                loginWin.Close();
                //unregisterCallbacks();
                loginWin = null;
            });
        }

        private void loginWin_CancelRequest() {
            connection.Stop();
        }
        

        private void serverGUI_OnSettingsMenuItemClicked() {
            Show();
            WindowState = System.Windows.WindowState.Normal;
        }


        private void Button_Click_1(object sender, RoutedEventArgs e) {
            Settings settings = (Settings)DataContext;
            try {
                connection.CheckSettings(settings);

                /*loginWin = new Graphics.LoginWindow();
                registerCallbacks();
                loginWin.Closing += loginWin_Closing;
                loginWin.Show();*/
                WindowState = System.Windows.WindowState.Minimized;
                connection.Start(settings);
            } catch (Exception ex) {
                MessageBox.Show("Error applying settings: " + ex.Message);
            }
        }

        private void loginWin_Closing(object sender, CancelEventArgs e) {
            //connection.Stop();
            this.Visibility = Visibility.Visible;
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,
                new Action(delegate() {
                this.WindowState = WindowState.Normal;
            }));
        }

        private void Window_StateChanged(object sender, EventArgs e) {
            switch (WindowState) {
                case System.Windows.WindowState.Minimized:
                    this.Hide();
                    break;
                case System.Windows.WindowState.Normal:
                    //TrayIcon.notifyIcon.Visible = false;
                    break;
            }
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e) {
            if (this.DataContext != null) {
                ((dynamic)this.DataContext).SecurePassword = ((PasswordBox)sender).SecurePassword;
            }
        }

        private void registerCallbacks() {
            loginWin.CancelRequest += loginWin_CancelRequest;
            loginWin.Closing += loginWin_Closing;
            connection.ClientConnected += loginWin.loginWindow_ClientConnected;
            connection.ClientConnected += trayIcon.trayIcon_OnClientConnected;
            connection.ClientAuthenticated += loginWin.loginWindow_ClientAuthenticated;
            connection.ClientDisconnected += loginWin.loginWindow_ClientDisconnected;
            connection.ClientDisconnected += trayIcon.trayIcon_OnClientDisconnected;
        }


        public void unregisterCallbacks() {
            connection.ConnectionStarted = null;
            connection.ConnectionStopped = null;
            connection.ClientConnected = null;
            connection.ClientAuthenticated = null;
            connection.ClientDisconnected = null;
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

        private void Button_Click(object sender, RoutedEventArgs e) {
            Settings settings = (Settings)DataContext;
            System.Windows.Forms.FolderBrowserDialog folderDialog = new System.Windows.Forms.FolderBrowserDialog();
            folderDialog.SelectedPath = "C:\\";
            System.Windows.Forms.DialogResult result = folderDialog.ShowDialog();
            if (result.ToString() == "OK") {
                settings.TempFolderPath = folderDialog.SelectedPath + '\\';
            }
        }

        void ServerGUI_Loaded(object sender, RoutedEventArgs e) {
            ClipboardMonitor.ClipboardViewer = this;
            ClipboardMonitor.EnableCapture();
            ClipboardMonitor.ClipboardChanged += ServerGUI_OnClipbardChanged;
        }

        void ServerGUI_Closing(object sender, CancelEventArgs e) {
            trayIcon.notifyIcon.Icon = null;
            trayIcon.notifyIcon.Visible = false;
            trayIcon.Dispose();
            unregisterCallbacks();
            ClipboardMonitor.DisableCapture();
        }


        public static void runOnUI(Action action) {
            Application.Current.Dispatcher.BeginInvoke(action, null);
        }

    }
}