using UnityEngine;
using System;

namespace Network {
    // thanks, Garry Newman
    public class ServerConsole : MonoBehaviour {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        ConsoleWindow console = new ConsoleWindow();
        ConsoleInput input = new ConsoleInput();
        string strInput;

        void Awake() {
            DontDestroyOnLoad(gameObject);

            console.Initialize();
            console.SetTitle("UNAPI Server");

            input.OnInputText += OnInputText;

            Application.logMessageReceived += HandleLog;

            Debug.Log("Console Started.");
        }

        void OnInputText(string obj) {
            // ConsoleSystem.Run(obj, true);
        }

        void HandleLog(string message, string stackTrace, LogType type) {
            if(type == LogType.Warning) {
                Console.ForegroundColor = ConsoleColor.Yellow;
            } else if(type == LogType.Error) {
                Console.ForegroundColor = ConsoleColor.Red;
            } else {
                Console.ForegroundColor = ConsoleColor.White;
            }

            if(Console.CursorLeft != 0) {
                input.ClearLine();
            }

            Console.WriteLine(message);

            input.RedrawInputLine();
        }

        void Update() {
            input.Update();
        }

        void OnDestroy() {
            console.Shutdown();
        }
#endif
    }
}