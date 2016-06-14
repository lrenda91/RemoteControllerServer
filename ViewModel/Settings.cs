using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Server.ViewModel;
using System.Windows.Data;
using System.Collections.Generic;
using System.ComponentModel;
using System.Security;
using System.Net;
using System.IO;

namespace Server.ViewModel
{
    /// <summary>
    /// ViewModel for ServerGUI window
    /// </summary>
    public class Settings
    {

        private ICommand _applyChangesCommand;

        public Settings() {
            MainConnectionPort = 7001;
            ClipboardReceivingPort = 8001;
            ClipboardTransferPort = 8000;
            InputReceivingPort = 9000;
            KeepAlivePort = 5000;
            Password = "ciaociao";
            TempFolderPath = Directory.GetCurrentDirectory() + "\\";
            List = new List<String>();
            List.Add(String.Format("5 MB"));
            List.Add(String.Format("10 MB"));
            List.Add(String.Format("20 MB"));
            List.Add(String.Format("50 MB"));
            MaxFileTransferMBString = List[0];
        }
        
        public ICommand Save
        {
            get
            {
                _applyChangesCommand = new SaveChangesCommand(this);
                return _applyChangesCommand;
            }
            set
            {
                _applyChangesCommand = value;
            }
        }

        public List<string> List { get; set; }

        public IPAddress ClientIP { get; set; }

        public short KeepAlivePort { get; set; }

        public short MainConnectionPort { get; set; }

        public short ClipboardReceivingPort { get; set; }

        public short ClipboardTransferPort { get; set; }

        public short InputReceivingPort { get; set; }

        public string MaxFileTransferMBString { get; set; }

        public string TempFolderPath { get; set; }

        string p;
        public string Password {
            get {
                return p;
            }
            set {
                SecureString ss = new SecureString();
                foreach (char c in value) {
                    ss.AppendChar(c);
                }
                SecurePassword = ss;
                p = value;
            }
        }

        public SecureString SecurePassword { get; set; }

        public String ClipboardBackgroundImage
        {
            get
            {
                return "images\\clipShare.png";
            }
        }

        public String InputBackgroundImage
        {
            get
            {
                return "images\\input.png";
            }
        }

        public String ConnectionImage {
            get {
                return "images\\connection.png";
            }
        }

    }




    
}