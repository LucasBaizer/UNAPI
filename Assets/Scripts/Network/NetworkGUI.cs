using UnityEngine;
using System;
using System.Threading;

namespace Network {
    public class NetworkGUI : MonoBehaviour {
        void Awake() {
            if(FindObjectOfType<NetworkBridge>() == null) {
                throw new Exception("There must be a NetworkBridge object in the scene");
            }
        }

        void OnGUI() {
            if(GUI.Button(new Rect(0, 0, 150, 30), "Start LAN Server")) {
                Thread s = new Thread(() => Server.Start());
                s.IsBackground = true;
                s.Start();
            } else if(GUI.Button(new Rect(0, 30, 150, 30), "Start LAN Client")) {
                Thread c = new Thread(() => Client.Connect("127.0.0.1"));
                c.IsBackground = true;
                c.Start();
            }
        }
    }
}