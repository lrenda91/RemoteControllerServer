using System;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using Server.Properties;
using System.Windows.Media.Imaging;
using System.Net;
using System.ComponentModel;

namespace Server {
    class ServerTrayIcon : IDisposable {

        public NotifyIcon notifyIcon;
        private IContainer container;

        public delegate void ExitMenuClickEventHandler();
        public event ExitMenuClickEventHandler ExitMenuClick;

        public ServerTrayIcon() {
            initializeTrayIcon();
            ClientName = "";
        }

        public string ClientName { get; set; }

        private void exitMenuItem_Click(object sender, EventArgs e) {
            Dispose();
            OnExitMenuClick();
        }

        public void OnClientConnected(IPEndPoint client) {
            ClientName = client.Address.ToString();
        }


        public void OnClientAuthenticated() {
            Connected = true;
            notifyIcon.Visible = true;
            notifyIcon.ShowBalloonTip(2000);
        }

        public void OnClientDisconnected(Exception exception) {
            if (exception != null) {
                ShowWarningMessage(exception.Message);
                return;
            }
            ClientName = null;
            Connected = false;
            notifyIcon.Visible = true;
            notifyIcon.ShowBalloonTip(2000);
        }

        private void ShowMessage(string message, ToolTipIcon icon) {
            notifyIcon.Icon = Properties.Resources.connectedIcon;
            notifyIcon.ShowBalloonTip(1000, notifyIcon.Text, message, icon);
        }

        public void ShowSimpleMessage(string message) {
            ShowMessage(message, ToolTipIcon.None);
        }

        public void ShowWarningMessage(string message) {
            ShowMessage(message, ToolTipIcon.Warning);
        }

        public void ShowErrorMessage(string message) {
            ShowMessage(message, ToolTipIcon.Error);
        }

        public bool Connected {
            set {
                //string iconName = value ? "connectedpng.ico" : "disconnect.ico";
                //notifyIcon.Icon = new Icon(App.GetResourceStream(new Uri("pack://application:,,/images/" + iconName)).Stream);
                notifyIcon.Icon = value ? Properties.Resources.connectedIcon : Properties.Resources.disconnectedIcon;
                notifyIcon.BalloonTipText = value ? "Client " + ClientName + " connected" : "Disconnected";
            }
        }

        
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        // NOTE: Leave out the finalizer altogether if this class doesn't 
        // own unmanaged resources itself, but leave the other methods
        // exactly as they are. 
        ~ServerTrayIcon() {
            // Finalizer calls Dispose(false)
            Dispose();
        }
        // The bulk of the clean-up code is implemented in Dispose(bool)
        protected virtual void Dispose(bool disposing) {
            if (disposing) {
                if (container != null) {
                    container.Dispose();
                }
                if (notifyIcon != null) {
                    notifyIcon.Dispose();
                }
            }
        }

        private void initializeTrayIcon() {
            container = new System.ComponentModel.Container();
            notifyIcon = new NotifyIcon(container);
            MenuItem exitMenuItem = new MenuItem("Exit", exitMenuItem_Click);
            notifyIcon.Text = "Remote Controller";
            notifyIcon.ContextMenu = new ContextMenu();
            notifyIcon.ContextMenu.MenuItems.AddRange(new MenuItem[] { 
                exitMenuItem 
            });
            notifyIcon.Visible = true;
            ShowSimpleMessage("Hello");
        }

        private void OnExitMenuClick() {
            if (ExitMenuClick != null) {
                ExitMenuClick();
            }
        }

    }
}
