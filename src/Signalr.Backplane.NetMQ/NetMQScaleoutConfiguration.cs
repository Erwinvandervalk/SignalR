namespace Signalr.Backplane.NetMQ
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.AspNet.SignalR.Messaging;

    public class NetMQScaleoutConfiguration : ScaleoutConfiguration
    {
        private readonly string publisherAddress;
        private readonly string[] _subscriberAddresses;

        public NetMQScaleoutConfiguration(string publisherAddress, IEnumerable<string> subscriberAddresses)
        {
            publisherAddress = publisherAddress;
            _subscriberAddresses = subscriberAddresses.ToArray();
        }

        public string[] SubscriberAddresses
        {
            get { return _subscriberAddresses; }
        }

        public string PublisherAddress
        {
            get { return publisherAddress; }
        }
    }
}