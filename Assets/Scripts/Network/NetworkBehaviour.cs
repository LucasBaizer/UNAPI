using UnityEngine;
using System.Reflection;

namespace Network {
    public class NetworkBehaviour : MonoBehaviour {
        /// <summary>
        /// True is this is the client that instantiated the object.
        /// </summary>
        public bool HasAuthority;
        [HideInInspector]
        public new NetworkTransform transform;
        public long NetworkId;

        public bool IsClient {
            get {
                return Side.IsClient;
            }
        }
        public bool IsServer {
            get {
                return Side.IsServer;
            }
        }

        public virtual void Awake() {
            transform = new NetworkTransform(this, base.transform);
            transform.UseNetwork = false;

            if(base.transform.parent != null)
                transform.parent = base.transform.parent;

            transform.position = base.transform.position;
            transform.rotation = base.transform.rotation;
            transform.localScale = base.transform.localScale;

            transform.UseNetwork = true;
        }

        public void SetRemote(string name, object value) {
            SetRemote(name, value, true);
        }

        public void SetRemote(string name, object value, bool local) {
            if(local) {
                UpdateLocalField(name, value);
            }

            if(IsClient) {
                if(HasAuthority) {
                    Client.Current.WriteHeader(NetworkData.UpdateField);
                    Client.Out.WriteLong(NetworkId);
                    Client.Out.WriteString(name);
                    Client.Out.WriteByte(NetworkData.GetObjectType(value));
                    NetworkData.WriteObject(value, Client.Out);
                    Client.Current.WriteTcp();
                } else {
                    throw new System.InvalidOperationException("Cannot set field without authority");
                }
            } else if(IsServer) {
                foreach(Socket client in ServerRegistry.Clients.Values) {
                    client.Buffer.WriteByte(NetworkData.Data);
                    client.Buffer.WriteByte(NetworkData.UpdateField);
                    client.Buffer.WriteLong(NetworkId);
                    client.Buffer.WriteString(name);
                    client.Buffer.WriteByte(NetworkData.GetObjectType(value));
                    NetworkData.WriteObject(value, client.Buffer);
                    client.WriteBufferTcp();
                }
            }
        }

        public void UpdateLocalField(string name, object value) {
            FieldInfo field = GetType().GetField(name);
            if(field == null) {
                field = GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            }
            if(field != null) {
                Synchronized[] arr = (Synchronized[]) field.GetCustomAttributes(typeof(Synchronized), false);
                if(arr.Length == 1) {
                    field.SetValue(this, value);
                } else {
                    Debug.LogWarning("Field is not marked as synchronized: " + name);
                }
            } else {
                Debug.LogWarning("Field does not exist: " + name);
            }
        }

        public void InvokeRemote(string name, params object[] args) {
            if(IsClient) {
                if(HasAuthority) {
                    Client.Current.WriteHeader(NetworkData.InvokeRPC);
                    Client.Out.WriteLong(NetworkId);
                    Client.Out.WriteString(name);
                    Client.Out.WriteByte((byte) args.Length);
                    foreach(object arg in args) {
                        Client.Out.WriteByte(NetworkData.GetObjectType(arg));
                        NetworkData.WriteObject(arg, Client.Out);
                    }
                    Client.Current.WriteTcp();
                } else {
                    throw new System.InvalidOperationException("Cannot invoke RPC without authority: " + name);
                }
            } else if(IsServer) {
                foreach(Socket client in ServerRegistry.Clients.Values) {
                    client.Buffer.WriteByte(NetworkData.Data);
                    client.Buffer.WriteByte(NetworkData.InvokeRPC);
                    client.Buffer.WriteLong(NetworkId);
                    client.Buffer.WriteString(name);
                    client.Buffer.WriteByte((byte) args.Length);
                    foreach(object arg in args) {
                        client.Buffer.WriteByte(NetworkData.GetObjectType(arg));
                        NetworkData.WriteObject(arg, client.Buffer);
                    }
                    client.WriteBufferTcp();
                }
            }
        }

        public void InvokeLocalMethod(string name, object[] args) {
            MethodInfo method = GetType().GetMethod(name);
            if(method == null) {
                method = GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
            }
            if(method != null) {
                RemoteMethod[] arr = (RemoteMethod[]) method.GetCustomAttributes(typeof(RemoteMethod), false);
                if(arr.Length == 1) {
                    method.Invoke(this, args);
                }
            }
        }

        public virtual void NetworkAwake() {
        }
    }
}
