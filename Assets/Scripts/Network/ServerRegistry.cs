using System.Collections.Generic;

namespace Network {
    public class ServerRegistry {
        public static Dictionary<long, Socket> Clients = new Dictionary<long, Socket>();
        public static OrderedDictionary<long, ObjectRegistration> Objects = new OrderedDictionary<long, ObjectRegistration>();

        public static IEnumerable<Socket> GetOtherClients(long id) {
            lock(Clients) {
                foreach(long otherId in Clients.Keys) {
                    if(otherId != id) {
                        yield return Clients[otherId];
                    }
                }
            }
        }

        public static IEnumerable<long> GetClientIDs() {
            lock(Clients) {
                List<long> keys = new List<long>();
                foreach(long key in Clients.Keys) {
                    keys.Add(key);
                }
                return keys;
            }
        }

        public static Socket GetClient(long id) {
            lock(Clients) {
                return Clients[id];
            }
        }

        public static IEnumerable<long> GetOtherClientIDs(long id) {
            lock(ServerRegistry.Clients) {
                foreach(long otherId in Clients.Keys) {
                    if(otherId != id) {
                        yield return otherId;
                    }
                }
            }
        }

        public static bool GetObject(long id, out ObjectRegistration obj) {
            if(Objects.ContainsKey(id)) {
                obj = Objects[id];
                return true;
            }
            obj = null;
            return false;
        }
    }

    public class ObjectRegistration {
        public NetworkBehaviour Object;
        public long ClientOwner;
        public string ResourcePath;
        public bool IsChild;
        public long[] ChildIds;

        public ObjectRegistration(NetworkBehaviour obj, long clientOwner, string resourcePath, bool isChild, long[] childIds) {
            Object = obj;
            ClientOwner = clientOwner;
            ResourcePath = resourcePath;
            IsChild = isChild;
            ChildIds = childIds;
        }
    }
}
