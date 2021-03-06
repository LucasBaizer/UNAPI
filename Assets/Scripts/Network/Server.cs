﻿using UnityEngine;
using System;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace Network {
    public class Server {
        private static readonly System.Random Rng = new System.Random();

        public static ServerConfiguration DefaultConfig = ServerConfiguration.Default();

        public ServerConfiguration Config;
        private TcpListener TcpServer;
        private UdpClient UdpServer;
        private bool Closed;

        private static Server _current;
        public static Server Current {
            get {
                if(!Side.IsServer) {
                    throw new InvalidOperationException("Cannot get server instance when not on the server");
                }
                return _current;
            }
            set {
                _current = value;
            }
        }

        public static event EventHandler<EventArgs> OnStart;

        public static void Start() {
            Start(DefaultConfig);
        }

        public static void Start(ServerConfiguration config) {
            Server server = new Server(config);

            if(OnStart != null) {
                NetworkBridge.Invoke(() => OnStart(server, EventArgs.Empty));
            }
        }

        public Server(ServerConfiguration config) {
            Config = config;

            TcpServer = new TcpListener(IPAddress.Any, config.TcpPort);
            UdpServer = new UdpClient(config.UdpPort);

            NetworkBridge.Log("Started TCP and UDP servers.");

            Current = this;

            Thread tcpThread = new Thread(() => {
                TcpServer.Start();

                while(!Closed) {
                    TcpClient client = TcpServer.AcceptTcpClient();
                    NetworkBridge.Log("Accepted TCP client.");

                    Socket socket = new Socket(client, null);
                    Stream tcp = client.GetStream();

                    if(Closed) { // disconnect socket that connected after server closed: is this necessary?
                        NetworkBridge.Log("Kicked client.");
                        lock(socket.Buffer) {
                            socket.Buffer.WriteByte(NetworkData.Management);
                            socket.Buffer.WriteByte(NetworkData.Disconnect);
                            socket.WriteBufferTcp();
                        }
                        return;
                    }

                    Thread tcpClientThread = new Thread(() => {
                        while(!Closed) {
                            byte[] raw = new byte[Socket.PacketSize];
                            int read = tcp.Read(raw, 0, raw.Length);

                            if(Closed) {
                                NetworkBridge.Log("Kicked client.");
                                lock(socket.Buffer) {
                                    socket.Buffer.WriteByte(NetworkData.Management);
                                    socket.Buffer.WriteByte(NetworkData.Disconnect);
                                    socket.WriteBufferTcp();
                                }
                                return;
                            }

                            byte[] trimmed = new byte[read];
                            Array.Copy(raw, trimmed, read);

                            ByteBuffer bufIn = new ByteBuffer(trimmed);
                            List<ByteBuffer> buffers = new List<ByteBuffer>();
                            while(bufIn.Pointer != bufIn.Length) {
                                int len = bufIn.ReadInt();
                                ByteBuffer buffer = new ByteBuffer(bufIn.ReadBytes(len));
                                buffers.Add(buffer);
                            }
                            try {
                                foreach(ByteBuffer buffer in buffers) {
                                    NetworkData.HandleServerData(buffer, socket);
                                }
                            } catch(Exception e) {
                                NetworkBridge.Invoke(() => Debug.LogException(e));
                            }
                        }
                    });
                    tcpClientThread.IsBackground = true;
                    tcpClientThread.Start();
                }
            });
            tcpThread.IsBackground = true;
            tcpThread.Start();

            Thread udpThread = new Thread(() => {
                IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                while(!Closed) {
                    byte[] raw = UdpServer.Receive(ref remote);

                    Socket socket = new Socket(null, UdpServer);
                    socket.UdpRemote = remote;

                    if(Closed) {
                        return;
                    }

                    ByteBuffer bufIn = new ByteBuffer(raw);
                    byte bufferCount = bufIn.ReadByte();
                    // NetworkBridge.Log("Received " + bufferCount + " buffer(s) of UDP data.");
                    for(int i = 0; i < bufferCount; i++) {
                        byte bufferLength = bufIn.ReadByte();
                        byte[] buffer = bufIn.ReadBytes(bufferLength);
                        try {
                            NetworkData.HandleServerData(new ByteBuffer(buffer), socket);
                        } catch(Exception e) {
                            NetworkBridge.Invoke(() => UnityEngine.Debug.LogException(e));
                        }
                    }
                }
            });
            udpThread.IsBackground = true;
            udpThread.Start();

            Side.NetworkSide = Side.Server;
        }

        public void Close() {
            foreach(long clientId in ServerRegistry.Clients.Keys) {
                Socket client = ServerRegistry.Clients[clientId];
                lock(client.Buffer) {
                    client.Buffer.WriteByte(NetworkData.Management);
                    client.Buffer.WriteByte(NetworkData.Disconnect);
                    client.WriteBufferTcp();
                    client.Tcp.Close();
                }
            }

            TcpServer.Stop();
            UdpServer.Close();
            Closed = true;
        }

        public static long GenerateId() {
            byte[] buf = new byte[8];
            Rng.NextBytes(buf);
            return BitConverter.ToInt64(buf, 0);
        }

        public static NetworkBehaviour Instantiate(string resource) {
            return Instantiate(resource, Vector3.zero, Quaternion.identity);
        }

        public static NetworkBehaviour Instantiate(string resource, Vector3 position, Quaternion rotation) {
            return Instantiate(resource, position, rotation, null);
        }

        public static NetworkBehaviour Instantiate(string resource, Vector3 position, Quaternion rotation, object parent) {
            return Instantiate(resource, position, rotation, Vector3.zero, parent);
        }

        public static NetworkBehaviour Instantiate(string resource, Vector3 position, Quaternion rotation, Vector3 scale, object parent) {
            if(!Side.IsServer) {
                throw new InvalidOperationException("Cannot call server-side functions when not on the server");
            }

            GameObject attempt = (GameObject) Resources.Load(resource);
            if(attempt == null) {
                throw new InvalidOperationException("Resource does not exist");
            }
            GameObject inst = UnityEngine.Object.Instantiate(attempt, position, rotation);
            inst.name = attempt.name;

            NetworkBehaviour net = inst.GetComponent<NetworkBehaviour>();
            if(net == null) {
                throw new InvalidOperationException("Resource must be a NetworkBehaviour");
            }
            net.transform.UseNetwork = false;

            if(scale != Vector3.zero) {
                net.transform.lossyScale = scale;
            }

            if(parent != null) {
                if(parent is NetworkBehaviour) {
                    net.transform.parent = ((NetworkBehaviour) parent).transform;
                } else if(parent is Transform) {
                    net.transform.parent = (Transform) parent;
                } else {
                    throw new InvalidOperationException("Parent must be a NetworkBehaviour or a Transform: " + parent.GetType().FullName);
                }
            }

            net.NetworkId = GenerateId();

            List<long> childIds = new List<long>();
            foreach(Transform child in inst.transform) {
                NetworkBehaviour c = child.GetComponent<NetworkBehaviour>();
                if(c != null) {
                    childIds.Add(c.NetworkId = GenerateId());
                    ServerRegistry.Objects[c.NetworkId] = new ObjectRegistration(c, long.MinValue, resource, true, new long[0]);
                    Debug.Log("Registered local child object with ID: " + c.NetworkId);
                }
            }

            ServerRegistry.Objects[net.NetworkId] = new ObjectRegistration(net, long.MinValue, resource, false, childIds.ToArray());
            Debug.Log("Registered local object with ID: " + net.NetworkId);

            net.transform.UseNetwork = true;
            net.NetworkAwake();

            foreach(Transform child in inst.transform) {
                NetworkBehaviour c = child.GetComponent<NetworkBehaviour>();
                if(c != null) {
                    c.transform.UseNetwork = true;
                    c.NetworkAwake();
                }
            }

            foreach(Socket client in ServerRegistry.Clients.Values) {
                lock(client.Buffer) {
                    client.Buffer.WriteByte(NetworkData.Data);
                    client.Buffer.WriteByte(NetworkData.InstantiateObject);
                    client.Buffer.WriteLong(net.NetworkId);
                    client.Buffer.WriteBool(false);
                    client.Buffer.WriteString(resource);
                    client.Buffer.WriteVector3(position);
                    client.Buffer.WriteQuaternion(rotation);
                    client.Buffer.WriteVector3(scale == Vector3.zero ? attempt.transform.lossyScale : scale);
                    client.Buffer.WriteSceneObject(parent);
                    client.Buffer.WriteByte((byte) childIds.Count);
                    foreach(long cid in childIds) {
                        client.Buffer.WriteLong(cid);
                    }
                    client.WriteBufferTcp();
                }
            }

            return net;
        }

        public static void Destroy(GameObject obj) {
            Destroy(obj.GetComponent<NetworkBehaviour>());
        }

        public static void Destroy(NetworkBehaviour net) {
            if(!Side.IsServer) {
                throw new InvalidOperationException("Cannot call server-side functions when not on the server");
            }

            if(net == null) {
                throw new NullReferenceException("Object to destroy must be an existing networked object");
            }

            foreach(Socket client in ServerRegistry.Clients.Values) {
                lock(client.Buffer) {
                    client.Buffer.WriteByte(NetworkData.Data);
                    client.Buffer.WriteByte(NetworkData.DestroyObject);
                    client.Buffer.WriteLong(net.NetworkId);
                    client.WriteBufferTcp();
                }
            }

            UnityEngine.Object.Destroy(net.gameObject);
        }
    }

    public struct ServerConfiguration {
        public int TcpPort;
        public int UdpPort;
        public int TickRate;

        public static ServerConfiguration Default() {
            return new ServerConfiguration(9292, 9293, 30);
        }

        public ServerConfiguration(int tcp, int udp, int tick) {
            TcpPort = tcp;
            UdpPort = udp;
            TickRate = tick;
        }
    }
}