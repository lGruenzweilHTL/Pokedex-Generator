using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;

namespace PokemonFetcher;

public class Program
{
    private const string CACHE_PATH = "cache.txt";
    private const int NUM_POKEMON = 151;
    private static readonly string[] regionsToFetch = ["kanto"];
    private static readonly string[] versionFilter = ["red", "blue", "yellow"];
    private static Dictionary<string, string[]> locationCache;
    
    // Pokemon that don't have the general kind
    private static readonly Dictionary<int, PokemonKind> PokemonKinds = new()
    {
        { 1, PokemonKind.Starter }, { 4, PokemonKind.Starter }, { 7, PokemonKind.Starter },
        { 144, PokemonKind.Legendary }, { 145, PokemonKind.Legendary }, { 146, PokemonKind.Legendary },
        { 150, PokemonKind.Legendary }, { 151, PokemonKind.Mythical },
        { 149, PokemonKind.PseudoLegendary }
    };
    
    private const string PATH_TO_POKEMON_KIND_SUBPAGE = "../KindOfPokemons-Subpage/kindOfPokemon.html";
    private static readonly Dictionary<PokemonKind, (string name, string link)> KindOfPokemonData = new()
    {
        { PokemonKind.General, ("General", PATH_TO_POKEMON_KIND_SUBPAGE + "#general") },
        { PokemonKind.Starter, ("Starter", PATH_TO_POKEMON_KIND_SUBPAGE + "#starter") },
        { PokemonKind.Legendary, ("Legendary", PATH_TO_POKEMON_KIND_SUBPAGE + "#legendary") },
        { PokemonKind.Mythical, ("Mythical", PATH_TO_POKEMON_KIND_SUBPAGE + "#mythical") },
        { PokemonKind.PseudoLegendary, ("Pseudo-Legendary", PATH_TO_POKEMON_KIND_SUBPAGE + "#pseudo") }
    };

    static void Main(string[] args)
    {
        if (!ArgsValid(args, out string mainPagePath, out string subPageDirectory, out string stylesheetPath,
                out string subPageStylesheet, out string locationPath))
        {
            return;
        }

        var timer = Stopwatch.StartNew();

        // Load API cache
        if (File.Exists(CACHE_PATH))
        {
            _apiCache = LoadApiCache(CACHE_PATH);
        }

        Console.WriteLine("Cache loaded successfully.");

        locationCache = regionsToFetch
            .Select(FetchRegion)
            .ToDictionary(r => r["name"].ToString(),
                r => r["locations"]
                    .AsArray()
                    .Select(l => l["name"].ToString())
                    .ToArray());
        
        var descriptions = JsonNode.Parse(File.ReadAllText("descriptions.json"))?.AsArray().Select(d => d.ToString()).ToArray();

        Console.WriteLine($@"Finished fetching the region(s) {string.Join(", ", regionsToFetch)}");

        var jsonObject = FetchPokemon(0, NUM_POKEMON);
        var pokemonArray = jsonObject["results"].AsArray();
        var htmlGenerator = new HtmlBuilder();
        htmlGenerator.OpenDocument("Pokedex", stylesheetPath);
        htmlGenerator.InsertText(
            "<h1 style=\"text-align: center\">Pokedex</h1><p style=\"text-align: center\">(First Generation)</p>");
        htmlGenerator.OpenTag("table");
        for (int i = 0; i < pokemonArray.Count;)
        {
            JsonNode? pokemon = pokemonArray[(Index)i];
            BuildSinglePokemon(ref htmlGenerator, mainPagePath, subPageDirectory, subPageStylesheet, locationPath,
                pokemon, descriptions[i], PokemonKinds.GetValueOrDefault(i + 1, PokemonKind.General));
            Console.WriteLine(
                $@"{++i}/{NUM_POKEMON} (after {timer.Elapsed:mm\:ss\.fff}, total of {ApiRequestCount} new API requests and {CachedRequestCount} cached ones)");
        }

        htmlGenerator.CloseTag();
        htmlGenerator.CloseDocument();

        File.WriteAllText(mainPagePath, htmlGenerator.ToString());

        timer.Stop();

        // Save API cache
        SaveApiCache(CACHE_PATH);
        Console.WriteLine("Cache saved successfully.");

        Console.WriteLine("Generation finished");
    }

