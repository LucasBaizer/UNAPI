using Network;
using UnityEngine;

public class StartScript : MonoBehaviour {
    void Awake() {
        Server.OnStart += (server, args) => {
            GameObject inst = (GameObject) Instantiate(Resources.Load("Camera"));

            inst.transform.position = new Vector3(0f, 2f, -4.5f);
            inst.transform.eulerAngles = new Vector3(12.5f, 0f, 0f);
        };

        Client.OnConnect += (sender, args) => {
            Debug.Log("OnConnect");
            Client.Current.OnAuthenticate += (sender0, args0) => {
                Debug.Log("OnAuthenticate");
                Client.Instantiate("Cube", new Vector3(0f, 0.5f, 0f), Quaternion.identity);
            };
        };
    }
}
