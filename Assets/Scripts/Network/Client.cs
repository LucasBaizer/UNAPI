using UnityEngine;
using System;
using System.Threading;
using System.IO;
using System.Collections.Generic;

namespace Network {
    public class Client {
        private static Client _current;
        public static Client Current {
            get {
                if(!Side.IsClient) {
                    throw new InvalidOperationException("Cannot get client instance when not on the server");
                }
                return _current;
            }
            set {
                _current = value;
            }
        }

        private ByteBuffer _out = new ByteBuffer(128);
        public static ByteBuffer Out {
            get {
                return Current._out;
            }
        }
        public static bool IsConnected {
            get {
                return Current != null;
            }
        }
        public bool IsAuthenticated;

        public static event EventHandler<EventArgs> OnConnect;

        public Socket Socket;
        public long ClientId;
        public event EventHandler<EventArgs> OnAuthenticate;
        public int TickRate = 1;
        private bool Closed;

        public static void Connect(string host) {
            Client client = new Client(host, 9292, 9293);

            if(OnConnect != null) {
                NetworkBridge.Invoke(() => OnConnect(client, EventArgs.Empty));
            }
        }

        public Client(string host, int tcpPort, int udpPort) {
            NetworkBridge.Log("Creating client...");

            Socket = new Socket(host, tcpPort, udpPort);
            Current = this;
            Side.NetworkSide = Side.Client;

            Thread tcpThread = new Thread(() => {
                Stream tcp = Socket.Tcp.GetStream();
                while(!Closed) {
                    // NetworkBridge.Log("Waiting to read TCP...");
                    byte[] raw = new byte[Socket.PacketSize];
                    int read = tcp.Read(raw, 0, raw.Length);
                    byte[] trim = new byte[read];
                    Array.Copy(raw, 0, trim, 0, trim.Length);

                    if(Closed) {
                        break;
                    }

                    ByteBuffer bufIn = new ByteBuffer(trim);
                    // bufIn.Debug(0, bufIn.Length);
                    // NetworkBridge.Log("Sub-Buffers: ");
                    List<ByteBuffer> buffers = new List<ByteBuffer>();
                    while(bufIn.Pointer != bufIn.Length) {
                        int len = bufIn.ReadInt();
                        ByteBuffer buf = new ByteBuffer(bufIn.ReadBytes(len));
                        // buf.Debug(0, buf.Length);
                        buffers.Add(buf);
                    }
                    try {
                        // Debug.Log("Executing buffers: ");
                        foreach(ByteBuffer buffer in buffers) {
                            // buffer.Debug(0, buffer.Length);
                            NetworkData.HandleClientData(buffer);
                        }
                    } catch(Exception e) {
                        NetworkBridge.Invoke(() => Debug.LogException(e));
                    }
                }
            });
            tcpThread.IsBackground = true;
            tcpThread.Start();

            Thread udpThread = new Thread(() => {
                while(IsConnected) {
                    byte[] raw = Socket.Udp.Receive(ref Socket.UdpRemote);
                    ByteBuffer bufIn = new ByteBuffer(raw);
                    try {
                        NetworkData.HandleClientData(bufIn);
                    } catch(Exception e) {
                        Debug.LogException(e);
                    }
                }
            });
            udpThread.IsBackground = true;
            udpThread.Start();

            Thread authThread = new Thread(() => {
                Thread.Sleep(100);

                Out.WriteByte(NetworkData.Management);
                Out.WriteByte(NetworkData.Authenticate);
                WriteTcp();

                NetworkBridge.Log("Wrote authentication information.");
            });
            authThread.IsBackground = true;
            authThread.Start();

            OnAuthenticate += (sender, args) => {
                Thread updateThread = new Thread(() => {
                    while(IsConnected) {
                        lock(UdpBuffers) {
                            if(UdpBuffers.Count > 0) {
                                WriteUdp();
                                UdpBuffers.Clear();
                                PositionUpdates.Clear();
                                RotationUpdates.Clear();
                                ScaleUpdates.Clear();
                            }
                        }

                        Thread.Sleep((int) (1000 / (float) TickRate));
                    }
                });
                updateThread.IsBackground = true;
                updateThread.Start();
            };
        }

        private List<byte[]> UdpBuffers = new List<byte[]>();
        private Dictionary<NetworkBehaviour, int> PositionUpdates = new Dictionary<NetworkBehaviour, int>();
        private Dictionary<NetworkBehaviour, int> RotationUpdates = new Dictionary<NetworkBehaviour, int>();
        private Dictionary<NetworkBehaviour, int> ScaleUpdates = new Dictionary<NetworkBehaviour, int>();

        public void PushUpdateTransform(NetworkBehaviour source, object update, byte code) {
            byte type = update is Vector3 ? NetworkData.Vector3Type : NetworkData.QuaternionType;

            lock(Out) {
                WriteHeader(NetworkData.UpdateTransform);
                Out.WriteLong(source.NetworkId);
                Out.WriteByte(type);
                if(type == NetworkData.Vector3Type) {
                    Out.WriteVector3((Vector3) update);
                } else if(type == NetworkData.QuaternionType) {
                    Out.WriteQuaternion((Quaternion) update);
                }

                if(code > 0) {
                    Out.WriteByte(code);
                }

                ByteBuffer copy = Out.Copy();

                if(type == NetworkData.Vector3Type) {
                    if(code == 1) {
                        PushUpdate(PositionUpdates, source, copy);
                    } else if(code == 2) {
                        PushUpdate(ScaleUpdates, source, copy);
                    }
                } else if(type == NetworkData.QuaternionType) {
                    PushUpdate(RotationUpdates, source, copy);
                }

                Out.Reset();
                Out.Clear();
            }
        }

