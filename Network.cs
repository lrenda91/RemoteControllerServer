using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net;
using System.Net.Sockets;
using Server.ViewModel;
using System.Runtime.InteropServices;

namespace Server.Net {

    public delegate void ConnectionEventHandler();
    
    public delegate void ClientConnectedEventHandler(IPEndPoint client);

    public delegate void ClientAuthenticatedEventHandler();

    public delegate void ClientDisconnectedEventHandler(Exception exception);

    public delegate void NetworkErrorEventHandler(SocketException exception);

    public class ClipboardConstants {
        public static long MaxFileSizeInBytes {
            get;
            set;
        }
        public const int PacketLength = 8 * 1024;
        public const int MaxPacketPayloadLength = PacketLength - (1 + 8 + 256 + 4);
    }

    public enum ClipboardPacketType : byte {
        TEXT = 0,
        FILE = 1,
        DIRECTORY = 2,
        BITMAP = 3,
        UPDATE = 4,
        CANCEL = 5,
        SET_DROPLIST = 6
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct ClipboardPacket {
        public byte type;   //establish whether clipboard update notification refers to plain text, file or directory
        public long totalLength; //a platform-specific long specifying total length of the fragmented file
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string name; //file name (only if clipboard notification refers to a file or directory)
        public int payloadLength; //a platform-specific integer specifying 'payload' array's length
        public byte[] payload; //real data to transfer
    }



    public class Utility {
        private Utility() { }
        public static void ShutdownSocket(Socket socket){
            if (socket != null) {
                if (socket.Connected) {
                    try {
                        socket.Shutdown(SocketShutdown.Both);
                    }
                    catch (Exception) { }
                }
                /**
                 * IMPORTANT!!!! In any case, CLOSE!!!!! otherwise threads using this socket will not terminate!!
                 */
                socket.Close();
            }
        }
        public static bool IsConnected(Socket socket)
        {
            try {
                return !(socket.Poll(1000000, SelectMode.SelectRead) && socket.Available == 0);
            }
            catch (SocketException) { return false; }
        }

        public static bool ReceiveBytes(Socket socket, byte[] buffer, int bytes, SocketFlags flags) {
            int offset = 0;
            int toReceive = bytes;
            do {
                int read = socket.Receive(buffer, offset, toReceive, flags);
                if (read == 0) {
                    return false;
                }
                offset += read;
                toReceive -= read;
            }
            while (offset < bytes);
            return true;
        }

        /*public static void SendBytes(Socket socket, byte[] buffer, int bytes, SocketFlags flags) {
            int tot = 0;
            do {
                int sent = socket.Send(buffer, bytes, flags);
                tot += sent;
            }
            while (tot < bytes);
        }*/

        public static void SendBytes(Socket socket, byte[] buffer, int bytes, SocketFlags flags) {
            int offset = 0;
            int toSend = bytes;
            do {
                int written = socket.Send(buffer, offset, toSend, flags);
                if (written == 0) {
                    break;
                }
                offset += written;
                toSend -= written;
            }
            while (offset < bytes);
        }

    }



    static class Serialization {

        public static byte[] getBytes(INPUT str) {
            int size = Marshal.SizeOf(str);
            byte[] arr = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);

            Marshal.StructureToPtr(str, ptr, true);
            Marshal.Copy(ptr, arr, 0, size);
            Marshal.FreeHGlobal(ptr);

            return arr;
        }

