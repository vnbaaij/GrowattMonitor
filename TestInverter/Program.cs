﻿using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using GrowattMonitorShared;

namespace GrowattMonitorShared
{
    enum InverterState
    {
        PING_REPLIED = 0,
        ANNOUNCE_SENT = 1,
        ANNOUNCE_RCVD = 2,
        IDENTIFY_RCVD = 3,
        IDENTIFY_REPLIED = 4
    }

    public class Program
    {
        private static string ipAddress = "192.168.1.103";

        private static readonly byte[] PING = new byte[] {
                0x00, 0xA8, 0x00, 0x05, 0x00, 0x0C, 0x01, 0x16, 0x0D, 0x22, 0x2C, 0x43, 0x58, 0x46, 0x46, 0x75, 0x45, 0x2E, 0x79, 0xC6
            };

        private static readonly byte[] ANNOUNCE = new byte[] {
                0x00, 0x02, 0x00, 0x05, 0x00, 0xD9, 0x01, 0x03, 0x0D, 0x22, 0x2C, 0x43, 0x58, 0x46, 0x46, 0x75,
                0x45, 0x2E, 0x46, 0x37, 0x2E, 0x33, 0x7F, 0x40, 0x5E, 0x46, 0x23, 0x43, 0x60, 0x46, 0x78, 0x66,
                0x70, 0x56, 0x76, 0x74, 0x47, 0x72, 0x43, 0x76, 0x60, 0x74, 0x75, 0x47, 0x72, 0x6F, 0x13, 0x61,
                0x10, 0x53, 0x57, 0x72, 0x6F, 0x02, 0x51, 0x7A, 0x64, 0x06, 0x3A, 0x5E, 0x59, 0x51, 0x74, 0x74,
                0x47, 0x72, 0x6F, 0x77, 0x61, 0x74, 0x75, 0x47, 0x76, 0x6C, 0x9F, 0x61, 0x6A, 0x73, 0x03, 0x78,
                0x29, 0x65, 0xF0, 0x60, 0x6A, 0x76, 0x24, 0x35, 0x30, 0x59, 0x46, 0x45, 0x76, 0x30, 0x58, 0x77,
                0x71, 0x85, 0x55, 0x47, 0x73, 0x6F, 0x77, 0x61, 0x74, 0x74, 0x47, 0x72, 0x6F, 0x70, 0x54, 0x7E,
                0x32, 0x55, 0xE3, 0x7C, 0xEE, 0x66, 0xEA, 0x7D, 0xA5, 0x60, 0xFC, 0x64, 0xEC, 0x74, 0xA6, 0x45,
                0x73, 0x6F, 0x5A, 0x61, 0x2D, 0x73, 0xA3, 0x72, 0x6E, 0x77, 0x6B, 0x74, 0x7D, 0x47, 0x76, 0x6F,
                0x5C, 0x62, 0x9C, 0x77, 0xAF, 0x71, 0x87, 0x74, 0x89, 0x77, 0x9C, 0x44, 0x9A, 0x6C, 0x9F, 0x62,
                0x9C, 0x33, 0x35, 0x1D, 0x18, 0x16, 0x15, 0x00, 0x54, 0x0E, 0x1C, 0x19, 0x12, 0x13, 0x00, 0x11,
                0x35, 0x13, 0x07, 0x15, 0x00, 0x45, 0x40, 0x76, 0x40, 0x6F, 0x77, 0x61, 0x71, 0x75, 0x76, 0x72,
                0x6F, 0x77, 0x60, 0x74, 0x74, 0x47, 0x72, 0x6F, 0x77, 0x61, 0x74, 0x74, 0x46, 0x61, 0xF3, 0x77,
                0xA9, 0x74, 0x74, 0x47, 0x72, 0x6F, 0x77, 0x61, 0x74, 0x74, 0x47, 0x72, 0x6F, 0x77, 0x61, 0x7E,
                0xE9
            };

