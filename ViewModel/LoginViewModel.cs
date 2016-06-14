using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace Server.ViewModel {
    public class LoginViewModel : INotifyPropertyChanged {

        //private Net.ConnectionAsynch connection;
        private bool _clientAuthenticated;
        private string _message;

        public event PropertyChangedEventHandler PropertyChanged;
        
        public LoginViewModel() {
            //connection = new Net.ConnectionAsynch(this);
            //connection.Password = "p";
        }
        
        protected virtual void NotifyPropertyChanged([CallerMemberName] String propertyName = "") {
            if (PropertyChanged != null) {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        /*
       #region methods to interact with connection and view
        public void WaitForClient() {
            connection.BeginAcceptClient();
            Message = "Waiting...";
        }
        public void ClientConnected(string IP) {
            Message = IP + " connected! Waiting for client password...";
        }
        public void AuthTryCompleted(bool authOK) {
            Authenticated = authOK;
            if (authOK) {
                Message = "Perfect. mo chiudo!!!";
            }
            else {
                Message = "Not authenticated. retrying";
                System.Threading.Thread.Sleep(500);
                WaitForClient();
            }
        }
        #endregion
        */
        #region fields to bind to view
        public bool Authenticated {
            get {
                return _clientAuthenticated;
            }
            set {
                _clientAuthenticated = value;
                NotifyPropertyChanged("Authenticated");
            }
        }

        public string Message {
            get {
                return _message;
            }
            set {
                _message = value;
                NotifyPropertyChanged("Message");
            }
        }
        #endregion
    }


    public class BoolToVisibleOrHidden : IValueConverter {
        #region Constructors
        /// <summary>
        /// The default constructor
        /// </summary>
        public BoolToVisibleOrHidden() { }
        #endregion

        #region IValueConverter Members
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            bool bValue = (bool)value;
            return bValue ? Visibility.Hidden : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            Visibility visibility = (Visibility)value;
            return visibility != Visibility.Visible;
        }
        #endregion
    }

}
