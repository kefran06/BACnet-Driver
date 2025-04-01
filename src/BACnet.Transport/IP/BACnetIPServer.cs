using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace BACnet.Transport.IP
{
    public class BACnetIPServer : IDisposable
    {
        private readonly int _port;
        private UdpClient _udpClient;
        private bool _isRunning;
        private Thread _listenerThread;
        private CancellationTokenSource _cancellationTokenSource;

        public event EventHandler<MessageReceivedEventArgs> MessageReceived;

        public BACnetIPServer(int port)
        {
            _port = port;
        }

        public void Start()
        {
            if (_isRunning)
                return;
                
            try
            {
                _isRunning = true;
                _cancellationTokenSource = new CancellationTokenSource();
                _udpClient = new UdpClient(_port);
                _listenerThread = new Thread(ListenForMessages);
                _listenerThread.IsBackground = true;
                _listenerThread.Start();
                
                Console.WriteLine($"BACnet IP Server started on port {_port}");
            }
            catch (Exception ex)
            {
                _isRunning = false;
                throw new InvalidOperationException($"Failed to start BACnet server: {ex.Message}", ex);
            }
        }

        public void Stop()
        {
            if (!_isRunning)
                return;
                
            try
            {
                _isRunning = false;
                _cancellationTokenSource?.Cancel();
                
                // Give the listener thread time to clean up
                if (_listenerThread != null && _listenerThread.IsAlive)
                {
                    if (!_listenerThread.Join(TimeSpan.FromSeconds(5)))
                    {
                        // If the thread doesn't exit cleanly, we'll just continue
                        Console.WriteLine("Warning: Listener thread did not exit cleanly.");
                    }
                }
                
                _udpClient?.Close();
                _udpClient = null;
                _listenerThread = null;
                
                Console.WriteLine("BACnet IP Server stopped");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping BACnet server: {ex.Message}");
            }
            finally
            {
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private void ListenForMessages()
        {
            IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            
            while (_isRunning)
            {
                try
                {
                    byte[] receivedData = _udpClient.Receive(ref remoteEndPoint);
                    
                    // Process the received message on a different thread to not block the listener
                    Task.Run(() => ProcessReceivedMessage(receivedData, remoteEndPoint));
                }
                catch (SocketException ex)
                {
                    if (_isRunning) // Only log if we're supposed to be running
                    {
                        Console.WriteLine($"Socket error while listening: {ex.Message}");
                        // Small delay to prevent tight error loop
                        Thread.Sleep(1000);
                    }
                }
                catch (ObjectDisposedException)
                {
                    // This can happen when the UDP client is closed while we're waiting for data
                    // It's expected during shutdown, so we'll just exit
                    break;
                }
                catch (Exception ex)
                {
                    if (_isRunning) // Only log if we're supposed to be running
                    {
                        Console.WriteLine($"Error in listener thread: {ex.Message}");
                        // Small delay to prevent tight error loop
                        Thread.Sleep(1000);
                    }
                }
            }
            
            Console.WriteLine("Listener thread exiting");
        }

        private void ProcessReceivedMessage(byte[] message, IPEndPoint remoteEndPoint)
        {
            try
            {
                Console.WriteLine($"Received message from {remoteEndPoint}, {message.Length} bytes");
                
                // Raise the MessageReceived event
                OnMessageReceived(new MessageReceivedEventArgs(message, remoteEndPoint));
                
                // Process the message (this would be implemented according to BACnet protocol)
                // For now, we'll just echo the message back
                SendResponse(message, remoteEndPoint);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing message: {ex.Message}");
            }
        }

        protected virtual void OnMessageReceived(MessageReceivedEventArgs e)
        {
            MessageReceived?.Invoke(this, e);
        }

        public void SendResponse(byte[] message, IPEndPoint remoteEndPoint)
        {
            if (!_isRunning || _udpClient == null)
            {
                throw new InvalidOperationException("Server is not running. Call Start() first.");
            }
            
            try
            {
                _udpClient.Send(message, message.Length, remoteEndPoint);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to send response: {ex.Message}", ex);
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}