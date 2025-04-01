using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;

namespace BACnet.Transport.MSTP
{
    public class BACnetMSTPClient
    {
        private SerialPort _serialPort;
        private const int MaxRetries = 3;
        private const int Timeout = 1000; // in milliseconds

        public BACnetMSTPClient(string portName, int baudRate)
        {
            _serialPort = new SerialPort(portName, baudRate);
            _serialPort.DataReceived += OnDataReceived;
        }

        public void Open()
        {
            if (!_serialPort.IsOpen)
            {
                _serialPort.Open();
            }
        }

        public void Close()
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.Close();
            }
        }

        public void SendMessage(byte[] message)
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.Write(message, 0, message.Length);
            }
            else
            {
                throw new InvalidOperationException("Serial port is not open.");
            }
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            int bytesToRead = _serialPort.BytesToRead;
            byte[] buffer = new byte[bytesToRead];
            _serialPort.Read(buffer, 0, bytesToRead);
            ProcessReceivedData(buffer);
        }

        private void ProcessReceivedData(byte[] data)
        {
            // Process the received data according to BACnet MSTP specifications
        }

        public byte[] ReceiveMessage()
        {
            byte[] receivedData = new byte[256]; // Adjust size as needed
            int bytesRead = 0;

            while (bytesRead < receivedData.Length)
            {
                if (_serialPort.Read(receivedData, bytesRead, receivedData.Length - bytesRead) > 0)
                {
                    bytesRead += _serialPort.BytesToRead;
                }
                else
                {
                    Thread.Sleep(Timeout);
                }
            }

            return receivedData;
        }
    }
}