        private static readonly byte[] IDENTIFY_04 = new byte[] {
                0x00, 0x01, 0x00, 0x05, 0x00, 0x11, 0x01, 0x19, 0x0D, 0x22, 0x2C, 0x43, 0x58, 0x46, 0x46, 0x75, 0x45, 0x2E, 0x77, 0x65, 0x74, 0x75, 0x76, 0x12, 0x16
            };


        private static readonly byte[] IDENTIFY_05 = new byte[] {
                0x00, 0x01, 0x00, 0x05, 0x00, 0x11, 0x01, 0x19, 0x0D, 0x22, 0x2C, 0x43, 0x58, 0x46, 0x46, 0x75, 0x45, 0x2E, 0x77, 0x64, 0x74, 0x75, 0x76, 0xEE, 0x17
            };

        private static readonly byte[] IDENTIFY_06 = new byte[] {
                0x00, 0x01, 0x00, 0x05, 0x00, 0x12, 0x01, 0x19, 0x0D, 0x22, 0x2C, 0x43, 0x58, 0x46, 0x46, 0x75, 0x45, 0x2E, 0x77, 0x67, 0x74, 0x76, 0x74, 0x40, 0xDA, 0x5E
            };

        private static readonly byte[] IDENTIFY_07 = new byte[] {
                0x00, 0x01, 0x00, 0x05, 0x00, 0x11, 0x01, 0x19, 0x0D, 0x22, 0x2C, 0x43, 0x58, 0x46, 0x46, 0x75, 0x45, 0x2E, 0x77, 0x66, 0x74, 0x75, 0x1F, 0x78, 0xD6
            };

        private static readonly byte[] IDENTIFY_08 = new byte[] {
                0x00, 0x01, 0x00, 0x05, 0x00, 0x1A, 0x01, 0x19, 0x0D, 0x22, 0x2C, 0x43, 0x58, 0x46, 0x46, 0x75,
                0x45, 0x2E, 0x77, 0x69, 0x74, 0x7E, 0x0D, 0x22, 0x2C, 0x43, 0x58, 0x46, 0x46, 0x75, 0x45, 0x2E,
                0xE2, 0x1D
            };

        private static readonly byte[] IDENTIFY_09 = new byte[] {
                0x00, 0x01, 0x00, 0x05, 0x00, 0x14, 0x01, 0x19, 0x0D, 0x22, 0x2C, 0x43, 0x58, 0x46, 0x46, 0x75,
                0x45, 0x2E, 0x77, 0x68, 0x74, 0x70, 0x1F, 0x2A, 0x37, 0x2F, 0x49, 0xC3
            };

        private static readonly byte[] IDENTIFY_0A = new byte[] {
                0x00, 0x01, 0x00, 0x05, 0x00, 0x11, 0x01, 0x19, 0x0D, 0x22, 0x2C, 0x43, 0x58, 0x46, 0x46, 0x75,
                0x45, 0x2E, 0x77, 0x6B, 0x74, 0x75, 0x77, 0x3A, 0xD5
            };

        private static readonly byte[] IDENTIFY_0B = new byte[] {
                0x00, 0x01, 0x00, 0x05, 0x00, 0x2D, 0x01, 0x19, 0x0D, 0x22, 0x2C, 0x43, 0x58, 0x46, 0x46, 0x75,
                0x45, 0x2E, 0x77, 0x6A, 0x74, 0x69, 0x64, 0x51, 0x5E, 0x4E, 0x53, 0x5A, 0x45, 0x71, 0x4A, 0x41,
                0x44, 0x4F, 0x47, 0x41, 0x68, 0x13, 0x1F, 0x07, 0x4E, 0x0C, 0x19, 0x2B, 0x5D, 0x4C, 0x4F, 0x51,
                0x4C, 0x45, 0x64, 0x34, 0x09
            };

