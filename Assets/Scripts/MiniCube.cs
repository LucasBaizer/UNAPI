using UnityEngine;
using Network;

public class MiniCube : NetworkBehaviour {
    public override void NetworkAwake() {
        base.NetworkAwake();

        Debug.Log("MiniCube: " + this.NetworkId);
    }
}
