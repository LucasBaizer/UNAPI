using UnityEngine;
using System.Reflection;
using System.Collections.Generic;

namespace Network {
    public class NetworkData {
        public const byte Data = 0;
        public const byte InstantiateObject = 1;
        public const byte DestroyObject = 2;
        public const byte UpdateTransform = 3;
        public const byte UpdateParent = 4;
        public const byte UpdateField = 5;
        public const byte InvokeRPC = 6;

        public const byte Management = 1;
        public const byte Authenticate = 0;
        public const byte Disconnect = 1;
        public const byte Complete = 2;

        public const byte QueryDatabase = 2;

        public const byte ObjectType = 0;
        public const byte Vector3Type = 1;
        public const byte QuaternionType = 2;
        public const byte IntType = 3;
        public const byte FloatType = 4;
        public const byte LongType = 5;
        public const byte BoolType = 6;
        public const byte StringType = 7;
        public const byte BehaviourType = 8;
        public const byte ByteType = 9;

        public static void HandleServerData(ByteBuffer data, Socket client) {
            byte type = data.ReadByte();

            if(type == Data) {
                byte dType = data.ReadByte();
                long clientId = data.ReadLong();

                if(client.SpecificUdp) {
                    Socket serverClient = ServerRegistry.GetClient(clientId);
                    if(serverClient.Udp == null) {
                        serverClient.Udp = client.Udp;
                        serverClient.UdpRemote = client.UdpRemote;
                    }
                }

                if(dType == InstantiateObject) {
                    string resourcePath = data.ReadString();
                    Vector3 position = data.ReadVector3();
                    Quaternion rotation = data.ReadQuaternion();
                    Vector3 scale = data.ReadVector3();
                    object parent = null;
                    NetworkBridge.AwaitInvoke(() => parent = data.ReadSceneObject());
                    bool here = data.ReadBool();
                    byte childCount = data.ReadByte();

                    long[] ids = new long[childCount + 1];
                    for(int i = 0; i < ids.Length; i++) {
                        ids[i] = Server.GenerateId();
                    }
                    long[] childIds = new long[childCount];
                    System.Array.Copy(ids, 1, childIds, 0, childCount);
                    NetworkBridge.Invoke(() => {
                        Debug.Log("Loading resource: " + resourcePath);
                        Object resource = Resources.Load(resourcePath);
                        GameObject inst = (GameObject) Object.Instantiate(resource);
                        inst.name = resource.name;

                        NetworkBehaviour net = inst.GetComponent<NetworkBehaviour>();

                        IEnumerable<long> userIds = here ? ServerRegistry.GetClientIDs() : ServerRegistry.GetOtherClientIDs(clientId);
                        foreach(long userId in userIds) {
                            Socket to = ServerRegistry.GetClient(userId);
                            lock(to.Buffer) {
                                to.Buffer.WriteByte(Data);
                                to.Buffer.WriteByte(InstantiateObject);
                                to.Buffer.WriteLong(ids[0]);
                                to.Buffer.WriteBool(!net.ServerOnly && userId == clientId);
                                to.Buffer.WriteString(resourcePath);
                                to.Buffer.WriteVector3(position);
                                to.Buffer.WriteQuaternion(rotation);
                                to.Buffer.WriteVector3(scale);
                                to.Buffer.WriteSceneObject(parent);
                                to.Buffer.WriteByte(childCount);
                                foreach(long id in childIds) {
                                    to.Buffer.WriteLong(id);
                                }
                                to.WriteBufferTcp();
                            }
                        }

                        net.NetworkId = ids[0];
                        net.transform.UseNetwork = false;
                        net.transform.position = position;
                        net.transform.rotation = rotation;
                        net.transform.lossyScale = scale;

                        if(parent == null) {
                            net.transform.SetParent((Transform) null);
                        } else {
                            if(parent is ObjectRegistration) {
                                net.transform.SetParent(((ObjectRegistration) parent).Object.transform);
                            } else if(parent is Transform) {
                                net.transform.SetParent((Transform) parent);
                            }
                        }

                        ServerRegistry.Objects[ids[0]] = new ObjectRegistration(net, clientId, resourcePath, false, childIds);
                        Debug.Log("Registered object with ID: " + ids[0] + ".");

                        int idCount = 0;
                        for(int i = 0; i < inst.transform.childCount; i++) {
                            Transform child = inst.transform.GetChild(i);
                            NetworkBehaviour cnet = child.GetComponent<NetworkBehaviour>();

                            if(cnet != null) {
                                cnet.NetworkId = childIds[idCount++];
                                cnet.transform.UseNetwork = true;
                                ServerRegistry.Objects[cnet.NetworkId] = new ObjectRegistration(net, clientId, resourcePath, true, new long[0]);
                                Debug.Log("Registered child with ID: " + cnet.NetworkId + ".");
                                cnet.NetworkAwake();
                            }
                        }

                        net.transform.UseNetwork = true;
                        net.NetworkAwake();
                    });
                } else if(dType == DestroyObject) {
                    long objectId = data.ReadLong();

                    ObjectRegistration obj = null;
                    if(!ServerRegistry.GetObject(objectId, out obj)) {
                        NetworkBridge.Warn("Received malformed data packet with type: " + dType + ": invalid object id: " + objectId);
                    } else {
                        if(obj.ClientOwner == clientId) {
                            Object.Destroy(obj.Object);
                            ServerRegistry.Objects.Remove(objectId);

                            foreach(long userId in ServerRegistry.GetClientIDs()) {
                                Socket to = ServerRegistry.GetClient(userId);
                                lock(to.Buffer) {
                                    to.Buffer.WriteByte(Data);
                                    to.Buffer.WriteByte(DestroyObject);
                                    to.Buffer.WriteLong(objectId);
                                    to.WriteBufferTcp();
                                }
                            }
                        }
                    }
                } else if(dType == UpdateTransform) {
                    long objectId = data.ReadLong();

                    ObjectRegistration reg = null;
                    if(!ServerRegistry.GetObject(objectId, out reg)) {
                        NetworkBridge.Warn("Received malformed data packet with type: " + dType + ": invalid object id: " + objectId);
                    } else {
                        NetworkBehaviour obj = reg.Object;
                        if((!obj.transform.RequiresAuthority || reg.ClientOwner == clientId) && obj.transform.AcceptUpdates) {
                            NetworkBridge.Invoke(() => {
                                bool old = obj.transform.UseNetwork;
                                obj.transform.UseNetwork = true;
                                byte tType = data.ReadByte();
                                if(tType == Vector3Type) {
                                    Vector3 vec = data.ReadVector3();
                                    byte vType = data.ReadByte();
                                    if(vType == 1) {
                                        obj.transform.position = vec;
                                    } else if(vType == 2) {
                                        obj.transform.lossyScale = vec;
                                    }
                                } else if(tType == QuaternionType) {
                                    obj.transform.rotation = data.ReadQuaternion();
                                } else {
                                    Debug.LogWarning("Received malformed data packet with type: " + dType + ": invalid transformation object type id: " + tType);
                                }
                                obj.transform.UseNetwork = old;
                            });
                        }
                    }
                } else if(dType == UpdateParent) {
                    long id = data.ReadLong();

                    ObjectRegistration reg = null;
                    if(!ServerRegistry.GetObject(id, out reg)) {
                        NetworkBridge.Warn("Received malformed data packet with type: " + dType + ": invalid object id: " + id);
                    } else {
                        NetworkBehaviour obj = reg.Object;

                        if((!obj.transform.RequiresAuthority || reg.ClientOwner == clientId) && obj.transform.AcceptUpdates) {
                            NetworkBridge.Invoke(() => {
                                object newParent = data.ReadSceneObject();
                                bool worldPositionStays = data.ReadBool();

                                if(newParent == null) {
                                    obj.transform.SetParent((Transform) null, worldPositionStays);
                                } else {
                                    if(newParent is ObjectRegistration) {
                                        obj.transform.SetParent(((ObjectRegistration) newParent).Object.transform, worldPositionStays);
                                    } else if(newParent is Transform) {
                                        obj.transform.SetParent((Transform) newParent, worldPositionStays);
                                    }
                                }
                            });
                        } else {
                            NetworkBridge.Warn("Received malformed data packet with type: " + dType + ": missing authority");
                        }
                    }
                } else if(dType == UpdateField) {
                    long id = data.ReadLong();
                    bool tcp = data.ReadBool();
                    string fieldName = data.ReadString();
                    byte dataType = data.ReadByte();
                    object value = ReadObject(data, dataType);

                    ObjectRegistration reg = null;
                    if(!ServerRegistry.GetObject(id, out reg)) {
                        NetworkBridge.Warn("Received malformed data packet with type: " + dType + ": invalid object id: " + id);
                    } else {
                        Synchronized sync;
                        FieldInfo field = reg.Object.GetLocal(fieldName, true, out sync);
                        if(field == null) {
                            NetworkBridge.Log("Received malformed data packet with type: " + dType + ": invalid field");
                        } else {
                            if(!sync.RequiresAuthority || reg.ClientOwner == clientId) {
                                NetworkBridge.Invoke(() => reg.Object.SetLocal(fieldName, value));

                                foreach(long userId in ServerRegistry.GetOtherClientIDs(clientId)) {
                                    Socket to = ServerRegistry.GetClient(userId);
                                    lock(to.Buffer) {
                                        to.Buffer.WriteByte(Data);
                                        to.Buffer.WriteByte(UpdateField);
                                        to.Buffer.WriteLong(id);
                                        to.Buffer.WriteString(fieldName);
                                        to.Buffer.WriteByte(dataType);
                                        WriteObject(value, to.Buffer);
                                        if(tcp) {
                                            to.WriteBufferTcp();
                                        } else {
                                            to.WriteBufferUdp();
                                        }
                                    }
                                }
                            } else {
                                NetworkBridge.Warn("Received malformed data packet with type: " + dType + ": missing authority");
                            }
                        }
                    }
                } else if(dType == InvokeRPC) {
                    long id = data.ReadLong();
                    string methodName = data.ReadString();
                    byte argCount = data.ReadByte();
                    object[] args = new object[argCount];
                    for(int i = 0; i < argCount; i++) {
                        args[i] = ReadObject(data, data.ReadByte());
                    }

                    ObjectRegistration reg = null;
                    if(!ServerRegistry.GetObject(id, out reg)) {
                        NetworkBridge.Warn("Received malformed data packet with type: " + dType + ": invalid object id: " + id);
                    } else {
                        RemoteMethod remote;
                        MethodInfo method = reg.Object.GetLocalMethod(methodName, out remote);
                        if(method == null) {
                            NetworkBridge.Warn("Received malformed data packet with type: " + dType + ": invalid method");
                        } else {
                            if(!remote.RequiresAuthority || reg.ClientOwner == clientId) {
                                NetworkBridge.Invoke(() => reg.Object.InvokeLocalMethod(methodName, args));

                                // the server shouldn't make other clients invoke the RPC, so this code is broken!
                                /* foreach(long userId in ServerRegistry.GetOtherClientIDs(clientId)) {
                                    Socket to = ServerRegistry.Clients[userId];
                                    to.Buffer.WriteByte(Data);
                                    to.Buffer.WriteByte(InvokeRPC);
                                    to.Buffer.WriteLong(id);
                                    to.Buffer.WriteString(methodName);
                                    to.Buffer.WriteByte(argCount);
                                    foreach(object arg in args) {
                                        WriteObject(arg, to.Buffer);
                                    }
                                    to.WriteBufferTcp();
                                } */
                            } else {
                                NetworkBridge.Warn("Received malformed data packet with type: " + dType + ": missing authority");
                            }
                        }
                    }
                } else {
                    NetworkBridge.Warn("Received malformed data packet with type: " + dType + ": invalid type");
                }
            } else if(type == Management) {
                byte mType = data.ReadByte();

                if(mType == Authenticate) {
                    long clientId = Server.GenerateId();
                    lock(client.Buffer) {
                        client.Buffer.WriteByte(Management);
                        client.Buffer.WriteByte(Authenticate);
                        client.Buffer.WriteLong(clientId);
                        client.Buffer.WriteByte((byte) Server.Current.Config.TickRate);
                        client.WriteBufferTcp();
                    }

                    lock(ServerRegistry.Clients) {
                        ServerRegistry.Clients[clientId] = client;
                        NetworkBridge.Log("Registered client with ID: " + clientId);
                    }

                    NetworkBridge.Invoke(() => {
                        foreach(long id in ServerRegistry.Objects.Keys) {
                            ObjectRegistration reg = ServerRegistry.Objects[id];

                            if(!reg.IsChild) {
                                Debug.Log("objid: " + id);
                                lock(client.Buffer) {
                                    client.Buffer.WriteByte(Data);
                                    client.Buffer.WriteByte(InstantiateObject);
                                    client.Buffer.WriteLong(id);
                                    client.Buffer.WriteBool(false);
                                    client.Buffer.WriteString(reg.ResourcePath);
                                    client.Buffer.WriteVector3(reg.Object.transform.position);
                                    client.Buffer.WriteQuaternion(reg.Object.transform.rotation);
                                    client.Buffer.WriteVector3(reg.Object.transform.lossyScale);

                                    Transform parent = reg.Object.transform.parent;
                                    /* NetworkBehaviour parentNet = null;
                                    if(parent != null) {
                                        parentNet = parent.gameObject.GetComponent<NetworkBehaviour>();
                                    } */
                                    client.Buffer.WriteSceneObject(parent);
                                    client.Buffer.WriteByte((byte) reg.ChildIds.Length);
                                    foreach(long cid in reg.ChildIds) {
                                        client.Buffer.WriteLong(cid);
                                    }
                                    client.WriteBufferTcp();
                                }
                            }

                            NetworkBehaviour net = reg.Object;
                            foreach(string key in net.FieldChanges.Keys) {
                                object val = net.FieldChanges[key];

                                Debug.Log("field: " + key + " = " + val);

                                lock(client.Buffer) {
                                    client.Buffer.WriteByte(Data);
                                    client.Buffer.WriteByte(UpdateField);
                                    client.Buffer.WriteLong(id);
                                    client.Buffer.WriteString(key);
                                    client.Buffer.WriteByte(GetObjectType(val));
                                    WriteObject(val, client.Buffer);
                                    client.WriteBufferTcp();
                                }
                            }
                        }

                        NetworkBridge.Log("Sent all existing objects and their updated fields to client.");

                        lock(client.Buffer) {
                            client.Buffer.WriteByte(Management);
                            client.Buffer.WriteByte(Complete);
                            client.WriteBufferTcp();

                            NetworkBridge.Log("Sent Management/Complete message.");
                        }
                    });
                } else {
                    NetworkBridge.Warn("Received malformed management packet with type: " + mType + ": invalid type");
                }
            } else if(type == QueryDatabase) {
                long id = data.ReadLong();
                string dataName = data.ReadString();
                byte argCount = data.ReadByte();
                object[] args = new object[argCount];
                for(int i = 0; i < argCount; i++) {
                    args[i] = ReadObject(data, data.ReadByte());
                }

                if(ServerDatabase.IsRegistered(dataName)) {
                    NetworkBridge.Invoke(() => {
                        lock(client.Buffer) {
                            client.Buffer.WriteByte(QueryDatabase);
                            client.Buffer.WriteByte(0);
                            client.Buffer.WriteLong(id);
                            byte[] query = ServerDatabase.Request(dataName, args);
                            NetworkBridge.Log("Query: " + dataName + ": " + query.Length + " bytes");
                            client.Buffer.WriteInt(query.Length);
                            client.Buffer.WriteBytes(query);
                            client.WriteBufferTcp();
                        }
                    });
                } else {
                    NetworkBridge.Warn("Received malformed query packet: data not found: " + dataName);

                    NetworkBridge.Invoke(() => {
                        lock(client.Buffer) {
                            client.Buffer.WriteByte(QueryDatabase);
                            client.Buffer.WriteByte(1);
                            client.WriteBufferTcp();
                        }
                    });
                }
            } else {
                NetworkBridge.Warn("Received malformed packet with type: " + type + ": invalid type (not data or management");
            }
        }

