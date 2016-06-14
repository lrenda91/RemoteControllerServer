using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.ComponentModel;
using System.Net.Sockets;
using System.Net;

namespace Server.Net {
    class KeepaliveChannel {

        public NetworkErrorEventHandler DeadClient;
        private BackgroundWorker worker;
        private Socket listener;

        public int MaxTries {
            get;
            set;
        }

        private int TimeoutMs;

        public int Port {
            get;
            set;
        }

        public KeepaliveChannel(int port) {
            worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.WorkerSupportsCancellation = true;
            worker.DoWork += worker_DoWork;
            worker.RunWorkerCompleted += worker_RunWorkerCompleted;
            Port = port;
        }

        private void worker_DoWork(object sender, DoWorkEventArgs e) {
            listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            listener.Bind(new IPEndPoint(IPAddress.Any, Port));
            listener.Listen(0);
            TimeoutMs = 3000;       //by default
            MaxTries = 3;           //by default
            listener.ReceiveTimeout = TimeoutMs;

            Socket client = listener.Accept();
            int fails = 0;
            int HeartBeatLength = 32;
            byte[] heartBeatBuffer = new byte[HeartBeatLength];
            while (!worker.CancellationPending) {
                try {
                    if (!Utility.ReceiveBytes(client, heartBeatBuffer, HeartBeatLength, SocketFlags.None)) {
                        Thread.Sleep(TimeoutMs);
                        throw new SocketException();
                    }
                    Console.WriteLine("KEEPALIVE OK");
                    fails = 0;
                } catch (SocketException se) {
                    fails++;
                    if (fails == MaxTries) {
                        Utility.ShutdownSocket(client);
                        throw se;      //forward exception to worker_RunWorkerCompleted
                    }
                }
            }
            Utility.ShutdownSocket(client);
        }

        void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            Utility.ShutdownSocket(listener);
            if (e.Error != null && e.Error.GetType() == typeof(SocketException)) {
                SocketException exception = e.Error as SocketException;
                if (DeadClient != null) {
                    DeadClient(exception);
                }
            }
            Console.WriteLine("KEEPALIVE Closed!!");
        }

        public void Start() {
            if (!worker.IsBusy) {
                worker.RunWorkerAsync();
            }
        }

        public void Interrupt() {
            if (worker.IsBusy) {
                worker.CancelAsync();
            }
        }
    }
}
