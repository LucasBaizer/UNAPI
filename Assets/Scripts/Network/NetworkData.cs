using UnityEngine;

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

        public const byte ObjectType = 0;
        public const byte Vector3Type = 1;
        public const byte QuaternionType = 2;
        public const byte IntType = 3;
        public const byte FloatType = 4;
        public const byte LongType = 5;
        public const byte BoolType = 6;
        public const byte StringType = 7;
        public const byte BehaviourType = 8;

        public static void HandleServerData(ByteBuffer data, Socket client) {
            byte type = data.ReadByte();

            if(type == Data) {
                byte dType = data.ReadByte();
                long clientId = data.ReadLong();

                if(client.SpecificUdp) {
                    Socket serverClient = ServerRegistry.Clients[clientId];
                    if(serverClient.Udp == null) {
                        serverClient.Udp = client.Udp;
                        serverClient.UdpRemote = client.UdpRemote;
                    }
                }

                if(dType == InstantiateObject) {
                    string resourcePath = data.ReadString();
                    Vector3 position = data.ReadVector3();
                    Quaternion rotation = data.ReadQuaternion();
                    object parent = null;
                    NetworkBridge.AwaitInvoke(() => parent = data.ReadSceneObject());
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

                        net.NetworkId = ids[0];
                        net.transform.UseNetwork = false;
                        net.transform.position = position;
                        net.transform.rotation = rotation;

                        if(parent == null) {
                            net.transform.Detach();
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

                    foreach(long userId in ServerRegistry.Clients.Keys) {
                        Socket to = ServerRegistry.Clients[userId];
                        to.Buffer.WriteByte(Data);
                        to.Buffer.WriteByte(InstantiateObject);
                        to.Buffer.WriteLong(ids[0]);
                        to.Buffer.WriteBool(userId == clientId);
                        to.Buffer.WriteString(resourcePath);
                        to.Buffer.WriteVector3(position);
                        to.Buffer.WriteQuaternion(rotation);
                        to.Buffer.WriteSceneObject(parent);
                        to.Buffer.WriteByte(childCount);
                        foreach(long id in childIds) {
                            to.Buffer.WriteLong(id);
                        }
                        to.WriteBufferTcp();
                    }
                } else if(dType == DestroyObject) {
                    long objectId = data.ReadLong();

                    ObjectRegistration obj = null;
                    if(dType != InstantiateObject && !ServerRegistry.GetObject(objectId, out obj)) {
                        Debug.LogWarning("Received malformed data packet with type: " + dType + ": invalid object id: " + objectId);
                    } else {
                        if(obj.ClientOwner == clientId) {
                            Object.Destroy(obj.Object);
                            ServerRegistry.Objects.Remove(objectId);

                            foreach(long userId in ServerRegistry.Clients.Keys) {
                                Socket to = ServerRegistry.Clients[userId];
                                to.Buffer.WriteByte(Data);
                                to.Buffer.WriteByte(DestroyObject);
                                to.Buffer.WriteLong(objectId);
                                to.WriteBufferTcp();
                            }
                        }
                    }
                } else if(dType == UpdateTransform) {
                    long objectId = data.ReadLong();

                    ObjectRegistration reg = null;
                    if(!ServerRegistry.GetObject(objectId, out reg)) {
                        Debug.LogWarning("Received malformed data packet with type: " + dType + ": invalid object id: " + objectId);
                    } else {
                        NetworkBehaviour obj = reg.Object;
                        if(obj.transform.AcceptUpdates && (!obj.HasAuthority || obj.transform.AcceptUpdatesWithAuthority)) {
                            NetworkBridge.Invoke(() => {
                                bool old = obj.transform.UseNetwork;
                                obj.transform.UseNetwork = true;
                                byte tType = data.ReadByte();
                                if(tType == Vector3Type) {
                                    obj.transform.position = data.ReadVector3();
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
                        Debug.LogWarning("Received malformed data packet with type: " + dType + ": invalid object id: " + id);
                    } else {
                        if(reg.ClientOwner == clientId) {
                            NetworkBehaviour obj = reg.Object;
                            if(obj.transform.AcceptUpdates && (!obj.HasAuthority || obj.transform.AcceptUpdatesWithAuthority)) {
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
                            }
                        } else {
                            Debug.LogWarning("Received malformed data packet with type: " + dType + ": missing authority");
                        }
                    }
                } else if(dType == UpdateField) {
                    long id = data.ReadLong();
                    string fieldName = data.ReadString();
                    byte dataType = data.ReadByte();
                    object value = ReadObject(data, dataType);

                    ObjectRegistration reg = null;
                    if(!ServerRegistry.GetObject(id, out reg)) {
                        Debug.LogWarning("Received malformed data packet with type: " + dType + ": invalid object id: " + id);
                    } else {
                        if(reg.ClientOwner == clientId) {
                            NetworkBridge.Invoke(() => reg.Object.SetLocal(fieldName, value));

                            foreach(long userId in ServerRegistry.GetOtherClientIDs(clientId)) {
                                Socket to = ServerRegistry.Clients[userId];
                                to.Buffer.WriteByte(Data);
                                to.Buffer.WriteByte(UpdateField);
                                to.Buffer.WriteLong(id);
                                to.Buffer.WriteString(fieldName);
                                to.Buffer.WriteByte(dataType);
                                WriteObject(value, to.Buffer);
                                to.WriteBufferTcp();
                            }
                        } else {
                            Debug.LogWarning("Received malformed data packet with type: " + dType + ": missing authority");
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
                        Debug.LogWarning("Received malformed data packet with type: " + dType + ": invalid object id: " + id);
                    } else {
                        if(reg.ClientOwner == clientId) {
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
                            Debug.LogWarning("Received malformed data packet with type: " + dType + ": missing authority");
                        }
                    }
                } else {
                    Debug.LogWarning("Received malformed data packet with type: " + dType + ": invalid type");
                }
            } else if(type == Management) {
                byte mType = data.ReadByte();

                if(mType == Authenticate) {
                    long clientId = Server.GenerateId();
                    client.Buffer.WriteByte(Management);
                    client.Buffer.WriteByte(Authenticate);
                    client.Buffer.WriteLong(clientId);
                    client.Buffer.WriteByte((byte) Server.Current.Config.TickRate);
                    client.WriteBufferTcp();

                    ServerRegistry.Clients[clientId] = client;
                    NetworkBridge.Log("Registered client with ID: " + clientId);

                    NetworkBridge.Invoke(() => {
                        foreach(long id in ServerRegistry.Objects.Keys) {
                            ObjectRegistration reg = ServerRegistry.Objects[id];

                            if(!reg.IsChild) {
                                client.Buffer.WriteByte(Data);
                                client.Buffer.WriteByte(InstantiateObject);
                                client.Buffer.WriteLong(id);
                                client.Buffer.WriteBool(false);
                                client.Buffer.WriteString(reg.ResourcePath);
                                client.Buffer.WriteVector3(reg.Object.transform.position);
                                client.Buffer.WriteQuaternion(reg.Object.transform.rotation);

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

                        NetworkBridge.Log("Sent all existing objects to client.");
                    });
                } else {
                    Debug.LogWarning("Received malformed management packet with type: " + mType + ": invalid type");
                }
            } else {
                Debug.LogWarning("Received malformed packet with type: " + type + ": invalid type (not data or management");
            }
        }

        public static void HandleClientData(ByteBuffer data) {
            byte type = data.ReadByte();

            if(type == Data) {
                byte dType = data.ReadByte();
                long id = data.ReadLong();

                NetworkBehaviour obj = null;
                if(dType != InstantiateObject && !ClientRegistry.GetObject(id, out obj)) {
                    Debug.LogWarning("Received malformed data packet with type: " + dType + ": invalid object id: " + id);
                } else {
                    if(dType == InstantiateObject) {
                        bool hasAuthority = data.ReadBool();
                        string resourcePath = data.ReadString();
                        Vector3 position = data.ReadVector3();
                        Quaternion rotation = data.ReadQuaternion();
                        object parent = null;
                        NetworkBridge.AwaitInvoke(() => parent = data.ReadSceneObject());
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

                            if(parent == null) {
                                net.transform.Detach();
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
                            net.NetworkAwake();
                        });
                    } else if(dType == DestroyObject) {
                        Object.Destroy(obj.gameObject);
                        ClientRegistry.Objects.Remove(id);
                    } else if(dType == UpdateTransform) {
                        if(obj.transform.AcceptUpdates && (!obj.HasAuthority || obj.transform.AcceptUpdatesWithAuthority)) {
                            NetworkBridge.Invoke(() => {
                                bool old = obj.transform.UseNetwork;
                                obj.transform.UseNetwork = false;
                                byte tType = data.ReadByte();
                                if(tType == Vector3Type) {
                                    obj.transform.position = data.ReadVector3();
                                } else if(tType == QuaternionType) {
                                    obj.transform.rotation = data.ReadQuaternion();
                                } else {
                                    Debug.LogWarning("Received malformed data packet with type: " + dType + ": invalid transformation object type id: " + tType);
                                }
                                obj.transform.UseNetwork = old;
                            });
                        }
                    } else if(dType == UpdateParent) {
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
                        string fieldName = data.ReadString();
                        byte dataType = data.ReadByte();
                        object value = ReadObject(data, dataType);

                        NetworkBridge.Invoke(() => obj.SetLocal(fieldName, value));
                    } else if(dType == InvokeRPC) {
                        string methodName = data.ReadString();
                        byte argCount = data.ReadByte();
                        object[] args = new object[argCount];
                        for(int i = 0; i < argCount; i++) {
                            args[i] = ReadObject(data, data.ReadByte());
                        }

                        NetworkBridge.Invoke(() => obj.InvokeLocalMethod(methodName, args));
                    } else {
                        Debug.LogWarning("Received malformed data packet with type: " + dType + ": invalid type");
                    }
                }
            } else if(type == Management) {
                byte mType = data.ReadByte();

                if(mType == Authenticate) {
                    Client.Current.ClientId = data.ReadLong();
                    NetworkBridge.Log("Client ID: " + Client.Current.ClientId);
                    Client.Current.TickRate = data.ReadByte();
                    NetworkBridge.Log("Network tick rate: " + Client.Current.TickRate);
                    NetworkBridge.Invoke(() => Client.Current.NotifyAuthenticate());
                } else if(mType == Disconnect) {
                    Client.Current.Socket.Close();
                } else {
                    Debug.LogWarning("Received malformed management packet with type: " + mType + ": invalid type");
                }
            } else {
                Debug.LogWarning("Received malformed packet with type: " + type + ": invalid type (not data or management)");
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
                buffer.WriteLong(((NetworkBehaviour) value).NetworkId);
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
                    if(Side.IsClient) {
                        NetworkBehaviour obj = null;
                        ClientRegistry.GetObject(id, out obj);
                        return obj;
                    } else {
                        ObjectRegistration obj = null;
                        ServerRegistry.GetObject(id, out obj);
                        return obj.Object;
                    }
                } else {
                    Debug.LogWarning("Invalid serialization type: " + dataType);
                    return null;
                }
            }
        }
    }
}