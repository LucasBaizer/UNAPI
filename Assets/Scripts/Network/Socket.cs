using System.Net;
using System.Net.Sockets;

namespace Network {
    public class Socket {
        public const int PacketSize = 2048;

        public ByteBuffer Buffer = new ByteBuffer(PacketSize);

        public TcpClient Tcp;
        public UdpClient Udp;

        public IPEndPoint TcpRemote;
        public IPEndPoint UdpRemote;

        public bool SpecificUdp;

        public Socket(string host, int tcpPort, int udpPort) {
            TcpRemote = new IPEndPoint(IPAddress.Parse(host), tcpPort);
            Tcp = new TcpClient();
            Tcp.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);

            NetworkBridge.Log("Connecting to TCP server...");
            Tcp.Connect(TcpRemote);
            NetworkBridge.Log("Connected to TCP server.");

            Udp = new UdpClient();
            SpecificUdp = false;

            NetworkBridge.Log("Connecting to UDP server...");
            Udp.Connect(host, udpPort);
            NetworkBridge.Log("Connected to UDP server.");
        }

        public Socket(TcpClient tcp, UdpClient udp) {
            Tcp = tcp;
            Udp = udp;
            SpecificUdp = true;
        }

        public void WriteBufferTcp() {
            try {
                ByteBuffer lengthBuffer = new ByteBuffer(4 + Buffer.Pointer);
                lengthBuffer.WriteInt(Buffer.Pointer);
                Buffer.CopyTo(lengthBuffer);

                Tcp.GetStream().Write(lengthBuffer.Bytes, 0, lengthBuffer.Pointer);
                Tcp.GetStream().Flush();

                Buffer.Reset();
                Buffer.Clear();
            } catch(System.IO.IOException) {
                NetworkBridge.Warn("Connection to remote is closed; closing connection.");

                if(Side.IsServer) {
                    lock(ServerRegistry.Clients) {
                        long toRemove = long.MinValue;
                        foreach(long id in ServerRegistry.Clients.Keys) {
                            if(this == ServerRegistry.Clients[id]) {
                                toRemove = id;
                            }
                        }
                        if(toRemove != long.MinValue) {
                            ServerRegistry.Clients.Remove(toRemove); // we do this to prevent a ConcurrentModificationException... if that exists in C#
                        } else {
                            NetworkBridge.Warn("Could not remove the client; their ID was not found in the registry.");
                        }
                    }
                } else if(Side.IsClient) {
                    Client.Current.Close();
                }
            }
        }

        public void WriteBufferUdp() {
            if(Udp == null) {
                throw new System.InvalidOperationException("Udp socket is null");
            }
            if(!SpecificUdp) {
                Udp.Send(Buffer.Bytes, Buffer.Pointer);
            } else {
                Udp.Send(Buffer.Bytes, Buffer.Pointer, UdpRemote);
            }

            Buffer.Reset();
            Buffer.Clear();
        }

        public void Close() {
            Tcp.Close();
            Udp.Close();
        }
    }
}