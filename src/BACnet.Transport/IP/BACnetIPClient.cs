using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using BACnet.Core.Protocol;

namespace BACnet.Transport.IP
{
    public class BACnetIPClient : IDisposable
    {
        private UdpClient? _udpClient;
        private IPEndPoint? _remoteEndPoint;
        private readonly string _ipAddress;
        private readonly int _port;
        private bool _isConnected = false;
        private bool _disposed = false;
        private CancellationTokenSource? _receiveCancellationTokenSource;
        private Task? _receiveTask;
        private readonly int _defaultTimeout = 5000; // 5 seconds default timeout
        
        /// <summary>
        /// Event raised when a message is received
        /// </summary>
        public event EventHandler<BACnetMessageReceivedEventArgs>? MessageReceived;

        /// <summary>
        /// Creates a new BACnet IP client
        /// </summary>
        /// <param name="ipAddress">IP address to communicate with</param>
        /// <param name="port">Port to communicate on (default BACnet port is 47808)</param>
        public BACnetIPClient(string ipAddress, int port)
        {
            _ipAddress = ipAddress;
            _port = port;
        }

        /// <summary>
        /// Raises the MessageReceived event
        /// </summary>
        /// <param name="e">Message event arguments</param>
        protected virtual void OnMessageReceived(BACnetMessageReceivedEventArgs e)
        {
            MessageReceived?.Invoke(this, e);
        }

        /// <summary>
        /// Establishes a connection to the remote endpoint
        /// </summary>
        public void Connect()
        {
            if (_isConnected)
                return;
            
            try
            {
                _udpClient = new UdpClient();
                _remoteEndPoint = new IPEndPoint(IPAddress.Parse(_ipAddress), _port);
                _isConnected = true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to connect to remote endpoint", ex);
            }
        }

        /// <summary>
        /// Establishes a connection and begins listening for incoming messages
        /// </summary>
        public void ConnectAndListen()
        {
            Connect();
            StartListening();
        }

        /// <summary>
        /// Starts listening for incoming messages asynchronously
        /// </summary>
        private void StartListening()
        {
            if (!_isConnected)
                throw new InvalidOperationException("Client is not connected. Call Connect() first.");
            
            _receiveCancellationTokenSource = new CancellationTokenSource();
            _receiveTask = Task.Run(async () =>
            {
                try
                {
                    while (!_receiveCancellationTokenSource.Token.IsCancellationRequested)
                    {
                        var result = await ReceiveAsync(_receiveCancellationTokenSource.Token);
                        OnMessageReceived(new BACnetMessageReceivedEventArgs(result));
                    }
                }
                catch (OperationCanceledException)
                {
                    // Normal cancellation, do nothing
                }
                catch (Exception)
                {
                    // Log exception or handle it
                    // Exception is intentionally unused here
                }
            });
        }

        /// <summary>
        /// Stops listening for incoming messages
        /// </summary>
        private void StopListening()
        {
            _receiveCancellationTokenSource?.Cancel();
            _receiveTask?.Wait(1000); // Wait up to 1 second for task to complete
            _receiveCancellationTokenSource?.Dispose();
            _receiveCancellationTokenSource = null;
            _receiveTask = null;
        }

        /// <summary>
        /// Disconnects from the remote endpoint
        /// </summary>
        public void Disconnect()
        {
            if (!_isConnected)
                return;
                
            try
            {
                StopListening();
                _udpClient?.Close();
                _isConnected = false;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to disconnect properly", ex);
            }
            finally
            {
                _udpClient = null;
            }
        }