        private static readonly byte[] IDENTIFY_0C = new byte[] {
                0x00, 0x01, 0x00, 0x05, 0x00, 0x1F, 0x01, 0x19, 0x0D, 0x22, 0x2C, 0x43, 0x58, 0x46, 0x46, 0x75,
                0x45, 0x2E, 0x77, 0x6D, 0x74, 0x7B, 0x1F, 0x2A, 0x37, 0x2F, 0x39, 0x2C, 0x2C, 0x1F, 0x2A, 0x37,
                0x2F, 0x39, 0x2C, 0x2C, 0x1F, 0xC8, 0x25
            };

        private static readonly byte[] IDENTIFY_0D = new byte[] {
                0x00, 0x01, 0x00, 0x05, 0x00, 0x12, 0x01, 0x19, 0x0D, 0x22, 0x2C, 0x43, 0x58, 0x46, 0x46, 0x75,
                0x45, 0x2E, 0x77, 0x6C, 0x74, 0x76, 0x76, 0x43, 0x7A, 0xBA
            };

        private static readonly byte[] IDENTIFY_0E = new byte[] {
                0x00, 0x01, 0x00, 0x05, 0x00, 0x1C, 0x01, 0x19, 0x0D, 0x22, 0x2C, 0x43, 0x58, 0x46, 0x46, 0x75,
                0x45, 0x2E, 0x77, 0x6F, 0x74, 0x78, 0x76, 0x4B, 0x5D, 0x59, 0x50, 0x42, 0x4C, 0x69, 0x43, 0x41,
                0x45, 0x51, 0x99, 0x44
            };

        private static readonly byte[] IDENTIFY_0F = new byte[] {
                0x00, 0x01, 0x00, 0x05, 0x00, 0x12, 0x01, 0x19, 0x0D, 0x22, 0x2C, 0x43, 0x58, 0x46, 0x46, 0x75,
                0x45, 0x2E, 0x77, 0x6E, 0x74, 0x76, 0x7F, 0x42, 0x2A, 0x04
            };

        private static readonly byte[] IDENTIFY_10 = new byte[] {
                0x00, 0x01, 0x00, 0x05, 0x00, 0x21, 0x01, 0x19, 0x0D, 0x22, 0x2C, 0x43, 0x58, 0x46, 0x46, 0x75,
                0x45, 0x2E, 0x77, 0x71, 0x74, 0x65, 0x05, 0x46, 0x55, 0x32, 0x57, 0x4E, 0x46, 0x03, 0x48, 0x58,
                0x44, 0x5B, 0x37, 0x35, 0x7D, 0x4A, 0x58, 0x70, 0x7F
            };

        private static readonly byte[] IDENTIFY_11 = new byte[] {
                0x00, 0x01, 0x00, 0x05, 0x00, 0x21, 0x01, 0x19, 0x0D, 0x22, 0x2C, 0x43, 0x58, 0x46, 0x46, 0x75,
                0x45, 0x2E, 0x77, 0x71, 0x74, 0x65, 0x05, 0x46, 0x55, 0x32, 0x57, 0x4E, 0x46, 0x03, 0x48, 0x58,
                0x44, 0x5B, 0x37, 0x35, 0x7D, 0x4A, 0x58, 0x70, 0x7F
            };

        private static readonly byte[] IDENTIFY_12 = new byte[] {
                0x00, 0x01, 0x00, 0x05, 0x00, 0x14, 0x01, 0x19, 0x0D, 0x22, 0x2C, 0x43, 0x58, 0x46, 0x46, 0x75,
                0x45, 0x2E, 0x77, 0x73, 0x74, 0x70, 0x72, 0x40, 0x58, 0x4E, 0xE0, 0xB8
            };

        private static readonly byte[] IDENTIFY_13 = new byte[] {
                0x00, 0x01, 0x00, 0x05, 0x00, 0x10, 0x01, 0x19, 0x0D, 0x22, 0x2C, 0x43, 0x58, 0x46, 0x46, 0x75,
                0x45, 0x2E, 0x77, 0x72, 0x74, 0x74, 0x82, 0x6F
            };