        public static INPUT fromBytes(ref byte[] arr) {
            INPUT str = new INPUT();
            str.dwType = BitConverter.ToInt32(arr, 0);
            if (str.dwType == 0) {
                str.mi = new MOUSEINPUT();
                str.mi.dx = BitConverter.ToInt32(arr, 4);
                str.mi.dy = BitConverter.ToInt32(arr, 8);
                str.mi.mouseData = BitConverter.ToUInt32(arr, 12);
                str.mi.dwFlags = BitConverter.ToUInt32(arr, 16);
                str.mi.time = BitConverter.ToUInt32(arr, 20);
                str.mi.dwExtraInfo = new IntPtr(arr[24]);
            } else if (str.dwType == 1) {
                str.ki = new KEYBDINPUT();
                str.ki.wVk = BitConverter.ToUInt16(arr, 4);
                str.ki.wScan = BitConverter.ToUInt16(arr, 6);
                str.ki.dwFlags = BitConverter.ToUInt32(arr, 8);
                str.ki.time = BitConverter.ToUInt32(arr, 12);
                str.ki.dwExtraInfo = new IntPtr(arr[16]);
                /*
                str.ki.wVk = BitConverter.ToInt32(arr, 4);
                str.ki.wScan = BitConverter.ToInt32(arr, 8);
                str.ki.dwFlags = BitConverter.ToUInt32(arr, 12);
                str.ki.time = BitConverter.ToInt32(arr, 16);
                str.ki.dwExtraInfo = new IntPtr(arr[20]);*/
            }
            return str;
        }

        public static byte[] GetClipboardBytes(ClipboardPacket p) {
            //byte[] res = new byte[1 + sizeof(long) + 256 + sizeof(int) + p.length];
            byte[] res = new byte[8 * 1024];
            int i = 0;
            res[i++] = (byte)p.type;
            for (int j = sizeof(long); j > 0; j--) {
                res[i++] = (byte)(p.totalLength >> (8 * ((sizeof(long) - j))));
            }
            byte[] s = System.Text.Encoding.Unicode.GetBytes(p.name);
            for (int j = 0; j < 256; j++) {
                res[i++] = (j < s.Length) ? s[j] : (byte)0;
            }
            for (int j = sizeof(int); j > 0; j--) {
                res[i++] = (byte)(p.payloadLength >> (8 * ((sizeof(int) - j))));
            }
            for (int j = 0; j < p.payloadLength; j++) {
                res[i++] = p.payload[j];
            }
            return res;
        }

        public static ClipboardPacket FromClipboardBytes(byte[] arr) {
            ClipboardPacket p = new ClipboardPacket();
            int i = 0;
            p.type = arr[i++];
            p.totalLength = BitConverter.ToInt64(arr, i);
            i += 8;
            p.name = System.Text.Encoding.Unicode.GetString(arr, i, 256).TrimEnd('\0');  //remove all string terminator characters
            i += 256;
            p.payloadLength = BitConverter.ToInt32(arr, i);
            i += 4;
            p.payload = new byte[p.payloadLength];
            for (int j = 0; j < p.payloadLength; j++, i++) {
                p.payload[j] = arr[i];
            }
            return p;
        }

        /*
        public static string SerializeObject<T>(T obj)
        {
            XmlSerializer xs = new XmlSerializer(typeof(T));
            MemoryStream memoryStream = new MemoryStream();
            XmlTextWriter xmlTextWriter = new XmlTextWriter(memoryStream, Encoding.Unicode);
            xs.Serialize(xmlTextWriter, obj);
            memoryStream = (MemoryStream)xmlTextWriter.BaseStream;
            return System.Text.Encoding.Unicode.GetString(memoryStream.ToArray());
        }
        
        public static T DeserializeObject<T>(string xml)
        {
            XmlSerializer xs = new XmlSerializer(typeof(T));
            MemoryStream memoryStream = new MemoryStream(System.Text.Encoding.Unicode.GetBytes(xml));
            XmlTextWriter xmlTextWriter = new XmlTextWriter(memoryStream, Encoding.Unicode);
            return (T)xs.Deserialize(memoryStream);
        }
        */
        /*
        public static byte[] GetBytes<T>(T t) where T : struct
        {
            MemoryStream stream = new MemoryStream();
            BinaryFormatter bFormatter = new BinaryFormatter();
            bFormatter.AssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Full;
            bFormatter.Serialize(stream, t);
            return stream.ToArray();
        }

        public static T FromBytes<T>(byte[] array) where T : struct
        {
            BinaryFormatter formatter = new BinaryFormatter();
            MemoryStream m = new MemoryStream(array);
            T res = (T) formatter.Deserialize(m);
            return res;
        }
        */
    }

    

}
