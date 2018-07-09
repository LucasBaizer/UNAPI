using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

namespace Network {
    public class SerialUtility {
        private static readonly BinaryFormatter Formatter = new BinaryFormatter();

        public static byte[] Serialize(object obj) {
            if(obj == null) {
                return new byte[0];
            }
            if(obj is ISerializer) {
                ISerializer s = (ISerializer) obj;
                byte[] serial = s.Serialize();
                byte[] bytes = new byte[serial.Length + 2];
                bytes[0] = 1;
                bytes[1] = s.GetSerializerId();
                Array.Copy(serial, 0, bytes, 2, serial.Length);
                return bytes;
            } else if(obj.GetType().IsArray) {
                object[] list = (object[]) obj;

                ByteBuffer buffer = new ByteBuffer(1024);
                buffer.WriteByte(2);
                buffer.WriteInt(list.Length);
                foreach(object item in list) {
                    if(item == null) {
                        buffer.WriteInt(0);
                    } else {
                        byte[] serial = Serialize(item);
                        buffer.WriteInt(serial.Length);
                        buffer.WriteBytes(serial);
                    }
                }

                buffer.Flip();

                return buffer.Bytes;
            } else if(obj is IList) {
                IList list = (IList) obj;

                ByteBuffer buffer = new ByteBuffer(1024);
                buffer.WriteByte(3);
                buffer.WriteInt(list.Count);
                foreach(object item in list) {
                    if(item == null) {
                        buffer.WriteInt(0);
                    } else {
                        byte[] serial = Serialize(item);
                        buffer.WriteInt(serial.Length);
                        buffer.WriteBytes(serial);
                    }
                }

                buffer.Flip();

                return buffer.Bytes;
            } else if(obj.GetType().IsPrimitive || obj is Vector3 || obj is Quaternion) {
                ByteBuffer buf = new ByteBuffer(18); // largest possible object here is a Quaternion, which will be header byte + object type byte + 16 data bytes
                buf.WriteByte(4);
                buf.WriteByte(NetworkData.GetObjectType(obj));
                NetworkData.WriteObject(obj, buf);

                buf.Flip();

                return buf;
            }

            MemoryStream stream = new MemoryStream();
            Formatter.Serialize(stream, obj);

            return stream.ToArray();
        }

        public static int GetSerializedSize(object obj) {
            return Serialize(obj).Length;
        }

        public static object Deserialize(byte[] obj) {
            if(obj.Length > 0) {
                if(obj[0] == 1) { // custom-serialized object
                    byte[] data = new byte[obj.Length - 2];
                    Array.Copy(obj, 2, data, 0, data.Length);
                    return DeserializerRegistry.Deserializers[obj[1]](new ByteBuffer(data));
                } else if(obj[0] == 2) { // array
                    ByteBuffer data = new ByteBuffer(obj);
                    data.Pointer = 1;

                    int length = data.ReadInt();
                    object[] result = new object[length];

                    for(int i = 0; i < length; i++) {
                        int len = data.ReadInt();
                        if(len == 0) {
                            result[i] = null;
                        } else {
                            result[i] = Deserialize(data.ReadBytes(len));
                        }
                    }

                    return result;
                } else if(obj[0] == 3) { // IList
                    ByteBuffer data = new ByteBuffer(obj);
                    data.Pointer = 1;

                    int length = data.ReadInt();
                    List<object> result = new List<object>(length);

                    for(int i = 0; i < length; i++) {
                        int len = data.ReadInt();
                        if(len == 0) {
                            result.Add(null);
                        } else {
                            result.Add(Deserialize(data.ReadBytes(len)));
                        }
                    }

                    return result;
                } else if(obj[0] == 4) {
                    ByteBuffer data = new ByteBuffer(obj);
                    data.Pointer = 1;

                    byte type = data.ReadByte();
                    return NetworkData.ReadObject(data, type);
                }

                MemoryStream stream = new MemoryStream();
                stream.Write(obj, 0, obj.Length);

                return Formatter.Deserialize(stream);
            }
            return null;
        }
    }
}