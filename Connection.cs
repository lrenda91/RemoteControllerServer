using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Collections;
using Server.ViewModel;
using System.Security;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using System.Net.NetworkInformation;

namespace Server.Net {

    public class MainConnection {

        private static readonly string ClientAcceptedResponse = "OK";
        private static readonly string ClientLeavingResponse = "BYE";
        private static readonly string ClientRejectedResponse = "NO";

        public ConnectionEventHandler Started, Stopped;
        public ClientConnectedEventHandler ClientConnected;
        public ClientAuthenticatedEventHandler ClientAuthenticated;
        public ClientDisconnectedEventHandler ClientDisconnected;

        private KeepaliveChannel keepAlive;

        private Socket listener, actualClient;
        private Thread listenerThread;
        private string passwdDigest;

        private object errorLockObj = new object();
        private SocketException ErrorOccurred = null;

        public MainConnection() {
            InputNetworkChannel.NetworkErrorOccured += OnNetworkError;
            ClipboardNetworkChannel.NetworkErrorOccured += OnNetworkError;
        }

        //the input is the plain password. the server keeps trace of its MD5 digest 
        public string Password {
            set {
                StringBuilder sb = new StringBuilder();
                MD5 md5 = MD5CryptoServiceProvider.Create();
                byte[] hash = md5.ComputeHash(Encoding.Unicode.GetBytes(value));
                foreach (byte b in hash)
                    sb.Append(b.ToString("x2"));
                passwdDigest = sb.ToString();
            }
        }

        public short Port {
            set {
                listener.Bind(new IPEndPoint(IPAddress.Any, value));
                listener.Listen(0);
            }
        }

        //private Settings sett;

        //check LOCAL settings (trying to make all Bind() calls)
        public void CheckSettings(Settings settings) {
            short[] ports = new short[]{
                settings.MainConnectionPort,
                settings.KeepAlivePort,
                settings.ClipboardReceivingPort,
                settings.ClipboardTransferPort,
                settings.InputReceivingPort
            };
            HashSet<short> set = new HashSet<short>();
            foreach (short port in ports) {
                if (port < 1024) {
                    throw new Exception("Some port has no valid value");
                }
                if (!set.Add(port)) {
                    throw new Exception("Port "+port+" duplicated! Please choose another one");
                }
            }
            Socket s;
            s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            s.Bind(new IPEndPoint(IPAddress.Any, settings.KeepAlivePort));
            s.Close();
            s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            s.Bind(new IPEndPoint(IPAddress.Any, settings.MainConnectionPort));
            s.Close();
            s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            s.Bind(new IPEndPoint(IPAddress.Any, settings.ClipboardReceivingPort));
            s.Close();
            s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            s.Bind(new IPEndPoint(IPAddress.Any, settings.ClipboardTransferPort));
            s.Close();
            s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            s.Bind(new IPEndPoint(IPAddress.Any, settings.InputReceivingPort));
            s.Close();
        }

        public void Start(Settings settings) {
            if (listenerThread != null || listenerThread != null) {
                throw new InvalidOperationException("Client accepting service already running");
            }
            IntPtr ptr = Marshal.SecureStringToBSTR(settings.SecurePassword);
            Password = Marshal.PtrToStringUni(ptr);

            listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Port = settings.MainConnectionPort;

            //create and start main network Thread
            listenerThread = new Thread(new ParameterizedThreadStart(WaitForClients));
            listenerThread.IsBackground = true;
            listenerThread.SetApartmentState(ApartmentState.STA);
            listenerThread.Start(settings);
        }

        public void Stop() {
            if (listener == null || listenerThread == null || !listenerThread.IsAlive) { //service is already stopped. No problems in this case
                return;
            }
            StopAllServices(null);

            //Utility.ShutdownSocket(listener);
            listener.Close();
            try { listenerThread.Join(); } catch (ThreadInterruptedException) { }
            listenerThread = null;
            listener = null;
        }

