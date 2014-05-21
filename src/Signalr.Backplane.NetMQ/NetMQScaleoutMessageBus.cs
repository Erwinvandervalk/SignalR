namespace Signalr.Backplane.NetMQ
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using global::NetMQ;
    using Microsoft.AspNet.SignalR;
    using Microsoft.AspNet.SignalR.Messaging;

    public class NetMQScaleoutMessageBus : ScaleoutMessageBus
    {
        private readonly NetMQScaleoutConfiguration _configuration;
        private readonly NetMQContext _context;
        private readonly BlockingCollection<OutgoingMessage> _outgoing = new BlockingCollection<OutgoingMessage>();
        private readonly Poller _poller;
        private readonly NetMQSocket _subscriber;

        public NetMQScaleoutMessageBus(IDependencyResolver resolver, NetMQScaleoutConfiguration configuration)
            : base(resolver, configuration)

        {
            _configuration = configuration;
            _context = NetMQContext.Create();

            _subscriber = _context.CreateSubscriberSocket();
            _subscriber.Bind(configuration.ListenAddress);
            _subscriber.Subscribe("");
            ulong seq = 0;
            _subscriber.ReceiveReady += (sender, args) =>
            {
                var bytes = args.Socket.Receive();
                var scaleoutMessage = ScaleoutMessage.FromBytes(bytes);
                OnReceived(0, seq, scaleoutMessage);
                seq++;
            };

            _poller = new Poller();
            _poller.AddSocket(_subscriber);
            Task.Run(() => _poller.Start());

            Task.Run(() =>
            {
                using (NetMQSocket publisher = _context.CreatePublisherSocket())
                {
                    foreach (string subscriberAddress in configuration.SubscriberAddresses)
                    {
                        publisher.Connect(subscriberAddress);
                    }
                    foreach (var outgoingMessage in _outgoing.GetConsumingEnumerable())
                    {
                        var scaleoutMessage = new ScaleoutMessage(outgoingMessage.Messages);
                        byte[] bytes = scaleoutMessage.ToBytes();
                        publisher.Send(bytes);
                        outgoingMessage.TaskCompletionSource.SetResult(0);
                    }
                }
            });
        }

        protected override Task Send(IList<Message> messages)
        {
            var outgoingMessage = new OutgoingMessage(messages);
            _outgoing.Add(outgoingMessage);
            return outgoingMessage.TaskCompletionSource.Task;
        }

        protected override int StreamCount
        {
            get { return _configuration.SubscriberAddresses.Length; }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _outgoing.CompleteAdding();
            _poller.Stop(true);
            _subscriber.Dispose();
            _context.Dispose();
        }

        private class OutgoingMessage
        {
            private readonly IList<Message> _messages;
            private readonly TaskCompletionSource<int> _taskCompletionSource = new TaskCompletionSource<int>();

            public OutgoingMessage(IList<Message> messages)
            {
                _messages = messages;
            }

            public IList<Message> Messages
            {
                get { return _messages; }
            }

            public TaskCompletionSource<int> TaskCompletionSource
            {
                get { return _taskCompletionSource; }
            }
        }
    }
}