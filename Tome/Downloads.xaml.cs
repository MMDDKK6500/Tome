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
                string? targetRelativePath = action.Type is ModActionType.Update or ModActionType.Delete
                    ? _manifest.Mods[id].RelativePath : null;

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

                        SubmissionFile remoteFile = submission.Files.Count > 1
                            ? await AskUserForFileAsync(submission)
                            : submission.Files[0];

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
                        InstalledMod registeredMod = _manifest.Mods[id];
                        string fullPath = Path.Combine(addonsFolder, registeredMod.RelativePath);
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

    private async Task<SubmissionFile> AskUserForFileAsync(SubmissionProfile submission)
    {
        FileSelectionText.Text = $"'{submission.Name}' has multiple files. Choose one to install:";
        _fileSelectionTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        List<OnlineModFileModel> options = [.. submission.Files!.Select(static file =>
            new OnlineModFileModel()
            {
                Text = !string.IsNullOrEmpty(file.Description)
                            ? $"{file.Name}: {file.Description}" : file.Name,
                File = file,
            })
        ];

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

            string addonsFolder = _deadlock.GetAddonsPath(_manifest.GamePath);
            string relativePath;

            if (action.Type is ModActionType.Update)
            {
                ArgumentException.ThrowIfNullOrEmpty(modFile.TargetRelativePath);
                File.Copy(tempPath, Path.Combine(addonsFolder, modFile.TargetRelativePath), overwrite: true);

                relativePath = modFile.TargetRelativePath;
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

            File.Copy(tempPath, Path.Combine(addonsFolder, relativePath));

            int modId = action.Mod.Id;
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
                    SubmissionCategoryType = string.Empty,
                    SubmissionCategoryTree = [],
                    FileId = modFile.GamebananaFile!.Id,
                    FileName = modFile.GamebananaFile!.Name,
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

    private void Cancel_Click(object sender, RoutedEventArgs e) =>
        _confirmTcs.TrySetResult(false);

    private void Continue_Click(object sender, RoutedEventArgs e) =>
        _confirmTcs.TrySetResult(true);

    private void Log(string line) =>
        LogsTextBox.Text += line + '\n';
}