    private static void BuildSinglePokemon(ref HtmlBuilder mainPageGenerator, string mainPagePath,
        string subPageDirectory, string stylesheet, string location, JsonNode pokemon, string description, PokemonKind kind)
    {
        #region Get Data

        string index = pokemon["url"].ToString().Split("/")[6];

        pokemon = FetchJson(pokemon["url"].ToString());

        string pokedexNumber = $"#{index.PadLeft(3, '0')}";
        string fileName = pokemon["name"].ToString();
        string name = FormatPokemonName(fileName);
        string image =
            "<img src='https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/other/official-artwork/" +
            index + ".png' alt=\"\"/>";
        string floatingImage =
            $"<img src='https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/{index}.png' class=\"float-left\" alt=\"{name}\"/>";
        string type = string.Join('/', pokemon["types"]
            .AsArray()
            .Select(type =>
            {
                var t = type["type"]["name"];
                return $"<a href=\"types.html#{t}\">{t}</a>";
            }));
        string typesSubpage = string.Join('/', pokemon["types"]
            .AsArray()
            .Select(type =>
            {
                var t = type["type"]["name"];
                return $"<a href=\"../types.html#{t}\">{t}</a>";
            }));
        string kindOfPokemonLink = $"<a href=\"{KindOfPokemonData[kind].link}\">{KindOfPokemonData[kind].name}</a>";
        var stats = pokemon["stats"];
        string hp = stats[0]["base_stat"].ToString();
        string attack = stats[1]["base_stat"].ToString();
        string defense = stats[2]["base_stat"].ToString();
        string specialAttack = stats[3]["base_stat"].ToString();
        string specialDefense = stats[4]["base_stat"].ToString();
        string speed = stats[5]["base_stat"].ToString();

        var types = pokemon["types"].AsArray().Select(type => type["type"]["name"].ToString());
        var effectiveness = CalculateTypeEffectiveness(types);
        var weaknessResistanceTable = BuildWeaknessResistanceTable(effectiveness);

        var locations = JsonNode.Parse(GetOrFetchUrl(pokemon["location_area_encounters"].ToString())).AsArray().Select(
            area => new PokemonEncounter(area["location_area"]["name"].ToString(),
                area["version_details"].AsArray().Select(vd => vd["version"]["name"].ToString()).ToArray())).ToArray();

        string locationString = BuildLocationMap(locations, subPageDirectory, location, locationCache);

        #endregion

        #region Build main page content

        mainPageGenerator.OpenTag("tr");

        mainPageGenerator.OpenTag("td");
        mainPageGenerator.InsertText(pokedexNumber);
        mainPageGenerator.CloseTag();

        mainPageGenerator.OpenTag("td");
        mainPageGenerator.InsertText(image);
        mainPageGenerator.CloseTag();

        {
            string targetPath = Path.Combine(subPageDirectory, fileName + ".html");

            mainPageGenerator.OpenTag("td");
            mainPageGenerator.InsertText($"<a href=\"{GetRelativePath(mainPagePath, targetPath)}\">{name}</a>");
            mainPageGenerator.CloseTag();
        }

        mainPageGenerator.OpenTag("td");
        mainPageGenerator.InsertText(type);
        mainPageGenerator.CloseTag();

        mainPageGenerator.CloseTag();

        #endregion

        #region Build sub page content

        string subpage = $"""
                          <!DOCTYPE html>
                          <html lang="en">
                          <head>
                              <meta charset="UTF-8">
                              <meta name="viewport" content="width=device-width, initial-scale=1">
                              <title>{name}</title>
                              <link rel="stylesheet" href="{stylesheet.Replace('\\', '/')}">
                          </head>
                          <body>
                          <nav class="navigation-bar">
                              <ul>
                                  <li><a href="../../index.html">Home</a></li>
                                  <li><a href="../pokedex.html">Pokedex</a></li>
                                  <li><a href="../types.html">Types</a></li>
                                  <li><a href="../KindOfPokemons-Subpage/kindOfPokemon.html">Kinds</a></li>
                              </ul>
                          </nav>
                          <h1>{name}</h1>
                              {floatingImage}
                              <p class="description">{description}</p>
                              <p class="clearfix">Type: {typesSubpage}</p>
                              <p class="kind clearfix">Kind of Pokémon: {kindOfPokemonLink}</p>
                              <table class="resistance-table clearfix">
                                  <thead>
                                      <tr>
                                          <th colspan="6">Weaknesses</th>
                                      </tr>
                                      <tr>
                                          <th>0x</th>
                                          <th>1/4x</th>
                                          <th>1/2x</th>
                                          <th>1x</th>
                                          <th>2x</th>
                                          <th>4x</th>
                                      </tr>
                                  </thead>
                                  <tbody>
                                      {weaknessResistanceTable}
                                  </tbody>
                              </table>
                              <br>
                              <div class="bar-chart">
                                  <div class="bar-container">
                                      <div class="bar-label">HP</div>
                                      <div class="bar" style="width: {hp}px;">{hp}</div>
                                  </div>
                                  <div class="bar-container">
                                      <div class="bar-label">Attack</div>
                                      <div class="bar" style="width: {attack}px;">{attack}</div>
                                  </div>
                                  <div class="bar-container">
                                      <div class="bar-label">Defense</div>
                                      <div class="bar" style="width: {defense}px;">{defense}</div>
                                  </div>
                                  <div class="bar-container">
                                      <div class="bar-label">Special Attack</div>
                                      <div class="bar" style="width: {specialAttack}px;">{specialAttack}</div>
                                  </div>
                                  <div class="bar-container">
                                      <div class="bar-label">Special Defense</div>
                                      <div class="bar" style="width: {specialDefense}px;">{specialDefense}</div>
                                  </div>
                                  <div class="bar-container">
                                      <div class="bar-label">Speed</div>
                                      <div class="bar" style="width: {speed}px;">{speed}</div>
                                  </div>
                              </div>
                              <div>
                                  <h2>Locations</h2>
                                      {locationString}
                              </div>
                          </body>
                          </html>
                          """;

        string path = Path.Combine(subPageDirectory, $"{fileName}.html");
        File.WriteAllText(path, subpage);

        #endregion
    }
    