        private static readonly byte[] IDENTIFY_14 = new byte[] {
                0x00, 0x01, 0x00, 0x05, 0x00, 0x24, 0x01, 0x19, 0x0D, 0x22, 0x2C, 0x43, 0x58, 0x46, 0x46, 0x75,
                0x45, 0x2E, 0x77, 0x75, 0x74, 0x60, 0x1F, 0x2A, 0x37, 0x2F, 0x39, 0x2C, 0x2C, 0x1F, 0x2A, 0x37,
                0x2F, 0x39, 0x2C, 0x2C, 0x1F, 0x2A, 0x37, 0x2F, 0x39, 0x2C, 0x23, 0xE8
            };

        private static readonly byte[] IDENTIFY_15 = new byte[] {
                0x00, 0x01, 0x00, 0x05, 0x00, 0x17, 0x01, 0x19, 0x0D, 0x22, 0x2C, 0x43, 0x58, 0x46, 0x46, 0x75,
                0x45, 0x2E, 0x77, 0x74, 0x74, 0x73, 0x76, 0x5C, 0x58, 0x59, 0x57, 0x5A, 0x4C, 0x78, 0x14
            };

        private static readonly byte[] DATA = new byte[] {
                0x00, 0x02, 0x00, 0x05, 0x01, 0x37, 0x01, 0x04, 0x0D, 0x22, 0x2C, 0x43, 0x58, 0x46, 0x46, 0x75,
                0x45, 0x2E, 0x46, 0x37, 0x2E, 0x33, 0x7F, 0x40, 0x5E, 0x46, 0x23, 0x43, 0x60, 0x46, 0x78, 0x66,
                0x7F, 0x73, 0x77, 0x74, 0x47, 0x72, 0x43, 0x77, 0x60, 0x74, 0x74, 0x47, 0x0B, 0x6A, 0xDD, 0x61,
                0x74, 0x74, 0x47, 0x72, 0x53, 0x72, 0xC2, 0x74, 0x74, 0x47, 0x72, 0x6F, 0x4A, 0x61, 0x74, 0x74,
                0x2F, 0x61, 0xE7, 0x7F, 0x99, 0x74, 0x74, 0x47, 0x72, 0x6F, 0x00, 0x61, 0x74, 0x74, 0x47, 0x72,
                0x6F, 0x77, 0x61, 0x74, 0x74, 0x47, 0x72, 0x6F, 0x77, 0x61, 0x74, 0x74, 0x47, 0x72, 0x6F, 0x77,
                0x61, 0xEF, 0xA7, 0x45, 0x46, 0xCC, 0xBA, 0x61, 0xB6, 0x74, 0x47, 0x72, 0x6F, 0x77, 0x61, 0x74,
                0x74, 0x47, 0x72, 0x6F, 0x77, 0x61, 0x74, 0x74, 0x47, 0x73, 0x6A, 0x79, 0x67, 0x74, 0x74, 0x47,
                0x72, 0x6F, 0x5A, 0x61, 0x2D, 0x3A, 0x67, 0x72, 0x6F, 0x77, 0x61, 0x74, 0x74, 0x47, 0x72, 0x6F,
                0x77, 0x2D, 0xC0, 0x74, 0x47, 0x72, 0x6F, 0x77, 0x61, 0x3B, 0x9D, 0x47, 0x72, 0xF3, 0xEA, 0x61,
                0x74, 0x74, 0x47, 0x72, 0x6F, 0x77, 0x61, 0x74, 0x74, 0x47, 0x72, 0x6F, 0x77, 0x61, 0x74, 0x74,
                0x47, 0x72, 0x6F, 0x77, 0x61, 0x74, 0x74, 0x47, 0x72, 0x6F, 0x77, 0x61, 0x74, 0x74, 0x47, 0x72,
                0x6F, 0x77, 0x61, 0x74, 0x74, 0x47, 0x72, 0x6F, 0x77, 0x61, 0x74, 0x74, 0x47, 0x72, 0x6F, 0x77,
                0x61, 0x74, 0x74, 0x47, 0x72, 0x6F, 0x77, 0x61, 0x74, 0x74, 0x47, 0x72, 0x6F, 0x77, 0x61, 0x74,
                0x2E, 0x47, 0xF4, 0x6F, 0x6E, 0x72, 0x7C, 0x73, 0x4D, 0x71, 0x75, 0x77, 0x61, 0x74, 0x6D, 0x54,
                0x7A, 0x68, 0x7E, 0x4C, 0x40, 0x74, 0x47, 0x72, 0x76, 0x64, 0x67, 0x61, 0x7D, 0x52, 0x70, 0x6F,
                0x5B, 0x61, 0x6D, 0x65, 0x46, 0x73, 0x6F, 0x77, 0x6F, 0x74, 0x74, 0x47, 0x72, 0x7E, 0x76, 0x60,
                0x74, 0x74, 0x47, 0x72, 0x6F, 0x77, 0x61, 0x74, 0x74, 0x47, 0x72, 0x6F, 0x77, 0x61, 0x74, 0x74,
                0x47, 0x72, 0x6F, 0x77, 0x61, 0x74, 0x74, 0x47, 0x72, 0x6F, 0x77, 0x61, 0x74, 0x74, 0x47, 0x72,
                0x6F, 0x77, 0x61, 0x74, 0x74, 0x47, 0x72, 0x6F, 0x77, 0x61, 0x74, 0x74, 0x47, 0x9B, 0xD3
            };

