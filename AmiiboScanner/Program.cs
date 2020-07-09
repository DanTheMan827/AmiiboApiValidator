using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AmiiboScanner
{
    class Program
    {
        enum ErrorBits
        {
            MissingArgs           = 0b1,
            MissingBin            = 0b10,
            MissingFromJson       = 0b100,
            MissingImage          = 0b1000,
            MissingGameSeries     = 0b10000,
            MissingAmiiboSeries   = 0b100000,
            NoAmiiboInGameSeries  = 0b1000000,
            NoAmiiboInSeries      = 0b10000000,
            NoAmiiboForCharacter  = 0b100000000,
            InvalidAmiiboSeriesID = 0b1000000000,
            InvalidAmiiboID       = 0b10000000000,
            InvalidCharacterID    = 0b100000000000,
            InvalidGameSeriesID   = 0b1000000000000,
            InvalidTypeID         = 0b10000000000000
        }
        static int Main(string[] args)
        {
            int returnValue = 0;
            if (args.Length < 1)
            {
                Console.WriteLine("No arguments provided");
                return (int)ErrorBits.MissingArgs;
            }

            var apiPath = args[0];
            string amiiboPath = null;

            if (apiPath.EndsWith("\\"))
                apiPath = apiPath.Substring(0, apiPath.Length - 1);

            var imagePath = $"{apiPath}\\images";
            var amiibo = new Dictionary<string, string>();
            var json = JsonSerializer.Deserialize<JsonModel>(File.ReadAllText($"{apiPath}\\database\\amiibo.json"));

            var needsSpacer = false;

            var writeSpacer = new Action(() =>
            {
                if (needsSpacer)
                {
                    Console.WriteLine("");
                    needsSpacer = false;
                }
            });

            if (args.Length > 1)
            {
                amiiboPath = args[1];

                if (amiiboPath.EndsWith("\\"))
                    amiiboPath = amiiboPath.Substring(0, amiiboPath.Length - 1);

                foreach (var file in Directory.GetFiles(amiiboPath, "*.bin", SearchOption.AllDirectories))
                {
                    string amiiboId = "0x" + BitConverter.ToString(File.ReadAllBytes(file).Skip(0x54).Take(8).ToArray()).Replace("-", "").ToLower();

                    if (!json.Amiibos.ContainsKey(amiiboId) && !amiibo.ContainsKey(amiiboId))
                    {
                        returnValue |= (int)ErrorBits.MissingFromJson;
                        Console.WriteLine($"Missing From JSON: {amiiboId} - {file.Substring(amiiboPath.Length + 1)}");
                        needsSpacer = true;
                    }

                    if (!amiibo.ContainsKey(amiiboId))
                    {
                        amiibo.Add(amiiboId, file);
                    }
                }

                writeSpacer();

                foreach (var pair in json.Amiibos)
                {
                    if (!amiibo.ContainsKey(pair.Key))
                    {
                        returnValue |= (int)ErrorBits.MissingBin;
                        Console.WriteLine($"Missing Bin: {pair.Key} - {pair.Value.Name}");
                        needsSpacer = true;
                    }
                }
            }

            writeSpacer();

            foreach (var pair in json.Amiibos.Where(e => !amiibo.ContainsKey(e.Key)))
            {
                amiibo.Add(pair.Key, pair.Value.Name);
            }

            foreach (var pair in amiibo)
            {
                if (!File.Exists($"{imagePath}\\icon_{pair.Key.Substring(2, 8)}-{pair.Key.Substring(10, 8)}.png"))
                {
                    returnValue |= (int)ErrorBits.MissingImage;
                    if (String.IsNullOrEmpty(amiiboPath))
                    {
                        Console.WriteLine($"Missing Image: {pair.Key} - {pair.Value}");
                    } 
                    else
                    {
                        Console.WriteLine($"Missing Image: {pair.Key} - {(pair.Value.StartsWith(amiiboPath) ? pair.Value.Substring(amiiboPath.Length + 1) : pair.Value)}");
                    }
                    needsSpacer = true;
                }
            }

            writeSpacer();

            foreach (var series in json.Amiibos.Keys.Select(e => e.Substring(0, 5)).Where(e => !json.GameSeries.ContainsKey(e)).Distinct())
            {
                returnValue |= (int)ErrorBits.MissingGameSeries;
                Console.WriteLine($"Missing Game Series: {series}");
                foreach (var pair in json.Amiibos.Where(e => e.Key.Substring(0, 5) == series))
                {
                    Console.WriteLine($"  {pair.Value.Name}");
                }
                Console.WriteLine("");
            }

            // No spacer here because the previous block adds one

            foreach (var series in json.Amiibos.Keys.Select(e => e.Substring(14, 2)).Where(e => !json.AmiiboSeries.ContainsKey($"0x{e}")).Distinct())
            {
                returnValue |= (int)ErrorBits.MissingAmiiboSeries;
                Console.WriteLine($"Missing Amiibo Series: 0x{series}");
                foreach (var pair in json.Amiibos.Where(e => e.Key.Substring(14, 2) == series))
                {
                    Console.WriteLine($"  {pair.Value.Name}");
                }
                Console.WriteLine("");
            }

            // No spacer here because the previous block adds one

            foreach (var pair in json.GameSeries)
            {
                bool inSeries = false;

                foreach (var amiiboPair in json.Amiibos)
                {
                    if (pair.Key.Substring(2) == amiiboPair.Key.Substring(2, 3))
                    {
                        inSeries = true;
                        break;
                    }
                }

                if (!inSeries)
                {
                    returnValue |= (int)ErrorBits.NoAmiiboInGameSeries;
                    Console.WriteLine($"No amiibo part of game series: {pair.Key} - {pair.Value}");
                    needsSpacer = true;
                }
            }

            writeSpacer();

            foreach (var pair in json.AmiiboSeries)
            {
                bool inSeries = false;

                foreach (var amiiboPair in json.Amiibos)
                {
                    if (pair.Key.Substring(2) == amiiboPair.Key.Substring(14, 2))
                    {
                        inSeries = true;
                        break;
                    }
                }

                if (!inSeries)
                {
                    returnValue |= (int)ErrorBits.NoAmiiboInSeries;
                    Console.WriteLine($"No amiibo part series: {pair.Key} - {pair.Value}");
                    needsSpacer = true;
                }
            }

            writeSpacer();

            foreach (var pair in json.Characters)
            {
                bool containsCharacterID = false;

                foreach (var amiiboPair in json.Amiibos)
                {
                    if (pair.Key.Substring(2) == amiiboPair.Key.Substring(2, 4))
                    {
                        containsCharacterID = true;
                        break;
                    }
                }

                if (!containsCharacterID)
                {
                    returnValue |= (int)ErrorBits.NoAmiiboForCharacter;
                    Console.WriteLine($"No amiibo for character: {pair.Key} - {pair.Value}");
                    needsSpacer = true;
                }
            }

            writeSpacer();

            var hexRegex = new Regex("^0x[a-f0-9]+$", RegexOptions.Compiled);

            foreach (var pair in json.AmiiboSeries.Where(e => e.Key.Length != 4 || !hexRegex.IsMatch(e.Key)))
            {
                returnValue |= (int)ErrorBits.InvalidAmiiboSeriesID;
                Console.WriteLine($"Invalid amiibo series ID: {pair.Key} - {pair.Value}");
                needsSpacer = true;
            }

            writeSpacer();

            foreach (var pair in json.Amiibos.Where(e => e.Key.Length != 18 || !hexRegex.IsMatch(e.Key)))
            {
                returnValue |= (int)ErrorBits.InvalidAmiiboID;
                Console.WriteLine($"Invalid amiibo ID: {pair.Key} - {pair.Value.Name}");
                needsSpacer = true;
            }

            writeSpacer();

            foreach (var pair in json.Characters.Where(e => e.Key.Length != 6 || !hexRegex.IsMatch(e.Key)))
            {
                returnValue |= (int)ErrorBits.InvalidCharacterID;
                Console.WriteLine($"Invalid amiibo character ID: {pair.Key} - {pair.Value}");
                needsSpacer = true;
            }

            writeSpacer();

            foreach (var pair in json.GameSeries.Where(e => e.Key.Length != 5 || !hexRegex.IsMatch(e.Key)))
            {
                returnValue |= (int)ErrorBits.InvalidGameSeriesID;
                Console.WriteLine($"Invalid game series ID: {pair.Key} - {pair.Value}");
                needsSpacer = true;
            }

            writeSpacer();

            foreach (var pair in json.Types.Where((e => e.Key.Length != 4 || !hexRegex.IsMatch(e.Key))))
            {
                returnValue |= (int)ErrorBits.InvalidTypeID;
                Console.WriteLine($"Invalid amiibo type ID: {pair.Key} - {pair.Value}");
                needsSpacer = true;
            }

            return returnValue;
        }
    }

    public class JsonModel
    {
        public class Amiibo
        {
            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("release")]
            public Dictionary<string, string> Release { get; set; }
        }

        [JsonPropertyName("amiibo_series")]
        public Dictionary<string, string> AmiiboSeries { get; set; }

        [JsonPropertyName("amiibos")]
        public Dictionary<string, Amiibo> Amiibos { get; set; }

        [JsonPropertyName("characters")]
        public Dictionary<string, string> Characters { get; set; }

        [JsonPropertyName("game_series")]
        public Dictionary<string, string> GameSeries { get; set; }

        [JsonPropertyName("types")]
        public Dictionary<string, string> Types { get; set; }
    }
}
