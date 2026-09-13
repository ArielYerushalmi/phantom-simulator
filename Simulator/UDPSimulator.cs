using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PhantomLibrary.Models;

namespace Simulator_best
{
    public class UDPSimulator : IDisposable
    {
        private const string TargetHost = "127.0.0.1";
        private const int TargetPort = 9999;
        private static readonly TimeSpan SendInterval = TimeSpan.FromSeconds(1);

        private readonly string _sateliteUpPath = Path.Combine(AppContext.BaseDirectory, "ICD", "SateliteUp.json");
        private readonly Random _rnd = new Random();
        private readonly List<byte> _byteList = new List<byte>();

        private List<FlightBox> _sateliteUpList;
        private Socket _socket;
        private IPEndPoint _ipEndPoint;

        // Accumulator state used to pack multiple sub-byte fields into a single output byte.
        private byte _byteAccumulator;
        private byte _bitsAccumulated;

        public void Initialize()
        {
            _ipEndPoint = new IPEndPoint(IPAddress.Parse(TargetHost), TargetPort);
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        }

        public async Task EncodingRawMessage()
        {
            if (!File.Exists(_sateliteUpPath))
            {
                Console.WriteLine("ICD Does Not Exists!");
                return;
            }

            var json = File.ReadAllText(_sateliteUpPath);
            _sateliteUpList = JsonConvert.DeserializeObject<List<FlightBox>>(json);

            if (_sateliteUpList == null || _sateliteUpList.Count < 2)
            {
                Console.WriteLine("ICD is empty or malformed - nothing to send.");
                return;
            }

            while (true)
            {
                // The last ICD entry describes the checksum byte itself; its value is
                // computed below rather than randomly generated, so it's excluded here.
                for (int i = 0; i < _sateliteUpList.Count - 1; i++)
                {
                    var parameter = _sateliteUpList[i];
                    byte value = (byte)_rnd.Next(parameter.Min, parameter.Max + 1);
                    Console.WriteLine($"id: {i}, Location: {parameter.Location}, Parameter: {parameter.Name} => Value: {value} ");

                    if (parameter.Bit == 8)
                    {
                        _byteList.Add(value);
                    }
                    else
                    {
                        AddByteCalc(value, parameter.StartBit, parameter.Bit);
                    }
                }

                PrepareToSend();
                await Task.Delay(SendInterval);
            }
        }

        public void AddByteCalc(byte value, int startBit, int bitWidth)
        {
            int bitsToShiftLeft = 8 - (startBit + bitWidth);
            _byteAccumulator |= (byte)(value << bitsToShiftLeft);
            _bitsAccumulated += (byte)bitWidth;

            if (_bitsAccumulated == 8)
            {
                _byteList.Add(_byteAccumulator);
                _byteAccumulator = 0;
                _bitsAccumulated = 0;
            }
        }

        public void PrepareToSend()
        {
            _byteList.Add(CheckSumCalculation(_byteList.ToArray()));
            byte[] frame = _byteList.ToArray();
            _byteList.Clear();

            if (!Send(frame))
            {
                return;
            }

            Console.WriteLine("Message sent!");
            Console.WriteLine();
        }

        public byte CheckSumCalculation(byte[] data)
        {
            int setBitCount = 0;
            for (int i = 0; i < data.Length - 1; i++)
            {
                setBitCount += BitOperations.PopCount(data[i]);
            }

            byte checksum = (byte)setBitCount;
            var checksumField = _sateliteUpList[_sateliteUpList.Count - 1];
            Console.WriteLine($"id: {checksumField.Id}, Location: {checksumField.Location}, Parameter: {checksumField.Name} => Value: {checksum} ");
            return checksum;
        }

        public bool Send(byte[] data)
        {
            try
            {
                _socket.SendTo(data, _ipEndPoint);
                return true;
            }
            catch (SocketException ex)
            {
                // Don't let a transient send failure (e.g. no listener yet, which on
                // Windows/UDP can surface as a connection-reset on a later send) crash
                // the loop - log it and keep sending on the next tick.
                Console.WriteLine($"UDP send failed: {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            _socket?.Dispose();
        }
    }
}