        private static readonly byte[] CONFIG_TIME = new byte[] {
                0x00, 0x01, 0x00, 0x05, 0x00, 0x23, 0x01, 0x18, 0x0D, 0x22, 0x2C, 0x43, 0x58, 0x46, 0x46, 0x75,
                0x45, 0x2E, 0x77, 0x7E, 0x74, 0x67, 0x75, 0x42, 0x5D, 0x47, 0x4C, 0x44, 0x45, 0x6A, 0x43, 0x5B,
                0x57, 0x50, 0x42, 0x4E, 0x73, 0x46, 0x55, 0x45, 0x51, 0xAB, 0xBE

        };

        private static readonly byte[][] IDENTIFY = new byte[][]
        {
                IDENTIFY_04,
                IDENTIFY_05,
                IDENTIFY_06,
                IDENTIFY_07,
                IDENTIFY_08,
                IDENTIFY_09,
                IDENTIFY_0A,
                IDENTIFY_0B,
                IDENTIFY_0C,
                IDENTIFY_0D,
                IDENTIFY_0E,
                IDENTIFY_0F,
                IDENTIFY_10,
                IDENTIFY_11,
                IDENTIFY_12,
                IDENTIFY_13,
                IDENTIFY_14,
                IDENTIFY_15,

        };


        private static NetworkStream stream = null;
        private static InverterState _state;
        private static int _outstanding = 0;
        private static Socket _socket = null;

        static void Main()
        {
            Console.WriteLine("\nPress Enter to start test inverter ...");
            Console.Read();

            Connect(ipAddress, 5279);
            Run();

        }

        public static void Connect(string server, int port)
        {
            IPAddress ip = IPAddress.Parse(server);
            IPEndPoint ipe = new IPEndPoint(ip, port);
            Socket tempSocket = new Socket(ipe.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                tempSocket.Connect(ipe);

                _socket = tempSocket;
                Console.WriteLine("Test inverter connected");

            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
            }
        }

