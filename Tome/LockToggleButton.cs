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

using System.Windows.Controls.Primitives;

namespace Tome;

public sealed class LockToggleButton : ToggleButton
{
    public bool IsLocked { get; set; }

    public void LockOn(bool isChecked)
    {
        IsChecked = isChecked;
        IsLocked = true;
    }

    public void LockOff(bool isChecked)
    {
        IsChecked = isChecked;
        IsLocked = false;
    }

    protected override void OnToggle()
    {
        if (IsLocked)
            return;

        base.OnToggle();
    }
}
