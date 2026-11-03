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

using GamebananaAPI.Data.Categories;
using GamebananaAPI.Data.Submission;
using Microsoft.Extensions.DependencyInjection;
using SharpCompress.Archives;
using SharpCompress.Common;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using Tome.Models;
using Tome.Services;

namespace Tome;

public partial class Downloads : Window
{
    [GeneratedRegex(@"\Apak\d\d_dir\.vpk\z", RegexOptions.CultureInvariant | RegexOptions.Compiled)]
    private static partial Regex VpkMatchRegexGen();

    private static readonly Regex s_vpkMatchRegex = VpkMatchRegexGen();

    private readonly FileService _files;
    private readonly DeadlockService _deadlock;
    private readonly GamebananaService _gamebanana;

    private readonly TomeManifest _manifest;
    private readonly ModActions _requestedActions;

    private readonly TaskCompletionSource<bool> _confirmTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TaskCompletionSource? _fileSelectionTcs = null;

    public Downloads(ModActions actions, TomeManifest manifest)
    {
        InitializeComponent();

        _manifest = manifest;
        _requestedActions = actions;
        _files = App.Services.GetRequiredService<FileService>();
        _deadlock = App.Services.GetRequiredService<DeadlockService>();
        _gamebanana = App.Services.GetRequiredService<GamebananaService>();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        FileSelectionBorder.Visibility = Visibility.Collapsed;
        DownloadProgressBorder.Visibility = Visibility.Collapsed;
        CancelContinueButtons.Visibility = Visibility.Collapsed;
    }

    private async void Window_ContentRendered(object sender, EventArgs e)
    {
        Queue<(ModAction, ModFile)>? actionsQueue = await LoadSubmissionsAsync();
        if (actionsQueue is null)
        {
            Close();
            return;
        }

        DialogResult = await ExecuteActionsAsync(actionsQueue);
        Close();
    }

    #region File Selection