        private void WaitForClients(object arg) {
            Settings settings = arg as Settings;
            keepAlive = new KeepaliveChannel(settings.KeepAlivePort);
            ClientAuthenticated += Connection_OnClientAuthenticated;
            ClientDisconnected += StopAllServices;
            ClientDisconnected += Connection_OnClientDisconnected;
            keepAlive.DeadClient += OnNetworkError;
            byte[] AcceptedMessage = Encoding.Unicode.GetBytes(ClientAcceptedResponse);
            byte[] RejectedMessage = Encoding.Unicode.GetBytes(ClientRejectedResponse);
            byte[] LeftMessage = Encoding.Unicode.GetBytes(ClientLeavingResponse);

            OnStarted();

            while (true) {   /*loop among all clients who will connect to me*/

                actualClient = null;
                IPEndPoint clientEndpoint = null;
                byte[] buffer = new byte[64];

                try {
                    while (true)      //loop until a client is authenticated
                    {
                        try {
                            actualClient = listener.Accept();
                        } catch (Exception) { /*listening socket closed by Stop()*/
                            goto finish;
                        }

                        clientEndpoint = (IPEndPoint)actualClient.RemoteEndPoint;
                        settings.ClientIP = clientEndpoint.Address;

                        OnClientConnected(clientEndpoint);

                        int recv = actualClient.Receive(buffer);
                        if (recv == 0) {    /*the client shuts down the Socket connection with the Shutdown method*/
                            throw new SocketException();
                        }

                        bool authenticated = Encoding.Unicode.GetString(buffer).Equals(passwdDigest);

                        if (authenticated) {
                            break;
                        }

                        Utility.SendBytes(actualClient, RejectedMessage, RejectedMessage.Length, SocketFlags.None);
                        //actualClient.Send(RejectedMessage);
                        actualClient.Shutdown(SocketShutdown.Both);
                        actualClient.Disconnect(true);
                    }
                } catch (SocketException) {
                    actualClient.Shutdown(SocketShutdown.Both);
                    actualClient.Disconnect(true);
                    continue;
                }

                OnClientAuthenticated(); // Client is authenticated. Invoke callbacks and open all channels

                try {
                    IPAddress clientIP = clientEndpoint.Address;

                    ClipboardNetworkChannel.StartService(settings.ClipboardReceivingPort);
                    InputNetworkChannel.StartService(settings.InputReceivingPort);

                    Utility.SendBytes(actualClient, AcceptedMessage, AcceptedMessage.Length, SocketFlags.None);

                    ClipboardTrasfer.Target = new IPEndPoint(clientIP, settings.ClipboardTransferPort);

                    //wait synchronously for BYE from client before closing correctly the socket
                    Utility.ReceiveBytes(actualClient, buffer, LeftMessage.Length, SocketFlags.None);
                    Console.WriteLine("Received " + Encoding.Unicode.GetString(buffer,0, LeftMessage.Length) + " from client");

                } catch (SocketException se) {
                    lock (errorLockObj) {
                        ErrorOccurred = se;
                    }
                } catch (Exception e) {
                    Console.WriteLine("{0}:{1}", e.GetType().Name, e.Message);
                }
                finally {
                    Utility.ShutdownSocket(actualClient);
                    lock (errorLockObj) {
                        OnClientDisconnected(ErrorOccurred);
                        ErrorOccurred = null;
                    }
                }
                //finished serving this client
            }

            finish:
            OnStopped();
            Console.WriteLine("Client accepting channel CLOSED!!! BYE");
        }

        private void Connection_OnClientAuthenticated() {
            keepAlive.Start();
        }

        private void Connection_OnClientDisconnected(Exception exception) {
            keepAlive.Interrupt();
        }

        void OnNetworkError(SocketException se) {
            /*if (NetworkErrorOccured != null) {
                NetworkErrorOccured(se);
            }*/
            //force current client connection to kill -> return calling Accept()
            lock (errorLockObj) {
                ErrorOccurred = se;
            }
            Utility.ShutdownSocket(actualClient);
        }

        private void StopAllServices(Exception exception) {
            ClipboardNetworkChannel.StopService();
            ClipboardTrasfer.StopService();
            InputNetworkChannel.StopService();
        }

        private void OnStarted() {
            if (Started != null) {
                Started();
            }
        }

        private void OnStopped() {
            if (Stopped != null) {
                Stopped();
            }
        }

        private void OnClientConnected(IPEndPoint client) {
            if (ClientConnected != null) {
                ClientConnected(client);
            }
        }

        private void OnClientAuthenticated() {
            if (ClientAuthenticated != null) {
                ClientAuthenticated();
            }
        }

        private void OnClientDisconnected(Exception exception) {
            if (ClientDisconnected != null) {
                ClientDisconnected(exception);
            }
        }

    }
}
