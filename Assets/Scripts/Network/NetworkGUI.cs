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

                /* GameObject inst = (GameObject) Instantiate(Resources.Load("Camera"));

                inst.transform.position = new Vector3(0f, 2f, -4.5f);
                inst.transform.eulerAngles = new Vector3(12.5f, 0f, 0f); */
            } else if(GUI.Button(new Rect(0, 30, 150, 30), "Start LAN Client")) {
                Thread c = new Thread(() => Client.Connect("127.0.0.1"));
                c.IsBackground = true;
                c.Start();

                /* Client.OnConnect += (sender, args) => {
                    NetworkBridge.Log("OnConnect");
                    Client.Current.OnAuthenticate += (sender0, args0) => {
                        NetworkBridge.Log("OnAuthenticate");
                        Client.Instantiate("Cube", new Vector3(0f, 0.5f, 0f), Quaternion.identity);
                    };
                }; */
            }
        }
    }
}