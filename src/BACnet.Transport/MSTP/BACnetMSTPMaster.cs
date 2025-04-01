using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;

namespace BACnet.Transport.MSTP
{
    public class BACnetMSTPMaster
    {
        private SerialPort _serialPort;
        private List<byte> _receivedData;

        public BACnetMSTPMaster(string portName, int baudRate)
        {
            _serialPort = new SerialPort(portName, baudRate);
            _receivedData = new List<byte>();
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

        public void Send(byte[] data)
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.Write(data, 0, data.Length);
            }
        }

        public void Receive()
        {
            if (_serialPort.IsOpen)
            {
                while (true)
                {
                    try
                    {
                        int bytesToRead = _serialPort.BytesToRead;
                        if (bytesToRead > 0)
                        {
                            byte[] buffer = new byte[bytesToRead];
                            _serialPort.Read(buffer, 0, bytesToRead);
                            _receivedData.AddRange(buffer);
                            ProcessReceivedData(buffer);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Handle exceptions (e.g., log them)
                    }
                    Thread.Sleep(100); // Adjust as necessary
                }
            }
        }

        private void ProcessReceivedData(byte[] data)
        {
            // Implement processing of received data according to BACnet MSTP specifications
        }

        public List<byte> GetReceivedData()
        {
            return new List<byte>(_receivedData);
        }
    }
}