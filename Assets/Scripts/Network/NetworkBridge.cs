using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace Network {
    public class NetworkBridge : MonoBehaviour {
        public delegate void Invocation();

        public static NetworkBridge Instance;

        public List<Invocation> Invocations = new List<Invocation>();
        private object Lock = new object();

        void Awake() {
            Instance = this;
            Application.runInBackground = true;
        }

        void FixedUpdate() {
            if(Invocations.Count > 0) {
                StartCoroutine("InvokeCoroutine");
            }
        }

        private IEnumerator InvokeCoroutine() {
            Invocation[] clone = new Invocation[Invocations.Count];
            for(int i = 0; i < clone.Length; i++) {
                if(i < Invocations.Count) {
                    clone[i] = Invocations[i];
                } else {
                    break;
                }
            }
            Invocations.Clear();
            foreach(Invocation invoke in clone) {
                try {
                    invoke();
                } catch(Exception e) {
                    Debug.LogError("Error in invocation: " + e.GetType().FullName + ": " + e.Message);
                    Debug.LogError(e.StackTrace);
                    PrintBase(e);
                    break;
                }
            }
            lock(Lock) {
                Monitor.PulseAll(Lock);
            }
            yield return null;
        }

        private static void PrintBase(Exception e) {
            Exception baseEx = e.GetBaseException();
            if(baseEx != null) {
                Debug.LogError("Caused by: " + baseEx.GetType().FullName + ": " + baseEx.Message);
                Debug.LogError(e.StackTrace);

                // PrintBase(baseEx);
            }
        }

        public static void Invoke(Invocation invoke) {
            Instance.Invocations.Add(invoke);
        }

        public static void AwaitInvoke(Invocation invoke) {
            Invoke(invoke);
            lock(Instance.Lock) {
                Monitor.Wait(Instance.Lock);
            }
        }

        public static void Log(object str) {
            Invoke(() => Debug.Log(str.ToString()));
        }
    }
}