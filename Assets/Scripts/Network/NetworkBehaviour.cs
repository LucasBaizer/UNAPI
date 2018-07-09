using UnityEngine;
using System;
using System.Reflection;
using System.Collections.Generic;

namespace Network {
    public class NetworkBehaviour : MonoBehaviour {
        internal Dictionary<string, object> FieldChanges = new Dictionary<string, object>();

        /// <summary>
        /// True is this is the client that instantiated the object.
        /// </summary>
        public bool HasAuthority;
        /// <summary>
        /// True if no client can have authority over the object.
        /// </summary>
        public bool ServerOnly;
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

        public void TrySetRemote(string name, object value) {
            SetRemote(name, value, false);
        }

        public void SetRemote(string name, object value, bool tcp) {
            if(IsClient) {
                // if(HasAuthority) {
                lock(Client.Out) {
                    Client.Current.WriteHeader(NetworkData.UpdateField);
                    Client.Out.WriteLong(NetworkId);
                    Client.Out.WriteBool(tcp);
                    Client.Out.WriteString(name);
                    Client.Out.WriteByte(NetworkData.GetObjectType(value));
                    NetworkData.WriteObject(value, Client.Out);
                    if(tcp) {
                        Client.Current.WriteTcp();
                    } else {
                        Client.Current.WriteUdp();
                    }
                }
                /* } else {
                    throw new System.InvalidOperationException("Cannot set field without authority");
                } */
            } else if(IsServer) {
                foreach(Socket client in ServerRegistry.Clients.Values) {
                    lock(client.Buffer) {
                        client.Buffer.WriteByte(NetworkData.Data);
                        client.Buffer.WriteByte(NetworkData.UpdateField);
                        client.Buffer.WriteLong(NetworkId);
                        client.Buffer.WriteString(name);
                        client.Buffer.WriteByte(NetworkData.GetObjectType(value));
                        NetworkData.WriteObject(value, client.Buffer);
                        if(tcp) {
                            client.WriteBufferTcp();
                        } else {
                            client.WriteBufferUdp();
                        }
                    }
                }
            }
        }

        public void SetEverywhere(string name, object value) {
            SetLocal(name, value);
            SetRemote(name, value);
        }

        public void SetLocal(string name, object value) {
            SetLocal(name, value, false);
        }

        internal FieldInfo GetLocal(string name, bool ignore, out Synchronized sync) {
            FieldInfo field = GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if(field != null) {
                Synchronized[] arr = (Synchronized[]) field.GetCustomAttributes(typeof(Synchronized), false);
                if(arr.Length == 1) {
                    if(ignore || (IsServer || !arr[0].RequiresAuthority || HasAuthority)) {
                        sync = arr[0];
                        return field;
                    } else {
                        Debug.LogWarning("Field requires authority, but there is no authority: " + name);
                    }
                } else {
                    Debug.LogWarning("Field is not marked as synchronized: " + name);
                }
            } else {
                Debug.LogWarning("Field does not exist: " + name);
            }

            sync = null;
            return null;
        }

        internal void SetLocal(string name, object value, bool ignore) {
            Synchronized sync;
            FieldInfo field = GetLocal(name, ignore, out sync);
            if(field != null) {
                field.SetValue(this, value);
                FieldChanges[name] = value;

                OnFieldSet(name, value);
            }
        }

        public void InvokeRemote(string name, params object[] args) {
            if(IsClient) {
                // if(HasAuthority) {
                lock(Client.Out) {
                    Client.Current.WriteHeader(NetworkData.InvokeRPC);
                    Client.Out.WriteLong(NetworkId);
                    Client.Out.WriteString(name);
                    Client.Out.WriteByte((byte) args.Length);
                    foreach(object arg in args) {
                        Client.Out.WriteByte(NetworkData.GetObjectType(arg));
                        NetworkData.WriteObject(arg, Client.Out);
                    }
                    Client.Current.WriteTcp();
                }
                /*} else {
                    throw new System.InvalidOperationException("Cannot invoke RPC without authority: " + name);
                }*/
            } else if(IsServer) {
                foreach(Socket client in ServerRegistry.Clients.Values) {
                    lock(client.Buffer) {
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
        }

        public void InvokeEverywhere(string name, params object[] args) {
            InvokeLocalMethod(name, args);
            InvokeRemote(name, args);
        }

        internal MethodInfo GetLocalMethod(string name, out RemoteMethod remote) {
            MethodInfo method = GetType().GetMethod(name);
            if(method == null) {
                method = GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
            }
            if(method != null) {
                RemoteMethod[] arr = (RemoteMethod[]) method.GetCustomAttributes(typeof(RemoteMethod), false);
                if(arr.Length == 1) {
                    if(IsServer || !arr[0].RequiresAuthority || HasAuthority) {
                        remote = arr[0];
                        return method;
                    } else {
                        Debug.LogWarning("Method requires authority, but there is no authority: " + name);
                    }
                } else {
                    Debug.LogWarning("Method is not marked as remote: " + name);
                }
            } else {
                Debug.LogWarning("Method does not exist: " + name);
            }

            remote = null;
            return null;
        }

        internal void InvokeLocalMethod(string name, object[] args) {
            RemoteMethod remote;
            MethodInfo method = GetLocalMethod(name, out remote);
            if(method != null) {
                try {
                    method.Invoke(this, args);
                } catch(TargetInvocationException e) {
                    Debug.LogError("Error in method: " + name);
                    throw e;
                }
            }
        }

        public virtual void NetworkAwake() {
        }

        public virtual void OnFieldSet(string name, object val) {
        }

        private string GetCaller() {
            MethodBase info = new System.Diagnostics.StackTrace().GetFrame(2).GetMethod();
            return info.DeclaringType.Name + "#" + info.Name;
        }

        public void EnsureClient() {
            if(!IsClient) {
                throw new InvalidOperationException("EnsureClient: Must be on the client: " + GetCaller());
            }
        }

        public void EnsureAuthority() {
            if(!HasAuthority) {
                throw new InvalidOperationException("EnsureAuthority: Must have client-side authority: " + GetCaller());
            }
        }

        public void EnsureServer() {
            if(!IsServer) {
                throw new InvalidOperationException("EnsureServer: Must be on the server: " + GetCaller());
            }
        }
    }
}
