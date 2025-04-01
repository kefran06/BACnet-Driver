# BACnet Driver

## Overview
The BACnet Driver project is a comprehensive implementation of the BACnet protocol, designed to facilitate communication between building automation and control systems. This project includes libraries for core BACnet functionalities, transport mechanisms, device management, and client-server interactions.

## Project Structure
The project is organized into several key components:

- **BACnet.Core**: Contains the core BACnet protocol implementation, including:
  - Protocol: APDU, NPDU, and BVLC classes for BACnet protocol layers
  - Objects: BACnet object model including Device, AnalogInput, AnalogOutput
  - Services: ReadProperty, WriteProperty, WhoIs and other BACnet services

- **BACnet.Transport**: Implements transport layers for BACnet communication:
  - IP: BACnetIPClient and BACnetIPServer for BACnet/IP communication
  - MSTP: BACnet MS/TP (Master-Slave/Token-Passing) protocol support

- **BACnet.Device**: Manages BACnet device instances and objects:
  - BACnetDevice: Represents a physical or virtual BACnet device
  - DeviceManager: Manages device discovery and tracking

- **BACnet.Client**: Provides client-side functionalities for BACnet communication

## Features
- Full BACnet protocol stack implementation (BVLC, NPDU, APDU)
- Object-oriented BACnet object model with property management
- Support for BACnet services (ReadProperty, WriteProperty, WhoIs/IAm)
- BACnet/IP client and server communication
- Robust error handling and resource management
- Thread-safe device and object management
- Example applications for both client and server scenarios

## Getting Started

### Prerequisites
- .NET SDK (version 6.0 or higher)
- A compatible IDE (Visual Studio, VS Code, JetBrains Rider)

### Installation
1. Clone the repository:
   ```
   git clone <repository-url>
   ```
2. Navigate to the project directory:
   ```
   cd BACnet-Driver
   ```
3. Restore the project dependencies:
   ```
   dotnet restore
   ```

### Building the Project
To build the entire solution:
```
dotnet build
```

To build a specific project:
```
dotnet build src/BACnet.Core
```

### Running Examples
The project includes example applications for both a BACnet client and server:

#### Running the Client Example
```
cd examples/SimpleClient
dotnet run
```

#### Running the Server Example
```
cd examples/SimpleServer
dotnet run
```

## Usage Examples

### Creating a BACnet Client
```csharp
// Create a BACnet client
var client = new BACnetClient("192.168.1.100", 47808);

// Connect to the BACnet network
client.Connect();

// Listen for incoming messages
client.MessageReceived += (sender, e) => {
    Console.WriteLine($"Received message: {e.Message}");
};

// Discover devices on the network
client.DiscoverDevices();

// Read a property from a device
var analogInput = new AnalogInput(1);
client.SendReadPropertyRequest(analogInput, "PresentValue");
```

### Creating a BACnet Server
```csharp
// Create a BACnet device
var device = new BACnetDevice(
    389001,                 // Device ID
    "BACnet Device",        // Name
    "Building 1",           // Location
    "ACME BACnet",          // Vendor name
    42,                     // Vendor ID
    101,                    // Model number
    1                       // Firmware revision
);

// Add objects to the device
var tempSensor = new AnalogInput(1) {
    ObjectName = "Zone Temperature",
    PresentValue = 72.5f,
    Units = "degF"
};

device.AddObject(tempSensor);

// Create and start a BACnet IP server
var server = new BACnetIPServer(47808);
server.Start();

// Handle incoming messages
server.MessageReceived += (sender, e) => {
    // Process the message
    // ...
};
```

## Contributing
Contributions are welcome! Please follow these steps to contribute:
1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Make your changes
4. Commit your changes (`git commit -m 'Add some amazing feature'`)
5. Push to the branch (`git push origin feature/amazing-feature`)
6. Open a Pull Request

## License
This project is licensed under the MIT License. See the LICENSE file for details.

## Contact
For questions or support, please reach out to the project maintainers.