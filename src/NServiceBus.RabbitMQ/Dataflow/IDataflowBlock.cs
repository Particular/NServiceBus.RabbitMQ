// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// =+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
//
// IDataflowBlock.cs
//
//
// The base interface for all dataflow blocks.
//
// =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-

namespace System.Threading.Tasks.Dataflow
{
    /// <summary>Represents a dataflow block.</summary>
    internal interface IDataflowBlock
    {
        // IMPLEMENT IMPLICITLY

        Task Completion { get; }

        void Complete();

        // IMPLEMENT EXPLICITLY

        void Fault(Exception exception);
    }
}