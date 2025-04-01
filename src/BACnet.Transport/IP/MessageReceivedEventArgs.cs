using System;
using System.Net;

namespace BACnet.Transport.IP
{
    /// <summary>
    /// Event arguments for message received events
    /// </summary>
    public class MessageReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// The received message
        /// </summary>
        public object Message { get; }

        /// <summary>
        /// The remote endpoint that sent the message (may be null for client-initiated messages)
        /// </summary>
        public IPEndPoint RemoteEndPoint { get; }

        /// <summary>
        /// Creates a new instance of MessageReceivedEventArgs with message only
        /// </summary>
        /// <param name="message">The received message</param>
        public MessageReceivedEventArgs(object message)
        {
            Message = message;
            RemoteEndPoint = null;
        }

        /// <summary>
        /// Creates a new instance of MessageReceivedEventArgs with message and remote endpoint
        /// </summary>
        /// <param name="message">The received message</param>
        /// <param name="remoteEndPoint">The remote endpoint that sent the message</param>
        public MessageReceivedEventArgs(byte[] message, IPEndPoint remoteEndPoint)
        {
            Message = message;
            RemoteEndPoint = remoteEndPoint;
        }
    }
}