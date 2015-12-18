// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// =+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
//
// IReceivableSourceBlock.cs
//
//
// The base interface for all source blocks.
//
// =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-

namespace System.Threading.Tasks.Dataflow
{
    using System.Collections.Generic;

    /// <summary>Represents a dataflow block that supports receiving of messages without linking.</summary>
    /// <typeparam name="TOutput">Specifies the type of data supplied by the <see cref="IReceivableSourceBlock{TOutput}" />.</typeparam>
    internal interface IReceivableSourceBlock<TOutput> : ISourceBlock<TOutput>
    {
        // IMPLEMENT IMPLICITLY

        bool TryReceive(Predicate<TOutput> filter, out TOutput item);

        // IMPLEMENT IMPLICITLY IF BLOCK SUPPORTS RECEIVING MORE THAN ONE ITEM, OTHERWISE EXPLICITLY

        bool TryReceiveAll(out IList<TOutput> items);
    }
}