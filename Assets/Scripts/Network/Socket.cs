using System.Net;
using System.Net.Sockets;

namespace Network {
    public class Socket {
        public const int PacketSize = 128;

        public ByteBuffer Buffer = new ByteBuffer(PacketSize);

        public TcpClient Tcp;
        public UdpClient Udp;

        public IPEndPoint TcpRemote;
        public IPEndPoint UdpRemote;

        public bool SpecificUdp;

        public Socket(string host, int tcpPort, int udpPort) {
            TcpRemote = new IPEndPoint(IPAddress.Parse(host), tcpPort);
            Tcp = new TcpClient();

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
            Tcp.GetStream().Write(Buffer.Bytes, 0, Buffer.Pointer);

            Buffer.Reset();
            Buffer.Clear();
        }

        public void WriteBufferUdp() {
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