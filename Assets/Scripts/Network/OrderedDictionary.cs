using System.Collections.Specialized;
using System.Collections.Generic;
using System;
using System.Collections;
using System.Linq;

namespace Network {
    public class OrderedDictionary<T, K> {
        public OrderedDictionary UnderlyingCollection = new OrderedDictionary();

        public K this[T key] {
            get {
                return (K) UnderlyingCollection[key];
            }
            set {
                UnderlyingCollection[key] = value;
            }
        }

        public K this[int index] {
            get {
                return (K) UnderlyingCollection[index];
            }
            set {
                UnderlyingCollection[index] = value;
            }
        }
        public ICollection<T> Keys {
            get {
                return UnderlyingCollection.Keys.OfType<T>().ToList();
            }
        }
        public ICollection<K> Values {
            get {
                return UnderlyingCollection.Values.OfType<K>().ToList();
            }
        }
        public bool IsReadOnly {
            get {
                return UnderlyingCollection.IsReadOnly;
            }
        }
        public int Count {
            get {
                return UnderlyingCollection.Count;
            }
        }
        public IDictionaryEnumerator GetEnumerator() {
            return UnderlyingCollection.GetEnumerator();
        }
        public void Insert(int index, T key, K value) {
            UnderlyingCollection.Insert(index, key, value);
        }
        public void RemoveAt(int index) {
            UnderlyingCollection.RemoveAt(index);
        }
        public bool Contains(T key) {
            return UnderlyingCollection.Contains(key);
        }
        public void Add(T key, K value) {
            UnderlyingCollection.Add(key, value);
        }
        public void Clear() {
            UnderlyingCollection.Clear();
        }
        public void Remove(T key) {
            UnderlyingCollection.Remove(key);
        }
        public void CopyTo(Array array, int index) {
            UnderlyingCollection.CopyTo(array, index);
        }
        public bool ContainsKey(T key) {
            return UnderlyingCollection.Contains(key);
        }
    }
}