        private static List<ByteBuffer> ExecuteLater = new List<ByteBuffer>();

        public static void HandleClientData(ByteBuffer data) {
            byte type = data.ReadByte();

            if(type == Data) {
                byte dType = data.ReadByte();
                long id = data.ReadLong();

                /* NetworkBehaviour obj = null;
                 if(dType != InstantiateObject && !ClientRegistry.GetObject(id, out obj)) {
                     NetworkBridge.Warn("Received malformed data packet with type: " + dType + ": invalid object id: " + id);
                 } else {*/
                if(dType == InstantiateObject) {
                    bool hasAuthority = data.ReadBool();
                    // Debug.Log(hasAuthority);
                    string resourcePath = data.ReadString();
                    // Debug.Log(resourcePath);
                    Vector3 position = data.ReadVector3();
                    Quaternion rotation = data.ReadQuaternion();
                    Vector3 scale = data.ReadVector3();
                    // Debug.Log(scale);
                    // data.Debug(data.Pointer, data.Length);
                    int ptr = data.Pointer;
                    object parent = null;
                    NetworkBridge.AwaitInvoke(() => {
                        try {
                            parent = data.ReadSceneObject();
                        } catch(System.IndexOutOfRangeException e) {
                            NetworkBridge.Warn("hasAuthority: " + hasAuthority);
                            NetworkBridge.Warn("resourcePath: " + resourcePath);
                            NetworkBridge.Warn("position: " + position);
                            NetworkBridge.Warn("rotation: " + position);
                            NetworkBridge.Warn("scale: " + position);
                            NetworkBridge.Warn("ptr: " + ptr);
                            NetworkBridge.Warn("len: " + data.Length);
                            NetworkBridge.Warn("byte: " + data.Bytes[ptr]);

                            data.Debug(0, data.Length);
                            throw e;
                        }
                    });
                    byte childCount = data.ReadByte();

                    long[] childIds = new long[childCount];
                    for(int i = 0; i < childCount; i++) {
                        childIds[i] = data.ReadLong();
                    }

                    NetworkBridge.Invoke(() => {
                        Object resource = Resources.Load(resourcePath);
                        GameObject inst = (GameObject) Object.Instantiate(resource);
                        inst.name = resource.name;

                        NetworkBehaviour net = inst.GetComponent<NetworkBehaviour>();

                        net.NetworkId = id;
                        net.HasAuthority = hasAuthority;
                        net.transform.UseNetwork = false;
                        net.transform.position = position;
                        net.transform.rotation = rotation;
                        net.transform.lossyScale = scale;

                        if(parent == null) {
                            net.transform.SetParent((Transform) null);
                        } else {
                            if(parent is NetworkBehaviour) {
                                net.transform.SetParent(((NetworkBehaviour) parent).transform);
                            } else if(parent is Transform) {
                                net.transform.SetParent((Transform) parent);
                            }
                        }

                        int i = 0;
                        foreach(Transform child in net.transform) {
                            NetworkBehaviour c = child.GetComponent<NetworkBehaviour>();
                            if(c != null) {
                                c.transform.UseNetwork = true;
                                c.HasAuthority = hasAuthority;
                                ClientRegistry.Objects[c.NetworkId = childIds[i++]] = c;
                                c.NetworkAwake();
                            }
                        }

                        net.transform.UseNetwork = true;
                        ClientRegistry.Objects[id] = net;
                        Debug.Log("Registered object with ID: " + id);
                        net.NetworkAwake();
                    });
                } else if(dType == DestroyObject) {
                    NetworkBehaviour obj = null;
                    if(!ClientRegistry.GetObject(id, out obj)) {
                        NetworkBridge.Warn("Received malformed data packet with type: " + dType + ": invalid object id: " + id);
                        return;
                    }
                    NetworkBridge.Invoke(() => Object.Destroy(obj.gameObject));
                    ClientRegistry.Objects.Remove(id);
                } else if(dType == UpdateTransform) {
                    NetworkBehaviour obj = null;
                    if(!ClientRegistry.GetObject(id, out obj)) {
                        NetworkBridge.Warn("Received malformed data packet with type: " + dType + ": invalid object id: " + id);
                        return;
                    }

                    if(obj.transform.AcceptUpdates && (!obj.HasAuthority || obj.transform.AcceptUpdatesWithAuthority)) {
                        NetworkBridge.Invoke(() => {
                            bool old = obj.transform.UseNetwork;
                            obj.transform.UseNetwork = false;
                            byte tType = data.ReadByte();
                            if(tType == Vector3Type) {
                                Vector3 vec = data.ReadVector3();
                                byte vType = data.ReadByte();
                                if(vType == 1) {
                                    obj.transform.position = vec;
                                } else if(vType == 2) {
                                    obj.transform.lossyScale = vec;
                                }
                            } else if(tType == QuaternionType) {
                                obj.transform.rotation = data.ReadQuaternion();
                            } else {
                                Debug.LogWarning("Received malformed data packet with type: " + dType + ": invalid transformation object type id: " + tType);
                            }
                            obj.transform.UseNetwork = old;
                        });
                    }
                } else if(dType == UpdateParent) {
                    NetworkBehaviour obj = null;
                    if(!ClientRegistry.GetObject(id, out obj)) {
                        NetworkBridge.Warn("Received malformed data packet with type: " + dType + ": invalid object id: " + id);
                        return;
                    }

                    if(obj.transform.AcceptUpdates && (!obj.HasAuthority || obj.transform.AcceptUpdatesWithAuthority)) {
                        NetworkBridge.Invoke(() => {
                            // ReadSceneObject needs to be called from an invocation to the bridge.
                            // is this safe?
                            object newParent = data.ReadSceneObject();
                            bool worldPositionStays = data.ReadBool();

                            bool old = obj.transform.UseNetwork;
                            obj.transform.UseNetwork = false;
                            if(newParent == null) {
                                obj.transform.SetParent((Transform) null, worldPositionStays);
                            } else {
                                if(newParent is NetworkBehaviour) {
                                    obj.transform.SetParent(((NetworkBehaviour) newParent).transform, worldPositionStays);
                                } else if(newParent is Transform) {
                                    obj.transform.SetParent((Transform) newParent, worldPositionStays);
                                }
                            }
                            obj.transform.UseNetwork = old;
                        });
                    }
                } else if(dType == UpdateField) {
                    if(!Client.Current.IsAuthenticated) {
                        data.Pointer = 0;
                        ExecuteLater.Add(data.CopyAll());
                    } else {
                        NetworkBehaviour obj = null;
                        if(!ClientRegistry.GetObject(id, out obj)) {
                            NetworkBridge.Warn("Received malformed data packet with type: " + dType + ": invalid object id: " + id);
                            return;
                        }

                        string fieldName = data.ReadString();
                        byte dataType = data.ReadByte();
                        object value = ReadObject(data, dataType);

                        NetworkBridge.Invoke(() => obj.SetLocal(fieldName, value, true));
                    }
                } else if(dType == InvokeRPC) {
                    NetworkBehaviour obj = null;
                    if(!ClientRegistry.GetObject(id, out obj)) {
                        NetworkBridge.Warn("Received malformed data packet with type: " + dType + ": invalid object id: " + id);
                        return;
                    }

                    string methodName = data.ReadString();
                    byte argCount = data.ReadByte();
                    object[] args = new object[argCount];
                    for(int i = 0; i < argCount; i++) {
                        args[i] = ReadObject(data, data.ReadByte());
                    }

                    NetworkBridge.Invoke(() => obj.InvokeLocalMethod(methodName, args));
                } else {
                    NetworkBridge.Warn("Received malformed data packet with type: " + dType + ": invalid type");
                }
            } else if(type == Management) {
                byte mType = data.ReadByte();

                if(mType == Authenticate) {
                    Client.Current.ClientId = data.ReadLong();
                    NetworkBridge.Log("Client ID: " + Client.Current.ClientId);
                    Client.Current.TickRate = data.ReadByte();
                    NetworkBridge.Log("Network tick rate: " + Client.Current.TickRate);
                } else if(mType == Disconnect) {
                    Client.Current.Socket.Close();
                } else if(mType == Complete) {
                    NetworkBridge.Log("Authentication complete.");
                    NetworkBridge.Invoke(() => {
                        Client.Current.NotifyAuthenticate();

                        foreach(ByteBuffer buf in ExecuteLater) {
                            HandleClientData(buf);
                        }

                        ExecuteLater.Clear();
                    });
                } else {
                    NetworkBridge.Warn("Received malformed management packet with type: " + mType + ": invalid type");
                }
            } else if(type == QueryDatabase) {
                byte code = data.ReadByte();
                if(code == 0) {
                    long id = data.ReadLong();
                    int len = data.ReadInt();
                    ByteBuffer ret = new ByteBuffer(len);
                    System.Array.Copy(data.Bytes, data.Pointer, ret.Bytes, 0, ret.Length);
                    NetworkBridge.Log("Query response: " + ret.Length + " bytes");

                    NetworkBridge.Invoke(() => {
                        Client.Queries[id](ret);
                        Client.Queries.Remove(id);
                    });
                } else {
                    NetworkBridge.Warn("Received malformed query packet: query unsuccessful");
                }
            } else {
                NetworkBridge.Warn("Received malformed packet with type: " + type + ": invalid type (not data or management)");
            }
        }

