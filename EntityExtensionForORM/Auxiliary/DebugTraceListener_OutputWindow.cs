
using SQLite.Net;
using System.Diagnostics;

namespace EntityExtensionForORM.Auxiliary
{
    public class DebugTraceListener_OutputWindow : ITraceListener  
    {
        public void Receive(string message)
        {
            Debug.WriteLine(message);
        }
    }
}
