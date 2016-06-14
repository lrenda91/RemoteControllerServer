using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Server.Net {

    class ClipboardTrasfer {

        //internal const int BUFFER_SIZE = 8 * 1024 - (1 + 8 + 256 + 4);    // 8KB per transfer

        //public static long MAX_FILE_SIZE;

        private static Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        public static IPEndPoint Target {
            set {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Connect(value);
            }
        }

        public static void StopService() {
            Utility.ShutdownSocket(socket);
            Console.WriteLine("ClipboardTransfer service CLOSED!!");
        }

        private static void SendFile(string fileToSend, ref string rootDir) {
            int readBytes = 0;
            long bytesToSend = new FileInfo(fileToSend).Length;

            ClipboardPacket packet = new ClipboardPacket();
            packet.type = (byte) ClipboardPacketType.FILE;
            packet.name = fileToSend.Substring(rootDir.Length);
            packet.totalLength = bytesToSend;

            Console.WriteLine("Sending file " + fileToSend);
            using (FileStream fileStream = new FileStream(fileToSend, FileMode.Open)) {
                byte[] fileData = new byte[ClipboardConstants.MaxPacketPayloadLength];

                 do{
                    readBytes = fileStream.Read(fileData, 0, fileData.Length);
                    bytesToSend -= readBytes;

                    packet.payloadLength = readBytes;
                    packet.payload = new byte[fileData.Length];
                    Array.Copy(fileData, packet.payload, fileData.Length);

                    byte[] toSend = Serialization.GetClipboardBytes(packet);
                    Utility.SendBytes(socket, toSend, toSend.Length, SocketFlags.None);
                }
                while (bytesToSend > 0);
            }
            Console.WriteLine("FILE: Sent file " + fileToSend);
        }

        private static void SendNewFolder(string folder, ref string rootDir) {
            ClipboardPacket packet = new ClipboardPacket();
            packet.type = (byte) ClipboardPacketType.DIRECTORY;
            packet.name = folder.Substring(rootDir.Length);
            packet.payloadLength = 0;
            packet.totalLength = 0;
            Console.WriteLine("DIRECTORY: Sending empty dir " + folder + " from local path " + rootDir + " ...");
            byte[] toSend = Serialization.GetClipboardBytes(packet);
            Utility.SendBytes(socket, toSend, toSend.Length, SocketFlags.None);
        }

        private static void SendText(string text) {
            ClipboardPacket packet = new ClipboardPacket();
            packet.type = (byte)ClipboardPacketType.TEXT;
            packet.payload = System.Text.Encoding.Unicode.GetBytes(text);
            packet.payloadLength = packet.payload.Length;
            packet.totalLength = 0;
            packet.name = String.Empty;
            byte[] toSend = Serialization.GetClipboardBytes(packet);
            Console.WriteLine("Text: sending '" + text + "' to client clipboard...");
            Utility.SendBytes(socket, toSend, toSend.Length, SocketFlags.None);
        }

        private static void SendBitmap(System.Windows.Media.Imaging.BitmapSource b) {
            ClipboardPacket packet = new ClipboardPacket();
            packet.type = (byte)ClipboardPacketType.BITMAP;
            packet.name = String.Empty;

            MemoryStream ms = new MemoryStream();
            System.Windows.Media.Imaging.BmpBitmapEncoder encoder = new System.Windows.Media.Imaging.BmpBitmapEncoder();
            System.Windows.Media.Imaging.BitmapImage bImg = new System.Windows.Media.Imaging.BitmapImage();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(b));
            encoder.Save(ms);
            byte[] getBitmapData = ms.ToArray();

            int maxPayloadLen = ClipboardConstants.MaxPacketPayloadLength;
            packet.payloadLength = maxPayloadLen;
            packet.totalLength = ms.Length;
            
            long bytesToSend = ms.Length;
            while (bytesToSend > 0) {
                if (bytesToSend < maxPayloadLen) {
                    packet.payloadLength = (int)bytesToSend;
                }
                packet.payload = new byte[maxPayloadLen];
                Array.Copy(getBitmapData, ms.Length - bytesToSend, packet.payload, 0, packet.payloadLength);
                bytesToSend -= packet.payloadLength;
                byte[] toSend = Serialization.GetClipboardBytes(packet);
                Utility.SendBytes(socket, toSend, toSend.Length, SocketFlags.None);
            }
            ms.Close();
        }

        private static void SendPathDropList(string path) {
            ClipboardPacket packet = new ClipboardPacket();
            packet.type = (byte)ClipboardPacketType.SET_DROPLIST;
            packet.payload = System.Text.Encoding.Unicode.GetBytes(path);
            packet.payloadLength = packet.payload.Length;
            packet.totalLength = 0;
            packet.name = String.Empty;
            byte[] toSend = Serialization.GetClipboardBytes(packet);
            Console.WriteLine("FileDropList: sending path " + path);
            Utility.SendBytes(socket, toSend, toSend.Length, SocketFlags.None);
        }

        public static void SendClipboardNotice(IDataObject obj) {
            if (obj.GetDataPresent(DataFormats.Text)) {
                string text = (string)obj.GetData(DataFormats.Text);
                ClipboardTrasfer.SendText(text);
            } else if (obj.GetDataPresent(DataFormats.FileDrop)) {
                /* get the list of absolute file paths actually inside Windows Clipboard */
                var dropList = (string[])obj.GetData(DataFormats.FileDrop, false);

                if (!ConfirmSend(dropList)) {
                    return;
                }

                /* get parent folder, i.e. the folder in which Windows Clipboard was changed */
                int lastDirSeparatorIndex = (dropList[0].LastIndexOf('\\') + 1);
                string parentDir = dropList[0].Remove(lastDirSeparatorIndex);

                string path = "";
                foreach (string s in dropList) {
                    path += s.Substring(parentDir.Length);
                    path += "|";
                }
                path = path.Remove(path.Length - 1);

                foreach (string absoluteFilePath in dropList) {
                    /* 
                     * Check if current absolute file path inside the Clipboard represents 
                     * a Directory and (if greater than MAX_SIZE) user confirmed its transfer
                     */
                    if (Directory.Exists(absoluteFilePath)) {
                        /* First, send to client the current folder... */
                        ClipboardTrasfer.SendNewFolder(absoluteFilePath, ref parentDir);

                        /* ...and all its subfolders */
                        string[] subDirs = Directory.GetDirectories(absoluteFilePath, "*.*", SearchOption.AllDirectories);
                        foreach (string dir in subDirs) {
                            ClipboardTrasfer.SendNewFolder(dir, ref parentDir);
                        }
                        /* finally, send to client all subfiles in order to 'fill' all previously sent folders */
                        string[] subFiles = Directory.GetFiles(absoluteFilePath, "*.*", System.IO.SearchOption.AllDirectories);
                        foreach (string file in subFiles) {
                            ClipboardTrasfer.SendFile(file, ref parentDir);
                        }
                    }

                    /* 
                     * Check if current absolute file path inside the Clipboard represents 
                     * a File and (if greater than MAX_SIZE) user confirmed its transfer
                     */
                    else if (File.Exists(absoluteFilePath)) {
                        ClipboardTrasfer.SendFile(absoluteFilePath, ref parentDir);
                    }
                }
                /*
                 * Finally, send the path drop list, so that Clipboard could change for the counterpart
                 */
                ClipboardTrasfer.SendPathDropList(path);

            } else if (obj.GetDataPresent(DataFormats.Bitmap)) {
                BitmapSource bitmap = (BitmapSource)obj.GetData(DataFormats.Bitmap);
                ClipboardTrasfer.SendBitmap(bitmap);
            }
        }

        private static bool ConfirmFile(string file) {
            long fileSize = new FileInfo(file).Length;
            if (fileSize > ClipboardConstants.MaxFileSizeInBytes) {
                MessageBoxResult result = MessageBox.Show(
                    file + "\nFile size exceeds " + (ClipboardConstants.MaxFileSizeInBytes / 1024 / 1024) +
                        " MB.\nDo you want to continue?",
                    "Updating Remote Clipboard",
                    MessageBoxButton.YesNo);
                return result == MessageBoxResult.Yes;
            }
            return true;
        }

        private static bool ConfirmFolder(string folder) {
            string[] files = Directory.GetFiles(folder, "*.*");
            long filesSize = 0;
            foreach (string f in files) {
                filesSize += new FileInfo(f).Length;
                if (filesSize > ClipboardConstants.MaxFileSizeInBytes) {
                    MessageBoxResult result = MessageBox.Show(
                        folder + "\nFolder size exceeds " + (ClipboardConstants.MaxFileSizeInBytes / 1024 / 1024) +
                        " MB.\nDo you want to continue?",
                        "Updating Remote Clipboard",
                        MessageBoxButton.YesNo);
                    return result == MessageBoxResult.Yes;
                }
            }
            return true;
        }

        private static long GetFolderSize(string folder) {
            string[] files = Directory.GetFiles(folder, "*.*");
            long filesSize = 0;
            foreach (string f in files) {
                filesSize += new FileInfo(f).Length;
            }
            return filesSize;
        }

        /*public static void ShowErrorMessage(Exception e) {
            MessageBoxResult result = MessageBox.Show(
                    e.Message,
                    e.TargetSite.Name + " exception!!",
                    MessageBoxButton.OK);
        }*/

        private static bool ConfirmSend(string[] path) {
            long total = 0;
            foreach (string absoluteFilePath in path) {
                if (Directory.Exists(absoluteFilePath)){
                    total += GetFolderSize(absoluteFilePath);
                }
                else if (File.Exists(absoluteFilePath)){
                    total += new FileInfo(absoluteFilePath).Length;
                }
            }
            if (total > ClipboardConstants.MaxFileSizeInBytes) {
                MessageBoxResult result = MessageBox.Show(
                    "Selected files size is " + (total / 1024 / 1024) +
                    " MB.\nDo you want to continue?",
                    "Updating Remote Clipboard",
                    MessageBoxButton.YesNo);
                return result == MessageBoxResult.Yes;
            }
            return true;
        }
    }
}
