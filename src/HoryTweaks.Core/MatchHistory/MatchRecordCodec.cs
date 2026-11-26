using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HoryTweaks.Core.MatchHistory;

/// <summary>
/// Serializes match records to a gzip-compressed JSON document. Player positions are packed into a
/// quantized little-endian binary blob (Base64 in the JSON) so a long match stays small.
/// </summary>
internal static class MatchRecordCodec
{
    /// <summary>
    /// The current file format version. Version 1 files (no player cosmetics) are still accepted;
    /// anything newer or older is rejected.
    /// </summary>
    internal const int FormatVersion = 2;

    private const float CoordinateScale = 100f;

    /// <summary>
    /// Encodes a match record as gzip-compressed UTF-8 JSON.
    /// </summary>
    internal static byte[] Encode(MatchRecord record)
    {
        var document = new MatchRecordDocument
        {
            Version = FormatVersion,
            StartedUtc = record.StartedUtc,
            DurationMs = record.DurationMs,
            MapId = record.MapId,
            MapScale = record.MapScale,
            MapFlipX = record.MapFlipX,
            HideAndSeek = record.HideAndSeek,
            GameOverReason = record.GameOverReason,
            LocalWon = record.LocalWon,
            Players = [.. record.Players.Select(MatchPlayerDocument.From)],
            Events = [.. record.Events],
            Frames = Convert.ToBase64String(EncodeFrames(record.Frames))
        };

        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            JsonSerializer.Serialize(gzip, document);
        }

        return output.ToArray();
    }

    /// <summary>
    /// Decodes a match record. Returns <c>null</c> for corrupt data or an unsupported version.
    /// When <paramref name="includeFrames"/> is false the position blob is skipped and
    /// <see cref="MatchRecord.Frames"/> is empty.
    /// </summary>
    internal static MatchRecord? Decode(byte[] data, bool includeFrames)
    {
        try
        {
            MatchRecordDocument? document;
            using (var input = new MemoryStream(data, writable: false))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            {
                document = JsonSerializer.Deserialize<MatchRecordDocument>(gzip);
            }

            if (document == null || document.Version < 1 || document.Version > FormatVersion || document.Players == null || document.Events == null)
                return null;

            IReadOnlyList<MatchFrame> frames = includeFrames
                ? DecodeFrames(Convert.FromBase64String(document.Frames ?? string.Empty))
                : [];

            return new MatchRecord(
                document.StartedUtc,
                document.DurationMs,
                document.MapId,
                document.MapScale,
                document.MapFlipX,
                document.HideAndSeek,
                document.GameOverReason,
                document.LocalWon,
                document.Players.Select(player => player.ToPlayer()).ToList(),
                document.Events,
                frames);
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException or FormatException or EndOfStreamException or ArgumentException)
        {
            return null;
        }
    }

    private static byte[] EncodeFrames(IReadOnlyList<MatchFrame> frames)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream))
        {
            foreach (var frame in frames)
            {
                int count = Math.Min(frame.Positions.Count, byte.MaxValue);
                writer.Write(frame.TimeMs);
                writer.Write((byte)count);
                for (int i = 0; i < count; i++)
                {
                    var position = frame.Positions[i];
                    writer.Write(position.PlayerId);
                    writer.Write(Quantize(position.X));
                    writer.Write(Quantize(position.Y));
                    writer.Write((byte)position.Flags);
                }
            }
        }

        return stream.ToArray();
    }

    private static List<MatchFrame> DecodeFrames(byte[] blob)
    {
        var frames = new List<MatchFrame>();
        using var reader = new BinaryReader(new MemoryStream(blob, writable: false));
        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            int timeMs = reader.ReadInt32();
            int count = reader.ReadByte();
            var positions = new MatchPosition[count];
            for (int i = 0; i < count; i++)
            {
                byte playerId = reader.ReadByte();
                float x = reader.ReadInt16() / CoordinateScale;
                float y = reader.ReadInt16() / CoordinateScale;
                var flags = (MatchPositionFlags)reader.ReadByte();
                positions[i] = new MatchPosition(playerId, x, y, flags);
            }

            frames.Add(new MatchFrame(timeMs, positions));
        }

        return frames;
    }

    private static short Quantize(float value)
    {
        return (short)Math.Clamp(Math.Round(value * CoordinateScale), short.MinValue, short.MaxValue);
    }

    private sealed class MatchRecordDocument
    {
        [JsonPropertyName("v")]
        public int Version { get; set; }

        [JsonPropertyName("startedUtc")]
        public DateTime StartedUtc { get; set; }

        [JsonPropertyName("durationMs")]
        public int DurationMs { get; set; }

        [JsonPropertyName("mapId")]
        public byte MapId { get; set; }

        [JsonPropertyName("mapScale")]
        public float MapScale { get; set; }

        [JsonPropertyName("mapFlipX")]
        public bool MapFlipX { get; set; }

        [JsonPropertyName("hideAndSeek")]
        public bool HideAndSeek { get; set; }

        [JsonPropertyName("gameOverReason")]
        public int GameOverReason { get; set; }

        [JsonPropertyName("localWon")]
        public bool LocalWon { get; set; }

        [JsonPropertyName("players")]
        public List<MatchPlayerDocument>? Players { get; set; }

        [JsonPropertyName("events")]
        public List<MatchEvent>? Events { get; set; }

        [JsonPropertyName("frames")]
        public string? Frames { get; set; }
    }

    /// <summary>
    /// Serialized shape of a <see cref="MatchPlayer"/>. Properties stay nullable so records written
    /// before cosmetics existed (version 1) deserialize cleanly; <see cref="ToPlayer"/> coalesces.
    /// </summary>
    private sealed class MatchPlayerDocument
    {
        public byte Id { get; set; }
        public string? Name { get; set; }
        public int ColorId { get; set; }
        public int RoleType { get; set; }
        public bool IsImpostor { get; set; }
        public bool Won { get; set; }
        public bool IsLocal { get; set; }
        public MatchPlayerState FinalState { get; set; }
        public int Kills { get; set; }
        public int TasksDone { get; set; }
        public int TasksTotal { get; set; }
        public string? HatId { get; set; }
        public string? SkinId { get; set; }
        public string? VisorId { get; set; }

        internal static MatchPlayerDocument From(MatchPlayer player)
        {
            return new MatchPlayerDocument
            {
                Id = player.Id,
                Name = player.Name,
                ColorId = player.ColorId,
                RoleType = player.RoleType,
                IsImpostor = player.IsImpostor,
                Won = player.Won,
                IsLocal = player.IsLocal,
                FinalState = player.FinalState,
                Kills = player.Kills,
                TasksDone = player.TasksDone,
                TasksTotal = player.TasksTotal,
                HatId = player.HatId,
                SkinId = player.SkinId,
                VisorId = player.VisorId
            };
        }

        internal MatchPlayer ToPlayer()
        {
            return new MatchPlayer(
                Id,
                Name ?? "",
                ColorId,
                RoleType,
                IsImpostor,
                Won,
                IsLocal,
                FinalState,
                Kills,
                TasksDone,
                TasksTotal,
                HatId ?? "",
                SkinId ?? "",
                VisorId ?? "");
        }
    }
}