        public static byte GetObjectType(object value) {
            if(value is Vector3) {
                return Vector3Type;
            } else if(value is Quaternion) {
                return QuaternionType;
            } else if(value is int) {
                return IntType;
            } else if(value is float) {
                return FloatType;
            } else if(value is long) {
                return LongType;
            } else if(value is bool) {
                return BoolType;
            } else if(value is string) {
                return StringType;
            } else if(value is NetworkBehaviour) {
                return BehaviourType;
            } else if(value is byte) {
                return ByteType;
            } else {
                return ObjectType;
            }
        }

        public static void WriteObject(object value, ByteBuffer buffer) {
            if(value is Vector3) {
                buffer.WriteVector3((Vector3) value);
            } else if(value is Quaternion) {
                buffer.WriteQuaternion((Quaternion) value);
            } else if(value is int) {
                buffer.WriteInt((int) value);
            } else if(value is float) {
                buffer.WriteFloat((float) value);
            } else if(value is long) {
                buffer.WriteLong((long) value);
            } else if(value is bool) {
                buffer.WriteBool((bool) value);
            } else if(value is string) {
                buffer.WriteString((string) value);
            } else if(value is NetworkBehaviour) {
                NetworkBehaviour net = (NetworkBehaviour) value;
                buffer.WriteLong(net == null ? long.MinValue : net.NetworkId);
            } else if(value is byte) {
                buffer.WriteByte((byte) value);
            } else {
                byte[] serial = SerialUtility.Serialize(value);
                buffer.WriteByte((byte) serial.Length);
                buffer.WriteBytes(serial);
            }
        }

