using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Text;
using System.Threading;

namespace FlashPolicyService
{
    public partial class Service : ServiceBase
    {
        private BackgroundWorker listenWorker = new BackgroundWorker();
        private string policyFileContents;
        private int serverPort = 843;

        public Service()
        {
            InitializeComponent();
            Logger.Instance.CreateLogSource();
            listenWorker.DoWork += ListenWorkerDoWork;
            listenWorker.WorkerSupportsCancellation = true;
        }

        protected override void OnStart(string[] args)
        {
            Logger.Instance.WriteToLog(EventLogEntryType.Information, "Starting server");
            if (listenWorker.IsBusy)
            {
                Logger.Instance.WriteToLog(EventLogEntryType.Warning, "Server is already running");
                return;
            }

            try
            {
                string policyFilePath = AppDomain.CurrentDomain.BaseDirectory;
                using (StreamReader reader = new StreamReader(policyFilePath + "crossdomain.xml"))
                {
                    policyFileContents = reader.ReadToEnd();
                }

                listenWorker.RunWorkerAsync();
            }
            catch (Exception ex)
            {
                Logger.Instance.WriteToLog(EventLogEntryType.Error, string.Format("An error has occured while starting the server:{0}{1}", Environment.NewLine, ex));
                return;
            }
        }

        private void ListenWorkerDoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                TcpListener tcpListener = new TcpListener(IPAddress.Any, serverPort);
                tcpListener.Start();
                BackgroundWorker worker = sender as BackgroundWorker;

                if (worker != null)
                {
                    while (!worker.CancellationPending)
                    {
                        TcpClient client = tcpListener.AcceptTcpClient();
                        Thread clientThread = new Thread(HandleClientComm) { IsBackground = true };
                        clientThread.Start(client);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.WriteToLog(EventLogEntryType.Error, string.Format("An error has occured while listening on port 843:{0}{1}", Environment.NewLine, ex));
            }
        }

        private void HandleClientComm(object client)
        {
            TcpClient tcpClient = (TcpClient)client;
            NetworkStream clientStream = tcpClient.GetStream();
            byte[] bytes = new byte[1024];
            int readCount;

            tcpClient.ReceiveTimeout = 10000;

            if (clientStream.CanRead)
            {
                while (true)
                {
                    readCount = 0;

                    try
                    {
                        readCount = clientStream.Read(bytes, 0, bytes.Length);
                    }
                    catch
                    {
                        // a socket error has occured
                        break;
                    }

                    if (readCount == 0)
                    {
                        // client disconnected
                        break;
                    }

                    string request = Encoding.ASCII.GetString(bytes, 0, readCount);
                    if (request.Equals("<policy-file-request/>\0"))
                    {
                        byte[] reply = Encoding.ASCII.GetBytes(policyFileContents);
                        clientStream.Write(reply, 0, reply.Count());
                        clientStream.Flush();
                    }

                    break;
                }
            }

            clientStream.Close();
            tcpClient.Close();
        }

        protected override void OnStop()
        {
            Logger.Instance.WriteToLog(EventLogEntryType.Information, "Stopping server");

            if (listenWorker.IsBusy)
            {
                listenWorker.CancelAsync();
            }
        }
    }
}