    private static string FormatPokemonName(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return string.Join('-', name.Split('-').Select(part => char.ToUpper(part[0]) + part[1..].ToLower()));
    }

    #region Fetch

    private static int ApiRequestCount = 0;
    private static int CachedRequestCount = 0;

    private static string Fetch(string url)
    {
        using HttpClient client = new();
        var response = client.GetAsync(url).Result;
        response.EnsureSuccessStatusCode();
        var content = response.Content.ReadAsStringAsync().Result;
        ApiRequestCount++;
        return content;
    }


    private static Dictionary<string, string> _apiCache = new();

    private static string GetOrFetchUrl(string url)
    {
        if (_apiCache.TryGetValue(url, out var content))
        {
            CachedRequestCount++;
            return content;
        }

        var result = Fetch(url);
        _apiCache.Add(url, result);
        return result;
    }

    private static JsonObject FetchJson(string url)
    {
        string content = GetOrFetchUrl(url);
        var jsonObject = JsonObject.Parse(content).AsObject();
        return jsonObject;
    }

    private static JsonObject FetchPokemon(int offset, int num)
    {
        return FetchJson($"https://pokeapi.co/api/v2/pokemon?offset={offset}&limit={num}");
    }

    private static JsonObject FetchRegion(string name)
    {
        return FetchJson($"https://pokeapi.co/api/v2/region/{name}");
    }

