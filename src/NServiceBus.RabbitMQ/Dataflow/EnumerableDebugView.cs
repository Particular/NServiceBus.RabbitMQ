// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// =+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
//
// EnumerableDebugView.cs
//
//
// Debugger type proxy for enumerables.
//
// =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-

namespace System.Threading.Tasks.Dataflow.Internal
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using System.Linq;

    /// <summary>Debugger type proxy for an enumerable of T.</summary>
    sealed class EnumerableDebugView<TKey, TValue>
    {
        /// <summary>Initializes the debug view.</summary>
        /// <param name="enumerable">The enumerable being debugged.</param>
        public EnumerableDebugView(IEnumerable<KeyValuePair<TKey, TValue>> enumerable)
        {
            Contract.Requires(enumerable != null, "Expected a non-null enumerable.");
            _enumerable = enumerable;
        }

        /// <summary>Gets the contents of the list.</summary>
        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public KeyValuePair<TKey, TValue>[] Items
        {
            get { return _enumerable.ToArray(); }
        }

        /// <summary>The enumerable being visualized.</summary>
        readonly IEnumerable<KeyValuePair<TKey, TValue>> _enumerable;
    }

    /// <summary>Debugger type proxy for an enumerable of T.</summary>
    sealed class EnumerableDebugView<T>
    {
        /// <summary>Initializes the debug view.</summary>
        /// <param name="enumerable">The enumerable being debugged.</param>
        public EnumerableDebugView(IEnumerable<T> enumerable)
        {
            Contract.Requires(enumerable != null, "Expected a non-null enumerable.");
            _enumerable = enumerable;
        }

        /// <summary>Gets the contents of the list.</summary>
        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public T[] Items
        {
            get { return _enumerable.ToArray(); }
        }

        /// <summary>The enumerable being visualized.</summary>
        readonly IEnumerable<T> _enumerable;
    }
}