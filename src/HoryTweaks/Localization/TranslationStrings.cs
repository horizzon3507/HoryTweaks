

using BetterAmongUs.Localization;

namespace BetterAmongUs.Generated;

/// <summary>
/// Provides strongly-typed translation keys for BAU.
/// Fields are generated from the English catalog; this partial holds the value type.
/// </summary>
public static partial class TranslationStrings
{
    /// <summary>
    /// Represents a translation key with the ability to get the localized string.
    /// </summary>
    public readonly struct TranslationString(string key)
    {
        public readonly string Key = key;
        public string LocalizedString => Translator.GetString(this);
        public override string ToString() => LocalizedString;
        public string Format(params string[] strings)
        {
            return string.Format(LocalizedString, args: strings);
        }
        public string Format(params TranslationString[] translationStrings)
        {
            var stringArgs = translationStrings.Select(ts => ts.LocalizedString).ToArray();
            return string.Format(LocalizedString, stringArgs);
        }
        public string Format(params object[] args)
        {
            return string.Format(LocalizedString, args);
        }
    }
}
