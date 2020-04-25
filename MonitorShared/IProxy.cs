using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace GrowattMonitorShared
{
    interface IProxy
    {
        Task Start(string remoteServerIp, ushort remoteServerPort, ushort localPort, string localIp = null);
    }
}
