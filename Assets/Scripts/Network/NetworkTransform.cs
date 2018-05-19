using System;
using System.Collections;
using UnityEngine;

namespace Network {
    public class NetworkTransform : IEnumerable {
        public Transform Transform {
            get; set;
        }
        public bool UseNetwork;
        public bool AcceptUpdates = true;
        public bool AcceptUpdatesWithAuthority = false;
        private NetworkBehaviour Net;

        public NetworkTransform(NetworkBehaviour net, Transform transform) {
            Transform = transform;
            Net = net;
        }

        private void WritePosition(Vector3 position) {
            if(Side.IsClient && Net.HasAuthority) {
                Client.Current.PushUpdatePosition(Net, position);
            } else if(Side.IsServer) {
                foreach(long userId in ServerRegistry.Clients.Keys) {
                    Socket to = ServerRegistry.Clients[userId];
                    to.Buffer.WriteByte(NetworkData.Data);
                    to.Buffer.WriteByte(NetworkData.UpdateTransform);
                    to.Buffer.WriteLong(Net.NetworkId);
                    to.Buffer.WriteByte(NetworkData.Vector3Type);
                    to.Buffer.WriteVector3(position);
                    to.WriteBufferUdp();
                }
            }
        }

        private void WriteRotation(Quaternion rotation) {
            if(Side.IsClient && Net.HasAuthority) {
                Client.Current.PushUpdatePosition(Net, rotation);
            } else if(Side.IsServer) {
                foreach(long userId in ServerRegistry.Clients.Keys) {
                    Socket to = ServerRegistry.Clients[userId];
                    to.Buffer.WriteByte(NetworkData.Data);
                    to.Buffer.WriteByte(NetworkData.UpdateTransform);
                    to.Buffer.WriteLong(Net.NetworkId);
                    to.Buffer.WriteByte(NetworkData.QuaternionType);
                    to.Buffer.WriteQuaternion(rotation);
                    to.WriteBufferUdp();
                }
            }
        }

        public Vector3 forward {
            get {
                return Transform.forward;
            }
        }

        public Vector3 right {
            get {
                return Transform.right;
            }
        }

        public Vector3 up {
            get {
                return Transform.up;
            }
        }

        public Vector3 eulerAngles {
            get {
                return rotation.eulerAngles;
            }

            set {
                rotation = Quaternion.Euler(value);
            }
        }

        public Vector3 localEulerAngles {
            get {
                return localRotation.eulerAngles;
            }

            set {
                localRotation = Quaternion.Euler(value);
            }
        }

        public Vector3 position {
            get {
                return Transform.position;
            }

            set {
                Transform.position = value;

                if(UseNetwork) {
                    WritePosition(position);
                }
            }
        }

        public Vector3 localPosition {
            get {
                return Transform.localPosition;
            }

            set {
                Transform.localPosition = value;

                if(UseNetwork) {
                    WritePosition(position);
                }
            }
        }

        public Quaternion rotation {
            get {
                return Transform.rotation;
            }

            set {
                Transform.rotation = value;

                if(UseNetwork) {
                    WriteRotation(rotation);
                }
            }
        }

        public Quaternion localRotation {
            get {
                return Transform.localRotation;
            }

            set {
                Transform.localRotation = value;

                if(UseNetwork) {
                    WriteRotation(rotation);
                }
            }
        }

        public Transform parent {
            get {
                return Transform.parent;
            }
            set {
                SetParent(value);
            }
        }

        public Vector3 localScale {
            get {
                return Transform.localScale;
            }
            set {
                Transform.localScale = value;
            }
        }

        public Transform Current {
            get {
                throw new NotImplementedException();
            }
        }

        public Vector3 TransformPoint(Vector3 vec) {
            return Transform.TransformPoint(vec);
        }

        public Vector3 InverseTransformPoint(Vector3 vec) {
            return Transform.InverseTransformPoint(vec);
        }

        public Transform Find(string name) {
            return Transform.Find(name);
        }

        public void Detach() {
            Transform.SetParent(null);

            if(!UseNetwork) {
                return;
            }

            Client.Current.WriteHeader(NetworkData.UpdateParent);
            Client.Out.WriteLong(long.MinValue);
            Client.Current.WriteTcp();
        }

        public void SetParent(NetworkTransform parent) {
            SetParent(parent.Transform);
        }

        public void SetParent(Transform parent) {
            SetParent(parent, true);
        }

        public void SetParent(NetworkTransform parent, bool worldPositionStays) {
            SetParent(parent.Transform, worldPositionStays);
        }

        public void SetParent(Transform parent, bool worldPositionStays) {
            if(parent == null) {
                Detach();
            }

            Transform.SetParent(parent, worldPositionStays);

            NetworkBehaviour net = parent.GetComponent<NetworkBehaviour>();
            if(net == null) {
                // throw new InvalidOperationException("Cannot set the parent of a networked object to a non-networked object");
                return;
            }

            if(!UseNetwork) {
                return;
            }

            if(Side.IsClient) {
                if(Net.HasAuthority) {
                    Client.Current.WriteHeader(NetworkData.UpdateParent);
                    Client.Out.WriteLong(Net.NetworkId);
                    Client.Out.WriteLong(net.NetworkId);
                    Client.Out.WriteBool(worldPositionStays);
                    Client.Current.WriteTcp();
                } else {
                    throw new InvalidOperationException("Cannot set parent without authority");
                }
            } else if(Side.IsServer) {
                foreach(Socket client in ServerRegistry.Clients.Values) {
                    client.Buffer.WriteByte(NetworkData.Data);
                    client.Buffer.WriteByte(NetworkData.UpdateParent);
                    client.Buffer.WriteLong(Net.NetworkId);
                    client.Buffer.WriteLong(net.NetworkId);
                    client.Buffer.WriteBool(worldPositionStays);
                    client.WriteBufferTcp();
                }
            }
        }

        public void SetPositionRotation(Vector3 position, Quaternion rotation) {
            bool old = UseNetwork;
            UseNetwork = false;
            this.position = position;
            this.rotation = rotation;
            UseNetwork = old;

            if(!UseNetwork) {
                return;
            }

            Client.Current.WriteHeader(NetworkData.UpdateTransform);
            Client.Out.WriteVector3(this.position);
            Client.Out.WriteQuaternion(this.rotation);
            Client.Current.WriteUdp();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return Transform.GetEnumerator();
        }

        public static implicit operator Transform(NetworkTransform net) {
            return net.Transform;
        }
    }
}