        private void PushUpdate(Dictionary<NetworkBehaviour, int> dict, NetworkBehaviour source, ByteBuffer copy) {
            lock(UdpBuffers) {
                if(dict.ContainsKey(source)) {
                    UdpBuffers[dict[source]] = copy.Bytes;
                } else {
                    dict[source] = UdpBuffers.Count;
                    UdpBuffers.Add(copy.Bytes);
                }
            }
        }

        public ByteBuffer CollectUdp() {
            ByteBuffer sum = new ByteBuffer(1024);
            sum.WriteByte((byte) UdpBuffers.Count);
            foreach(byte[] buffer in UdpBuffers) {
                sum.WriteByte((byte) buffer.Length);
                sum.WriteBytes(buffer);
            }

            sum.Trim();
            return sum;
        }

        public void WriteTcp() {
            Socket.Buffer = Out;
            Socket.WriteBufferTcp();
        }

        public void WriteUdp() {
            Socket.Buffer = CollectUdp();
            Socket.WriteBufferUdp();
        }

        public void Close() {
            Socket.Close();
            Closed = true;

            Current = null;
        }

        /// <summary>
        /// Writes out a byte, another byte, and then a long.
        /// 
        /// Byte 1: NetworkData.Data
        /// Byte 2: code parameter
        /// Long: Client UID
        /// </summary>
        public void WriteHeader(byte code) {
            Out.WriteByte(NetworkData.Data);
            Out.WriteByte(code);
            Out.WriteLong(ClientId);
        }

        public void NotifyAuthenticate() {
            IsAuthenticated = true;
            if(OnAuthenticate != null) {
                OnAuthenticate(this, EventArgs.Empty);
            }
        }

        public static void Instantiate(string resource) {
            Instantiate(resource, Vector3.zero, Quaternion.identity);
        }

        public static void Instantiate(string resource, Vector3 position, Quaternion rotation) {
            Instantiate(resource, position, rotation, null);
        }

        public static void Instantiate(string resource, Vector3 position, Quaternion rotation, object parent) {
            Instantiate(resource, position, rotation, parent, true);
        }

        public static void Instantiate(string resource, Vector3 position, Quaternion rotation, object parent, bool here) {
            if(!Side.IsClient) {
                throw new InvalidOperationException("Cannot call client-side functions when not on the client");
            }

            GameObject attempt = (GameObject) Resources.Load(resource);

            lock(Out) {
                Current.WriteHeader(NetworkData.InstantiateObject);
                Out.WriteString(resource);
                Out.WriteVector3(position);
                Out.WriteQuaternion(rotation);
                Out.WriteVector3(attempt.transform.lossyScale);
                Out.WriteSceneObject(parent);
                Out.WriteBool(here);
                byte count = 0;
                foreach(Transform child in attempt.transform) {
                    NetworkBehaviour c = child.GetComponent<NetworkBehaviour>();
                    if(c != null) {
                        count++;
                    }
                }
                Out.WriteByte(count);
                Current.WriteTcp();
            }
        }

        public static void Destroy(GameObject obj) {
            Destroy(obj.GetComponent<NetworkBehaviour>());
        }

        public static void Destroy(NetworkBehaviour net) {
            if(!Side.IsClient) {
                throw new InvalidOperationException("Cannot call client-side functions when not on the client");
            }

            if(net == null) {
                throw new NullReferenceException("Object to destroy must be an existing networked object");
            }

            lock(Out) {
                Current.WriteHeader(NetworkData.DestroyObject);
                Out.WriteLong(net.NetworkId);
                Current.WriteTcp();
            }
        }

        public delegate void QueryCallback(ByteBuffer data);

        private static long QueryNonce = 0;
        internal static Dictionary<long, QueryCallback> Queries = new Dictionary<long, QueryCallback>();

        public static void Query(string data, QueryCallback then, params object[] args) {
            if(!Side.IsClient) {
                throw new InvalidOperationException("Cannot call client-side functions when not on the client");
            }

            Queries[QueryNonce] = then;
            Debug.Log("Querying data: " + data + "...");

            lock(Out) {
                Out.WriteByte(NetworkData.QueryDatabase);
                Out.WriteLong(QueryNonce++);
                Out.WriteString(data);
                Out.WriteByte((byte) args.Length);
                foreach(object arg in args) {
                    Out.WriteByte(NetworkData.GetObjectType(arg));
                    NetworkData.WriteObject(arg, Out);
                }

                Current.WriteTcp();
            }
        }

        public delegate void QueryObjectCallback<T>(T data);

        public static void Query<T>(string data, QueryObjectCallback<T> then, params object[] args) {
            Query(data, raw => then((T) SerialUtility.Deserialize(raw)), args);
        }
    }
}