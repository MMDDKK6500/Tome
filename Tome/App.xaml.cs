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
        services.AddSingleton<DeepLinkingService>();
        services.AddSingleton<MainWindow>();

        Services = services.BuildServiceProvider();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DeepLinkingService deepLinking = Services.GetRequiredService<DeepLinkingService>();
        string? link = e.Args.FirstOrDefault();

        if (deepLinking.IsCurrentAppInstance)
        {
            DeepLinkingService.ExtractSubmissionFromArgs(link, out int subId, out string? subType);
            Services.GetRequiredService<MainWindow>().Show(subId, subType);
            return;
        }

        try
        {
            deepLinking.ForwardDeepLink(link);
        }
        finally
        {
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        (Services as IDisposable)?.Dispose();
        base.OnExit(e);
    }
}

