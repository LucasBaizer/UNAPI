using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Network {
    public class SerialUtility {
        private static readonly BinaryFormatter Formatter = new BinaryFormatter();

        public static byte[] Serialize(object obj) {
            MemoryStream stream = new MemoryStream();
            Formatter.Serialize(stream, obj);

            return stream.ToArray();
        }

        public static object Deserialize(byte[] obj) {
            MemoryStream stream = new MemoryStream();
            stream.Write(obj, 0, obj.Length);

            return Formatter.Deserialize(stream);
        }
    }
}