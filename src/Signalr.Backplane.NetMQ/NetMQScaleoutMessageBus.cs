using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Remoting.Messaging;
using System.Threading;

using Microsoft.AspNet.SignalR.Tracing;

namespace Signalr.Backplane.NetMQ
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using global::NetMQ;
    using Microsoft.AspNet.SignalR;
    using Microsoft.AspNet.SignalR.Messaging;

    /// <summary>
    /// the NetMQScaleoutMessageBus uses NetMQ (0MQ implementation) to distribute messages. 
    /// 
    /// This class uses a publish subscribe pattern. Each node will listen to published messages on all other nodes. 
    /// </summary>
    public class NetMQScaleoutMessageBus : ScaleoutMessageBus
    {
        private TraceSource _trace;
        private bool _running;
        private static long latestMessageId = 0;
        private readonly NetMQScaleoutConfiguration _configuration;
        private NetMQContext _context;
        private NetMQSocket _publisherSocket;
        readonly List<NetMQSocket> _subscriberSockets = new List<NetMQSocket>();

        public NetMQScaleoutMessageBus(IDependencyResolver resolver, NetMQScaleoutConfiguration configuration)
            : base(resolver, configuration)

        {
            _configuration = configuration;
            _context = NetMQContext.Create();

            var traceManager = resolver.Resolve<ITraceManager>();
            _trace = traceManager["SignalR." + typeof(NetMQScaleoutMessageBus).Name];

            SetupPublisher();

            ThreadPool.QueueUserWorkItem(Subscribe);
        }


        protected override Task Send(int streamIndex, IList<Message> messages)
        {
            TraceMessages(messages, "Sending from " + _configuration.PublisherAddress);

            return Task.Factory.StartNew(() =>
            {
                var id = GetMessageId();
                var message = new NetMqMessage(id, messages);
                _publisherSocket.Send(message.GetBytes());

                SendMessageToSelf(streamIndex, id, message);

            });
        }

        private void SendMessageToSelf(int streamIndex, long id, NetMqMessage message)
        {
            Task.Factory.StartNew(() =>
            {
                this.OnReceived(streamIndex, (ulong)id, message.ScaleoutMessage);
            });
        }

        private static long GetMessageId()
        {
            long id = Interlocked.Increment(ref latestMessageId);
            return id;
        }

        private void SetupPublisher()
        {
            Open(0);

            _publisherSocket = _context.CreatePublisherSocket();
            _publisherSocket.Bind(_configuration.PublisherAddress);
        }

        private void Subscribe(object state)
        {
            _running = true;
            foreach (string subscriberAddress in _configuration.SubscriberAddresses)
            {
                var subscriberSocket = _context.CreateSubscriberSocket();
                subscriberSocket.Connect(subscriberAddress);
                subscriberSocket.Subscribe("");

                _subscriberSockets.Add(subscriberSocket);
                Task.Factory.StartNew(() => WaitForMessages(subscriberSocket));
            }

        }

        private void WaitForMessages(NetMQSocket subscriberSocket)
        {
            while (_running)
            {
                try
                {
                    var bytes = subscriberSocket.Receive();

                    var message = NetMqMessage.FromBytes(bytes);

                    TraceMessages(message.ScaleoutMessage.Messages, "Receiving at " + _configuration.PublisherAddress);
                    OnReceived(0, (ulong)message.MessageId, message.ScaleoutMessage);
                }
                catch (Exception)
                {

                    throw;
                }
            }
        }

        private void TraceMessages(IList<Message> messages, string messageType)
        {
            if (!_trace.Switch.ShouldTrace(TraceEventType.Verbose))
            {
                return;
            }

            foreach (Message message in messages)
            {
                _trace.TraceVerbose("{0} {1} bytes over Service Bus: {2}", messageType, message.Value.Array.Length, message.GetString());
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_publisherSocket != null)
                {
                    _publisherSocket.Dispose();
                    _publisherSocket = null;
                }

                foreach (var subscriber in this._subscriberSockets.ToArray())
                {
                    subscriber.Dispose();
                    _subscriberSockets.Remove(subscriber);
                }

                // Setting running to false will stop the subscriber tasks
                _running = false;

                if (_context != null)
                {
                    _context.Dispose();
                    _context = null;
                }
            }
            base.Dispose(disposing);
        }
    }

    internal class NetMqMessage
    {
        private readonly long _messageId;
        private ScaleoutMessage _scaleoutMessage;

        public NetMqMessage(long messageId, IList<Message> messages )
        {
            this._messageId = messageId;
            _scaleoutMessage = new ScaleoutMessage(messages);
        }

        public NetMqMessage(long messageId, ScaleoutMessage message)
        {
            this._messageId = messageId;
            _scaleoutMessage = message;
        }

        public long MessageId
        {
            get { return _messageId; }
        }

        public ScaleoutMessage ScaleoutMessage
        {
            get { return _scaleoutMessage; }
        }

        public byte[] GetBytes()
        {
            var binaryId = BitConverter.GetBytes(MessageId);
            using (MemoryStream memoryStream = new MemoryStream())
            {
                byte[] messagesBytes = ScaleoutMessage.ToBytes();
                memoryStream.Write(binaryId, 0, binaryId.Length);
                memoryStream.Write(messagesBytes, 0, messagesBytes.Length);

                return memoryStream.ToArray();
            }
        }

        public static NetMqMessage FromBytes(byte[] bytes)
        {
            using (BinaryReader r = new BinaryReader(new MemoryStream(bytes)))
            {
                long messageId = r.ReadInt64();
                byte[] messageBytes = r.ReadBytes(bytes.Length - 8);
                var scaleoutMessage = ScaleoutMessage.FromBytes(messageBytes);

                return new NetMqMessage(messageId, scaleoutMessage);
            }
        }
    }
}