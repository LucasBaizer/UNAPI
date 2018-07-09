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
        private Thread UnityThread;
        private object Lock = new object();
        private object AddLock = new object();

        void Awake() {
            Instance = this;
            UnityThread = Thread.CurrentThread;
            Application.runInBackground = true;
        }

        void FixedUpdate() {
            if(Invocations.Count > 0) {
                StartCoroutine("InvokeCoroutine");
            }
        }

        private IEnumerator InvokeCoroutine() {
            Invocation[] clone;

            lock(AddLock) {
                clone = new Invocation[Invocations.Count];
                for(int i = 0; i < clone.Length; i++) {
                    clone[i] = Invocations[i];
                }
                Invocations.Clear();
            }

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

        public static bool Invoke(Invocation invoke) {
            if(Thread.CurrentThread != Instance.UnityThread) {
                lock(Instance.AddLock) {
                    Instance.Invocations.Add(invoke);
                }
                return true;
            } else {
                invoke();
                return false;
            }
        }

        public static void AwaitInvoke(Invocation invoke) {
            if(Invoke(invoke)) {
                lock(Instance.Lock) {
                    Monitor.Wait(Instance.Lock);
                }
            }
        }

        public static void Log(object str) {
            Invoke(() => Debug.Log(str == null ? "null" : str.ToString()));
        }

        public static void Warn(object str) {
            Invoke(() => Debug.LogWarning(str == null ? "null" : str.ToString()));
        }
    }
}