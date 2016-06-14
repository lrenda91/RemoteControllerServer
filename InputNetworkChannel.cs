using System;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using System.ComponentModel;
using System.Windows.Input;

using Server;

namespace Server.Net {
    class InputNetworkChannel {

        public static NetworkErrorEventHandler NetworkErrorOccured;

        private static Socket listener;
        private static Thread workerThread;

        public static void StartService(short port) {
            if (listener != null || workerThread != null) {
                throw new InvalidOperationException("Input Listening service already running");
            }
            //listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            listener.Bind(new IPEndPoint(IPAddress.Any, port));

            workerThread = new Thread(new ThreadStart(listenInput));
            workerThread.SetApartmentState(ApartmentState.STA);
            workerThread.Start();
        }

        public static void StopService() {
            NetworkErrorOccured = null;
            if (listener == null || workerThread == null) { //service is already stopped. No problems in this case
                return;
            }

            //raise the ObjectDisposedException, breaking the loop and correctly terminating the infinite events reception
            Utility.ShutdownSocket(listener);
            //listener.Close();
            try { workerThread.Join(); } catch (ThreadInterruptedException) { }
            workerThread = null;
            listener = null;
            //client = null;
            Console.WriteLine("Mouse/Keyboard receiver service CLOSED!!");
        }

        private static void listenInput() {
            int size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(INPUT));
            byte[] buffer = new byte[size];
            INPUT[] inputs = new INPUT[1];
            while (true) {
                try {
                    if (!Utility.ReceiveBytes(listener, buffer, size, SocketFlags.None)) {
                        break;
                    }
                    inputs[0] = Serialization.fromBytes(ref buffer);
                    log(inputs[0]);
                    Win32.SendInput(1, inputs, size);
                } catch (ObjectDisposedException ode) {       //closed
                    Console.WriteLine(ode.GetType().ToString());
                    break;
                } catch (SocketException se) {
                    if (NetworkErrorOccured != null) {
                        NetworkErrorOccured(se);
                    }
                } catch (Exception e) {
                    Console.WriteLine(e.GetType().ToString());
                    break;
                }
            }
            listener.Close();
        }


        private static void log(INPUT input) {
            if (input.dwType == InputSimulator.MOUSE_TYPE+100) {    //mouse
                Console.Write("MOUSE:");
                uint flags = input.mi.dwFlags;
                if ((flags & InputSimulator.MOUSEEVENTF_ABSOLUTE) == InputSimulator.MOUSEEVENTF_ABSOLUTE) { Console.Write(" ABSOLUTE"); }
                if ((flags & InputSimulator.MOUSEEVENTF_LEFTDOWN) == InputSimulator.MOUSEEVENTF_LEFTDOWN) { Console.Write("LEFTDOWN"); }
                if ((flags & InputSimulator.MOUSEEVENTF_LEFTUP) == InputSimulator.MOUSEEVENTF_LEFTUP) { Console.Write(" LEFTUP"); }
                if ((flags & InputSimulator.MOUSEEVENTF_RIGHTDOWN) == InputSimulator.MOUSEEVENTF_RIGHTDOWN) { Console.Write(" RIGHTDOWN"); }
                if ((flags & InputSimulator.MOUSEEVENTF_RIGHTUP) == InputSimulator.MOUSEEVENTF_RIGHTUP) { Console.Write(" RIGHTUP"); }
                if ((flags & InputSimulator.MOUSEEVENTF_WHEEL) == InputSimulator.MOUSEEVENTF_WHEEL) { Console.Write(" WHEEL"); }
                if ((flags & InputSimulator.MOUSEEVENTF_MOVE) == InputSimulator.MOUSEEVENTF_MOVE) {
                    Console.Write(" MOVE");
                    Console.Write(" (" + input.mi.dx + "," + input.mi.dy + ")");
                }
                if ((flags & InputSimulator.MOUSEEVENTF_MIDDLEDOWN) == InputSimulator.MOUSEEVENTF_MIDDLEDOWN) { Console.Write(" MIDDLEDOWN"); }
                if ((flags & InputSimulator.MOUSEEVENTF_ABSOLUTE) == InputSimulator.MOUSEEVENTF_MIDDLEUP) { Console.Write(" MIDDLEUP"); }
                Console.WriteLine();
            } else if (input.dwType == InputSimulator.KEYBOARD_TYPE) {   //keyboard
                VirtualKeyCode vk = (VirtualKeyCode)input.ki.wVk;
                VirtualKeyCode unicodeVK = (VirtualKeyCode)input.ki.wScan;
                var flags = input.ki.dwFlags;
                if ((flags & InputSimulator.KEYEVENTF_UNICODE) == InputSimulator.KEYEVENTF_UNICODE) {
                    Console.Write("KEYBOARD: '" + unicodeVK);
                } else {
                    Console.Write("KEYBOARD: '" + vk);
                }

                if (flags == InputSimulator.KEYEVENTF_KEYDOWN) { Console.Write(" KEYDOWN"); }
                if ((flags & InputSimulator.KEYEVENTF_KEYUP) == InputSimulator.KEYEVENTF_KEYUP) { Console.Write(" KEYUP"); }
                if ((flags & InputSimulator.KEYEVENTF_EXTENDEDKEY) == InputSimulator.KEYEVENTF_EXTENDEDKEY) { Console.Write(" EXTENDEDKEY"); }
                Console.WriteLine();
            }
        }

    }
}