        /// <summary>
        /// Sends a BACnet message to the remote endpoint
        /// </summary>
        /// <param name="message">Message to send</param>
        public void Send(object message)
        {
            if (!_isConnected)
                throw new InvalidOperationException("Client is not connected. Call Connect() first.");
                
            try
            {
                byte[] bytes = SerializeMessage(message);
                if (_udpClient != null && _remoteEndPoint != null)
                {
                    _udpClient.Send(bytes, bytes.Length, _remoteEndPoint);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to send message", ex);
            }
        }

        /// <summary>
        /// Sends a BACnet message to the remote endpoint asynchronously
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
        /// <returns>Task representing the asynchronous operation</returns>
        public async Task SendAsync(object message, CancellationToken cancellationToken = default)
        {
            if (!_isConnected)
                throw new InvalidOperationException("Client is not connected. Call Connect() first.");
                
            try
            {
                byte[] bytes = SerializeMessage(message);
                if (_udpClient != null && _remoteEndPoint != null)
                {
                    await _udpClient.SendAsync(bytes, bytes.Length, _remoteEndPoint)
                        .ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to send message", ex);
            }
        }

        /// <summary>
        /// Receives a BACnet message from the remote endpoint
        /// </summary>
        /// <returns>The received message</returns>
        public object Receive()
        {
            if (!_isConnected || _udpClient == null || _remoteEndPoint == null)
                throw new InvalidOperationException("Client is not connected. Call Connect() first.");
                
            try
            {
                var tempEndPoint = _remoteEndPoint;
                var receivedResults = _udpClient.Receive(ref tempEndPoint);
                _remoteEndPoint = tempEndPoint;
                return DeserializeMessage(receivedResults) ?? Array.Empty<byte>();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to receive message", ex);
            }
        }

        /// <summary>
        /// Receives a BACnet message from the remote endpoint asynchronously
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
        /// <returns>Task representing the asynchronous operation with the received message</returns>
        public async Task<object> ReceiveAsync(CancellationToken cancellationToken = default)
        {
            if (!_isConnected || _udpClient == null)
                throw new InvalidOperationException("Client is not connected. Call Connect() first.");
                
            try
            {
                var result = await _udpClient.ReceiveAsync()
                    .WithCancellation(cancellationToken)
                    .ConfigureAwait(false);

                _remoteEndPoint = result.RemoteEndPoint;
                return DeserializeMessage(result.Buffer) ?? Array.Empty<byte>();
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException)
                    throw;
                    
                throw new InvalidOperationException("Failed to receive message", ex);
            }
        }

        /// <summary>
        /// Sends a message and waits for a response
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="timeout">Timeout in milliseconds</param>
        /// <returns>Response message</returns>
        public object SendAndReceive(object message, int timeout = 0)
        {
            Send(message);
            
            var receiveTimeout = timeout > 0 ? timeout : _defaultTimeout;
            
            using (var cts = new CancellationTokenSource(receiveTimeout))
            {
                try
                {
                    return ReceiveAsync(cts.Token).GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    throw new TimeoutException($"No response received within {receiveTimeout}ms");
                }
            }
        }

        /// <summary>
        /// Sends a message and waits for a response asynchronously
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="timeout">Timeout in milliseconds</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation</param>
        /// <returns>Task representing the asynchronous operation with the response message</returns>
        public async Task<object> SendAndReceiveAsync(object message, int timeout = 0, CancellationToken cancellationToken = default)
        {
            await SendAsync(message, cancellationToken).ConfigureAwait(false);
            
            var receiveTimeout = timeout > 0 ? timeout : _defaultTimeout;
            
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                cts.CancelAfter(receiveTimeout);
                try
                {
                    return await ReceiveAsync(cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException($"No response received within {receiveTimeout}ms");
                }
            }
        }

        /// <summary>
        /// Serializes a message to bytes for transmission
        /// </summary>
        /// <param name="message">Message to serialize</param>
        /// <returns>Serialized bytes</returns>
        private byte[] SerializeMessage(object message)
        {
            // Handle different message types for BACnet protocol
            if (message is byte[] bytes)
            {
                return bytes; // Already serialized
            }
            else if (message is BVLC bvlcMessage)
            {
                return bvlcMessage.Encode(); // Use BVLC's built-in encoding
            }
            else if (message is NPDU npduMessage)
            {
                // Encapsulate NPDU in BVLC for proper BACnet/IP transport
                var bvlcWrapper = new BVLC(BVLC.BVLC_ORIGINAL_UNICAST_NPDU, npduMessage.Encode());
                return bvlcWrapper.Encode();
            }
            else if (message is APDU apduMessage)
            {
                // For APDU, encapsulate in NPDU and then BVLC
                var npduWrapper = new NPDU
                {
                    Version = NPDU.BACNET_PROTOCOL_VERSION,
                    Control = 0, // Normal message, no network layer message
                    ApplicationData = apduMessage.Encode()
                };
                
                var bvlcWrapper = new BVLC(BVLC.BVLC_ORIGINAL_UNICAST_NPDU, npduWrapper.Encode());
                return bvlcWrapper.Encode();
            }
            else if (message is string messageString)
            {
                // For debugging or simple text commands, wrap in a BACnet unconfirmed text message
                var apduText = new APDU
                {
                    PDUType = APDU.UnconfirmedRequest,
                    ServiceChoice = 5, // Unconfirmed Text Message 
                    Parameters = System.Text.Encoding.ASCII.GetBytes(messageString)
                };
                
                var npduText = new NPDU
                {
                    Version = NPDU.BACNET_PROTOCOL_VERSION,
                    Control = 0,
                    ApplicationData = apduText.Encode()
                };
                
                var bvlcText = new BVLC(BVLC.BVLC_ORIGINAL_UNICAST_NPDU, npduText.Encode());
                return bvlcText.Encode();
            }
            else if (message is Dictionary<string, object> serviceDictionary)
            {
                // Handle higher-level service requests (like ReadProperty, WriteProperty)
                if (serviceDictionary.TryGetValue("service", out var serviceObj) && 
                    serviceObj is string service)
                {
                    byte serviceChoice;
                    byte pduType = APDU.ConfirmedRequest; // Default for most services
                    
                    // Map service name to BACnet service choice
                    switch (service.ToLower())
                    {
                        case "readproperty":
                            serviceChoice = APDU.ReadProperty;
                            break;
                        case "writeproperty":
                            serviceChoice = APDU.WriteProperty;
                            break;
                        case "whois":
                            serviceChoice = APDU.WhoIs;
                            pduType = APDU.UnconfirmedRequest;
                            break;
                        case "iam":
                            serviceChoice = APDU.IAm;
                            pduType = APDU.UnconfirmedRequest;
                            break;
                        // Additional services could be added here
                        default:
                            throw new NotSupportedException($"Unsupported BACnet service: {service}");
                    }
                    
                    // Create parameter data according to service type
                    byte[]? parameters = Array.Empty<byte>();
                    if (serviceDictionary.TryGetValue("parameters", out var paramsObj))
                    {
                        if (paramsObj is byte[] paramBytes)
                        {
                            parameters = paramBytes;
                        }
                        else
                        {
                            // Specific parameter encoding based on service
                            parameters = EncodeServiceParameters(service, paramsObj);
                        }
                    }
                    
                    // Construct the APDU
                    var apdu = new APDU
                    {
                        PDUType = pduType,
                        ServiceChoice = serviceChoice,
                        Parameters = parameters
                    };
                    
                    // Add invoke ID for confirmed requests
                    if (pduType == APDU.ConfirmedRequest && 
                        serviceDictionary.TryGetValue("invokeId", out var invokeIdObj) &&
                        invokeIdObj is byte invokeId)
                    {
                        apdu.InvokeID = invokeId;
                    }
                    
                    // Encapsulate in NPDU and BVLC
                    var npdu = new NPDU
                    {
                        Version = NPDU.BACNET_PROTOCOL_VERSION,
                        Control = 0,
                        ApplicationData = apdu.Encode()
                    };
                    
                    // If destination network is specified, add NPDU routing information
                    if (serviceDictionary.TryGetValue("destNet", out var destNetObj) &&
                        destNetObj is ushort destNet)
                    {
                        // Note: If the NPDU class doesn't have these properties,
                        // they should be handled differently or ignored
                        try
                        {
                            // Using reflection to set properties if they exist
                            var npduType = npdu.GetType();
                            var destNetProperty = npduType.GetProperty("DestinationNetworkAddress");
                            var destAddrProperty = npduType.GetProperty("DestinationMACAddress");
                            var destSpecifiedField = npduType.GetField("DESTINATION_SPECIFIED");

                            if (destNetProperty != null)
                                destNetProperty.SetValue(npdu, destNet);

                            if (destSpecifiedField != null)
                            {
                                var destSpecifiedValue = destSpecifiedField.GetValue(null);
                                if (destSpecifiedValue != null)
                                    npdu.Control |= Convert.ToByte(destSpecifiedValue);
                            }
                            
                            if (serviceDictionary.TryGetValue("destAddr", out var destAddrObj) &&
                                destAddrObj is byte[] destAddr &&
                                destAddrProperty != null)
                            {
                                destAddrProperty.SetValue(npdu, destAddr);
                            }
                        }
                        catch
                        {
                            // If properties don't exist or can't be set, we'll just ignore them
                            // This allows for flexibility with different versions of the NPDU class
                        }
                    }
                    
                    var bvlc = new BVLC(BVLC.BVLC_ORIGINAL_UNICAST_NPDU, npdu.Encode());
                    return bvlc.Encode();
                }
                else
                {
                    throw new ArgumentException("Service dictionary must contain a 'service' key with string value");
                }
            }
            
            throw new ArgumentException($"Unsupported message type: {message.GetType().Name}");
        }
        
        private byte[] EncodeServiceParameters(string service, object parameters)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                switch (service.ToLower())
                {
                    case "readproperty":
                        if (parameters is Dictionary<string, object> readParams)
                        {
                            // Encode object identifier
                            if (readParams.TryGetValue("objectType", out var objTypeObj) &&
                                readParams.TryGetValue("objectInstance", out var objInstObj))
                            {
                                ushort objectType = Convert.ToUInt16(objTypeObj);
                                uint objectInstance = Convert.ToUInt32(objInstObj);
                                // Fix casting issue by using proper unsigned type conversion
                                uint shiftedType = ((uint)objectType) << 22;
                                uint maskedInstance = objectInstance & 0x3FFFFF;
                                uint objectId = shiftedType | maskedInstance;
                                
                                // Write context tag 0 (object identifier)
                                writer.Write((byte)0x0C); // Context tag 0, length 4 bytes
                                writer.Write((byte)0x04); // Length 4 bytes
                                writer.Write((byte)((objectId >> 24) & 0xFF));
                                writer.Write((byte)((objectId >> 16) & 0xFF));
                                writer.Write((byte)((objectId >> 8) & 0xFF));
                                writer.Write((byte)(objectId & 0xFF));
                                
                                // Encode property identifier
                                if (readParams.TryGetValue("propertyId", out var propIdObj))
                                {
                                    uint propertyId = Convert.ToUInt32(propIdObj);
                                    
                                    // Write context tag 1 (property identifier)
                                    if (propertyId <= 255)
                                    {
                                        writer.Write((byte)0x19); // Context tag 1, length 1
                                        writer.Write((byte)propertyId);
                                    }
                                    else
                                    {
                                        writer.Write((byte)0x1A); // Context tag 1, length 2
                                        writer.Write((byte)((propertyId >> 8) & 0xFF));
                                        writer.Write((byte)(propertyId & 0xFF));
                                    }
                                    
                                    // Optional array index
                                    if (readParams.TryGetValue("arrayIndex", out var arrayIdxObj))
                                    {
                                        uint arrayIndex = Convert.ToUInt32(arrayIdxObj);
                                        
                                        // Write context tag 2 (array index)
                                        if (arrayIndex <= 255)
                                        {
                                            writer.Write((byte)0x29); // Context tag 2, length 1
                                            writer.Write((byte)arrayIndex);
                                        }
                                        else
                                        {
                                            writer.Write((byte)0x2A); // Context tag 2, length 2
                                            writer.Write((byte)((arrayIndex >> 8) & 0xFF));
                                            writer.Write((byte)(arrayIndex & 0xFF));
                                        }
                                    }
                                }
                            }
                        }
                        break;
                        
                    case "writeproperty":
                        if (parameters is Dictionary<string, object> writeParams)
                        {
                            // Encode object identifier (same as ReadProperty)
                            if (writeParams.TryGetValue("objectType", out var objTypeObj) &&
                                writeParams.TryGetValue("objectInstance", out var objInstObj))
                            {
                                ushort objectType = Convert.ToUInt16(objTypeObj);
                                uint objectInstance = Convert.ToUInt32(objInstObj);
                                // Fix casting issue by using proper unsigned type conversion
                                uint shiftedType = ((uint)objectType) << 22;
                                uint maskedInstance = objectInstance & 0x3FFFFF;
                                uint objectId = shiftedType | maskedInstance;
                                
                                // Write context tag 0 (object identifier)
                                writer.Write((byte)0x0C); // Context tag 0, length 4 bytes
                                writer.Write((byte)0x04); // Length 4 bytes
                                writer.Write((byte)((objectId >> 24) & 0xFF));
                                writer.Write((byte)((objectId >> 16) & 0xFF));
                                writer.Write((byte)((objectId >> 8) & 0xFF));
                                writer.Write((byte)(objectId & 0xFF));
                                
                                // Encode property identifier
                                if (writeParams.TryGetValue("propertyId", out var propIdObj))
                                {
                                    uint propertyId = Convert.ToUInt32(propIdObj);
                                    
                                    // Write context tag 1 (property identifier)
                                    if (propertyId <= 255)
                                    {
                                        writer.Write((byte)0x19); // Context tag 1, length 1
                                        writer.Write((byte)propertyId);
                                    }
                                    else
                                    {
                                        writer.Write((byte)0x1A); // Context tag 1, length 2
                                        writer.Write((byte)((propertyId >> 8) & 0xFF));
                                        writer.Write((byte)(propertyId & 0xFF));
                                    }
                                    
                                    // Optional array index
                                    if (writeParams.TryGetValue("arrayIndex", out var arrayIdxObj))
                                    {
                                        uint arrayIndex = Convert.ToUInt32(arrayIdxObj);
                                        
                                        // Write context tag 2 (array index)
                                        if (arrayIndex <= 255)
                                        {
                                            writer.Write((byte)0x29); // Context tag 2, length 1
                                            writer.Write((byte)arrayIndex);
                                        }
                                        else
                                        {
                                            writer.Write((byte)0x2A); // Context tag 2, length 2
                                            writer.Write((byte)((arrayIndex >> 8) & 0xFF));
                                            writer.Write((byte)(arrayIndex & 0xFF));
                                        }
                                    }
                                    
                                    // Property value - opening tag 3
                                    writer.Write((byte)0x3E); // Context tag 3, opening tag
                                    
                                    // Write actual value with appropriate application tag
                                    if (writeParams.TryGetValue("value", out var valueObj))
                                    {
                                        EncodeApplicationTaggedValue(writer, valueObj);
                                    }
                                    
                                    // Property value - closing tag 3
                                    writer.Write((byte)0x3F); // Context tag 3, closing tag
                                    
                                    // Optional priority
                                    if (writeParams.TryGetValue("priority", out var priorityObj))
                                    {
                                        uint priority = Convert.ToUInt32(priorityObj);
                                        
                                        // Write context tag 4 (priority)
                                        writer.Write((byte)0x49); // Context tag 4, length 1
                                        writer.Write((byte)priority);
                                    }
                                }
                            }
                        }
                        break;
                        
                    case "whois":
                        if (parameters is Dictionary<string, object> whoisParams)
                        {
                            // Optional device instance range
                            if (whoisParams.TryGetValue("lowLimit", out var lowLimitObj) &&
                                whoisParams.TryGetValue("highLimit", out var highLimitObj))
                            {
                                uint lowLimit = Convert.ToUInt32(lowLimitObj);
                                uint highLimit = Convert.ToUInt32(highLimitObj);
                                
                                // Write context tag 0 (low limit)
                                if (lowLimit <= 255)
                                {
                                    writer.Write((byte)0x09); // Context tag 0, length 1
                                    writer.Write((byte)lowLimit);
                                }
                                else if (lowLimit <= 65535)
                                {
                                    writer.Write((byte)0x0A); // Context tag 0, length 2
                                    writer.Write((byte)((lowLimit >> 8) & 0xFF));
                                    writer.Write((byte)(lowLimit & 0xFF));
                                }
                                else
                                {
                                    writer.Write((byte)0x0C); // Context tag 0, length 4
                                    writer.Write((byte)((lowLimit >> 24) & 0xFF));
                                    writer.Write((byte)((lowLimit >> 16) & 0xFF));
                                    writer.Write((byte)((lowLimit >> 8) & 0xFF));
                                    writer.Write((byte)(lowLimit & 0xFF));
                                }
                                
                                // Write context tag 1 (high limit)
                                if (highLimit <= 255)
                                {
                                    writer.Write((byte)0x19); // Context tag 1, length 1
                                    writer.Write((byte)highLimit);
                                }
                                else if (highLimit <= 65535)
                                {
                                    writer.Write((byte)0x1A); // Context tag 1, length 2
                                    writer.Write((byte)((highLimit >> 8) & 0xFF));
                                    writer.Write((byte)(highLimit & 0xFF));
                                }
                                else
                                {
                                    writer.Write((byte)0x1C); // Context tag 1, length 4
                                    writer.Write((byte)((highLimit >> 24) & 0xFF));
                                    writer.Write((byte)((highLimit >> 16) & 0xFF));
                                    writer.Write((byte)((highLimit >> 8) & 0xFF));
                                    writer.Write((byte)(highLimit & 0xFF));
                                }
                            }
                        }
                        break;
                        
                    // Additional services could be added here
                    
                    default:
                        throw new NotSupportedException($"Parameter encoding for service {service} not implemented");
                }
                
                return stream.ToArray();
            }
        }
        