        public static void Run()
        {
            // Startup
            SendPing();

            // Enter the loop.
            while (true)
            {
                if (_socket == null)
                    Connect(ipAddress, 5279);

                // Receive the data sent by the monitor.
                byte[] bytes = SocketReceive();
                // And process it
                ProcessBytes(bytes);
            }

            while (true)
            {



                Message msg;
                byte[] buffer;
                if (msg == null || msg.Remaining.Length == 0)
                    buffer = SocketReceive();
                if (buffer?.Length == 0)
                    break;
                msg = Message.CreateFromByteBuffer(buffer);
                //var data = msg.Decode();

                msg.Dump("IN");

                if (msg.Type == MessageType.PING)
                    Console.WriteLine("Ping reply received");

                if (_state == InverterState.ANNOUNCE_SENT && msg.Type == MessageType.IDENTIFY)
                {
                    Console.WriteLine("Identify Received");
                    _state = InverterState.IDENTIFY_RCVD;
                    _outstanding = 0;

                    var data = msg.DecodeIdentifyRequest();
                    Console.WriteLine($"\nReveived Query request for config items {data["first"]} to {data["last"]} acknowledging...");
                    byte[] reply = ((byte[])data["datalogger"])
                        .Concat(BitConverter.GetBytes((ushort)data["first"]))
                        .Concat(BitConverter.GetBytes((ushort)data["last"])).ToArray();

                    var ack = Message.Create(5, MessageType.IDENTIFY, reply);

                    stream.Write(reply, 0, reply.Length);

                    SendConfig();
                    _state = InverterState.IDENTIFY_REPLIED;
                }

                if (_state == InverterState.ANNOUNCE_SENT && msg.Type == MessageType.ANNOUNCE && msg.IsAck)
                {
                    Console.WriteLine("ACK to Announce Received");
                    _state = InverterState.ANNOUNCE_RCVD;
                    _outstanding = 0;
                }
                else if (_state == InverterState.ANNOUNCE_RCVD && msg.Type == MessageType.IDENTIFY)
                {
                    _state = InverterState.IDENTIFY_RCVD;

                    var data = msg.DecodeIdentifyRequest();
                    Console.WriteLine($"\nReveived Query request for config items {data["first"]} to {data["last"]} acknowledging...");
                    byte[] reply = ((byte[])data["datalogger"])
                        .Concat(new byte[] { 0x00 })
                        .Concat((byte[])data["first"])
                        .Concat(new byte[] { 0x00 })
                        .Concat((byte[])data["last"]).ToArray();

                    var ack = Message.Create(5, MessageType.IDENTIFY, reply);
                    stream.Write(reply, 0, reply.Length);

                    SendConfig();
                    _state = InverterState.IDENTIFY_REPLIED;
                }

                buffer = msg.Remaining;
            }

        }

        private static void ProcessBytes(byte[] buffer)
        {
            while (buffer?.Length > 0)
            {
                // Process the data sent by the client.
                var msg = Message.CreateFromByteBuffer(buffer);

                msg.Dump("IN");

                msg.Inverter = null;
                Message reply = null;

                if (msg != null)
                {
                    reply = ProcessMessage(msg);
                    buffer = msg.Remaining;

                }
                if (reply != null)
                {
                    SendMessage(reply);
                }
            }
        }

