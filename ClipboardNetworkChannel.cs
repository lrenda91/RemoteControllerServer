using System;
using System.Windows;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using System.Windows.Media.Imaging;

namespace Server.Net {

    class ClipboardNetworkChannel {

        //public static int BYTES_PER_TRANSFER = 8 * 1024;    //8KB per file packet transfer
        public static string TemporaryDirectory;
        public static NetworkErrorEventHandler NetworkErrorOccured;

        private static Thread thread;
        //private static ManualResetEvent dicMutex = new ManualResetEvent(true);
        //private static IDictionary<string, AsynchFileReceiver> transfers = new Dictionary<string, AsynchFileReceiver>();
        
        private static MemoryStream bitmapStream;
        private static AsynchFileReceiver bitmapDownload = null;
        private static AsynchFileReceiver currentDownload = null;
        private static string currentFileName;
        private static object objLock = new object();

        private static Socket listener;
        private static Socket client;

        public static void StartService(short port) {
            if (listener != null || thread != null) {
                throw new InvalidOperationException("Clipboard Receiving service already running");
            }
            listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(new IPEndPoint(IPAddress.Any, port));

            thread = new Thread(new ThreadStart(listenClipboard));
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        public static void StopService() {
            NetworkErrorOccured = null;
            if (listener == null || thread == null) { //service is already stopped. No problems in this case
                return;
            }
            Utility.ShutdownSocket(client);
            listener.Close();
            try { thread.Join(); } catch (ThreadInterruptedException) { }
            thread = null;
            listener = null;
            Console.WriteLine("ClipboardReceiver service CLOSED!!");
        }


        static internal void downloadCompleted(AsynchFileReceiver download) {
            lock (objLock) {
                currentFileName = null;
                if (download == currentDownload) currentDownload = null;
                if (download == bitmapDownload) bitmapDownload = null;
                Monitor.PulseAll(objLock);
            }
        }


        private static void listenClipboard() {
            try {
                listener.Listen(0);
                client = listener.Accept();
            } catch (Exception) {
                listener.Close();
                return;
            }

            byte[] rawPacket = new byte[ClipboardConstants.PacketLength];

            while (true) {
                try {
                    if (!Utility.ReceiveBytes(client, rawPacket, rawPacket.Length, SocketFlags.None)) {
                        break;
                    }

                    ClipboardPacket packet = Serialization.FromClipboardBytes(rawPacket);
                    ClipboardPacketType packetType = (ClipboardPacketType)packet.type;

                    switch (packetType) {
                        case ClipboardPacketType.TEXT:
                            Clipboard.SetText(System.Text.Encoding.Unicode.GetString(packet.payload));
                            break;

                        case ClipboardPacketType.DIRECTORY:
                            Clipboard.Clear();
                            string dir = TemporaryDirectory + packet.name;
                            Directory.CreateDirectory(dir);
                            break;

                        case ClipboardPacketType.FILE:
                            string file = TemporaryDirectory + packet.name;
                            lock (objLock) {
                                while (currentDownload != null && !packet.name.Equals(currentFileName)) {
                                    Monitor.Wait(objLock);
                                }
                                if (currentDownload == null) {
                                    currentFileName = packet.name;
                                    currentDownload = new AsynchFileReceiver(file, packet.totalLength);
                                    Console.WriteLine("START " + file);
                                    currentDownload.Start();
                                }
                                currentDownload.newFragmentAvailable(ref packet.payload);
                            }
                            break;

                        case ClipboardPacketType.BITMAP:
                            lock (objLock) {
                                if (bitmapStream == null) {
                                    bitmapStream = new MemoryStream((int)packet.totalLength);
                                }
                                bitmapStream.Write(packet.payload, 0, packet.payload.Length);
                                if (bitmapStream.Position == packet.totalLength) {
                                    var bitmap = new BitmapImage();
                                    bitmap.BeginInit();
                                    bitmap.StreamSource = bitmapStream;
                                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                    bitmap.EndInit();
                                    bitmap.Freeze();
                                    Clipboard.SetImage(bitmap);
                                    bitmapStream.Dispose();
                                    bitmapStream = null;
                                }
                            }
                            break;

                        case ClipboardPacketType.UPDATE:
                            ClipboardTrasfer.SendClipboardNotice(Clipboard.GetDataObject());
                            break;

                        case ClipboardPacketType.SET_DROPLIST:
                            string fileNames = Encoding.Unicode.GetString(packet.payload);
                            string[] filesArray = fileNames.Split('|');
                            StringCollection sc = new StringCollection();
                            foreach (string s in filesArray) {
                                sc.Add(TemporaryDirectory + s);
                            }
                            try {
                                Thread t = new Thread(new ThreadStart(() => {
                                    Clipboard.SetFileDropList(sc);
                                }));
                                t.SetApartmentState(ApartmentState.STA);
                                t.Start();
                            } catch (Exception) { }
                            break;

                    }
                } catch (SocketException se) {
                    if (NetworkErrorOccured != null) {
                        NetworkErrorOccured(se);
                    }
                    //break;
                } catch (ObjectDisposedException) {
                    break;
                } catch (Exception ex) {
                    Console.WriteLine(ex.Message);
                    break;
                }
            }
            client.Close();
            listener.Close();
        }


    }

}
