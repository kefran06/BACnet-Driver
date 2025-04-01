using BACnet.Core.Objects;
using BACnet.Core.Services;
using BACnet.Transport.IP;

namespace BACnet.Client
{
    public class BACnetClient : IDisposable
    {
        private readonly BACnetIPClient _ipClient;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _messageHandlingTask;
        private bool _isRunning = false;

        public BACnetClient(string ipAddress, int port, CancellationTokenSource cancellationTokenSource, Task messageHandlingTask)
        {
            _cancellationTokenSource = cancellationTokenSource;
            _messageHandlingTask = messageHandlingTask;
            _ipClient = new BACnetIPClient(ipAddress, port);
        }

        public void Connect()
        {
            _ipClient.Connect();
            StartMessageHandling();
        }

        public void Disconnect()
        {
            StopMessageHandling();
            _ipClient.Disconnect();
        }

        public void SendReadPropertyRequest(BACnetObject bacnetObject, string propertyIdentifier)
        {
            var readProperty = new ReadProperty(bacnetObject, propertyIdentifier);
            _ipClient.Send(readProperty);
        }

        public void SendWritePropertyRequest(BACnetObject bacnetObject, string propertyIdentifier, object value)
        {
            var writeProperty = new WriteProperty(bacnetObject, propertyIdentifier, value);
            _ipClient.Send(writeProperty);
        }

        public void DiscoverDevices()
        {
            var whoIs = new WhoIs();
            _ipClient.Send(whoIs);
        }

        public void SendReadPropertyRequest(ushort objectType, uint instanceNumber, uint propertyId)
        {
            // Create a ReadProperty request using numeric IDs
            // This overload is useful when we only have the numeric identifiers
            
            // Create parameters dictionary for the BACnet service
            var parameters = new Dictionary<string, object>
            {
                { "service", "readproperty" },
                { "parameters", new Dictionary<string, object>
                  {
                      { "objectType", objectType },
                      { "objectInstance", instanceNumber },
                      { "propertyId", propertyId }
                  }
                },
                { "invokeId", (byte)1 } // Use a fixed invoke ID for simplicity
            };
            
            _ipClient.Send(parameters);
        }

        public event EventHandler<MessageReceivedEventArgs>? MessageReceived;

        protected virtual void OnMessageReceived(MessageReceivedEventArgs e)
        {
            MessageReceived?.Invoke(this, e);
        }

        private void StartMessageHandling()
        {
            if (_isRunning)
                return;
                
            _isRunning = true;
            _cancellationTokenSource = new CancellationTokenSource();
            _messageHandlingTask = Task.Run(() => HandleIncomingMessages(_cancellationTokenSource.Token));
        }

        private void StopMessageHandling()
        {
            if (!_isRunning)
                return;
                
            _isRunning = false;
            _cancellationTokenSource?.Cancel();
            try
            {
                _messageHandlingTask?.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException ex) when (ex.InnerException is TaskCanceledException)
            {
                // Expected exception when task is cancelled
            }
            
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _messageHandlingTask = null;
        }

        private void HandleIncomingMessages(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var message = _ipClient.Receive();
                    OnMessageReceived(new MessageReceivedEventArgs(message));
                }
                catch (Exception ex)
                {
                    // Log the exception or notify subscribers
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        Console.WriteLine($"Error handling incoming message: {ex.Message}");
                        // Small delay to avoid tight loop in case of persistent errors
                        Task.Delay(1000, cancellationToken).Wait();
                    }
                }
            }
        }

        public void Dispose()
        {
            StopMessageHandling();
            
            if (_ipClient is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    public class MessageReceivedEventArgs : EventArgs
    {
        public object Message { get; }

        public MessageReceivedEventArgs(object message)
        {
            Message = message;
        }
    }
}