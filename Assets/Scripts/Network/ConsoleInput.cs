using UnityEngine;
using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.IO;

namespace Network {
    // thanks, Garry Newman
    public class ConsoleInput {
        public event Action<string> OnInputText;
        public string inputString;

        public void ClearLine() {
            Console.CursorLeft = 0;
            Console.Write(new string(' ', Console.BufferWidth));
            Console.CursorTop--;
            Console.CursorLeft = 0;
        }

        public void RedrawInputLine() {
            if(inputString.Length == 0)
                return;

            if(Console.CursorLeft > 0)
                ClearLine();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(inputString);
        }

        internal void OnBackspace() {
            if(inputString.Length < 1)
                return;

            inputString = inputString.Substring(0, inputString.Length - 1);
            RedrawInputLine();
        }

        internal void OnEscape() {
            ClearLine();
            inputString = "";
        }

        internal void OnEnter() {
            ClearLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("> " + inputString);

            var strtext = inputString;
            inputString = "";

            if(OnInputText != null) {
                OnInputText(strtext);
            }
        }

        public void Update() {
            if(!Console.KeyAvailable) {
                return;
            }
            var key = Console.ReadKey();

            if(key.Key == ConsoleKey.Enter) {
                OnEnter();
                return;
            }

            if(key.Key == ConsoleKey.Backspace) {
                OnBackspace();
                return;
            }

            if(key.Key == ConsoleKey.Escape) {
                OnEscape();
                return;
            }

            if(key.KeyChar != '\u0000') {
                inputString += key.KeyChar;
                RedrawInputLine();
                return;
            }
        }
    }
}