    private static Dictionary<string, string> LoadApiCache(string path)
    {
        var content = File.ReadAllLines(path);
        return content.Select(line => line.Split('\t')).ToDictionary(parts => parts[0], parts => parts[1]);
    }

    private static void SaveApiCache(string path)
    {
        var array = _apiCache.Select(pair => $"{pair.Key}\t{pair.Value}");
        File.WriteAllLines(path, array);
    }

    #endregion

    private static bool ArgsValid(string[] args, out string mainPagePath, out string subPagePath,
        out string stylesheetPath, out string subPageStylesheet, out string locationMaps)
    {
        mainPagePath = string.Empty;
        subPagePath = string.Empty;
        stylesheetPath = string.Empty;
        subPageStylesheet = string.Empty;
        locationMaps = string.Empty;

        // Expected args: Main page path, Subpage directory path, Stylesheet path, Pokemon count
        if (args.Length != 5)
        {
            Console.WriteLine(
                "Invalid number of arguments.\nHow to use: dotnet run <mainPagePath> <subPageDirectory> <stylesheetPath> (relative to mainPagePath) <subPageStylesheet> (relative to subPageDirectory)");
            return false;
        }

        mainPagePath = args[0];
        subPagePath = args[1];
        stylesheetPath = args[2];
        subPageStylesheet = args[3];
        locationMaps = args[4];
        return true;
    }

    #region Types

    // Define type effectiveness (outer dictionary: defender, inner dictionary: attacker)
    private static readonly Dictionary<string, Dictionary<string, double>> TypeWeaknesses = new()
    {
        {
            "normal", new Dictionary<string, double>
            {
                { "fighting", 2 }, { "ghost", 0 }
            }
        },
        {
            "fire", new Dictionary<string, double>
            {
                { "fire", 0.5 }, { "water", 2 }, { "grass", 0.5 }, { "ice", 0.5 }, { "ground", 2 }, { "bug", 0.5 },
                { "rock", 2 }, { "steel", 0.5 }, { "fairy", 0.5 }
            }
        },
        {
            "water", new Dictionary<string, double>
            {
                { "fire", 0.5 }, { "water", 0.5 }, { "electric", 2 }, { "grass", 2 }, { "ice", 0.5 }, { "steel", 0.5 }
            }
        },
        {
            "electric", new Dictionary<string, double>
            {
                { "electric", 0.5 }, { "ground", 2 }, { "flying", 0.5 }, { "steel", 0.5 }
            }
        },
        {
            "grass", new Dictionary<string, double>
            {
                { "fire", 2 }, { "water", 0.5 }, { "electric", 0.5 }, { "grass", 0.5 }, { "ice", 2 }, { "poison", 2 },
                { "ground", 0.5 }, { "flying", 2 }, { "bug", 2 }
            }
        },
        {
            "ice", new Dictionary<string, double>
            {
                { "fire", 2 }, { "ice", 0.5 }, { "fighting", 2 }, { "rock", 2 }, { "steel", 2 }
            }
        },
        {
            "fighting", new Dictionary<string, double>
            {
                { "flying", 2 }, { "psychic", 2 }, { "bug", 0.5 }, { "rock", 0.5 }, { "dark", 0.5 }, { "fairy", 2 }
            }
        },
        {
            "poison", new Dictionary<string, double>
            {
                { "grass", 0.5 }, { "fighting", 0.5 }, { "poison", 0.5 }, { "ground", 2 }, { "psychic", 2 },
                { "bug", 0.5 }, { "fairy", 0.5 }
            }
        },
        {
            "ground", new Dictionary<string, double>
            {
                { "water", 2 }, { "grass", 2 }, { "ice", 2 }, { "poison", 0.5 }, { "rock", 0.5 }, { "electric", 0 }
            }
        },
        {
            "flying", new Dictionary<string, double>
            {
                { "electric", 2 }, { "grass", 0.5 }, { "fighting", 0.5 }, { "bug", 0.5 }, { "rock", 2 }, { "ice", 2 },
                { "ground", 0 }
            }
        },
        {
            "psychic", new Dictionary<string, double>
            {
                { "fighting", 0.5 }, { "psychic", 0.5 }, { "bug", 2 }, { "ghost", 2 }, { "dark", 2 }
            }
        },
        {
            "bug", new Dictionary<string, double>
            {
                { "fire", 2 }, { "grass", 0.5 }, { "fighting", 0.5 }, { "ground", 0.5 }, { "flying", 2 }, { "rock", 2 }
            }
        },
        {
            "rock", new Dictionary<string, double>
            {
                { "normal", 0.5 }, { "fire", 0.5 }, { "water", 2 }, { "grass", 2 }, { "fighting", 2 },
                { "poison", 0.5 }, { "ground", 2 }, { "flying", 0.5 }, { "steel", 2 }
            }
        },
        {
            "ghost", new Dictionary<string, double>
            {
                { "normal", 0 }, { "fighting", 0 }, { "poison", 0.5 }, { "bug", 0.5 }, { "ghost", 2 }, { "dark", 2 }
            }
        },
        {
            "dragon", new Dictionary<string, double>
            {
                { "fire", 0.5 }, { "water", 0.5 }, { "grass", 0.5 }, { "electric", 0.5 }, { "ice", 2 }, { "dragon", 2 },
                { "fairy", 2 }
            }
        },
        {
            "dark", new Dictionary<string, double>
            {
                { "fighting", 2 }, { "psychic", 0 }, { "bug", 2 }, { "ghost", 0.5 }, { "dark", 0.5 }, { "fairy", 2 }
            }
        },
        {
            "steel", new Dictionary<string, double>
            {
                { "normal", 0.5 }, { "fire", 2 }, { "grass", 0.5 }, { "ice", 0.5 }, { "fighting", 2 }, { "poison", 0 },
                { "ground", 2 }, { "flying", 0.5 }, { "psychic", 0.5 }, { "bug", 0.5 }, { "rock", 0.5 },
                { "dragon", 0.5 }, { "steel", 0.5 }, { "fairy", 0.5 }
            }
        },
        {
            "fairy", new Dictionary<string, double>
            {
                { "fighting", 0.5 }, { "poison", 2 }, { "bug", 0.5 }, { "dragon", 0 }, { "dark", 0.5 }, { "steel", 2 }
            }
        }
    };

