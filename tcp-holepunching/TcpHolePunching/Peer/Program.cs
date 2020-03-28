using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;
using System.Threading;

namespace Peer
{
    public class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "Peer - TCP Hole Punching Proof of Concept";

            Console.Write("Endpoint of the introducer (try 50.18.245.235:1618): ");

            var input = Console.ReadLine();
            input = (String.IsNullOrEmpty(input)) ? "50.18.245.235:1618" : input;
            var introducerEndpoint = input.Parse();

            // Connect to a remote device.  
            try
            {
                // Establish the remote endpoint for the socket.  
                // This example uses port 11000 on the local computer.  
                IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
                IPAddress ipAddress = ipHostInfo.AddressList[0];
                byte[] bytes = new byte[1024];

                // Create a TCP/IP socket.  
                Socket sender = new Socket(AddressFamily.InterNetwork,
                    SocketType.Stream, ProtocolType.Tcp);
                sender.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);

                // Connect the socket to the remote endpoint. Catch any errors.  
                try
                {
                    sender.Connect(introducerEndpoint);

                    Console.WriteLine("Socket connected to {0}",
                        sender.RemoteEndPoint.ToString());

                    string localEPString = sender.LocalEndPoint.ToString();

                    // Encode the data string into a byte array.  
                    byte[] msg = Encoding.ASCII.GetBytes(localEPString);
                    msg = PrefixLenToMessage(msg);
                    
                    int bytesSent = sender.Send(msg);

                    // Receive the response from the remote device.  
                    int bytesRec = sender.Receive(bytes);

                    var strippedMsg = MsgToAddress(bytes);

                    Console.WriteLine("Echoed test = {0}",
                        Encoding.ASCII.GetString(strippedMsg, 0, strippedMsg.Length));

                    var prefixedpublicAddr = PrefixLenToMessage(strippedMsg);

                    sender.Send(prefixedpublicAddr);
                    
                    bytesRec = sender.Receive(bytes);

                    Console.WriteLine("Echoed test = {0}",
                         Encoding.ASCII.GetString(bytes, 0, bytes.Length));

                    EndPoint clientPublicAddress = GetPublicAddrFromMsg(MsgToAddress(bytes));
                    EndPoint clientPrivateAddress = GetPrivateAddrFromMsg(MsgToAddress(bytes));

                    EndPoint localEP = sender.LocalEndPoint;

                    List<Thread> threads = new List<Thread>();
                    Thread accept = new Thread(() => Accept(localEP));
                    //accept.Start();
                    threads.Add(accept);
                    Thread connectToPublic = new Thread(() => Connect(localEP, clientPublicAddress));
                    connectToPublic.Start();
                    //threads.Add(connectToPublic);
                    Thread connectToPrivate = new Thread(() => Connect(localEP, clientPrivateAddress));
                    connectToPrivate.Start();
                    //threads.Add(connectToPrivate);

                    //foreach (var thread in threads)
                    //{
                    //    Console.WriteLine($"starting thread {0}", thread.Name);
                    //    thread.Start();
                    //}

                    // Release the socket.  
                    sender.Shutdown(SocketShutdown.Both);
                    sender.Close();

                }
                catch (ArgumentNullException ane)
                {
                    Console.WriteLine("ArgumentNullException : {0}", ane.ToString());
                }
                catch (SocketException se)
                {
                    Console.WriteLine("SocketException : {0}", se.ToString());
                }
                catch (Exception e)
                {
                    Console.WriteLine("Unexpected exception : {0}", e.ToString());
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            Application.Run();
        }

        static void Accept(EndPoint localAddr)
        {
            Socket sender = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            sender.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
            sender.Bind(localAddr);
            sender.Listen(1);
            sender.ReceiveTimeout = 5;
            while(true)
            {
                try
                {
                    var socket = sender.Accept();
                } catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    continue;
                }
                Console.WriteLine($"Accept connected to {0}", localAddr);
            }
        }

        static void Connect(EndPoint localAddr, EndPoint clientAddress)
        {
            Socket sender = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            sender.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
            sender.Bind(localAddr);
            byte[] bytes = new byte[1024];
            while (true)
            {
                try
                {
                    sender.Connect(clientAddress);

                } catch (Exception e)
                {
                    Console.WriteLine($"Exception from Connect method: {0}", e.Message);
                    continue;
                }
                SendHelloToPeer(sender);
                Console.WriteLine($"Connected to {0}", clientAddress.ToString());
            }
        }

        static void SendHelloToPeer(Socket sender)
        {
            byte[] hellomsg = Encoding.ASCII.GetBytes("Holy its c# here");
            var prefixedHello = PrefixLenToMessage(hellomsg);
            byte[] bytes = new byte[1024];
            try
            {
                sender.Send(prefixedHello);
                sender.Receive(bytes);

                Console.WriteLine("Hello back from peer? = {0}",
                     Encoding.ASCII.GetString(bytes, 0, bytes.Length));
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception from SendHelloToPeer method: {0}", e.Message);
            }
        }

        static EndPoint GetPublicAddrFromMsg(byte[] msg)
        {
            string msgString = Encoding.ASCII.GetString(msg, 0, msg.Length).Split('|')[0];
            return new IPEndPoint(IPAddress.Parse(msgString.Split(':')[0]), Convert.ToInt32(msgString.Split(':')[1]));
        }

        static EndPoint GetPrivateAddrFromMsg(byte[] msg)
        {
            string msgString = Encoding.ASCII.GetString(msg, 0, msg.Length).Split('|')[1];
            return new IPEndPoint(IPAddress.Parse(msgString.Split(':')[0]), Convert.ToInt32(msgString.Split(':')[1]));
        }


        static byte[] PrefixLenToMessage(byte[] msg)
        {
            int len = msg.Length;
            byte[] prefix = BitConverter.GetBytes(len);
            byte[] prefixedMsg = prefix.Concat(msg).ToArray();

            return prefixedMsg;
        }

        static byte[] MsgToAddress(byte[] msg)
        {
            var len = msg[3];
            msg = msg.Skip(4).Take(len).ToArray();
            return msg;
        }
    }
}
