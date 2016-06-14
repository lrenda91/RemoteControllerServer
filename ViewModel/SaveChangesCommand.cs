using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.IO;

using Server.Net;

namespace Server.ViewModel {
    public class SaveChangesCommand : ICommand {
        private Settings settings;
        public SaveChangesCommand(Settings s) {
            settings = s;
        }
        public event EventHandler CanExecuteChanged {
            add {
                CommandManager.RequerySuggested += value;
            }
            remove {
                CommandManager.RequerySuggested -= value;
            }
        }
        public bool CanExecute(object parameter) {
            return !String.IsNullOrEmpty(settings.Password) &&
                settings.Password.Length >= 8;
            //&& Directory.Exists(settings.TempFolderPath);
        }
        public void Execute(object parameter) {
            ClipboardNetworkChannel.TemporaryDirectory = settings.TempFolderPath;

            //ClipboardTrasfer.MAX_FILE_SIZE = int.Parse(settings.MaxFileTransferMBString.Split()[0]) * 1024 * 1024;
            ClipboardConstants.MaxFileSizeInBytes = int.Parse(settings.MaxFileTransferMBString.Split()[0]) * 1024 * 1024;

            //Console.WriteLine("len=" + ClipboardConstants.MaxPacketPayloadLength + " file size: " + ClipboardConstants.MaxFileSizeInBytes +
            // "tem: " + ClipboardNetworkChannel.TemporaryDirectory);

        }
    }
}
