using UnityEngine;
using Network;

public class CubeController : NetworkBehaviour {
    [Synchronized]
    private int Value = 0;

    public override void NetworkAwake() {
        if(HasAuthority) {
            GameObject inst = (GameObject) Instantiate(Resources.Load("Camera"));

            inst.transform.parent = this.transform;
            inst.transform.localPosition = new Vector3(0f, 2f, -4.5f);
            inst.transform.localEulerAngles = new Vector3(12.5f, 0f, 0f);

            Debug.Log("Instantiated camera.");

            SetRemote("Value", 5);
            InvokeRemote("SomethingRemote");
        }
    }

    [RemoteMethod]
    private void SomethingRemote() {
        Debug.Log("Value: " + Value);
        Debug.Log("Side: " + Side.NetworkSide);
        InvokeRemote("SomethingRemoteParameterized", Value - 1);

        NetworkBehaviour instance = Server.Instantiate("GenericCube", new Vector3(0f, 2f, 0f), Quaternion.identity, GameObject.Find("Plane").transform);
    }

    [RemoteMethod]
    private void SomethingRemoteParameterized(int value) {
        Debug.Log("Returned Value: " + value);
    }

    void Update() {
        if(HasAuthority) {
            if(Input.GetKey(KeyCode.W)) {
                transform.position += transform.forward / 10f;
            } else if(Input.GetKey(KeyCode.S)) {
                transform.position += -transform.forward / 10f;
            }
            if(Input.GetKey(KeyCode.A)) {
                transform.localEulerAngles += new Vector3(0f, -1f, 0f);
            } else if(Input.GetKey(KeyCode.D)) {
                transform.localEulerAngles += new Vector3(0f, 1f, 0f);
            }
        }
    }
}
