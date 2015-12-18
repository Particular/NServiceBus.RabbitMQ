// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// =+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
//
// QueuedMap.cs
//
//
// A key-value pair queue, where pushing an existing key into the collection overwrites
// the existing value.
//
// =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-

namespace System.Threading.Tasks.Dataflow.Internal
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;

    /// <summary>
    /// Provides a data structure that supports pushing and popping key/value pairs.
    /// Pushing a key/value pair for which the key already exists results in overwriting
    /// the existing key entry's value.
    /// </summary>
    /// <typeparam name="TKey">Specifies the type of keys in the map.</typeparam>
    /// <typeparam name="TValue">Specifies the type of values in the map.</typeparam>
    /// <remarks>This type is not thread-safe.</remarks>
    [DebuggerDisplay("Count = {Count}")]
    [DebuggerTypeProxy(typeof(EnumerableDebugView<,>))]
    sealed class QueuedMap<TKey, TValue>
    {
        /// <summary>Initializes the QueuedMap.</summary>
        internal QueuedMap()
        {
            _queue = new ArrayBasedLinkedQueue<KeyValuePair<TKey, TValue>>();
            _mapKeyToIndex = new Dictionary<TKey, int>();
        }

        /// <summary>Initializes the QueuedMap.</summary>
        /// <param name="capacity">The initial capacity of the data structure.</param>
        internal QueuedMap(int capacity)
        {
            _queue = new ArrayBasedLinkedQueue<KeyValuePair<TKey, TValue>>(capacity);
            _mapKeyToIndex = new Dictionary<TKey, int>(capacity);
        }

        /// <summary>Gets the number of items in the data structure.</summary>
        internal int Count
        {
            get { return _mapKeyToIndex.Count; }
        }

        /// <summary>Pushes a key/value pair into the data structure.</summary>
        /// <param name="key">The key for the pair.</param>
        /// <param name="value">The value for the pair.</param>
        internal void Push(TKey key, TValue value)
        {
            // Try to get the index of the key in the queue. If it's there, replace the value.
            int indexOfKeyInQueue;
            if (!_queue.IsEmpty && _mapKeyToIndex.TryGetValue(key, out indexOfKeyInQueue))
            {
                _queue.Replace(indexOfKeyInQueue, new KeyValuePair<TKey, TValue>(key, value));
            }
            // If it's not there, add it to the queue and then add the mapping.
            else
            {
                indexOfKeyInQueue = _queue.Enqueue(new KeyValuePair<TKey, TValue>(key, value));
                _mapKeyToIndex.Add(key, indexOfKeyInQueue);
            }
        }

        /// <summary>Try to pop the next element from the data structure.</summary>
        /// <param name="item">The popped pair.</param>
        /// <returns>true if an item could be popped; otherwise, false.</returns>
        internal bool TryPop(out KeyValuePair<TKey, TValue> item)
        {
            var popped = _queue.TryDequeue(out item);
            if (popped)
            {
                _mapKeyToIndex.Remove(item.Key);
            }
            return popped;
        }

        /// <summary>Tries to pop one or more elements from the data structure.</summary>
        /// <param name="items">The items array into which the popped elements should be stored.</param>
        /// <param name="arrayOffset">The offset into the array at which to start storing popped items.</param>
        /// <param name="count">The number of items to be popped.</param>
        /// <returns>
        /// The number of items popped, which may be less than the requested number if fewer existed in the data
        /// structure.
        /// </returns>
        internal int PopRange(KeyValuePair<TKey, TValue>[] items, int arrayOffset, int count)
        {
            // As this data structure is internal, only assert incorrect usage.
            // If this were to ever be made public, these would need to be real argument checks.
            Contract.Requires(items != null, "Requires non-null array to store into.");
            Contract.Requires(count >= 0 && arrayOffset >= 0, "Count and offset must be non-negative");
            Contract.Requires(arrayOffset + count >= 0, "Offset plus count overflowed");
            Contract.Requires(arrayOffset + count <= items.Length, "Range must be within array size");

            var actualCount = 0;
            for (var i = arrayOffset; actualCount < count; i++, actualCount++)
            {
                KeyValuePair<TKey, TValue> item;
                if (TryPop(out item))
                {
                    items[i] = item;
                }
                else
                {
                    break;
                }
            }

            return actualCount;
        }

        /// <summary>A map from key to index into the list.</summary>
        /// <remarks>The correctness of this map relies on the list only having elements removed from its end.</remarks>
        readonly Dictionary<TKey, int> _mapKeyToIndex;

        /// <summary>The queue of elements.</summary>
        readonly ArrayBasedLinkedQueue<KeyValuePair<TKey, TValue>> _queue;

        /// <summary>
        /// A queue structure that uses an array-based list to store its items
        /// and that supports overwriting elements at specific indices.
        /// </summary>
        /// <typeparam name="T">The type of the items storedin the queue</typeparam>
        /// <remarks>This type is not thread-safe.</remarks>
        sealed class ArrayBasedLinkedQueue<T>
        {
            /// <summary>Initializes the Queue instance.</summary>
            internal ArrayBasedLinkedQueue()
            {
                _storage = new List<KeyValuePair<int, T>>();
            }

            /// <summary>Initializes the Queue instance.</summary>
            /// <param name="capacity">The capacity of the internal storage.</param>
            internal ArrayBasedLinkedQueue(int capacity)
            {
                _storage = new List<KeyValuePair<int, T>>(capacity);
            }

            internal bool IsEmpty
            {
                get { return _headIndex == TERMINATOR_INDEX; }
            }

            /// <summary>Enqueues an item.</summary>
            /// <param name="item">The item to be enqueued.</param>
            /// <returns>The index of the slot where item was stored.</returns>
            internal int Enqueue(T item)
            {
                int newIndex;

                // If there is a free slot, reuse it
                if (_freeIndex != TERMINATOR_INDEX)
                {
                    Debug.Assert(0 <= _freeIndex && _freeIndex < _storage.Count, "Index is out of range.");
                    newIndex = _freeIndex;
                    _freeIndex = _storage[_freeIndex].Key;
                    _storage[newIndex] = new KeyValuePair<int, T>(TERMINATOR_INDEX, item);
                }
                // If there is no free slot, add one
                else
                {
                    newIndex = _storage.Count;
                    _storage.Add(new KeyValuePair<int, T>(TERMINATOR_INDEX, item));
                }

                if (_headIndex == TERMINATOR_INDEX)
                {
                    // Point _headIndex to newIndex if the queue was empty
                    Debug.Assert(_tailIndex == TERMINATOR_INDEX, "If head indicates empty, so too should tail.");
                    _headIndex = newIndex;
                }
                else
                {
                    // Point the tail slot to newIndex if the queue was not empty
                    Debug.Assert(_tailIndex != TERMINATOR_INDEX, "If head does not indicate empty, neither should tail.");
                    _storage[_tailIndex] = new KeyValuePair<int, T>(newIndex, _storage[_tailIndex].Value);
                }

                // Point the tail slot newIndex
                _tailIndex = newIndex;

                return newIndex;
            }

            /// <summary>Tries to dequeue an item.</summary>
            /// <param name="item">The item that is dequeued.</param>
            internal bool TryDequeue(out T item)
            {
                // If the queue is empty, just initialize the output item and return false
                if (_headIndex == TERMINATOR_INDEX)
                {
                    Debug.Assert(_tailIndex == TERMINATOR_INDEX, "If head indicates empty, so too should tail.");
                    item = default(T);
                    return false;
                }

                // If there are items in the queue, start with populating the output item
                Debug.Assert(0 <= _headIndex && _headIndex < _storage.Count, "Head is out of range.");
                item = _storage[_headIndex].Value;

                // Move the popped slot to the head of the free list
                var newHeadIndex = _storage[_headIndex].Key;
                _storage[_headIndex] = new KeyValuePair<int, T>(_freeIndex, default(T));
                _freeIndex = _headIndex;
                _headIndex = newHeadIndex;
                if (_headIndex == TERMINATOR_INDEX)
                {
                    _tailIndex = TERMINATOR_INDEX;
                }

                return true;
            }

            /// <summary>Replaces the item of a given slot.</summary>
            /// <param name="index">The index of the slot where the value should be replaced.</param>
            /// <param name="item">The item to be places.</param>
            internal void Replace(int index, T item)
            {
                Debug.Assert(0 <= index && index < _storage.Count, "Index is out of range.");
#if DEBUG
                // Also assert that index does not belong to the list of free slots
                for (var idx = _freeIndex; idx != TERMINATOR_INDEX; idx = _storage[idx].Key)
                {
                    Debug.Assert(idx != index, "Index should not belong to the list of free slots.");
                }
#endif
                _storage[index] = new KeyValuePair<int, T>(_storage[index].Key, item);
            }

            /// <summary>
            /// The queue where the items will be stored.
            /// The key of each entry is the index of the next entry in the queue.
            /// </summary>
            readonly List<KeyValuePair<int, T>> _storage;

            /// <summary>Index of the first free slot.</summary>
            int _freeIndex = TERMINATOR_INDEX;

            /// <summary>Index of the first queue item.</summary>
            int _headIndex = TERMINATOR_INDEX;

            /// <summary>Index of the last queue item.</summary>
            int _tailIndex = TERMINATOR_INDEX;

            /// <summary>Terminator index.</summary>
            const int TERMINATOR_INDEX = -1;
        }
    }
}