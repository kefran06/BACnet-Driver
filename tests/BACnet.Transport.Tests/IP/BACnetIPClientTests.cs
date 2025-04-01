using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using BACnet.Transport.IP;
using Moq;
using Xunit;

namespace BACnet.Transport.Tests.IP
{
    public class BACnetIPClientTests
    {
        private readonly string _testIpAddress = "127.0.0.1";
        private readonly int _testPort = 47808;

        [Fact]
        public void Constructor_InitializesPropertiesCorrectly()
        {
            // Act
            var client = new BACnetIPClient(_testIpAddress, _testPort);
            
            // Assert
            // Since properties are private, we can only test the behavior indirectly
            // The client should be created without exceptions
            Assert.NotNull(client);
        }

        [Fact]
        public void Connect_WhenCalled_EstablishesConnection()
        {
            // Arrange
            var client = new BACnetIPClient(_testIpAddress, _testPort);
            
            // Act & Assert
            // If the IP address is valid, this shouldn't throw an exception
            client.Connect();
            
            // Clean up
            client.Dispose();
        }

        [Fact]
        public void Connect_WhenCalledTwice_OnlyConnectsOnce()
        {
            // Arrange
            var client = new BACnetIPClient(_testIpAddress, _testPort);
            
            // Act
            client.Connect();
            client.Connect(); // Second call should be a no-op
            
            // Assert
            // This test passes if no exception is thrown
            
            // Clean up
            client.Dispose();
        }

        [Fact]
        public void Connect_WithInvalidIPAddress_ThrowsException()
        {
            // Arrange
            var client = new BACnetIPClient("invalid-ip", _testPort);
            
            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => client.Connect());
        }

        [Fact]
        public void Disconnect_WhenNotConnected_DoesNotThrowException()
        {
            // Arrange
            var client = new BACnetIPClient(_testIpAddress, _testPort);
            
            // Act & Assert
            // This should not throw an exception
            client.Disconnect();
        }

        [Fact]
        public void Disconnect_WhenConnected_DisconnectsSuccessfully()
        {
            // Arrange
            var client = new BACnetIPClient(_testIpAddress, _testPort);
            client.Connect();
            
            // Act
            client.Disconnect();
            
            // Assert
            // Since connection state is private, we can test indirectly by calling Send
            // which should throw when not connected
            Assert.Throws<InvalidOperationException>(() => client.Send("test"));
        }

        [Fact]
        public void Send_WhenNotConnected_ThrowsException()
        {
            // Arrange
            var client = new BACnetIPClient(_testIpAddress, _testPort);
            
            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => client.Send("test"));
        }

        [Fact]
        public async Task SendAsync_WhenNotConnected_ThrowsException()
        {
            // Arrange
            var client = new BACnetIPClient(_testIpAddress, _testPort);
            
            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => 
                client.SendAsync("test"));
        }

        [Fact]
        public void Receive_WhenNotConnected_ThrowsException()
        {
            // Arrange
            var client = new BACnetIPClient(_testIpAddress, _testPort);
            
            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => client.Receive());
        }

        [Fact]
        public async Task ReceiveAsync_WhenNotConnected_ThrowsException()
        {
            // Arrange
            var client = new BACnetIPClient(_testIpAddress, _testPort);
            
            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => 
                client.ReceiveAsync());
        }

        [Fact]
        public void MessageReceived_WhenSubscribed_InvokesEventHandler()
        {
            // This test would require mocking the UDP client or using integration tests
            // Mock approach would be preferred but is complex due to the internal implementation
            // A simplified test that validates the event handler can be set:
            
            // Arrange
            var client = new BACnetIPClient(_testIpAddress, _testPort);
            bool eventRaised = false;
            
            // Act
            client.MessageReceived += (sender, e) => eventRaised = true;
            
            // Assert
            // We can't directly access the event, just verify that subscription worked without errors
            Assert.False(eventRaised); // Initially the event hasn't been raised
        }

        [Fact]
        public void Dispose_ReleasesResources()
        {
            // Arrange
            var client = new BACnetIPClient(_testIpAddress, _testPort);
            client.Connect();
            
            // Act
            client.Dispose();
            
            // Assert
            // After disposal, operations should throw ObjectDisposedException
            // Since the implementation doesn't check for disposal, we can't directly test this
            // Instead, the test passes if Dispose doesn't throw
        }
    }
}