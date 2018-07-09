using System.Collections.Generic;

namespace Network {
    public interface ISerializer {
        byte[] Serialize();

        byte GetSerializerId();
    }

    public class DeserializerRegistry {
        public delegate object Deserializer(ByteBuffer data);

        public static Dictionary<byte, Deserializer> Deserializers = new Dictionary<byte, Deserializer>();
    }
}