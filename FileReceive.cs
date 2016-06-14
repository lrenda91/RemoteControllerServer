using System;
using System.Text;
using System.Windows.Threading;
using System.ComponentModel;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using Server.Graphics;
using System.Windows;

namespace Server.Net {

    class AsynchFileReceiver {

        private static readonly int CANCEL_TIMEOUT_MILLIS = 10000;

        private readonly double TOTAL_LENGTH;
        private byte[] currentFragment;
        private string fileName;

        private BackgroundWorker worker;
        private AutoResetEvent nextFragmentAvailable = new AutoResetEvent(false);
        private AutoResetEvent currentFragmentConsumed = new AutoResetEvent(true);

        private ProgressWindow view;
        private bool littleFile;

        public AsynchFileReceiver(string file, long totLength) {
            TOTAL_LENGTH = (double)totLength;
            fileName = file;
            littleFile = (totLength < 1024*1024);

            runOnUI(() => {
                worker = new BackgroundWorker();
                worker.WorkerReportsProgress = true;
                worker.WorkerSupportsCancellation = true;
                worker.DoWork += FileReceiver_DoWork;
                worker.ProgressChanged += worker_ProgressChanged;
                worker.RunWorkerCompleted += worker_RunWorkerCompleted;
            });
        }

        public void Start() {   
            runOnUI(() => {
                if (!worker.IsBusy) {
                    worker.RunWorkerAsync();
                    if (littleFile) {
                        return;
                    }
                    view = new ProgressWindow(fileName, (long)TOTAL_LENGTH);
                    view.Show();
                    view.Message = "Transfer file '" + fileName + "' to my file system: "+TOTAL_LENGTH+ " bytes";
                }
            });
        }

        public bool inProgress() {
            return worker.IsBusy;
        }

        public void newFragmentAvailable(ref byte[] data) {
            //wait until the current fragment has been correctly written to the destination file
            currentFragmentConsumed.WaitOne();

            currentFragment = data;
            nextFragmentAvailable.Set();
        }

        private void FileReceiver_DoWork(object sender, DoWorkEventArgs e) {
            Console.WriteLine("CREO FILE " + fileName);
            using (FileStream stream = new FileStream(fileName, FileMode.Create)) {

                double writtenBytes = 0.0;
                while (writtenBytes < TOTAL_LENGTH) {
                    //wait for availability (from the network) of the next file fragment or TIMEOUT expiration
                    if (worker.CancellationPending || nextFragmentAvailable.WaitOne(CANCEL_TIMEOUT_MILLIS) == false) {
                        e.Cancel = true;
                        break;
                    }

                    stream.Write(currentFragment, 0, currentFragment.Length);
                    writtenBytes += currentFragment.Length;

                    int progressPercentage = (int)(writtenBytes / TOTAL_LENGTH * 100.0);
                    worker.ReportProgress(progressPercentage);

                    currentFragmentConsumed.Set();
                }
            }
        }


        private void worker_ProgressChanged(object sender, ProgressChangedEventArgs e) {
            if (view != null) {
                view.bar.Value = e.ProgressPercentage; // Do all the ui thread updates here
                view.progressLabel.Content = e.ProgressPercentage + " % progress...";
            }
            //Console.WriteLine(e.ProgressPercentage + " %");
        }

        private void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            string finalMessage = String.Empty;
            if (e.Error != null) {
                finalMessage = e.Error.GetType().ToString() + ": " + e.Error.Message;
            }
            else if (e.Cancelled) {
                finalMessage = "Timeout expired. File will be deleted";
                Thread.Sleep(300);
                File.Delete(fileName);
            }
            else {
                Console.WriteLine(fileName + " download COMPLETED!!!");
            }
            
            ClipboardNetworkChannel.downloadCompleted(this);
            if (view != null) {
                view.progressLabel.Content = finalMessage;
                Thread.Sleep(300);
                view.Close();
            }
        }

        private void runOnUI(Action action) {
            Application.Current.Dispatcher.BeginInvoke(action, null);
        }

    }
}