        private void EncodeApplicationTaggedValue(BinaryWriter writer, object value)
        {
            if (value is bool boolValue)
            {
                writer.Write((byte)(boolValue ? 0x11 : 0x10)); // Boolean app tag 1, true/false
            }
            else if (value is byte byteValue)
            {
                writer.Write((byte)0x21); // Unsigned app tag 2, length 1
                writer.Write(byteValue);
            }
            else if (value is ushort ushortValue)
            {
                writer.Write((byte)0x22); // Unsigned app tag 2, length 2
                writer.Write((byte)((ushortValue >> 8) & 0xFF));
                writer.Write((byte)(ushortValue & 0xFF));
            }
            else if (value is uint uintValue)
            {
                if (uintValue <= 0xFF)
                {
                    writer.Write((byte)0x21); // Unsigned app tag 2, length 1
                    writer.Write((byte)uintValue);
                }
                else if (uintValue <= 0xFFFF)
                {
                    writer.Write((byte)0x22); // Unsigned app tag 2, length 2
                    writer.Write((byte)((uintValue >> 8) & 0xFF));
                    writer.Write((byte)(uintValue & 0xFF));
                }
                else
                {
                    writer.Write((byte)0x24); // Unsigned app tag 2, length 4
                    writer.Write((byte)((uintValue >> 24) & 0xFF));
                    writer.Write((byte)((uintValue >> 16) & 0xFF));
                    writer.Write((byte)((uintValue >> 8) & 0xFF));
                    writer.Write((byte)(uintValue & 0xFF));
                }
            }
            else if (value is int intValue)
            {
                writer.Write((byte)0x34); // Signed app tag 3, length 4
                writer.Write((byte)((intValue >> 24) & 0xFF));
                writer.Write((byte)((intValue >> 16) & 0xFF));
                writer.Write((byte)((intValue >> 8) & 0xFF));
                writer.Write((byte)(intValue & 0xFF));
            }
            else if (value is float floatValue)
            {
                writer.Write((byte)0x44); // Real app tag 4, length 4
                byte[] bytes = BitConverter.GetBytes(floatValue);
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(bytes);
                }
                writer.Write(bytes);
            }
            else if (value is double doubleValue)
            {
                writer.Write((byte)0x55); // Double app tag 5, length 8
                byte[] bytes = BitConverter.GetBytes(doubleValue);
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(bytes);
                }
                writer.Write(bytes);
            }
            else if (value is string stringValue)
            {
                byte[] stringBytes = System.Text.Encoding.ASCII.GetBytes(stringValue);
                if (stringBytes.Length <= 253)
                {
                    writer.Write((byte)(0x70 | (stringBytes.Length + 1))); // CharString app tag 7, inline length
                    writer.Write((byte)0); // String encoding (0 = ANSI X3.4)
                    writer.Write(stringBytes);
                }
                else
                {
                    // Extended encoding for longer strings
                    writer.Write((byte)0x7F); // CharString app tag 7, extended length
                    if (stringBytes.Length + 1 <= 0xFFFF) // +1 for encoding byte
                    {
                        writer.Write((byte)0x02); // 2-byte length
                        writer.Write((byte)(((stringBytes.Length + 1) >> 8) & 0xFF));
                        writer.Write((byte)((stringBytes.Length + 1) & 0xFF));
                    }
                    else
                    {
                        writer.Write((byte)0x04); // 4-byte length
                        writer.Write((byte)(((stringBytes.Length + 1) >> 24) & 0xFF));
                        writer.Write((byte)(((stringBytes.Length + 1) >> 16) & 0xFF));
                        writer.Write((byte)(((stringBytes.Length + 1) >> 8) & 0xFF));
                        writer.Write((byte)((stringBytes.Length + 1) & 0xFF));
                    }
                    writer.Write((byte)0); // String encoding (0 = ANSI X3.4)
                    writer.Write(stringBytes);
                }
            }
            else if (value is byte[] byteArray)
            {
                if (byteArray.Length <= 253)
                {
                    writer.Write((byte)(0x50 | byteArray.Length)); // OctetString app tag 5, inline length
                    writer.Write(byteArray);
                }
                else
                {
                    // Extended encoding for longer byte arrays
                    writer.Write((byte)0x5F); // OctetString app tag 5, extended length
                    if (byteArray.Length <= 0xFFFF)
                    {
                        writer.Write((byte)0x02); // 2-byte length
                        writer.Write((byte)((byteArray.Length >> 8) & 0xFF));
                        writer.Write((byte)(byteArray.Length & 0xFF));
                    }
                    else
                    {
                        writer.Write((byte)0x04); // 4-byte length
                        writer.Write((byte)((byteArray.Length >> 24) & 0xFF));
                        writer.Write((byte)((byteArray.Length >> 16) & 0xFF));
                        writer.Write((byte)((byteArray.Length >> 8) & 0xFF));
                        writer.Write((byte)(byteArray.Length & 0xFF));
                    }
                    writer.Write(byteArray);
                }
            }
            else if (value is DateTime dateTimeValue)
            {
                writer.Write((byte)0xA4); // DateTime app tag 10, length 4 (actually should be 8)
                // DateTime encoding: year, month, day, hour, minute, second, hundredths, weekday
                writer.Write((byte)((dateTimeValue.Year - 1900) & 0xFF));
                writer.Write((byte)dateTimeValue.Month);
                writer.Write((byte)dateTimeValue.Day);
                writer.Write((byte)dateTimeValue.Hour);
                writer.Write((byte)dateTimeValue.Minute);
                writer.Write((byte)dateTimeValue.Second);
                writer.Write((byte)(dateTimeValue.Millisecond / 10));
                writer.Write((byte)(dateTimeValue.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)dateTimeValue.DayOfWeek));
            }
            else if (value is Enum enumValue)
            {
                int enumInt = Convert.ToInt32(enumValue);
                if (enumInt <= 255)
                {
                    writer.Write((byte)0x91); // Enumerated app tag 9, length 1
                    writer.Write((byte)enumInt);
                }
                else
                {
                    writer.Write((byte)0x92); // Enumerated app tag 9, length 2
                    writer.Write((byte)((enumInt >> 8) & 0xFF));
                    writer.Write((byte)(enumInt & 0xFF));
                }
            }
            else
            {
                throw new ArgumentException($"Cannot encode value of type {value.GetType().Name}");
            }
        }

        /// <summary>
        /// Deserializes a received byte array into a BACnet message
        /// </summary>
        /// <param name="receivedData">The raw data received from the network</param>
        /// <returns>The deserialized message, typically as a byte array for further processing</returns>
        private object? DeserializeMessage(byte[]? receivedData)
        {
            if (receivedData == null || receivedData.Length == 0)
            {
                return Array.Empty<byte>();
            }

            // For BACnet, we typically just return the raw bytes for further protocol-specific processing
            // This allows higher layers to decode according to their needs
            return receivedData;
        }

        /// <summary>
        /// Disposes the resources used by the BACnetIPClient
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the resources used by the BACnetIPClient
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                Disconnect();
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Extension methods for Task operations
    /// </summary>
    internal static class TaskExtensions
    {
        /// <summary>
        /// Adds cancellation support to a task
        /// </summary>
        public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            using (cancellationToken.Register(s => ((TaskCompletionSource<bool>?)s)?.TrySetResult(true), tcs))
            {
                if (task != await Task.WhenAny(task, tcs.Task).ConfigureAwait(false))
                    throw new OperationCanceledException(cancellationToken);
            }
            return await task.ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Event arguments for message received events
    /// </summary>
    public class BACnetMessageReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// The received message
        /// </summary>
        public object Message { get; }

        /// <summary>
        /// Creates a new instance of MessageReceivedEventArgs
        /// </summary>
        /// <param name="message">The received message</param>
        public BACnetMessageReceivedEventArgs(object message)
        {
            Message = message ?? Array.Empty<byte>();
        }
    }
}