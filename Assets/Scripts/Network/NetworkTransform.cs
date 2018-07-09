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
        public bool RequiresAuthority = true;
        private NetworkBehaviour Net;

        public NetworkTransform(NetworkBehaviour net, Transform transform) {
            Transform = transform;
            Net = net;
        }

        private void WritePosition(Vector3 position) {
            if(Side.IsClient && (!RequiresAuthority || Net.HasAuthority)) {
                Client.Current.PushUpdateTransform(Net, position, 1);
            } else if(Side.IsServer) {
                foreach(Socket to in ServerRegistry.Clients.Values) {
                    lock(to.Buffer) {
                        to.Buffer.WriteByte(NetworkData.Data);
                        to.Buffer.WriteByte(NetworkData.UpdateTransform);
                        to.Buffer.WriteLong(Net.NetworkId);
                        to.Buffer.WriteByte(NetworkData.Vector3Type);
                        to.Buffer.WriteVector3(position);
                        to.Buffer.WriteByte(1);
                        to.WriteBufferUdp();
                    }
                }
            }
        }

        private void WriteScale(Vector3 position) {
            if(Side.IsClient && (!RequiresAuthority || Net.HasAuthority)) {
                Client.Current.PushUpdateTransform(Net, position, 2);
            } else if(Side.IsServer) {
                foreach(Socket to in ServerRegistry.Clients.Values) {
                    lock(to.Buffer) {
                        to.Buffer.WriteByte(NetworkData.Data);
                        to.Buffer.WriteByte(NetworkData.UpdateTransform);
                        to.Buffer.WriteLong(Net.NetworkId);
                        to.Buffer.WriteByte(NetworkData.Vector3Type);
                        to.Buffer.WriteVector3(position);
                        to.Buffer.WriteByte(2);
                        to.WriteBufferUdp();
                    }
                }
            }
        }

        private void WriteRotation(Quaternion rotation) {
            if(Side.IsClient && (!RequiresAuthority || Net.HasAuthority)) {
                Client.Current.PushUpdateTransform(Net, rotation, 0);
            } else if(Side.IsServer) {
                foreach(Socket to in ServerRegistry.Clients.Values) {
                    lock(to.Buffer) {
                        to.Buffer.WriteByte(NetworkData.Data);
                        to.Buffer.WriteByte(NetworkData.UpdateTransform);
                        to.Buffer.WriteLong(Net.NetworkId);
                        to.Buffer.WriteByte(NetworkData.QuaternionType);
                        to.Buffer.WriteQuaternion(rotation);
                        to.WriteBufferUdp();
                    }
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

                if(UseNetwork) {
                    WriteScale(lossyScale);
                }
            }
        }

        public Vector3 lossyScale {
            get {
                return Transform.lossyScale;
            }
            set {
                Transform.SetGlobalScale(value);

                if(UseNetwork) {
                    WriteScale(value);
                }
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

        /* public void Detach() {
            Transform.SetParent(null);

            if(!UseNetwork) {
                return;
            }

            Client.Current.WriteHeader(NetworkData.UpdateParent);
            Client.Out.WriteLong(long.MinValue);
            Client.Current.WriteTcp();
        } */

        public void SetParent(NetworkTransform parent) {
            SetParent(parent == null ? null : parent.Transform);
        }

        public void SetParent(Transform parent) {
            SetParent(parent, true);
        }

        public void SetParent(NetworkTransform parent, bool worldPositionStays) {
            SetParent(parent.Transform, worldPositionStays);
        }

        public void SetParent(Transform parent, bool worldPositionStays) {
            SetParent(parent, worldPositionStays, true);
        }

        public void SetParent(Transform parent, bool worldPositionStays, bool here) {
            if(here) {
                /* if(parent == null) {
                    Detach();
                } */

                Transform.SetParent(parent, worldPositionStays);
            }

            if(!UseNetwork) {
                return;
            }

            NetworkBehaviour net = parent == null ? null : parent.GetComponent<NetworkBehaviour>();
            object sceneObject = net;
            if(net == null) {
                sceneObject = parent;
            }

            if(Side.IsClient) {
                if(!RequiresAuthority || Net.HasAuthority) {
                    Client.Current.WriteHeader(NetworkData.UpdateParent);
                    Client.Out.WriteLong(Net.NetworkId);
                    Client.Out.WriteSceneObject(sceneObject);
                    Client.Out.WriteBool(worldPositionStays);
                    Client.Current.WriteTcp();
                } else {
                    throw new InvalidOperationException("Cannot set parent without authority");
                }
            } else if(Side.IsServer) {
                foreach(Socket client in ServerRegistry.Clients.Values) {
                    lock(client.Buffer) {
                        client.Buffer.WriteByte(NetworkData.Data);
                        client.Buffer.WriteByte(NetworkData.UpdateParent);
                        client.Buffer.WriteLong(Net.NetworkId);
                        client.Buffer.WriteSceneObject(sceneObject);
                        client.Buffer.WriteBool(worldPositionStays);
                        client.WriteBufferTcp();
                    }
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