        public static object ReadObject(ByteBuffer data, byte dataType) {
            if(dataType == ObjectType) {
                byte dataLength = data.ReadByte();
                byte[] fieldData = data.ReadBytes(dataLength);
                return SerialUtility.Deserialize(fieldData);
            } else {
                if(dataType == Vector3Type) {
                    return data.ReadVector3();
                } else if(dataType == QuaternionType) {
                    return data.ReadQuaternion();
                } else if(dataType == IntType) {
                    return data.ReadInt();
                } else if(dataType == FloatType) {
                    return data.ReadFloat();
                } else if(dataType == LongType) {
                    return data.ReadLong();
                } else if(dataType == BoolType) {
                    return data.ReadBool();
                } else if(dataType == StringType) {
                    return data.ReadString();
                } else if(dataType == BehaviourType) {
                    long id = data.ReadLong();
                    if(id == long.MinValue) {
                        return null;
                    }
                    if(Side.IsClient) {
                        NetworkBehaviour obj = null;
                        ClientRegistry.GetObject(id, out obj);
                        return obj;
                    } else {
                        ObjectRegistration obj = null;
                        ServerRegistry.GetObject(id, out obj);
                        return obj.Object;
                    }
                } else if(dataType == ByteType) {
                    return data.ReadByte();
                } else {
                    NetworkBridge.Warn("Invalid serialization type: " + dataType);
                    return null;
                }
            }
        }
    }
}
