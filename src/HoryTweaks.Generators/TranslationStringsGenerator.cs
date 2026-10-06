using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

using System.Text;
using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace HoryTweaks.Generators;

/// <summary>
/// Generates <c>BetterAmongUs.Generated.TranslationStrings</c> fields from the English
/// translation catalog so translation keys stay strongly typed without a PowerShell step.
/// </summary>
[Generator]
public sealed class TranslationStringsGenerator : IIncrementalGenerator
{
    private const string CatalogFileName = "en_US.json";
    private const string ExcludedKey = "LanguageID";

    private static readonly DiagnosticDescriptor CatalogInvalid = new(
        id: "HTG001",
        title: "Translation catalog is missing or invalid",
        messageFormat: "Cannot generate TranslationStrings: {0}",
        category: "HoryTweaks.Generators",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor IdentifierCollision = new(
        id: "HTG002",
        title: "Translation keys collide after identifier conversion",
        messageFormat: "Translation keys {0} produce the same C# identifier '{1}'",
        category: "HoryTweaks.Generators",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor IdentifierEmpty = new(
        id: "HTG002",
        title: "Translation key produces an empty identifier",
        messageFormat: "Translation key '{0}' produces an empty C# identifier",
        category: "HoryTweaks.Generators",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var catalogs = context.AdditionalTextsProvider
            .Where(static file => Path.GetFileName(file.Path).Equals(CatalogFileName, StringComparison.OrdinalIgnoreCase))
            .Select(static (file, cancellationToken) => file.GetText(cancellationToken))
            .Collect();

        context.RegisterSourceOutput(catalogs, static (sourceContext, texts) =>
        {
            if (texts.Length == 0)
            {
                sourceContext.ReportDiagnostic(Diagnostic.Create(
                    CatalogInvalid, Location.None, $"catalog '{CatalogFileName}' is not listed in AdditionalFiles"));
                return;
            }

            var text = texts[0];
            if (text is null)
            {
                sourceContext.ReportDiagnostic(Diagnostic.Create(
                    CatalogInvalid, Location.None, $"catalog '{CatalogFileName}' could not be read"));
                return;
            }

            var entries = ParseCatalog(text.ToString(), out var parseError);
            if (parseError is not null)
            {
                sourceContext.ReportDiagnostic(Diagnostic.Create(CatalogInvalid, Location.None, parseError));
                return;
            }

            Emit(sourceContext, entries!);
        });
    }

    private static List<KeyValuePair<string, string>>? ParseCatalog(string json, out string? error)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            error = $"catalog '{CatalogFileName}' is empty";
            return null;
        }

        Dictionary<string, string>? raw;
        try
        {
            var settings = new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true };
            var serializer = new DataContractJsonSerializer(typeof(Dictionary<string, string>), settings);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            raw = serializer.ReadObject(stream) as Dictionary<string, string>;
        }
        catch (Exception ex) when (ex is SerializationException or XmlException or InvalidOperationException or ArgumentException)
        {
            error = $"catalog '{CatalogFileName}' is not a valid flat JSON object: {ex.Message}";
            return null;
        }

        var entries = new List<KeyValuePair<string, string>>(raw?.Count ?? 0);
        if (raw is not null)
        {
            foreach (var pair in raw)
            {
                if (!string.Equals(pair.Key, ExcludedKey, StringComparison.Ordinal))
                {
                    entries.Add(pair);
                }
            }
        }

        if (entries.Count == 0)
        {
            error = $"catalog '{CatalogFileName}' contains no translation entries";
            return null;
        }

        entries.Sort(static (a, b) => string.CompareOrdinal(a.Key, b.Key));
        error = null;
        return entries;
    }

    private static void Emit(SourceProductionContext context, List<KeyValuePair<string, string>> entries)
    {
        var identifiers = new Dictionary<string, string>(StringComparer.Ordinal);
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("namespace BetterAmongUs.Generated;");
        builder.AppendLine();
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// Provides strongly-typed translation keys for BAU.");
        builder.AppendLine("/// Each field represents a translation key that can be used with the Translator.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine("public static partial class TranslationStrings");
        builder.AppendLine("{");

        var emitted = 0;
        foreach (var pair in entries)
        {
            var identifier = ToIdentifier(pair.Key);
            if (identifier.Length == 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(IdentifierEmpty, Location.None, pair.Key));
                continue;
            }

            var bareIdentifier = identifier.TrimStart('@');
            if (identifiers.TryGetValue(bareIdentifier, out var previous))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    IdentifierCollision, Location.None, $"'{previous}' and '{pair.Key}'", bareIdentifier));
                continue;
            }
            identifiers.Add(bareIdentifier, pair.Key);

            var literal = SymbolDisplay.FormatLiteral(pair.Key, quote: true);
            var comment = EscapeComment(pair.Value ?? string.Empty);
            builder.AppendLine();
            builder.AppendLine("    /// <summary>");
            builder.AppendLine($"    /// Base Translation: {comment}");
            builder.AppendLine("    /// </summary>");
            builder.AppendLine($"    public static readonly TranslationString {identifier} = new({literal});");
            emitted++;
        }

        builder.AppendLine("}");

        if (emitted == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                CatalogInvalid, Location.None, $"catalog '{CatalogFileName}' produced no usable translation entries"));
            return;
        }

        context.AddSource("TranslationStrings.g.cs", SourceText.From(builder.ToString(), Encoding.UTF8));
    }

    private static string ToIdentifier(string key)
    {
        var builder = new StringBuilder(key.Length + 1);
        if (key.Length > 0 && key[0] >= '0' && key[0] <= '9')
        {
            builder.Append('_');
        }

        foreach (var c in key)
        {
            builder.Append(IsIdentifierPart(c) ? c : '_');
        }

        // Collapse runs of two or more underscores into one.
        var collapsed = new StringBuilder(builder.Length);
        var pendingUnderscore = false;
        foreach (var c in builder.ToString())
        {
            if (c == '_')
            {
                if (pendingUnderscore)
                {
                    continue;
                }
                pendingUnderscore = true;
            }
            else
            {
                pendingUnderscore = false;
            }
            collapsed.Append(c);
        }

        var identifier = collapsed.ToString().TrimEnd('_');
        if (identifier.Length == 0)
        {
            return string.Empty;
        }

        return SyntaxFacts.GetKeywordKind(identifier) != SyntaxKind.None || IsReservedContextual(identifier)
            ? "@" + identifier
            : identifier;
    }

    private static bool IsReservedContextual(string identifier) =>
        // Contextual keywords that break member identifiers in this position.
        identifier is "value" or "and" or "or" or "not" or "with" or "init"
            or "required" or "scoped" or "file" or "managed" or "unmanaged" or "allows";


    private static bool IsIdentifierPart(char c) =>
        c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_';

    private static string EscapeComment(string value)
    {
        var escaped = value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
        // Keep every line of the translation inside the /// comment: encode raw
        // newlines as character entities so the doc comment stays a single block.
        return escaped.Replace("\r", "&#13;").Replace("\n", "&#10;");
    }
}
