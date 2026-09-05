using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Fulcrum.Core.Resources
{
    /// <summary>
    /// Resolves raw iRacing manufacturer/model and club/country values into
    /// stable resource keys consumed by every Fulcrum dashboard.
    /// </summary>
    public sealed class RelativeResourceResolver
    {
        private static readonly AliasRule[] ManufacturerRules =
        {
            Rule("PORSCHE", "porsche", "porsche9922cup", "porsche992cup", "porsche718gt4", "718 cayman", "cayman gt4", "911 cup", "911 gt3 cup", "gt3 cup", "992 cup", "992.2", "911 gt3 r"),
            Rule("LIGIER", "ligier", "ligierjsp320", "ligierjs320", "js p320", "jsp320", "js p3", "jsp3"),
            Rule("DALLARA", "dallara", "ir18", "dw12", "p217"),
            Rule("ACURA", "acura", "arx"),
            Rule("BMW", "bmw", "bmwm4gt4", "bmwm4gt4evo", "bmwm4gt4evo2025", "bmwm4gt4evo2024", "bmwm4gt4evo2023", "bmwg82gt4", "bmwg82m4gt4", "bmwm4", "m4gt4", "g82m4", "bmw m4 gt4", "bmw m4 gt4 evo", "g82", "g82 gt4", "m4 gt3", "m4 gt4", "m4 gt4 evo", "m2"),
            Rule("FORD", "ford", "fordmustanggt4", "mustanggt4", "mustang gt4", "mustang"),
            Rule("FERRARI", "ferrari", "296 gt3", "488"),
            Rule("MERCEDES", "mercedes", "mercedesamggt4", "mercedes-amg", "amg gt4", "amg"),
            Rule("CHEVROLET", "chevrolet", "corvette"),
            Rule("CADILLAC", "cadillac"),
            Rule("AUDI", "audi"),
            Rule("LAMBORGHINI", "lamborghini"),
            Rule("MCLAREN", "mclaren", "mclaren570sgt4", "570s gt4", "570s"),
            Rule("ASTONMARTIN", "aston martin", "astonmartin", "astonmartinvantagegt4", "vantage gt4"),
            Rule("TOYOTA", "toyota"),
            Rule("LEXUS", "lexus"),
            Rule("HONDA", "honda"),
            Rule("HPD", "hpd", "honda performance development", "arx-01c", "arx01c"),
            Rule("MAZDA", "mazda"),
            Rule("NISSAN", "nissan"),
            Rule("VOLKSWAGEN", "volkswagen", "vw"),
            Rule("SUBARU", "subaru"),
            Rule("KIA", "kia"),
            Rule("HYUNDAI", "hyundai"),
            Rule("BUICK", "buick", "regal"),
            Rule("HOLDEN", "holden", "commodore"),
            Rule("LOTUS", "lotus", "lotus49", "lotus 49", "lotus79", "lotus 79"),
            Rule("PONTIAC", "pontiac", "solstice"),
            Rule("RADICAL", "radical", "sr10", "sr8", "sr3"),
            Rule("RAM", "ram"),
            Rule("RAY", "ray", "ray gr22", "gr22", "formula ford", "formula 1600", "ff1600"),
            Rule("RENAULT", "renault"),
            Rule("RILEY", "riley", "mkxx", "daytona prototype"),
            Rule("RUF", "ruf", "rt12r", "ctr3"),
            Rule("TATUUS", "tatuus", "pm-18", "pm18", "usf-17", "usf17", "ft-60", "ft60"),
            Rule("WILLIAMS", "williams", "fw31"),
            Rule("IRACING", "fia f4", "formulair04", "formula ir-04", "formula ir04", "ir-04", "formulavee", "mini stock", "ministock", "dirtministock", "street stock", "streetstock", "dirtstreetstock", "late model stock", "latemodel2023", "super late model", "superlatemodel", "fia cross car", "crosscartn11", "dirtmodified", "dirtlatemodel", "dirtmicrosprint", "dirtmidget", "dirtsprint", "dirtumpmod", "protrucks", "skmodified", "silvercrown", "specracer")
        };

        private static readonly AliasRule[] CountryRules =
        {
            Rule("MX", "mx", "mexico", "méxico"),
            Rule("CA", "ca", "canada"),
            Rule("BR", "br", "brazil", "brasil"),
            Rule("AR", "ar", "argentina"),
            Rule("GB", "gb", "uk", "united kingdom", "great britain"),
            Rule("DE", "de", "germany", "deutschland"),
            Rule("FR", "fr", "france"),
            Rule("ES", "es", "spain", "iberia"),
            Rule("IT", "it", "italy"),
            Rule("AU", "au", "australia"),
            Rule("NZ", "nz", "new zealand"),
            Rule("JP", "jp", "japan"),
            Rule("KR", "kr", "korea", "south korea"),
            Rule("NL", "nl", "netherlands"),
            Rule("BE", "be", "belgium"),
            Rule("PL", "pl", "poland"),
            Rule("SE", "se", "sweden"),
            Rule("FI", "fi", "finland"),
            Rule("NO", "no", "norway"),
            Rule("DK", "dk", "denmark"),
            Rule("PT", "pt", "portugal"),
            Rule("AT", "at", "austria"),
            Rule("CH", "ch", "switzerland"),
            Rule("US", "us", "usa", "united states", "atlantic", "california",
                "carolina", "central", "florida", "georgia", "great plains",
                "illinois", "indiana", "massachusetts", "mid-south", "mid south",
                "midwest", "new england", "new jersey", "new york", "northwest",
                "ohio", "pennsylvania", "rocky mountain", "southeast",
                "south east", "southwest", "texas", "virginia", "washington",
                "wisconsin")
        };

        /// <summary>
        /// Resolves a car manufacturer from the structured iRacing car identity.
        /// CarPath is authoritative when known. This deliberately avoids using
        /// DriverInfoRaw because diagnostic/raw fields can contain unrelated
        /// numbers and strings which must never decide a vehicle brand.
        /// </summary>
        public string ResolveManufacturerAliasForCar(
            string manufacturer,
            string className,
            string carPath,
            string carScreenName,
            string carName)
        {
            string byPath = ResolveManufacturerFromCarPath(carPath);
            if (!string.IsNullOrEmpty(byPath)) return byPath;

            // Next trust an explicit manufacturer value from iRacing.
            string byManufacturer = Resolve(manufacturer, ManufacturerRules);
            if (!string.IsNullOrEmpty(byManufacturer)) return byManufacturer;

            // Last-resort fallback is limited to human-readable car fields.
            // Do not include DriverInfoRaw or session-wide diagnostics here.
            string descriptor = Join(Join(carScreenName, carName), className);
            return Resolve(descriptor, ManufacturerRules);
        }

        public string ResolveLogoResourceKeyForCar(
            string manufacturer,
            string className,
            string carPath,
            string carScreenName,
            string carName)
        {
            string alias = ResolveManufacturerAliasForCar(
                manufacturer,
                className,
                carPath,
                carScreenName,
                carName);

            return alias.Length == 0
                ? string.Empty
                : "Brand_" + ToResourceName(alias);
        }

        private static string ResolveManufacturerFromCarPath(string carPath)
        {
            string p = NormalizeCarPath(carPath);
            if (p.Length == 0) return string.Empty;

            // OEM / constructor paths. Exact model paths are listed where the
            // path itself does not contain the manufacturer name.
            if (p.StartsWith("acura")) return "ACURA";
            if (p.StartsWith("amvantage") || p.StartsWith("astonmartin")) return "ASTONMARTIN";
            if (p.StartsWith("audi")) return "AUDI";
            if (p.StartsWith("bmw")) return "BMW";
            if (p.Contains("buick")) return "BUICK";
            if (p.StartsWith("cadillac")) return "CADILLAC";
            if (p.StartsWith("chevy") || p.Contains("chevy") || p.StartsWith("c6r") ||
                p.StartsWith("c7vettedp") || p.StartsWith("c8rvettegte") || p.Contains("corvette") ||
                p.Contains("silverado") || p.Contains("camarozl1") || p.Contains("chevymontecarlo") ||
                p.Contains("camaro2019") || p.Contains("cruze")) return "CHEVROLET";
            if (p.StartsWith("dallara")) return "DALLARA";
            if (p.StartsWith("ferrari")) return "FERRARI";
            if (p.StartsWith("ford") || p.Contains("ford") || p.StartsWith("fr500s") ||
                p.Contains("mustang") || p.Contains("ford34c") || p.Contains("fordf150") ||
                p.Contains("fordtaurus")) return "FORD";
            if (p.StartsWith("honda") || p.EndsWith("\\honda")) return "HONDA";
            if (p.StartsWith("hpd")) return "HPD";
            if (p.Contains("holden")) return "HOLDEN";
            if (p.StartsWith("hyundai")) return "HYUNDAI";
            if (p.StartsWith("kia")) return "KIA";
            if (p.StartsWith("lamborghini")) return "LAMBORGHINI";
            if (p.StartsWith("ligier")) return "LIGIER";
            if (p.StartsWith("lotus")) return "LOTUS";
            if (p.StartsWith("mx5") || p.StartsWith("formulamazda")) return "MAZDA";
            if (p.StartsWith("mclaren")) return "MCLAREN";
            if (p.StartsWith("mercedes")) return "MERCEDES";
            if (p.StartsWith("nissan")) return "NISSAN";
            if (p.StartsWith("solstice") || p.Contains("pontiac")) return "PONTIAC";
            if (p.StartsWith("porsche")) return "PORSCHE";
            if (p.StartsWith("radical")) return "RADICAL";
            if (p.StartsWith("raygr22")) return "RAY";
            if (p.StartsWith("renault") || p.StartsWith("formularenault")) return "RENAULT";
            if (p.StartsWith("rileydp")) return "RILEY";
            if (p.StartsWith("ruf")) return "RUF";
            if (p.StartsWith("subaru")) return "SUBARU";
            if (p.StartsWith("indypropm18") || p.StartsWith("usf2000usf17")) return "TATUUS";
            if (p.StartsWith("toyota") || p.Contains("toyota") || p.Contains("arcatoyota") ||
                p.Contains("tundra") || p.Contains("supra2019") || p.Contains("corolla")) return "TOYOTA";
            if (p.StartsWith("vw") || p.StartsWith("jettatdi")) return "VOLKSWAGEN";
            if (p.StartsWith("williams")) return "WILLIAMS";

            // Cars without a meaningful external OEM/constructor identity in the
            // Relative use the iRacing brand rather than guessing another make.
            if (p.StartsWith("formulair04") || p.StartsWith("formulavee") ||
                p.StartsWith("ministock") || p.StartsWith("dirtministock") ||
                p.StartsWith("streetstock") || p.StartsWith("dirtstreetstock") ||
                p.StartsWith("latemodel2023") || p.StartsWith("superlatemodel") ||
                p.StartsWith("crosscartn11") || p.StartsWith("dirtmodified") ||
                p.StartsWith("dirtlatemodel") || p.StartsWith("dirtmicrosprint") ||
                p.StartsWith("dirtmidget") || p.StartsWith("dirtsprint") ||
                p.StartsWith("dirtumpmod") || p.StartsWith("protrucks") ||
                p.StartsWith("skmodified") || p.StartsWith("silvercrown") ||
                p.StartsWith("specracer")) return "IRACING";

            return string.Empty;
        }

        private static string NormalizeCarPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string p = value.Trim().Replace('/', '\\').ToLowerInvariant();
            while (p.StartsWith("\\")) p = p.Substring(1);
            return p;
        }

        public string ResolveManufacturerAlias(string manufacturer, string className, string descriptor = null)
        {
            string raw = Join(Join(manufacturer, className), descriptor);
            return Resolve(raw, ManufacturerRules);
        }

        public string ResolveLogoResourceKey(string manufacturer, string className, string descriptor = null)
        {
            string alias = ResolveManufacturerAlias(manufacturer, className, descriptor);
            return alias.Length == 0 ? string.Empty : "Brand_" + ToResourceName(alias);
        }

        public string ResolveCountryAlias(string countryCode, string clubName)
        {
            string code = Normalize(countryCode).ToUpperInvariant();
            if (!IsMissingValue(code) && (code.Length == 2 || code.Length == 3))
            {
                return code;
            }

            string club = Normalize(clubName);
            if (IsMissingValue(club)) return string.Empty;

            return Resolve(club, CountryRules);
        }

        public string ResolveFlagResourceKey(string countryCode, string clubName)
        {
            string alias = ResolveCountryAlias(countryCode, clubName);
            return alias.Length == 0 ? string.Empty : "Flag_" + alias;
        }

        private static AliasRule Rule(string key, params string[] aliases)
        {
            return new AliasRule(key, aliases);
        }

        private static string Resolve(string raw, AliasRule[] rules)
        {
            string normalized = Normalize(raw);
            if (IsMissingValue(normalized)) return string.Empty;

            for (int index = 0; index < rules.Length; index++)
            {
                AliasRule rule = rules[index];
                for (int aliasIndex = 0; aliasIndex < rule.Aliases.Length; aliasIndex++)
                {
                    string alias = Normalize(rule.Aliases[aliasIndex]);
                    if (alias.Length == 0) continue;

                    // Two-letter ISO codes must match exactly. Treating them
                    // as substrings caused the placeholder "None" to match
                    // Norway (NO). Longer names may still match inside raw
                    // iRacing descriptors such as "Porsche 992.2".
                    string compactValue = Compact(normalized);
                    string compactAlias = Compact(alias);
                    bool matches = alias.Length <= 2
                        ? normalized == alias
                        : normalized == alias ||
                          ContainsWholePhrase(normalized, alias) ||
                          (compactAlias.Length >= 4 && compactValue.Contains(compactAlias));

                    if (matches) return rule.Key;
                }
            }

            return string.Empty;
        }

        private static bool IsMissingValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ||
                   value == "none" ||
                   value == "null" ||
                   value == "unknown" ||
                   value == "n/a" ||
                   value == "-";
        }

        private static bool ContainsWholePhrase(string value, string phrase)
        {
            if (value == phrase) return true;
            string paddedValue = " " + value + " ";
            string paddedPhrase = " " + phrase + " ";
            return paddedValue.Contains(paddedPhrase);
        }


        private static string Compact(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            StringBuilder builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsLetterOrDigit(c)) builder.Append(c);
            }
            return builder.ToString();
        }

        private static string Join(string first, string second)
        {
            return (first ?? string.Empty) + " " + (second ?? string.Empty);
        }

        private static string ToResourceName(string value)
        {
            // Resource names are case-sensitive inside SimHub dashboards.
            // TextInfo.ToTitleCase would produce "Bmw", while the embedded
            // dashboard resource is intentionally named Brand_BMW.
            if (value == "BMW") return "BMW";
            if (value == "ASTONMARTIN") return "AstonMartin";
            if (value == "MERCEDES") return "Mercedes";
            if (value == "MCLAREN") return "McLaren";
            if (value == "VOLKSWAGEN") return "Volkswagen";
            if (value == "IRACING") return "iRacing";
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.ToLowerInvariant());
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string decomposed = value.Normalize(NormalizationForm.FormD);
            StringBuilder builder = new StringBuilder(decomposed.Length);
            for (int index = 0; index < decomposed.Length; index++)
            {
                char character = decomposed[index];
                UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(character);
                if (category != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(char.ToLowerInvariant(character));
                }
            }
            return builder.ToString().Normalize(NormalizationForm.FormC).Trim();
        }

        private sealed class AliasRule
        {
            public readonly string Key;
            public readonly string[] Aliases;
            public AliasRule(string key, string[] aliases)
            {
                Key = key ?? string.Empty;
                Aliases = aliases ?? new string[0];
            }
        }
    }
}
