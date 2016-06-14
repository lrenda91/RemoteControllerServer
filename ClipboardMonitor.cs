using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace Server {

    public delegate void CliboardUpdateEventHandler(IDataObject content);

    public class ClipboardMonitor {

        public static CliboardUpdateEventHandler ClipboardChanged;
        private static HwndSource source = null;
        private static IntPtr hWndNextViewer;

        public static Window ClipboardViewer {
            set {
                IntPtr handle = new WindowInteropHelper(value).Handle;
                source = HwndSource.FromHwnd(handle);
            }
        }

        public static void EnableCapture() {
            if (source != null) {
                ClipboardChanged = null; 
                ClipboardChanged += printClipboardContent;
                source.AddHook(WinProc);
                hWndNextViewer = Win32.SetClipboardViewer(source.Handle);
            }
        }

        public static void DisableCapture() {
            if (source != null) {
                Win32.ChangeClipboardChain(source.Handle, hWndNextViewer);
                hWndNextViewer = IntPtr.Zero;
                source.RemoveHook(WinProc);
                ClipboardChanged = null; 
            }            
        }

        private static IntPtr WinProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
            switch (msg) {
                case Win32.WM_CHANGECBCHAIN:
                    if (wParam == hWndNextViewer) {
                        // clipboard viewer chain changed, need to fix it. 
                        hWndNextViewer = lParam;
                    }
                    else if (hWndNextViewer != IntPtr.Zero) {
                        // pass the message to the next viewer. 
                        Win32.SendMessage(hWndNextViewer, msg, wParam, lParam);
                    }
                    break;

                case Win32.WM_DRAWCLIPBOARD:
                    // clipboard content changed 
                    OnClipboardChanged();
                    // pass the message to the next viewer, avoiding infinite recursion.
                    if (hWndNextViewer != source.Handle) {
                        Win32.SendMessage(hWndNextViewer, msg, wParam, lParam);
                    }
                    break;
            }
            return IntPtr.Zero;
        }
 
        private static void OnClipboardChanged() {
            IDataObject iData = Clipboard.GetDataObject();
            if (ClipboardChanged != null) {
                ClipboardChanged(iData);
            }          
        }

        private static void printClipboardContent(IDataObject dataObject){
            /*Console.Write("Clipboard captured: ");
            foreach (string f in dataObject.GetFormats()) {
                Console.Write(f + " ");
            }
            Console.WriteLine();*/
        }

    }

    public class ClipboardChangedEventArgs : EventArgs {
        public readonly IDataObject DataObject;
        public ClipboardChangedEventArgs(IDataObject dataObject) {
            DataObject = dataObject;
        }
    }
        
}