        private static Message ProcessMessage(Message msg)
        {
            var data = msg.Decode();

            Message reply = null;

            //int pingtimer = DateTime.Now.Second + 10;
            //int anntimer = DateTime.Now.Second + 5;
            //int datatimer = DateTime.Now.Second;


            //Thread.Sleep(1000);
            //if (DateTime.Now.Second > pingtimer)
            //{
            //    pingtimer = DateTime.Now.Second + 10;
            //    SendPing();
            //}

            //if (_state == InverterState.ANNOUNCE_SENT && DateTime.Now.Second > anntimer)
            //{
            //    anntimer = DateTime.Now.Second + 5;
            //    _outstanding++;
            //    SendAnnounce();
            //}
            //if (_state == InverterState.IDENTIFY_REPLIED && DateTime.Now.Second > datatimer)
            //{
            //    datatimer = DateTime.Now.Second + 10;
            //    SendData();
            //}

            //if (_outstanding > 15)
            //{
            //    return null;
            //}
            switch (msg.Type)
            {
                case MessageType.PING:
                    Console.WriteLine("==>");
                    Console.WriteLine("Ping reply received");

                    reply = Message.CreateFromByteBuffer(ANNOUNCE);

                    _state = InverterState.ANNOUNCE_SENT;
                    break;
                case MessageType.ANNOUNCE:
                    if (_state == InverterState.ANNOUNCE_SENT && msg.IsAck)
                    {
                        Console.WriteLine("==>");
                        Console.WriteLine("Announce ACK received");

                        _state = InverterState.ANNOUNCE_RCVD;

                        reply = Message.CreateFromByteBuffer(DATA);
                    }
                    else
                    {
                        Console.WriteLine("Announce received");
                    }
                    break;
                case MessageType.IDENTIFY:
                    if (_state == InverterState.PING_REPLIED )
                    {
                        Console.WriteLine("==>");
                        Console.WriteLine("Identify Received");

                        _state = InverterState.IDENTIFY_RCVD;
                        //_outstanding = 0;

                        var idreqdata = msg.DecodeIdentifyRequest();
                        Console.WriteLine($"\nReveived request for config items {idreqdata["first"]} to {idreqdata["last"]} acknowledging...");

                        //byte[] ack = ((byte[])idreqdata["datalogger"])
                        //    .Concat(BitConverter.GetBytes((ushort)idreqdata["first"]))
                        //    .Concat(BitConverter.GetBytes((ushort)idreqdata["last"])).ToArray();

                        //reply = Message.Create(5, MessageType.IDENTIFY, ack);
                        reply = null;
                        _state = InverterState.IDENTIFY_REPLIED;

                        SendConfig();
                    }
                    break;
                case MessageType.CURRDATA:
                    if (msg.IsAck)
                    {
                        Console.WriteLine("==>");
                        Console.WriteLine("Data ACK received");
                    }
                    break;


            }



            //MessageType.IDENTIFY => ProcessIdentify(msg),
            //MessageType.DATA => ProcessData(msg, (Telegram)data),
            //MessageType.CONFIG => ProcessConfAck((Config)data),
            //MessageType.REBOOT => null,
            //MessageType.CONFACK => ProcessConfAck((List<Config>)data),//case MessageType.CONFACK50:
            return reply;
        }

        private static byte[] SocketReceive()
        {


            // Buffer to store the response bytes.
            byte[] bytes = new Byte[2048];

            int i = _socket.Receive(bytes);
            bytes = bytes[0..i];

            return bytes;
        }

        private static void SendMessage(Message msg)
        {
            _socket.SendBufferSize = msg.Size;
            int i = _socket.Send(msg.Content);
            if (i > 0)
                msg.Dump("OUT");
        }

        private static void SendPing()
        {
            var msg = Message.CreateFromByteBuffer(PING);
            SendMessage(msg);

            _state = InverterState.PING_REPLIED;

            Console.WriteLine("Sent PING answer");

        }

        private static void SendAnnounce()
        {
            var msg = Message.CreateFromByteBuffer(ANNOUNCE);
            SendMessage(msg);

            _state = InverterState.ANNOUNCE_SENT;

            Console.WriteLine("Sent ANNOUNCE answer");

        }
        private static void SendData()
        {
            var msg = Message.CreateFromByteBuffer(DATA);
            SendMessage(msg);
            Console.WriteLine("Sent DATA");
        }

        private static void SendConfig()
        {
            foreach (var buffer in IDENTIFY)
            {
                var msg = Message.CreateFromByteBuffer(buffer);


                SendMessage(msg);
                Console.WriteLine($"Sent Config item details (0x{BitConverter.ToUInt16(msg.Body[10..12].ReverseWhenLittleEndian()):X2})");
            }
        }

    }
}
