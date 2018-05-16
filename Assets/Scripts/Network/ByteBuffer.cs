using UnityEngine;
using System;
using System.Text;

namespace Network {
    public class ByteBuffer {
        public byte[] Bytes;
        public int Pointer = 0;

        public int Length {
            get {
                return Bytes.Length;
            }
        }

        public ByteBuffer(int size) {
            Bytes = new byte[size];
        }

        public ByteBuffer(byte[] buffer) {
            Bytes = buffer;
        }

        public ByteBuffer Copy() {
            byte[] newBytes = new byte[Pointer];
            Array.Copy(Bytes, newBytes, Pointer);

            return new ByteBuffer(newBytes);
        }

        /// <summary>
        /// Cuts off all bytes after the pointer.
        /// </summary>
        public void Trim() {
            byte[] newBytes = new byte[Pointer];

            Array.Copy(Bytes, newBytes, Pointer);

            Bytes = newBytes;
        }

        /// <summary>
        /// Resets the buffer's pointer to 0, while keeping all the contents the same.
        /// </summary>
        public void Reset() {
            Pointer = 0;
        }

        /// <summary>
        /// Trims and then resets the buffer.
        /// </summary>
        public void Flip() {
            Trim();
            Reset();
        }

        /// <summary>
        /// Sets all the bytes in the buffer to 0. Pointer remains the same.
        /// </summary>
        public void Clear() {
            Array.Clear(Bytes, 0, Bytes.Length);
        }

        public void CopyTo(ByteBuffer buffer) {
            byte[] newBytes = new byte[Pointer];
            Array.Copy(Bytes, newBytes, Pointer);

            buffer.WriteBytes(newBytes);
        }

        public void WriteByte(byte b) {
            if(Pointer == Length) {
                throw new IndexOutOfRangeException("Pointer exceeds length: " + Pointer);
            }
            Bytes[Pointer++] = b;
        }

        public byte ReadByte() {
            if(Pointer == Length) {
                throw new IndexOutOfRangeException("Pointer exceeds length: " + Pointer);
            }
            return Bytes[Pointer++];
        }

        public void WriteBytes(byte[] bytes) {
            WriteBytes(bytes, 0, bytes.Length);
        }

        public void WriteBytes(byte[] bytes, int offset, int length) {
            for(int i = offset; i < length; i++) {
                WriteByte(bytes[i]);
            }
        }

        public byte[] ReadBytes(int length) {
            byte[] buf = new byte[length];

            for(int i = 0; i < length; i++) {
                buf[i] = ReadByte();
            }

            return buf;
        }

        public void WriteInt(int i) {
            WriteBytes(BitConverter.GetBytes(i));
        }

        public int ReadInt() {
            return BitConverter.ToInt32(ReadBytes(4), 0);
        }

        public void WriteFloat(float f) {
            WriteBytes(BitConverter.GetBytes(f));
        }

        public float ReadFloat() {
            return BitConverter.ToSingle(ReadBytes(4), 0);
        }

        public void WriteLong(long l) {
            WriteBytes(BitConverter.GetBytes(l));
        }

        public long ReadLong() {
            return BitConverter.ToInt64(ReadBytes(8), 0);
        }

        public void WriteVector3(Vector3 vector) {
            WriteFloat(vector.x);
            WriteFloat(vector.y);
            WriteFloat(vector.z);
        }

        public Vector3 ReadVector3() {
            Vector3 vector = new Vector3();
            vector.x = ReadFloat();
            vector.y = ReadFloat();
            vector.z = ReadFloat();
            return vector;
        }

        public void WriteQuaternion(Quaternion quat) {
            WriteFloat(quat.x);
            WriteFloat(quat.y);
            WriteFloat(quat.z);
            WriteFloat(quat.w);
        }

        public Quaternion ReadQuaternion() {
            Quaternion quat = new Quaternion();
            quat.x = ReadFloat();
            quat.y = ReadFloat();
            quat.z = ReadFloat();
            quat.w = ReadFloat();
            return quat;
        }

        public void WriteString(string str) {
            WriteByte((byte) str.Length);
            WriteBytes(Encoding.UTF8.GetBytes(str));
        }

        public string ReadString() {
            byte length = ReadByte();
            return Encoding.UTF8.GetString(ReadBytes(length), 0, length);
        }

        public void WriteBool(bool b) {
            WriteByte((byte) (b ? 1 : 0));
        }

        public bool ReadBool() {
            return ReadByte() == 1;
        }
    }
}