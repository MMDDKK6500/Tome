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

namespace GamebananaAPI.Data.Submission;

public class SubmissionFile
{
    [JsonPropertyName("_idRow")]
    public int Id { get; set; }

    [JsonPropertyName("_sFile")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("_sDescription")]
    public string? Description { get; set; }

    [JsonPropertyName("_sVersion")]
    public string? Version { get; set; }

    [JsonPropertyName("_nFilesize")]
    public long Size { get; set; }

    [JsonPropertyName("_tsDateAdded")]
    [JsonConverter(typeof(UnixDateTimeConverter))]
    public DateTime Added { get; set; }

    [JsonPropertyName("_nDownloadCount")]
    public int Downloads { get; set; }

    [JsonPropertyName("_sDownloadUrl")]
    public string DownloadUrl { get; set; } = null!;

    [JsonPropertyName("_sMd5Checksum")]
    public string Md5Checksum { get; set; } = null!;
}