    // Calculate resistances/weaknesses
    private static Dictionary<string, double> CalculateTypeEffectiveness(IEnumerable<string> types)
    {
        var effectiveness = new Dictionary<string, double>();

        foreach (var type in types)
        {
            foreach (var (targetType, multiplier) in TypeWeaknesses[type])
            {
                if (!effectiveness.TryAdd(targetType, multiplier))
                {
                    effectiveness[targetType] *= multiplier;
                }
            }
        }

        // Ensure all types have at least a neutral effectiveness
        foreach (var type in TypeWeaknesses.Keys)
        {
            effectiveness.TryAdd(type, 1);
        }

        return effectiveness;
    }

    // Fill the table in the subpage
    private static string BuildWeaknessResistanceTable(Dictionary<string, double> effectiveness)
    {
        var table = new StringBuilder();
        table.Append("<tr>");

        var categories = new[] { 0, 0.25, 0.5, 1, 2, 4 };
        foreach (var category in categories)
        {
            table.Append("<td>");
            var types = effectiveness.Where(e => e.Value == category).Select(e => e.Key);
            table.Append(string.Join(", ", types));
            table.Append("</td>");
        }

        table.Append("</tr>");
        return table.ToString();
    }

    #endregion

    #region Maps

    private static string BuildLocationMap(PokemonEncounter[] encounters, string fileDirectory,
        string drawingPath, Dictionary<string, string[]> locationCache)
    {
        StringBuilder locationString = new();

        if (encounters.Length == 0 || locationCache.Count == 0) return "<p>None</p>";

        // Group by region
        var groupedLocations = from area in encounters
            where area.Versions.Any(versionFilter.Contains)
            let region =
                locationCache.FirstOrDefault(r => r.Value.Any(area.Name.StartsWith),
                    new KeyValuePair<string, string[]>(null, null))
            let location = region.Value?.Where(area.Name.StartsWith).MaxBy(a => a.Length)
            group (area.Name, location) by region.Key;

        if (!groupedLocations.Any()) return "<p>None</p>";

        string relativePath = GetRelativePath(fileDirectory, drawingPath);

        List<string> backupLocations = [];
        foreach (var groupedLocation in groupedLocations)
        {
            // Add region name
            bool regionExists = false;
            if (groupedLocation.Key != null)
            {
                string region = groupedLocation.Key;
                string regionPath = Path.Combine(relativePath, $"Regions/{region}.png");
                regionExists = File.Exists(Path.Combine(drawingPath, $"Regions/{region}.png"));
                if (regionExists)
                    locationString.Append(
                        $"<div class=\"image-container\"><img src=\"{regionPath}\" alt=\"{region}\"/>");
            }

            foreach (var (encounter, locationName) in groupedLocation)
            {
                // Try to find area drawing
                if (!regionExists) goto BackupList;

                string areaPath = Path.Combine(drawingPath, $"Areas/{encounter}.png");
                bool areaExists = File.Exists(areaPath);
                if (areaExists)
                {
                    string path = Path.Combine(relativePath, $"Areas/{encounter}.png");
                    locationString.Append($"<img src=\"{path}\" alt=\"{encounter}\"/>");
                    continue;
                }

                // Fallback: Try to find location drawing
                string locationPath = Path.Combine(drawingPath, $"Locations/{locationName}.png");
                bool locationExists = File.Exists(locationPath);
                if (locationExists)
                {
                    string path = Path.Combine(relativePath, $"Locations/{locationName}.png");
                    locationString.Append($"<img src=\"{path}\" alt=\"{locationName}\"/>");
                    continue;
                }

                // Fallback: Add to list (<ul class="location-list">)
                BackupList:
                backupLocations.Add(encounter);
            }

            if (regionExists) locationString.Append("</div>");
        }

        locationString.Append("<ul class=\"location-list\">");
        foreach (var backupLocation in backupLocations)
        {
            locationString.Append($"<li>{backupLocation}</li>");
        }

        locationString.Append("</ul>");

        return locationString.ToString();
    }

