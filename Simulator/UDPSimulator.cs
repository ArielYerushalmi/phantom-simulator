using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using System.Threading;
using PhantomLibrary.Models;

namespace Simulator_best
{
    public class UDPSimulator
    {
        private string SateliteUpPATH = Path.Combine(AppContext.BaseDirectory, "ICD", "SateliteUp.json");
        private string json;
        private List<FlightBox> SateliteUpList = new List<FlightBox>();
        private List<Byte> byteList = new List<Byte>();
        private Byte bitsToShiftLeft = 0;
        private Byte shiftedNumberLeft;
        private Byte byteCalc = 0;
        private Byte numOfBits;
        private Random rnd = new Random();
        private Socket _socket;
        private IPEndPoint _ipEndPoint;

        public void Initialize()
        {
            _ipEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9999);
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        }
        public async Task EncodingRawMessage()
        {
            if (File.Exists(SateliteUpPATH))
            {
                json = File.ReadAllText(SateliteUpPATH);
                SateliteUpList = JsonConvert.DeserializeObject<List<FlightBox>>(json);
                while (true)
                {
                    for (int i = 0; i < SateliteUpList.Count - 1; i++)
                    {
                        Byte ByteNumber = (Byte)(rnd.Next(SateliteUpList[i].Min, SateliteUpList[i].Max + 1));
                        Console.WriteLine($"id: {i}, Location: {SateliteUpList[i].Location}, Parameter: {SateliteUpList[i].Name} => Value: {ByteNumber} ");
                        if (SateliteUpList[i].Bit == 8)
                        {
                            byteList.Add(ByteNumber);
                        }
                        else
                        {
                            AddByteCalc(ByteNumber, i);
                        }
                    }
                    PrepareToSend();
                    await Task.Delay(1000);
                }
            }
            else
            {
                Console.WriteLine("ICD Does Not Exists!");
            }
        }

        public void AddByteCalc(Byte ByteNumber, int i)
        {
            bitsToShiftLeft = (Byte)(8 - (SateliteUpList[i].StartBit + SateliteUpList[i].Bit));
            shiftedNumberLeft = (Byte)(ByteNumber << bitsToShiftLeft);
            byteCalc = (Byte)(byteCalc | shiftedNumberLeft);
            numOfBits += (Byte)(SateliteUpList[i].Bit);
            if (numOfBits == 8)
            {
                byteList.Add(byteCalc);
                numOfBits = 0;
                byteCalc = 0;
            }
        }

        public void PrepareToSend()
        {
            byteList.Add(checkSumCalculation(byteList.ToArray()));
            Byte[] byteArray = byteList.ToArray();
            byteList.Clear();
            Send(byteArray);
            Console.WriteLine("Message sent!");
            Console.WriteLine();
        }

        public byte checkSumCalculation(byte[] data)
        {
            byte counter = 0;
            for (int i = 0; i < data.Length - 1; i++)
            {
                string str = Convert.ToString(data[i], 2).PadLeft(8, '0');

                for (int j = 0; j < 8; j++)
                {
                    if (str[j] == '1')
                    {
                        counter++;
                    }
                }
            }
            Console.WriteLine($"id: {SateliteUpList[SateliteUpList.Count - 2].Id}, Location: {SateliteUpList[SateliteUpList.Count - 1].Location}, Parameter: {SateliteUpList[SateliteUpList.Count - 1].Name} => Value: {counter} ");
            return counter;


        }
        public void Send(byte[] data)
        {
            _socket.SendTo(data, _ipEndPoint);
        }
    }
}
