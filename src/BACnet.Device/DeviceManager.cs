using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using BACnet.Core.Objects;
using BACnet.Core.Protocol;
using BACnet.Core.Services;

namespace BACnet.Device
{
    /// <summary>
    /// Manages BACnet devices for discovery, tracking and communication
    /// </summary>
    public class DeviceManager
    {
        private readonly Dictionary<uint, BACnetDevice> _devices = new Dictionary<uint, BACnetDevice>();
        private readonly object _deviceLock = new object();
        
        // The default BACnet port
        private const int DEFAULT_BACNET_PORT = 47808;
        
        // UDP client for network communication
        private UdpClient _udpClient;
        private CancellationTokenSource _discoveryTokenSource;
        
        /// <summary>
        /// Event raised when a new device is discovered
        /// </summary>
        public event EventHandler<BACnetDeviceEventArgs> DeviceDiscovered;
        
        /// <summary>
        /// Event raised when a device is added to the manager
        /// </summary>
        public event EventHandler<BACnetDeviceEventArgs> DeviceAdded;
        
        /// <summary>
        /// Event raised when a device is removed from the manager
        /// </summary>
        public event EventHandler<BACnetDeviceEventArgs> DeviceRemoved;

        /// <summary>
        /// Initializes a new instance of the DeviceManager class
        /// </summary>
        public DeviceManager()
        {
        }

        /// <summary>
        /// Adds a device to the manager
        /// </summary>
        /// <param name="device">The device to add</param>
        /// <returns>True if the device was added, false if a device with that ID already exists</returns>
        public bool AddDevice(BACnetDevice device)
        {
            if (device == null)
                throw new ArgumentNullException(nameof(device));

            bool added = false;
            
            lock (_deviceLock)
            {
                if (!_devices.ContainsKey(device.DeviceId))
                {
                    _devices[device.DeviceId] = device;
                    added = true;
                }
            }
            
            if (added)
            {
                OnDeviceAdded(new BACnetDeviceEventArgs(device));
            }
            
            return added;
        }

        /// <summary>
        /// Removes a device from the manager
        /// </summary>
        /// <param name="device">The device to remove</param>
        /// <returns>True if the device was removed, false if it was not found</returns>
        public bool RemoveDevice(BACnetDevice device)
        {
            if (device == null)
                throw new ArgumentNullException(nameof(device));

            return RemoveDevice(device.DeviceId);
        }
        
        /// <summary>
        /// Removes a device by ID from the manager
        /// </summary>
        /// <param name="deviceId">The ID of the device to remove</param>
        /// <returns>True if the device was removed, false if it was not found</returns>
        public bool RemoveDevice(uint deviceId)
        {
            BACnetDevice device = null;
            bool removed = false;
            
            lock (_deviceLock)
            {
                if (_devices.TryGetValue(deviceId, out device))
                {
                    _devices.Remove(deviceId);
                    removed = true;
                }
            }
            
            if (removed && device != null)
            {
                OnDeviceRemoved(new BACnetDeviceEventArgs(device));
            }
            
            return removed;
        }

        /// <summary>
        /// Gets a device by ID
        /// </summary>
        /// <param name="deviceId">The ID of the device</param>
        /// <returns>The device, or null if it was not found</returns>
        public BACnetDevice GetDevice(uint deviceId)
        {
            lock (_deviceLock)
            {
                return _devices.TryGetValue(deviceId, out var device) ? device : null;
            }
        }
        
        /// <summary>
        /// Gets a device by name
        /// </summary>
        /// <param name="deviceName">The name of the device</param>
        /// <returns>The device, or null if it was not found</returns>
        public BACnetDevice GetDeviceByName(string deviceName)
        {
            if (string.IsNullOrEmpty(deviceName))
                return null;
                
            lock (_deviceLock)
            {
                return _devices.Values.FirstOrDefault(d => d.DeviceName == deviceName);
            }
        }

        /// <summary>
        /// Gets all devices managed by this manager
        /// </summary>
        /// <returns>A list of all devices</returns>
        public List<BACnetDevice> GetAllDevices()
        {
            lock (_deviceLock)
            {
                return _devices.Values.ToList();
            }
        }
        
        /// <summary>
        /// Discovers devices on the network by broadcasting WhoIs and processing I-Am responses
        /// </summary>
        /// <param name="broadcastAddress">The broadcast address to use</param>
        /// <param name="port">The port to use (default BACnet port is 47808)</param>
        /// <param name="timeoutMs">How long to wait for responses in milliseconds</param>
        /// <returns>A list of discovered devices</returns>
        public async Task<List<BACnetDevice>> DiscoverDevicesAsync(
            IPAddress broadcastAddress, 
            int port = DEFAULT_BACNET_PORT, 
            int timeoutMs = 5000)
        {
            // Cancel any ongoing discovery
            StopDiscovery();
            
            _discoveryTokenSource = new CancellationTokenSource();
            var cancellationToken = _discoveryTokenSource.Token;
            
            try
            {
                // Create WhoIs service for device discovery
                var whoIs = new WhoIs();
                
                // Initialize UDP client for broadcast
                _udpClient = new UdpClient();
                _udpClient.EnableBroadcast = true;
                _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, port));
                
                Console.WriteLine($"Broadcasting WhoIs to {broadcastAddress}:{port}");
                
                // Start listening for responses
                var listenTask = StartListening(whoIs, cancellationToken);
                
                // Encode the WhoIs request
                var whoIsAPDU = whoIs.EncodeAPDU();
                var npdu = new NPDU
                {
                    Version = NPDU.BACNET_PROTOCOL_VERSION,
                    Control = 0,    // No special control flags for broadcast
                    ApplicationData = whoIsAPDU.Encode()
                };
                
