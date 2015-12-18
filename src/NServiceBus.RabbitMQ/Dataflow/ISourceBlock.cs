// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// =+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
//
// ISourceBlock.cs
//
//
// The base interface for all source blocks.
//
// =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-

namespace System.Threading.Tasks.Dataflow
{
    using System.Diagnostics.CodeAnalysis;

    /// <summary>Represents a dataflow block that is a source of data.</summary>
    /// <typeparam name="TOutput">Specifies the type of data supplied by the <see cref="ISourceBlock{TOutput}" />.</typeparam>
    internal interface ISourceBlock<out TOutput> : IDataflowBlock
    {
        // IMPLEMENT IMPLICITLY

        IDisposable LinkTo(ITargetBlock<TOutput> target, DataflowLinkOptions linkOptions);

        // IMPLEMENT EXPLICITLY

        [SuppressMessage("Microsoft.Design", "CA1021:AvoidOutParameters", MessageId = "2#")]
        TOutput ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<TOutput> target, out bool messageConsumed);

        bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<TOutput> target);

        void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<TOutput> target);
    }
}