// Copyright (C) 2024 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of dkg service node
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions
// are met:
// 1. Redistributions of source code must retain the above copyright
// notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
// TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
// PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDERS OR CONTRIBUTORS
// BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.

using dkgServiceNode.Data;
using dkgServiceNode.Models;
using System.Collections.Concurrent;

namespace dkgServiceNode.Services.RequestProcessors
{
    public abstract class RequestProcessorBase : IDisposable
    {
        protected readonly int databaseReconnectDelay = 3000;
        protected readonly int queueReparseDelay;
        protected readonly int bulkInsertLimit;

        protected readonly CancellationTokenSource cancellationTokenSource = new();

        protected Task? backgroundTask = null;
        protected NodeCompositeContext? ncContext = null;

        protected readonly ConcurrentQueue<Node> requestQueue = new();

        protected volatile bool isRunning = false;
        protected readonly ILogger logger;
        protected readonly string connectionString;
        protected bool disposed = false;

        public RequestProcessorBase(
            string connectionStr,
            int bInsertLimit,
            int qReparseDelay,
            ILogger lgger
        )
        {
            connectionString = connectionStr;
            bulkInsertLimit = bInsertLimit;
            queueReparseDelay = qReparseDelay;
            logger = lgger;
        }

        public void Start(NodeCompositeContext nContext)
        {
            if (isRunning)
            {
                logger.LogWarning("Request Processor is already running. 'Start' ignored.");
            }
            else
            {
                isRunning = true;
                ncContext = nContext;
                backgroundTask = Task.Run(ProcessRequests, cancellationTokenSource.Token);
                logger.LogInformation("Request Processor has been started.");
            }
        }

        public void Stop()
        {
            if (!isRunning)
            {
                logger.LogWarning("Request Processor is not running. 'Stop' ignored.");
            }
            else
            {
                cancellationTokenSource.Cancel();
                backgroundTask?.Wait();
                isRunning = false;
                logger.LogInformation("Request Processor has been stopped.");
            }
        }

        protected abstract Task ProcessRequests();

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    Stop();
                    cancellationTokenSource.Dispose();
                    backgroundTask?.Dispose();
                }
                ncContext = null;
                disposed = true;
            }
        }

        ~RequestProcessorBase()
        {
            Dispose(false);
        }
    }
}