                var bvlc = new BVLC(BVLC.BVLC_ORIGINAL_BROADCAST_NPDU, npdu.Encode());
                var data = bvlc.Encode();
                
                // Send the WhoIs broadcast
                await _udpClient.SendAsync(data, data.Length, new IPEndPoint(broadcastAddress, port));
                
                Console.WriteLine("WhoIs sent, waiting for responses...");
                
                // Wait for timeout before finishing discovery
                await Task.Delay(timeoutMs, cancellationToken);
                
                // Return the discovered devices
                List<Core.Objects.Device> discoveredCoreDevices = whoIs.GetDiscoveredDevices().ToList();
                List<BACnetDevice> discoveredDevices = new List<BACnetDevice>();
                
                foreach (var coreDevice in discoveredCoreDevices)
                {
                    var device = new BACnetDevice(
                        coreDevice.ObjectIdentifier,
                        coreDevice.DeviceName,
                        coreDevice.Location,
                        coreDevice.VendorName,
                        coreDevice.VendorId,
                        coreDevice.ModelNumber,
                        coreDevice.FirmwareRevision
                    );
                    
                    // Add the device to our management (only if not already present)
                    AddDevice(device);
                    
                    discoveredDevices.Add(device);
                }
                
                return discoveredDevices;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Device discovery was cancelled");
                return new List<BACnetDevice>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during device discovery: {ex.Message}");
                return new List<BACnetDevice>();
            }
            finally
            {
                StopDiscovery();
            }
        }
        
        /// <summary>
        /// Starts listening for I-Am responses from the network
        /// </summary>
        /// <param name="whoIs">The WhoIs service to process responses with</param>
        /// <param name="cancellationToken">A token to monitor for cancellation</param>
        /// <returns>A task representing the listening operation</returns>
        private async Task StartListening(WhoIs whoIs, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Wait for a response
                    var result = await _udpClient.ReceiveAsync().ConfigureAwait(false);
                    
                    // Process the response
                    await Task.Run(() => ProcessResponse(result, whoIs), cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during cancellation
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error listening for BACnet responses: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Processes a received UDP response that might be a BACnet I-Am
        /// </summary>
        /// <param name="result">The received UDP data</param>
        /// <param name="whoIs">The WhoIs service to process responses with</param>
        private void ProcessResponse(UdpReceiveResult result, WhoIs whoIs)
        {
            try
            {
                // Parse the BVLC
                var bvlc = new BVLC();
                bvlc.Decode(result.Buffer);
                
                // Verify it's a BACnet/IP message
                if (bvlc.Type != BVLC.BVLL_TYPE_BACNET_IP)
                    return;
                
                // Extract NPDU
                var npdu = new NPDU();
                int consumed = npdu.Decode(bvlc.Data, 0, bvlc.Data.Length);
                
                // Only process if there's application data
                if (npdu.ApplicationData == null || npdu.ApplicationData.Length == 0)
                    return;
                
                // Extract APDU
                var apdu = new APDU();
                apdu.Decode(npdu.ApplicationData);
                
                // Check if this is an I-Am response
                if (apdu.PDUType == APDU.UnconfirmedRequest && apdu.ServiceChoice == APDU.IAm)
                {
                    // Process I-Am response
                    var sourceEndPoint = result.RemoteEndPoint;
                    var device = whoIs.ProcessIAmResponse(
                        apdu, 
                        sourceEndPoint.Address.ToString(), 
                        sourceEndPoint.Port);
                    
                    if (device != null)
                    {
                        var bacnetDevice = new BACnetDevice(
                            device.ObjectIdentifier,
                            device.DeviceName,
                            device.Location,
                            device.VendorName,
                            device.VendorId,
                            device.ModelNumber,
                            device.FirmwareRevision
                        );
                        
                        // Add device network information
                        bacnetDevice.IPAddress = sourceEndPoint.Address;
                        bacnetDevice.Port = sourceEndPoint.Port;
                        
                        // Trigger device discovered event
                        OnDeviceDiscovered(new BACnetDeviceEventArgs(bacnetDevice));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing BACnet response: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Stops any ongoing device discovery
        /// </summary>
        public void StopDiscovery()
        {
            try
            {
                // Cancel any ongoing operations
                _discoveryTokenSource?.Cancel();
                _discoveryTokenSource?.Dispose();
                _discoveryTokenSource = null;
                
                // Clean up the UDP client
                _udpClient?.Close();
                _udpClient?.Dispose();
                _udpClient = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping discovery: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Invokes the DeviceDiscovered event
        /// </summary>
        /// <param name="e">The event arguments</param>
        protected virtual void OnDeviceDiscovered(BACnetDeviceEventArgs e)
        {
            DeviceDiscovered?.Invoke(this, e);
        }
        
        /// <summary>
        /// Invokes the DeviceAdded event
        /// </summary>
        /// <param name="e">The event arguments</param>
        protected virtual void OnDeviceAdded(BACnetDeviceEventArgs e)
        {
            DeviceAdded?.Invoke(this, e);
        }
        
        /// <summary>
        /// Invokes the DeviceRemoved event
        /// </summary>
        /// <param name="e">The event arguments</param>
        protected virtual void OnDeviceRemoved(BACnetDeviceEventArgs e)
        {
            DeviceRemoved?.Invoke(this, e);
        }
    }
    
    /// <summary>
    /// Event arguments for BACnet device events
    /// </summary>
    public class BACnetDeviceEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the BACnet device
        /// </summary>
        public BACnetDevice Device { get; }
        
        /// <summary>
        /// Initializes a new instance of the BACnetDeviceEventArgs class
        /// </summary>
        /// <param name="device">The BACnet device</param>
        public BACnetDeviceEventArgs(BACnetDevice device)
        {
            Device = device;
        }
    }
}