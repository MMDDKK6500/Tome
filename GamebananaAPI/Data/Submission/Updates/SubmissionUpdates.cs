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

using System.Text.Json.Serialization;

namespace GamebananaAPI.Data.Submission.Updates;

public class SubmissionUpdates
{
    [JsonPropertyName("_aRecords")]
    public List<SubmissionUpdate> Updates { get; set; } = null!;

    [JsonPropertyName("_aMetadata")]
    public ListMetadata Metadata { get; set; } = null!;
}

public class SubmissionUpdate
{
    [JsonPropertyName("_idRow")]
    public int Id { get; set; }

    [JsonPropertyName("_tsDateAdded")]
    public DateTime Added { get; set; }

    [JsonPropertyName("_sName")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("_sText")]
    public string? Text { get; set; }

    [JsonPropertyName("_bIsSignificant")]
    public bool IsSignificant { get; set; }

    [JsonPropertyName("_sVersion")]
    public string? Version { get; set; }

    [JsonPropertyName("_aChangelog")]
    public List<Changelog>? Changelog { get; set; }
}

public class Changelog
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = null!;

    [JsonPropertyName("cat")]
    public string? Category { get; set; }
}
