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

using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Tome.Models;

namespace Tome;

public partial class AddLocalMod : Window
{
    private readonly TomeManifest _manifest;
    private readonly ModActions _pendingActions;
    public (int Id, InstalledMod Mod)? Replacing { get; private set; }

    public AddLocalMod(TomeManifest manifest, ModActions pendingActions)
    {
        InitializeComponent();

        _manifest = manifest;
        _pendingActions = pendingActions;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        List<ModReplacing> replacements = new(_manifest.Mods.Count + 1)
        {
            new("Install as new", 0, null)
        };

        foreach ((int id, InstalledMod mod) in _manifest.Mods)
        {
            if (_pendingActions.TryGetValue(id, out ModAction? pending)
                && pending.Type is not ModActionType.Update)
                continue;

            replacements.Add(new ModReplacing(mod.ListingMetadata?.Name ?? mod.RelativePath, id, mod));
        }

        ModReplacement.ItemsSource = replacements;
        ModReplacement.SelectedIndex = 0;
    }

    private void ModReplacement_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems is not { Count: > 0 }
            || e.AddedItems[0] is not ModReplacing replacing)
            return;

        bool isReplacing = replacing.Mod is not null;

        Replacing = isReplacing ? (replacing.ModId, replacing.Mod!) : null;
        ModName.IsEnabled = ModAuthor.IsEnabled = !isReplacing;

        if (isReplacing)
        {
            ModName.Text = replacing.Mod!.ListingMetadata?.Name;
            ModAuthor.Text = replacing.Mod!.ListingMetadata?.Author;
        }
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Title = "Select the mod file/archive",
            CheckPathExists = true,
            AddExtension = true,
            Filter = "Valve Pack (*.vpk)|*.vpk|" +
                     "RAR Archive (*.rar)|*.rar|" +
                     "ZIP Archive (*.zip)|*.zip|" +
                     "TAR Archive (*.tar)|*.tar|" +
                     "GZip Archive (*.gz)|*.gz|" +
                     "7-Zip Archive (*.7z)|*.7z|" +
                     "All Supported Files (*.vpk;*.rar;*.zip;*.tar;*.gz;*.7z)|*.vpk;*.rar;*.zip;*.tar;*.gz;*.7z|" +
                     "All Files (*.*)|*.*",
            FilterIndex = 6,
        };

        if (dialog.ShowDialog() == true)
            ModPath.Text = dialog.FileName;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(ModName.Text))
        {
            MessageBox.Show("Please enter a name for the mod to continue.", "Empty name",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (!File.Exists(ModPath.Text))
        {
            MessageBox.Show("Please enter a valid path for the mod to continue.", "Invalid path",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        DialogResult = true;
        Close();
    }
}
