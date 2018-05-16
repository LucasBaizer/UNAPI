using UnityEngine;
using System.Collections.Generic;

namespace Network {
    public class ClientRegistry {
        public static Dictionary<long, NetworkBehaviour> Objects = new Dictionary<long, NetworkBehaviour>();

        public static bool GetObject(long id, out NetworkBehaviour obj) {
            if(Objects.ContainsKey(id)) {
                obj = Objects[id];
                return true;
            }
            obj = null;
            return false;
        }
    }
}
