using System;

namespace Network {
    public class RemoteMethod : Attribute {
        public bool RequiresAuthority;

        public RemoteMethod() : this(true) {
        }

        public RemoteMethod(bool requiresAuthority) {
            RequiresAuthority = requiresAuthority;
        }
    }
}