    #endregion

    #region Paths

    private static string GetRelativePath(string src, string target)
    {
        string basePath = Directory.Exists(src)
            ? src
            : Path.GetDirectoryName(src);
        string relativePath = Path.GetRelativePath(basePath, target);
        return relativePath.Replace('\\', '/');
    }

    #endregion
}

public class HtmlBuilder
{
    private readonly Stack<string> _tagStack = new();
    private readonly StringBuilder _htmlBuilder = new();

    public void OpenDocument(string title, string? stylesheet = null)
    {
        string stylesheetLink = stylesheet != null ? $"<link rel=\"stylesheet\" href=\"{stylesheet}\">" : string.Empty;
        _htmlBuilder.Append(
            $"<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"UTF-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\"><title>{title}</title>{stylesheetLink}</head><body>");
    }

    public void CloseDocument()
    {
        _htmlBuilder.Append("</body></html>");
    }

    public void OpenTag(string tag)
    {
        string tagName = tag.Split(' ')[0];
        _tagStack.Push(tagName);
        _htmlBuilder.Append($"<{tag}>");
    }

    public void InsertText(string text)
    {
        _htmlBuilder.Append(text);
    }

    public void CloseTag()
    {
        var tag = _tagStack.Pop();
        _htmlBuilder.Append($"</{tag}>");
    }

    public override string ToString()
    {
        return _htmlBuilder.ToString();
    }
}

public record PokemonEncounter(string Name, string[] Versions);

public enum PokemonKind
{
    General,
    Starter,
    Legendary,
    Mythical,
    PseudoLegendary
}