    private async Task<Queue<(ModAction, ModFile)>?> LoadSubmissionsAsync()
    {
        try
        {
            CancelContinueButtons.IsEnabled = false;
            CancelContinueButtons.Visibility = Visibility.Visible;
            
            Log("Loading submission details, please wait...");

            StringBuilder sb = new();
            sb.AppendLine("The following actions have been requested:");

            Queue<(ModAction, ModFile)> queue = [];
            string addonsFolder = _deadlock.GetAddonsPath(_manifest.GamePath);

            foreach ((int id, ModAction action) in _requestedActions)
            {
                _manifest.Mods.TryGetValue(id, out InstalledMod? registeredMod);
                string? targetRelativePath = action.Type is ModActionType.Update or ModActionType.Delete
                    ? registeredMod!.RelativePath : null;

                ModFile file;
                FileInfo fileInfo;
                switch (action.Type)
                {
                    case ModActionType.Update:
                    case ModActionType.Install:
                        if (action.Mod.IsLocal)
                        {
                            fileInfo = new FileInfo(action.Mod.Path!);
                            file = new ModFile(
                                Source: new Uri(fileInfo.FullName, UriKind.Absolute),
                                SourceName: fileInfo.Name,
                                SourceMd5: CryptographyUtil.Md5File(fileInfo.FullName),
                                SourceSize: fileInfo.Length,

                                TargetRelativePath: targetRelativePath,
                                GamebananaSubmission: null,
                                GamebananaFile: null
                            );
                            break;
                        }

                        SubmissionProfile submission = await _gamebanana.GetSubmissionAsync(id, action.Mod.SubmissionType!);
                        if (submission.Files is not { Count: > 0 })
                        {
                            MessageBoxResult result = MessageBox.Show(this, $"'{submission.Name}' does not have any downloadable file. Skip?", "Invalid mod",
                                                                            MessageBoxButton.OKCancel, MessageBoxImage.Warning);

                            if (result == MessageBoxResult.OK)
                                continue;

                            return null;
                        }

                        SubmissionFile remoteFile;
                        if (action.Type is ModActionType.Update)
                        {
                            SubmissionFile? autoUpdate = submission.Files.FirstOrDefault(file =>
                                file.Name == registeredMod?.GamebananaMetadata?.FileName);

                            autoUpdate ??= await AskUserForMissingUpdateAsync(submission);
                            if (autoUpdate is null)
                                continue;

                            remoteFile = autoUpdate;
                        }
                        else if (submission.Files.Count > 1)
                            remoteFile = await AskUserForFileAsync(submission);
                        else
                            remoteFile = submission.Files[0];

                        file = new ModFile(
                            Source: new Uri(remoteFile.DownloadUrl),
                            SourceName: remoteFile.Name,
                            SourceMd5: remoteFile.Md5Checksum,
                            SourceSize: remoteFile.Size,

                            TargetRelativePath: targetRelativePath,
                            GamebananaSubmission: submission,
                            GamebananaFile: remoteFile
                        );
                        break;

                    case ModActionType.Delete:
                        string fullPath = Path.Combine(addonsFolder, registeredMod!.RelativePath);
                        fileInfo = new FileInfo(fullPath);

                        file = new ModFile(
                            Source: new Uri(fileInfo.FullName, UriKind.Absolute),
                            SourceName: fileInfo.Name,
                            SourceMd5: registeredMod.OriginalFileMd5Checksum,
                            SourceSize: fileInfo.Length,

                            TargetRelativePath: targetRelativePath,
                            GamebananaSubmission: null,
                            GamebananaFile: null
                        );
                        break;

                    default:
                        throw new NotImplementedException($"Case for '{action.Type}' not implemented.");
                }

                sb.AppendLine($"- {action.Type} '{action.Mod.Name ?? "[no name provided]"}'");
                queue.Enqueue((action, file));
            }

            Log(sb.ToString());
            CancelContinueButtons.IsEnabled = true;

            if (!await _confirmTcs.Task)
            {
                MessageBox.Show(this, "The requested actions were cancelled.", "Actions cancelled",
                                      MessageBoxButton.OK, MessageBoxImage.Information);

                return null;
            }

            CancelContinueButtons.Visibility = Visibility.Hidden;
            return queue;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not load submissions",
                                  MessageBoxButton.OK, MessageBoxImage.Error);

            return null;
        }
    }

    private async Task<SubmissionFile?> AskUserForMissingUpdateAsync(SubmissionProfile submission)
    {
        string message = $"'{submission.Name}' does not host the originally installed file anymore. Choose an alternative, or skip updating:";
        List<OnlineModFileModel> options = [.. submission.Files!.Select(static file =>
            new OnlineModFileModel()
            {
                Text = !string.IsNullOrEmpty(file.Description)
                            ? $"{file.Name}: {file.Description}" : file.Name,
                File = file,
            }).Append(
            new OnlineModFileModel()
            {
                Text = "Skip update",
                File = null,
            })
        ];

        return await BuiltInFileDialogAsync(message, options);
    }

    private async Task<SubmissionFile> AskUserForFileAsync(SubmissionProfile submission)
    {
        string message = $"'{submission.Name}' has multiple files. Choose one to install:";
        List<OnlineModFileModel> options = [.. submission.Files!.Select(static file =>
            new OnlineModFileModel()
            {
                Text = !string.IsNullOrEmpty(file.Description)
                            ? $"{file.Name}: {file.Description}" : file.Name,
                File = file,
            })
        ];

        return await BuiltInFileDialogAsync(message, options)!;
    }

    private async Task<SubmissionFile> BuiltInFileDialogAsync(string message, List<OnlineModFileModel> options)
    {
        FileSelectionText.Text = message;
        _fileSelectionTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        options[0].IsSelected = true;
        FileSelectionButtons.ItemsSource = options;

        FileSelectionBorder.Visibility = Visibility.Visible;
        await _fileSelectionTcs.Task;

        OnlineModFileModel result = options.First(static file => file.IsSelected);
        FileSelectionBorder.Visibility = Visibility.Collapsed;
        return result.File;
    }

    private void FileSelection_Click(object sender, RoutedEventArgs e) =>
        _fileSelectionTcs?.TrySetResult();

    #endregion

    private async Task<bool> ExecuteActionsAsync(Queue<(ModAction, ModFile)> queue)
    {
        List<string> tempFiles = [];

        try
        {
            DownloadProgressBorder.Visibility = Visibility.Visible;
            DownloadProgressBar.Minimum = 0;
            DownloadProgressBar.Maximum = 1;

            foreach ((ModAction action, ModFile file) in queue)
            {
                switch (action.Type)
                {
                    case ModActionType.Update:
                    case ModActionType.Install:
                        string modName = action.Mod.Name ?? file.Source.ToString();
                        CurrentDownloadText.Text = $"Downloading {modName}...";
                        DownloadProgressBar.Value = 0;

                        string path = Path.GetTempFileName();
                        tempFiles.Add(path);

                        string? contentType = await _gamebanana.DownloadAsync(file.Source, path, progress =>
                            DownloadProgressBar.Value = (double)progress / file.SourceSize);

                        CurrentDownloadText.Text = $"Processing {modName}...";
                        DownloadProgressBar.IsIndeterminate = true;

                        if (!await ProcessDownloadedContentAsync(path, action, file))
                            return false;

                        DownloadProgressBar.IsIndeterminate = false;
                        break;

                    case ModActionType.Delete:
                        CurrentDownloadText.Text = $"Deleting {action.Mod.Name ?? file.TargetRelativePath}...";
                        DownloadProgressBar.Value = 1;

                        string filePath = file.Source.LocalPath;
                        string md5 = CryptographyUtil.Md5File(filePath);

                        if (!md5.Equals(file.SourceMd5, StringComparison.InvariantCulture))
                        {
                            MessageBoxResult result = MessageBox.Show(this, $"'{action.Mod.Name ?? file.TargetRelativePath}' does not match its known checksum. " +
                                                                            $"You may have edited the mod outside of Tome. Continue with deletion?", "Checksum mismatch",
                                                                            MessageBoxButton.YesNo, MessageBoxImage.Warning);

                            if (result == MessageBoxResult.No)
                                continue;
                        }

                        _manifest.Mods.Remove(action.Mod.Id);
                        _files.SaveManifest(_manifest);

                        File.Delete(filePath);
                        break;

                    default:
                        throw new NotImplementedException($"Case for '{action.Type}' not implemented.");
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not complete all actions",
                                  MessageBoxButton.OK, MessageBoxImage.Error);

            return false;
        }
        finally
        {
            foreach (string tempFile in tempFiles)
                File.Delete(tempFile);
        }
    }

    private async Task<bool> ProcessDownloadedContentAsync(string path, ModAction action, ModFile file)
    {
        string ext = Path.GetExtension(file.SourceName);
        using FileStream stream = File.OpenRead(path);

        if (ext is ".vpk")
            return await InstallFileAsync(stream, action, file);

        if (!ArchiveFactory.IsArchive(stream, out ArchiveType? archiveType) || archiveType is null)
        {
            MessageBoxResult result = MessageBox.Show(this, $"'{file.SourceName}' from '{action.Mod.Name ?? file.Source.ToString()}' is an unknown or unsupported format. Skip?", "Invalid mod file",
                                                            MessageBoxButton.OKCancel, MessageBoxImage.Warning);

            return result == MessageBoxResult.OK;
        }

        if (!string.IsNullOrEmpty(file.SourceMd5)
            && !file.SourceMd5.Equals(CryptographyUtil.Md5Stream(stream), StringComparison.InvariantCulture))
        {
            MessageBoxResult result = MessageBox.Show(this, $"'{file.SourceName}' does not match its known checksum and is corrupt. Skip?", "Checksum mismatch",
                                                            MessageBoxButton.OKCancel, MessageBoxImage.Error);

            return result == MessageBoxResult.OK;
        }

        using IArchive archive = ArchiveFactory.OpenArchive(stream);
        IArchiveEntry[] entries = [.. archive.Entries.Where(static entry => !entry.IsDirectory)];

        if (entries.Length == 0)
        {
            MessageBoxResult result = MessageBox.Show(this, $"'{file.SourceName}' from '{action.Mod.Name ?? file.Source.ToString()}' is empty. Skip?", "Invalid mod archive",
                                                            MessageBoxButton.OKCancel, MessageBoxImage.Error);

            return result == MessageBoxResult.OK;
        }

        if (entries.Length > 1)
        {
            MessageBoxResult result = MessageBox.Show(this, $"'{file.SourceName}' from '{action.Mod.Name ?? file.Source.ToString()}' contains multiple installable files. " +
                                                            $"This is not currently supported. Skip?", "Invalid mod archive",
                                                            MessageBoxButton.OKCancel, MessageBoxImage.Error);

            return result == MessageBoxResult.OK;
        }

        IArchiveEntry entry = entries[0];
        if (Path.GetExtension(entry.Key) is not ".vpk")
        {
            MessageBoxResult result = MessageBox.Show(this, $"'{file.SourceName}' from '{action.Mod.Name ?? file.Source.ToString()}' contains non-vpk files, which are " +
                                                            $"currently unsupported. Skip?", "Unsupported mod content",
                                                            MessageBoxButton.OKCancel, MessageBoxImage.Error);

            return result == MessageBoxResult.OK;
        }

        Stream entryStream = entry.OpenEntryStream();
        return await InstallFileAsync(entryStream, action, file with
        {
            SourceName = Path.GetFileName(entry.Key)!,
            SourceSize = entry.Size,
            SourceMd5 = string.Empty
        });
    }

    private async Task<bool> InstallFileAsync(Stream content, ModAction action, ModFile modFile)
    {
        string tempPath = Path.GetTempFileName();
        try
        {
            using (FileStream writeStream = File.Create(tempPath))
                await content.CopyToAsync(writeStream);

            string md5 = CryptographyUtil.Md5File(tempPath);

            if (!string.IsNullOrEmpty(modFile.SourceMd5)
                && !modFile.SourceMd5.Equals(md5, StringComparison.InvariantCulture))
            {
                MessageBoxResult result = MessageBox.Show(this, $"'{modFile.SourceName}' does not match its known checksum and is corrupt. Skip?", "Checksum mismatch",
                                                                MessageBoxButton.OKCancel, MessageBoxImage.Error);

                return result == MessageBoxResult.OK;
            }

            int modId = action.Mod.Id;
            string addonsFolder = _deadlock.GetAddonsPath(_manifest.GamePath);
            string relativePath;

            if (action.Type is ModActionType.Update)
            {
                ArgumentException.ThrowIfNullOrEmpty(modFile.TargetRelativePath);
                relativePath = modFile.TargetRelativePath;

                string existingMd5 = CryptographyUtil.Md5File(Path.Combine(addonsFolder, relativePath));
                if (!existingMd5.Equals(_manifest.Mods[modId].OriginalFileMd5Checksum, StringComparison.InvariantCulture))
                {
                    MessageBoxResult result = MessageBox.Show(this, $"'{action.Mod.Name ?? modFile.TargetRelativePath}' does not match its known checksum. " +
                                                                    $"You may have edited the mod outside of Tome. Continue with update?", "Checksum mismatch",
                                                                    MessageBoxButton.YesNo, MessageBoxImage.Warning);

                    if (result == MessageBoxResult.No)
                        return false;
                }
            }
            else
            {
                relativePath = Path.GetFileName(modFile.SourceName);
                
                string fullPath = string.Empty;
                for (int i = 1; i < 100; i++)
                {
                    fullPath = Path.Combine(addonsFolder, relativePath);
                    string fileName = Path.GetFileName(fullPath);

                    if (s_vpkMatchRegex.IsMatch(fileName) && !File.Exists(fullPath))
                        break;

                    relativePath = $"pak{i:D2}_dir.vpk";
                }

                if (string.IsNullOrEmpty(fullPath))
                {
                    MessageBoxResult result = MessageBox.Show(this, $"You have already installed the maximum amount of mods supported by Tome. Skip?", "Maximum mods installed",
                                                                    MessageBoxButton.OKCancel, MessageBoxImage.Error);

                    return result == MessageBoxResult.OK;
                }
            }

            File.Copy(tempPath, Path.Combine(addonsFolder, relativePath), overwrite: true);

            if (!_manifest.Mods.TryGetValue(modId, out InstalledMod? mod))
            {
                mod = new InstalledMod
                {
                    RelativePath = relativePath,
                    OriginalFileMd5Checksum = md5,
                };
            }

            if (!string.IsNullOrEmpty(action.Mod.Name)
                && !string.IsNullOrEmpty(action.Mod.Author))
            {
                mod.ListingMetadata = new ListingMetadata
                {
                    Name = action.Mod.Name,
                    Author = action.Mod.Author,
                };
            }

            mod.GamebananaMetadata = !action.Mod.IsLocal
                ? new GamebananaMetadata
                {
                    SubmissionId = modFile.GamebananaSubmission!.Id,
                    SubmissionType = action.Mod.SubmissionType!,
                    SubmissionCategoryType = modFile.GamebananaSubmission!.Category?.Type,
                    SubmissionCategoryTree = modFile.GamebananaSubmission!.Category is not null
                        ? await GetCategoryTreeAsync(modFile.GamebananaSubmission!.Category) : [],
                    FileId = modFile.GamebananaFile!.Id,
                    FileName = modFile.GamebananaFile!.Name,
                    FileMd5Checksum = modFile.GamebananaFile!.Md5Checksum,
                    FileUploadDate = modFile.GamebananaFile!.Added,
                } : null;

            mod.OriginalFileMd5Checksum = md5;
            _manifest.Mods[modId] = mod;

            _files.SaveManifest(_manifest);
            return true;
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    private async Task<List<int>> GetCategoryTreeAsync(Category category)
    {
        IReadOnlyDictionary<int, CategoryDefinition> results = await _gamebanana.GetFlattenedCategoryDefinitionsAsync(category.Type);

        if (!results.TryGetValue(category.Id, out CategoryDefinition? definition))
            throw new InvalidDataException("Invalid category ID received from Gamebanana API for submission.");

        List<int> ids = [];
        do
        {
            ids.Insert(0, definition.Id);
            results.TryGetValue(definition.ParentId, out definition);
        } while (definition != null);
        
        return ids;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) =>
        _confirmTcs.TrySetResult(false);

    private void Continue_Click(object sender, RoutedEventArgs e) =>
        _confirmTcs.TrySetResult(true);

    private void Log(string line) =>
        LogsTextBox.Text += line + '\n';
}
