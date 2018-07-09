using System;

namespace Network {
    public class Synchronized : Attribute {
        public bool RequiresAuthority;

        public Synchronized() : this(true) {
        }

        public Synchronized(bool requiresAuthority) {
            RequiresAuthority = requiresAuthority;
        }
    }
}