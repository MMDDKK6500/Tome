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
using System.Windows;
using Tome.Services;

namespace Tome;

public sealed partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        ServiceCollection services = new();
        services.AddSingleton<FileService>();
        services.AddSingleton<DeadlockService>();
        services.AddSingleton<GamebananaService>();
        services.AddSingleton<MainWindow>();

        Services = services.BuildServiceProvider();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ExtractSubmissionFromArgs(e.Args, out int subId, out string? subType);
        Services.GetRequiredService<MainWindow>().Show(subId, subType);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        (Services as IDisposable)?.Dispose();
        base.OnExit(e);
    }

    private bool ExtractSubmissionFromArgs(string[] args,
        out int submissionId, [NotNullWhen(true)] out string? submissionType)
    {
        submissionId = 0;
        submissionType = null;
        if (args.Length == 0)
            return false;

        ReadOnlySpan<char> arg = args[0];
        if (!arg.StartsWith("tome:"))
            return false;
        
        int subIdStart = arg.LastIndexOf(',');
        if (subIdStart == -1 || subIdStart == arg.Length - 1)
            return false;

        int subTypeStart = arg[..subIdStart].LastIndexOf(',');
        if (subTypeStart == -1 || subTypeStart == subIdStart - 1)
            return false;

        if (!int.TryParse(arg[(subIdStart + 1)..], out submissionId))
            return false;

        submissionType = new string(arg[(subTypeStart + 1)..subIdStart]);
        return submissionType is "Sound" or "Mod";
    }
}

