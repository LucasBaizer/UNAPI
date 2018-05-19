using System;

namespace Network {
    [Serializable]
    public struct ResourceObject<T> where T : NetworkBehaviour {
        public T Object;
        public string Resource;

        public static implicit operator T(ResourceObject<T> obj) {
            return obj.Object;
        }

        public static implicit operator string(ResourceObject<T> obj) {
            return obj.Resource;
        }
    }
}