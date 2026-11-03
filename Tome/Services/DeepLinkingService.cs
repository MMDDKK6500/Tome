// Copyright 2026 URAV ADVANCED LEARNING SYSTEMS PRIVATE LIMITED
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Windows;

namespace Tome.Services;

public sealed class DeepLinkingService : IDisposable
{
    private const string MutexId = "com.Uralstech.Tome.DeepLinkMutex";
    private const string PipeId = "com.Uralstech.Tome.DeepLinkPipe";

    public readonly bool IsCurrentAppInstance;
    private readonly Mutex _mutex;

    private CancellationTokenSource? _cancellation;
    private bool _disposed;

    public DeepLinkingService()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexId, out bool wasCreated);
        IsCurrentAppInstance = wasCreated;

        if (!IsCurrentAppInstance)
            return;

        CancellationTokenSource cts = new();
        _cancellation = cts;

        _ = ServerLoopAsync(cts.Token)
            .ContinueWith(_ =>
            {
                _cancellation = null;
                cts.Dispose();
            }, TaskContinuationOptions.RunContinuationsAsynchronously);
    }

    public void ForwardDeepLink(string? link)
    {
        if (IsCurrentAppInstance)
            throw new InvalidOperationException();

        using NamedPipeClientStream client = new(".", PipeId, PipeDirection.Out);
        client.Connect(1000);

        using StreamWriter writeStream = new(client, Encoding.UTF8, 1024)
        {
            AutoFlush = true
        };

        writeStream.WriteLine(link);
    }

    private async Task ServerLoopAsync(CancellationToken token)
    {
        if (!IsCurrentAppInstance)
            throw new InvalidOperationException();

        using NamedPipeServerStream server = new(PipeId, PipeDirection.In, maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

        while (!token.IsCancellationRequested)
        {
            await server.WaitForConnectionAsync(token);

            try
            {
                token.ThrowIfCancellationRequested();

                using StreamReader readStream = new(server, encoding: Encoding.UTF8, bufferSize: 1024, leaveOpen: true);
                string? link = await readStream.ReadLineAsync(token);

                if (!ExtractSubmissionFromArgs(link, out int subId, out string? subType))
                    continue;

                MainWindow window = App.Services.GetRequiredService<MainWindow>();
                await window.Dispatcher.InvokeAsync(() =>
                {
                    if (window.WindowState == WindowState.Minimized)
                        window.WindowState = WindowState.Normal;

                    window.Activate();
                    window.OpenDeepLink(subId, subType);
                });
            }
            finally
            {
                server.Disconnect();
            }
        }
    }

    public static bool ExtractSubmissionFromArgs(string? link,
        out int submissionId, [NotNullWhen(true)] out string? submissionType)
    {
        submissionId = 0;
        submissionType = null;
        if (string.IsNullOrEmpty(link))
            return false;

        ReadOnlySpan<char> linkSpan = link;
        if (!linkSpan.StartsWith("tome:", StringComparison.InvariantCultureIgnoreCase))
            return false;

        int subIdStart = linkSpan.LastIndexOf(',');
        if (subIdStart == -1 || subIdStart == linkSpan.Length - 1)
            return false;

        int subTypeStart = linkSpan[..subIdStart].LastIndexOf(',');
        if (subTypeStart == -1 || subTypeStart == subIdStart - 1)
            return false;

        if (!int.TryParse(linkSpan[(subIdStart + 1)..], out submissionId))
            return false;

        submissionType = new string(linkSpan[(subTypeStart + 1)..subIdStart]);
        return submissionType is "Sound" or "Mod";
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _cancellation?.Cancel();

        if (IsCurrentAppInstance)
            _mutex.ReleaseMutex();

        _mutex.Dispose();
        _disposed = true;
    }
}
