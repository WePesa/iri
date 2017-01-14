﻿namespace iri.utils
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    public class ConcurrentOrderedList<T> : IList<T>, ICollection<T>, IList, ICollection, IReadOnlyList<T>, IReadOnlyCollection<T>, IEnumerable<T>, IEnumerable
    {
        #region Fields
        readonly List<T> _list;
        readonly IComparer<T> _comparer;
        private readonly object _thisLock = new object();
        #endregion
        #region Constructors
        ConcurrentOrderedList(List<T> list, IComparer<T> comparer)
        {
            lock (_thisLock)
            {
                _list = list;
                _comparer = comparer;
            }
        }
        public ConcurrentOrderedList()
            : this(new List<T>(), Comparer<T>.Default)
        {
        }
        public ConcurrentOrderedList(IComparer<T> comparer)
            : this(new List<T>(), comparer)
        {
        }
        public ConcurrentOrderedList(IEnumerable<T> collection)
            : this(collection, Comparer<T>.Default)
        {
        }

        public ConcurrentOrderedList(IEnumerable<T> collection, IComparer<T> comparer)
            : this(new List<T>(collection), comparer)
        {
            lock (_thisLock)
            {
                _list.Sort(comparer);
            }
        }

        public ConcurrentOrderedList(int capacity)
            : this(new List<T>(capacity), Comparer<T>.Default)
        {
        }
        public ConcurrentOrderedList(int capacity, IComparer<T> comparer)
            : this(new List<T>(capacity), comparer)
        {
        }
        //yet to be implemented
        //public void OrderedList(Comparison<T> comparison);

        #endregion

        #region Properties
        public int Capacity
        {
            get
            {
                return _list.Capacity;
            }
            set
            {
                lock (_thisLock)
                {
                    _list.Capacity = value;
                }
            }
        }
        public int Count
        {
            get
            {
                return _list.Count;
            }
        }
        object IList.this[int index]
        {
            get
            {
                return _list[index];
            }
            set
            {
                lock (_thisLock)
                {
                    _list[index] = (T)value;
                }
            }
        }
        public T this[int index]
        {
            get
            {
                return _list[index];
            }
            set
            {
                lock (_thisLock)
                {
                    _list[index] = value;
                }
            }
        }
        bool ICollection.IsSynchronized
        {
            get
            {
                return false;
            }
        }
        object ICollection.SyncRoot
        {
            get
            {
                return _list;
            }
        } //? should return this 
        bool IList.IsFixedSize
        {
            get
            {
                return false;
            }
        }
        bool IList.IsReadOnly
        {
            get
            {
                return false;
            }
        }
        bool ICollection<T>.IsReadOnly
        {
            get
            {
                return false;
            }
        }
        #endregion


        #region Methods
        void ICollection<T>.Add(T item)
        {
            Add(item);
        }
        /// <summary>
        /// Adds a new item to the appropriate index of the SortedList
        /// </summary>
        /// <param name="item">The item to be removed</param>
        /// <returns>The index at which the item was inserted</returns>
        public int Add(T item)
        {
            lock (_thisLock)
            {
                int index = BinarySearch(item);
                if (index < 0)
                {
                    index = ~index;
                }

                _list.Insert(index, item);

                return index;
            }
        }
        int IList.Add(object item)
        {
            return Add((T)item);
        }
        //NOT performance tested against other ways algorithms yet
        public void AddRange(IEnumerable<T> collection)
        {
            lock (_thisLock)
            {
                var insertList = new List<T>(collection);
                if (insertList.Count == 0)
                {
                    return;
                }
                //if we insert backwards, index we are inserting at does not keep incrementing
                insertList.Sort(_comparer);
                int searchLength = _list.Count;
                for (int i = insertList.Count - 1; i >= 0; i--)
                {
                    T item = insertList[i];
                    int insertIndex = BinarySearch(0, searchLength, item);
                    if (insertIndex < 0)
                    {
                        insertIndex = ~insertIndex;
                    }
                    else
                    {
                        while (--insertIndex >= 0 && _list[insertIndex].Equals(item)) { }
                        insertIndex++;
                    }
                    if (insertIndex <= 0)
                    {
                        _list.InsertRange(0, insertList.GetRange(0, i + 1));
                        break;
                    }
                    searchLength = insertIndex - 1;
                    item = _list[searchLength];
                    int endInsert = i;
                    while (--i >= 0 && _comparer.Compare(insertList[i], item) > 0) { }
                    i++;
                    _list.InsertRange(insertIndex, insertList.GetRange(i, endInsert - i + 1));
                }
            }
        }
        public int BinarySearch(T item)
        {
            lock (_thisLock)
            {
                return _list.BinarySearch(item, _comparer);
            }
        }
        public int BinarySearch(int index, int count, T item)
        {
            lock (_thisLock)
            {
                return _list.BinarySearch(index, count, item, _comparer);
            }
        }
        public ReadOnlyCollection<T> AsReadOnly()
        {
            lock (_thisLock)
            {
                return _list.AsReadOnly();
            }
        }
        public void Clear()
        {
            lock (_thisLock)
            {
                _list.Clear();
            }
        }

        public List<T> Clone()
        {
            lock (_thisLock)
            {
                return new List<T>(_list);
            }
        }

        public bool Contains(T item)
        {
            return BinarySearch(item) >= 0;
        }
        bool IList.Contains(object item)
        {
            return Contains((T)item);
        }
        public List<TOutput> ConvertAll<TOutput>(Converter<T, TOutput> converter)
        {
            lock (_thisLock)
            {
                return _list.ConvertAll(converter);
            }
        }
        public void CopyTo(T[] array)
        {
            lock (_thisLock)
            {
                _list.CopyTo(array);
            }
        }
        public void CopyTo(T[] array, int arrayIndex)
        {
            lock (_thisLock)
            {
                _list.CopyTo(array, arrayIndex);
            }
        }
        void ICollection.CopyTo(Array array, int arrayIndex)
        {
            lock (_thisLock)
            {
                _list.CopyTo((T[])array, arrayIndex);
            }
        }
        public void CopyTo(int index, T[] array, int arrayIndex, int count)
        {
            lock (_thisLock)
            {
                _list.CopyTo(index, array, arrayIndex, count);
            }
        }
        public void ForEach(Action<T> action)
        {
            lock (_thisLock)
            {
                foreach (T item in _list)
                {
                    action(item);
                }
            }
        }

        public T RemoveAndGet(int index)
        {
            lock (_thisLock)
            {
                T value = _list[index];
                _list.RemoveAt(index);
                return value;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Clone().GetEnumerator();
        }
        public IEnumerator<T> GetEnumerator()
        {
            return Clone().GetEnumerator();
        }
        public List<T> GetRange(int index, int count)
        {
            lock (_thisLock)
            {
                return _list.GetRange(index, count);
            }
        }

        public bool Remove(T item)
        {
            lock (_thisLock)
            {
                int index = BinarySearch(item);
                if (index < 0)
                {
                    return false;
                }

                _list.RemoveAt(index);

                return true;
            }
        }
        void IList.Remove(object item)
        {
            Remove((T)item);
        }

        public void RemoveAt(int index)
        {
            lock (_thisLock)
            {
                _list.RemoveAt(index);
            }
        }
        public void RemoveRange(int index, int count)
        {
            lock (_thisLock)
            {
                _list.RemoveRange(index, count);
            }
        }
        public T[] ToArray()
        {
            lock (_thisLock)
            {
                return _list.ToArray();
            }
        }
        public void TrimExcess()
        {
            lock (_thisLock)
            {
                _list.TrimExcess();
            }
        }
        public int IndexOf(T item)
        {
            lock (_thisLock)
            {
                int index = BinarySearch(item);
                if (index < 0) return -1;
                while (_list[--index].Equals(item)) { }
                return ++index;
            }
        }
        int IList.IndexOf(object item)
        {
            return IndexOf((T)item);
        }
        public int LastIndexOf(T item)
        {
            lock (_thisLock)
            {
                int index = BinarySearch(item);
                if (index < 0) return -1;
                while (_list[++index].Equals(item)) { }
                return --index;
            }
        }
        #endregion
        #region NotImplemented
        const string InsertExceptionMsg = "SortedList detemines position to insert automatically - use add method without an index";
        void IList.Insert(int index, object item)
        {
            throw new NotImplementedException(InsertExceptionMsg);
        }
        void IList<T>.Insert(int index, T item)
        {
            throw new NotImplementedException(InsertExceptionMsg);
        }
        #endregion
    }
}
