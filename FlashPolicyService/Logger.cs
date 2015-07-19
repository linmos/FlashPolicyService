using System.Diagnostics;

namespace FlashPolicyService
{
    public sealed class Logger
    {
        static Logger _instance;
        static readonly object Padlock = new object();

        public static Logger Instance
        {
            get
            {
                lock (Padlock)
                {
                    return _instance ?? (_instance = new Logger());
                }
            }
        }

        private const string Source = "FlashPolicyService";
        private const string LogName = "Flash Policy Service";

        public void CreateLogSource()
        {
            var attempts = 0;
            while (!EventLog.SourceExists(Source) && attempts < 5)
            {
                EventLog.CreateEventSource(new EventSourceCreationData(Source, LogName));
                System.Threading.Thread.Sleep(3000);
                attempts++;
            }

        }

        public void WriteToLog(EventLogEntryType type, object message)
        {
            EventLog.WriteEntry(Source, message.ToString(), type);
        }
    }
}
