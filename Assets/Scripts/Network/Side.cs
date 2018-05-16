namespace Network {
    public class Side {
        public const int Unset = -1;
        public const int Client = 0;
        public const int Server = 1;

        public static int NetworkSide = -1;

        public static bool IsUnset {
            get {
                return NetworkSide == Unset;
            }
        }

        public static bool IsClient {
            get {
                return NetworkSide == Client;
            }
        }

        public static bool IsServer {
            get {
                return NetworkSide == Server;
            }
        }

        public static int TickRate {
            get {
                return IsClient ? Network.Client.Current.TickRate : Network.Server.Current.Config.TickRate;
            }
        }
    }
}