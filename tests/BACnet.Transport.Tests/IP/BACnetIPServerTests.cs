using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using BACnet.Transport.IP;
using Xunit;

namespace BACnet.Transport.Tests.IP
{
    public class BACnetIPServerTests
    {
        private readonly int _testPort = 47809;  // Using a different port than client tests

        [Fact]
        public void Constructor_InitializesCorrectly()
        {
            // Act
            var server = new BACnetIPServer(_testPort);
            
            // Assert
            Assert.NotNull(server);
        }

        [Fact]
        public void Start_StartsServerSuccessfully()
        {
            // Arrange
            var server = new BACnetIPServer(_testPort);
            
            try
            {
                // Act
                server.Start();
                
                // Assert
                // If the port is available, this shouldn't throw an exception
            }
            finally
            {
                // Clean up
                server.Stop();
            }
        }

        [Fact]
        public void Start_WhenCalledTwice_OnlyStartsOnce()
        {
            // Arrange
            var server = new BACnetIPServer(_testPort);
            
            try
            {
                // Act
                server.Start();
                server.Start(); // Second call should be a no-op
                
                // Assert
                // This test passes if no exception is thrown
            }
            finally
            {
                // Clean up
                server.Stop();
            }
        }

        [Fact]
        public void Start_WithPortInUse_ThrowsException()
        {
            // Arrange
            // First create a UDP client that binds to the test port
            using (var udpClient = new UdpClient(_testPort))
            {
                var server = new BACnetIPServer(_testPort);
                
                // Act & Assert
                Assert.Throws<InvalidOperationException>(() => server.Start());
            }
        }

        [Fact]
        public void Stop_WhenNotStarted_DoesNotThrowException()
        {
            // Arrange
            var server = new BACnetIPServer(_testPort);
            
            // Act & Assert
            // This should not throw an exception
            server.Stop();
        }

        [Fact]
        public void Stop_WhenStarted_StopsServerSuccessfully()
        {
            // Arrange
            var server = new BACnetIPServer(_testPort);
            server.Start();
            
            // Act
            server.Stop();
            
            // Assert
            // We can test the server stopped by starting it again
            // If it's stopped properly, we should be able to start it again without exception
            server.Start();
            server.Stop(); // Clean up
        }

        [Fact]
        public void SendResponse_WhenNotStarted_ThrowsException()
        {
            // Arrange
            var server = new BACnetIPServer(_testPort);
            var remoteEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 12345);
            var testData = new byte[] { 1, 2, 3, 4 };
            
            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => 
                server.SendResponse(testData, remoteEndPoint));
        }

        [Fact]
        public void MessageReceived_WhenSubscribed_AllowsSubscription()
        {
            // This test can only verify that the event subscription works
            // Testing that the event is raised would require integration tests
            
            // Arrange
            var server = new BACnetIPServer(_testPort);
            bool eventHandlerCalled = false;
            
            // Act
            server.MessageReceived += (sender, e) => eventHandlerCalled = true;
            
            // Assert
            // If we get here without an exception, the event subscription worked
            Assert.False(eventHandlerCalled); // Initially the handler hasn't been called
        }

        [Fact]
        public void Dispose_CallsStop()
        {
            // Arrange
            var server = new BACnetIPServer(_testPort);
            server.Start();
            
            // Act
            server.Dispose();
            
            // Assert
            // If Stop was called during Dispose, we should be able to start another server on the same port
            var server2 = new BACnetIPServer(_testPort);
            server2.Start();
            server2.Stop(); // Clean up
        }
    }
}