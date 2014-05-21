namespace Signalr.Backplane.NetMQ
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.AspNet.SignalR.Messaging;

    public class NetMQScaleoutConfiguration : ScaleoutConfiguration
    {
        private readonly string _listenAddress;
        private readonly string[] _subscriberAddresses;

        public NetMQScaleoutConfiguration(string listenAddress, IEnumerable<string> subscriberAddresses)
        {
            _listenAddress = listenAddress;
            _subscriberAddresses = subscriberAddresses.ToArray();
        }

        public string[] SubscriberAddresses
        {
            get { return _subscriberAddresses; }
        }

        public string ListenAddress
        {
            get { return _listenAddress; }
        }
    }
}