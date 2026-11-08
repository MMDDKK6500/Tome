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
using GamebananaAPI.Data.Game.Featured;
using GamebananaAPI.Data.Game.Search;
using GamebananaAPI.Data.Game.TopSubs;
using GamebananaAPI.Data.Submission;
using GamebananaAPI.Data.Submission.Updates;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GamebananaAPI.Data;

[JsonSerializable(typeof(List<TopLevelCategoryDefinition>))]
[JsonSerializable(typeof(List<CategoryDefinition>))]
[JsonSerializable(typeof(FeaturedSubmissions))]
[JsonSerializable(typeof(SearchSubmissions))]
[JsonSerializable(typeof(List<TopSubmission>))]
[JsonSerializable(typeof(SubmissionProfile))]
[JsonSerializable(typeof(SubmissionUpdates))]
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Serialization | JsonSourceGenerationMode.Metadata,
                             Converters = [typeof(UnixDateTimeConverter)])]
internal sealed partial class GamebananaJson : JsonSerializerContext;

internal sealed class UnixDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        long timestamp = reader.GetInt64();
        return DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        throw new NotImplementedException();
}
