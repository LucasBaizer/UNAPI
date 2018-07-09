using System.Collections.Generic;

namespace Network {
    public class ServerDatabase {
        public delegate byte[] Supplier(object[] args);

        private static Dictionary<string, Supplier> Suppliers = new Dictionary<string, Supplier>();

        public static void Register(string name, Supplier supplier) {
            Suppliers[name] = supplier;

            UnityEngine.Debug.Log("Registered supplier with name: " + name);
        }

        public static bool IsRegistered(string name) {
            return Suppliers.ContainsKey(name);
        }

        public static byte[] Request(string name, params object[] args) {
            return Suppliers[name](args);
        }
    }
}