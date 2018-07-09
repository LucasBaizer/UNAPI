using UnityEngine;
using System.Collections;

namespace Network {
    public static class Extensions {
        public static void SetGlobalScale(this Transform transform, Vector3 globalScale) {
            if(transform == null)
                return;

            transform.localScale = Vector3.one;
            transform.localScale = new Vector3(globalScale.x / transform.lossyScale.x, globalScale.y / transform.lossyScale.y, globalScale.z / transform.lossyScale.z);
        }
    }
}