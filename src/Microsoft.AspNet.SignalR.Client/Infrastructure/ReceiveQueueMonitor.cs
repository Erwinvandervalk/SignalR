// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.md in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR.Infrastructure;

#if NETFX_CORE
using Windows.System.Threading;
#endif

namespace Microsoft.AspNet.SignalR.Client.Infrastructure
{
    internal sealed class ReceiveQueueMonitor : IDisposable
    {
#if !NETFX_CORE
        private Timer _timer;
#else
        private ThreadPoolTimer _timer;
#endif

        private IConnection _connection;
        private TaskQueue _queue;
        private Task _lastExecutingTask;
        private TimeSpan _deadlockErrorTimeout;

        public ReceiveQueueMonitor(IConnection connection, TaskQueue queue, TimeSpan deadlockErrorTimeout)
        {
            _connection = connection;
            _queue = queue;
            _lastExecutingTask = queue.ExecutingTask;
            _deadlockErrorTimeout = deadlockErrorTimeout;

#if !NETFX_CORE
            _timer = new Timer(_ => Beat(), state: null, dueTime: deadlockErrorTimeout, period: deadlockErrorTimeout);
#else
            _timer = ThreadPoolTimer.CreatePeriodicTimer(_ => Beat(), period: deadlockErrorTimeout);
#endif
        }

        // This is only able to detect deadlocks because Connection enqueues callbacks using
        // Task.Factory.StartNew. Otherwise _queue.ExecutingTask would stay null during the deadlock.
        private void Beat()
        {
            var currentExecutingTask = _queue.ExecutingTask;

            if (currentExecutingTask != null
                && currentExecutingTask == _lastExecutingTask
                && !currentExecutingTask.IsCompleted)
            {
                var errorMessage = String.Format(Resources.Error_PossibleDeadlockDetected,
                                                 _deadlockErrorTimeout.TotalSeconds);
                _connection.OnError(new TimeoutException(errorMessage));
            }

            _lastExecutingTask = currentExecutingTask;
        }

        /// <summary>
        /// Dispose off the timer
        /// </summary>
        public void Dispose()
        {
                if (_timer != null)
                {
#if !NETFX_CORE
                    _timer.Dispose();
                    _timer = null;
#else
                    _timer.Cancel();
                    _timer = null;
#endif
                }
        }
    }
}
