using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;

namespace OnlineTestProject
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string serverIp = "127.0.0.1"; // Use your server's IP address
            int serverPort = 3000;

            List<PacketData> receivedPackets = new List<PacketData>();
            HashSet<int> receivedSequences = new HashSet<int>();

            // Establishing a TCP connection
            using (TcpClient client = new TcpClient(serverIp, serverPort))
            using (NetworkStream stream = client.GetStream())
            {
                // Send a request to stream all packets
                byte[] streamAllRequest = CreateRequestPayload(1);
                stream.Write(streamAllRequest, 0, streamAllRequest.Length);

                // Receive packets from the server
                byte[] buffer = new byte[17]; // Each packet is 17 bytes long
                int bytesRead;

                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    PacketData packet = ParsePacket(buffer);
                    receivedPackets.Add(packet);
                    receivedSequences.Add(packet.Sequence);
                }
            }

            // Identify and request missing sequences
            List<int> missingSequences = FindMissingSequences(receivedSequences);

            if (missingSequences.Count > 0)
            {
                using (TcpClient client = new TcpClient(serverIp, serverPort))
                using (NetworkStream stream = client.GetStream())
                {
                    foreach (int seq in missingSequences)
                    {
                        byte[] resendRequest = CreateRequestPayload(2, (byte)seq);
                        stream.Write(resendRequest, 0, resendRequest.Length);

                        byte[] buffer = new byte[17];
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);

                        if (bytesRead > 0)
                        {
                            PacketData packet = ParsePacket(buffer);
                            receivedPackets.Add(packet);
                        }
                    }
                }
            }

            // Save the received data in JSON format
            string outputJson = JsonConvert.SerializeObject(receivedPackets, Formatting.Indented);
            File.WriteAllText("OrderBookData.json", outputJson);

            Console.WriteLine("Data saved to OrderBookData.json");
        }

        // Helper function to create the request payload
        static byte[] CreateRequestPayload(byte callType, byte resendSeq = 0)
        {
            return new byte[] { callType, resendSeq };
        }

        // Helper function to parse received packets
        static PacketData ParsePacket(byte[] packet)
        {
            PacketData data = new PacketData
            {
                Symbol = Encoding.ASCII.GetString(packet, 0, 4),
                BuySellIndicator = Encoding.ASCII.GetString(packet, 4, 1),
                Quantity = BitConverter.ToInt32(packet.Skip(5).Take(4).Reverse().ToArray(), 0),
                Price = BitConverter.ToInt32(packet.Skip(9).Take(4).Reverse().ToArray(), 0),
                Sequence = BitConverter.ToInt32(packet.Skip(13).Take(4).Reverse().ToArray(), 0)
            };
            return data;
        }

        // Helper function to find missing sequences
        static List<int> FindMissingSequences(HashSet<int> receivedSequences)
        {
            int maxSequence = receivedSequences.Max();
            List<int> missingSequences = new List<int>();

            for (int i = 1; i < maxSequence; i++)
            {
                if (!receivedSequences.Contains(i))
                {
                    missingSequences.Add(i);
                }
            }
            return missingSequences;
        }
    }

    // Class representing the data structure for each packet
    public class PacketData
    {
        public required string Symbol { get; set; }
        public required string BuySellIndicator { get; set; }
        public int Quantity { get; set; }
        public int Price { get; set; }
        public int Sequence { get; set; }
    }
}