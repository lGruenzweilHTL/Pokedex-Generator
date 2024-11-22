using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;

namespace PokemonFetcher;

class Program {
    static void Main(string[] args) {
        if (!ArgsValid(args, out string mainPagePath, out string subPageDirectory, out string stylesheetPath,
                out string subPageStylesheet)) {
            Console.Write("Enter path to mainPage (including filename): ");
            mainPagePath = Console.ReadLine() ?? "index.html";
            Console.Write("Enter path to subPageDirectory: ");
            subPageDirectory = Console.ReadLine() ?? "subpages";
            Console.Write("Enter path to stylesheet: ");
            stylesheetPath = Console.ReadLine() ?? "style.css";
            Console.Write("Enter path to subPageStylesheet: ");
            subPageStylesheet = Console.ReadLine() ?? "substyle.css";
        }


        // Build main page
        var jsonObject = FetchPokemon(0, 151);
        var pokemonArray = jsonObject["results"].AsArray();
        var htmlGenerator = new HtmlBuilder();
        htmlGenerator.OpenDocument("Pokedex", stylesheetPath);
        htmlGenerator.InsertText("<h1 style=\"text-align: center\">Pokedex</h1><p style=\"text-align: center\">(First Generation)</p>");
        htmlGenerator.OpenTag("table");
        foreach (var pokemon in pokemonArray) {
            BuildSinglePokemon(ref htmlGenerator, mainPagePath, subPageDirectory, subPageStylesheet, pokemon);
        }

        htmlGenerator.CloseTag();
        htmlGenerator.CloseDocument();

        File.WriteAllText(mainPagePath, htmlGenerator.ToString());
    }

    private static void BuildSinglePokemon(ref HtmlBuilder mainPageGenerator, string mainPagePath, string subPageDirectory, string stylesheet, JsonNode pokemon) {
        #region Get Data

        string index = pokemon["url"].ToString().Split("/")[6];

        pokemon = FetchJson(pokemon["url"].ToString());

        string pokedexNumber = $"#{index.PadLeft(3, '0')}";
        string name = pokemon["name"].ToString();
        string image =
            "<img src='https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/other/official-artwork/" +
            index + ".png' alt=\"\"/>";
        string floatingImage = $"<img src='https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/{index}.png' class=\"float-left\" alt=\"{name}\"/>";
        string type = string.Join('/', pokemon["types"]
            .AsArray()
            .Select(type => {
                var t = type["type"]["name"];
                return $"<a href=\"types.html#{t}\">{t}</a>";
            }));
        /*var stats = pokemon["stats"]
            .AsArray()
            .Select(stat => {
                string statName = stat["stat"]["name"].ToString();
                string baseStat = stat["base_stat"].ToString();
                return $"{statName}: {baseStat}";
            });*/
        var stats = pokemon["stats"];
        string hp = stats[0]["base_stat"].ToString();
        string attack = stats[1]["base_stat"].ToString();
        string defense = stats[2]["base_stat"].ToString();
        string specialAttack = stats[3]["base_stat"].ToString();
        string specialDefense = stats[4]["base_stat"].ToString();
        string speed = stats[5]["base_stat"].ToString();

        var locations = JsonObject.Parse(Fetch(pokemon["location_area_encounters"].ToString())).AsArray()
            .Select(location => location["location_area"]["name"].ToString());
        string locationString = locations.Any() ? string.Join("", locations.Select(l => $"<li>{l}</li>")) : "None";

        var types = pokemon["types"].AsArray().Select(type => type["type"]["name"].ToString());
        var effectiveness = CalculateTypeEffectiveness(types);
        var weaknessResistanceTable = BuildWeaknessResistanceTable(effectiveness);

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
            string basePath = Path.GetDirectoryName(mainPagePath);
            string targetPath = Path.Combine(subPageDirectory, name + ".html");
            string relativePath = Path.GetRelativePath(basePath, targetPath);

            mainPageGenerator.OpenTag("td");
            mainPageGenerator.InsertText($"<a href=\"{relativePath.Replace('\\', '/')}\">{name}</a>");
            mainPageGenerator.CloseTag();
        }

        mainPageGenerator.OpenTag("td");
        mainPageGenerator.InsertText(type);
        mainPageGenerator.CloseTag();

        /*
        mainPageGenerator.OpenTag("td");
        mainPageGenerator.OpenTag("ul class=\"stats\"");
        foreach (var stat in stats) {
            mainPageGenerator.OpenTag("li");
            mainPageGenerator.InsertText(stat);
            mainPageGenerator.CloseTag();
        }

        mainPageGenerator.CloseTag();
        mainPageGenerator.CloseTag();
        */

        mainPageGenerator.CloseTag();

        #endregion

        #region Build sub page content

        string doc = $"""
                      <!DOCTYPE html>
                      <html lang="en">
                      <head>
                        <meta charset="UTF-8">
                        <meta name="viewport" content="width=device-width, initial-scale=1">
                        <title>{name}</title>
                        <link rel="stylesheet" href="{stylesheet.Replace('\\', '/')}">
                      </head>
                      <body>
                      {floatingImage}
                              <p>TODO: write text manually</p>
                              <table class="resistance-table clearfix">
                              <thead>
                              <tr>
                              <th colspan="6">Weaknesses/Resistances</th>
                              </tr>
                                  <tr>
                                      <th>0x</th>
                                      <th>1/4x</th>
                                      <th>1/2x</th>
                                      <th>1x</th>
                                      <th>2x</th>
                                      <th>4x</th>
                                  </tr>
                                  <tr>
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
                          <ul class="location-list">
                              {locationString}
                          </ul>
                          </div>
                      """;

        string path = Path.Combine(subPageDirectory, $"{name}.html");
        File.WriteAllText(path, doc);

        #endregion
    }

    private static string Fetch(string url) {
        using HttpClient client = new();
        var response = client.GetAsync(url).Result;
        response.EnsureSuccessStatusCode();
        var content = response.Content.ReadAsStringAsync().Result;
        return content;
    }

    private static JsonObject FetchJson(string url) {
        string content = Fetch(url);
        var jsonObject = JsonNode.Parse(content).AsObject();
        return jsonObject;
    }

    private static JsonObject FetchPokemon(int offset, int num) {
        return FetchJson($"https://pokeapi.co/api/v2/pokemon?offset={offset}&limit={num}");
    }

    private static bool ArgsValid(string[] args, out string mainPagePath, out string subPagePath,
        out string stylesheetPath, out string subPageStylesheet) {
        mainPagePath = string.Empty;
        subPagePath = string.Empty;
        stylesheetPath = string.Empty;
        subPageStylesheet = string.Empty;

        // Expected args: Main page path, Subpage directory path, Stylesheet path, Pokemon count
        if (args.Length != 4) {
            Console.WriteLine("Invalid number of arguments.\nHow to use: dotnet run <mainPagePath> <subPageDirectory> <stylesheetPath> (relative to mainPagePath) <subPageStylesheet> (relative to subPageDirectory)");
            return false;
        }

        mainPagePath = args[0];
        subPagePath = args[1];
        stylesheetPath = args[2];
        subPageStylesheet = args[3];
        return true;
    }

    #region Types

    // Define type effectiveness
    private static readonly Dictionary<string, Dictionary<string, double>> TypeEffectiveness = new() {
        { "normal", new Dictionary<string, double> { { "rock", 0.5 }, { "ghost", 0 }, { "steel", 0.5 } } },
        { "fire", new Dictionary<string, double> { { "fire", 0.5 }, { "water", 0.5 }, { "grass", 2 }, { "ice", 2 }, { "bug", 2 }, { "rock", 0.5 }, { "dragon", 0.5 }, { "steel", 2 } } },
        { "water", new Dictionary<string, double> { { "fire", 2 }, { "water", 0.5 }, { "grass", 0.5 }, { "ground", 2 }, { "rock", 2 }, { "dragon", 0.5 } } },
        { "electric", new Dictionary<string, double> { { "water", 2 }, { "electric", 0.5 }, { "grass", 0.5 }, { "ground", 0 }, { "flying", 2 }, { "dragon", 0.5 } } },
        { "grass", new Dictionary<string, double> { { "fire", 0.5 }, { "water", 2 }, { "grass", 0.5 }, { "poison", 0.5 }, { "ground", 2 }, { "flying", 0.5 }, { "bug", 0.5 }, { "rock", 2 }, { "dragon", 0.5 }, { "steel", 0.5 } } },
        { "ice", new Dictionary<string, double> { { "fire", 0.5 }, { "water", 0.5 }, { "grass", 2 }, { "ice", 0.5 }, { "ground", 2 }, { "flying", 2 }, { "dragon", 2 }, { "steel", 0.5 } } },
        { "fighting", new Dictionary<string, double> { { "normal", 2 }, { "ice", 2 }, { "poison", 0.5 }, { "flying", 0.5 }, { "psychic", 0.5 }, { "bug", 0.5 }, { "rock", 2 }, { "ghost", 0 }, { "dark", 2 }, { "steel", 2 }, { "fairy", 0.5 } } },
        { "poison", new Dictionary<string, double> { { "grass", 2 }, { "poison", 0.5 }, { "ground", 0.5 }, { "rock", 0.5 }, { "ghost", 0.5 }, { "steel", 0 }, { "fairy", 2 } } },
        { "ground", new Dictionary<string, double> { { "fire", 2 }, { "electric", 2 }, { "grass", 0.5 }, { "poison", 2 }, { "flying", 0 }, { "bug", 0.5 }, { "rock", 2 }, { "steel", 2 } } },
        { "flying", new Dictionary<string, double> { { "electric", 0.5 }, { "grass", 2 }, { "fighting", 2 }, { "bug", 2 }, { "rock", 0.5 }, { "steel", 0.5 } } },
        { "psychic", new Dictionary<string, double> { { "fighting", 2 }, { "poison", 2 }, { "psychic", 0.5 }, { "dark", 0 }, { "steel", 0.5 } } },
        { "bug", new Dictionary<string, double> { { "fire", 0.5 }, { "grass", 2 }, { "fighting", 0.5 }, { "poison", 0.5 }, { "flying", 0.5 }, { "psychic", 2 }, { "ghost", 0.5 }, { "dark", 2 }, { "steel", 0.5 }, { "fairy", 0.5 } } },
        { "rock", new Dictionary<string, double> { { "fire", 2 }, { "ice", 2 }, { "fighting", 0.5 }, { "ground", 0.5 }, { "flying", 2 }, { "bug", 2 }, { "steel", 0.5 } } },
        { "ghost", new Dictionary<string, double> { { "normal", 0 }, { "psychic", 2 }, { "ghost", 2 }, { "dark", 0.5 } } },
        { "dragon", new Dictionary<string, double> { { "dragon", 2 }, { "steel", 0.5 }, { "fairy", 0 } } },
        { "dark", new Dictionary<string, double> { { "fighting", 0.5 }, { "psychic", 2 }, { "ghost", 2 }, { "dark", 0.5 }, { "fairy", 0.5 } } },
        { "steel", new Dictionary<string, double> { { "fire", 0.5 }, { "water", 0.5 }, { "electric", 0.5 }, { "ice", 2 }, { "rock", 2 }, { "steel", 0.5 }, { "fairy", 2 } } },
        { "fairy", new Dictionary<string, double> { { "fire", 0.5 }, { "fighting", 2 }, { "poison", 0.5 }, { "dragon", 2 }, { "dark", 2 }, { "steel", 0.5 } } }
    };

// Calculate resistances/weaknesses
    private static Dictionary<string, double> CalculateTypeEffectiveness(IEnumerable<string> types) {
        var effectiveness = new Dictionary<string, double>();

        foreach (var type in types) {
            foreach (var (targetType, multiplier) in TypeEffectiveness[type]) {
                if (!effectiveness.TryAdd(targetType, multiplier)) {
                    effectiveness[targetType] *= multiplier;
                }
            }
        }

        return effectiveness;
    }

// Fill the table in the subpage
    private static string BuildWeaknessResistanceTable(Dictionary<string, double> effectiveness) {
        var table = new StringBuilder();
        table.Append("<tr>");

        var categories = new[] { 0.0, 0.25, 0.5, 1.0, 2.0, 4.0 };
        foreach (var category in categories) {
            table.Append("<td>");
            var types = effectiveness.Where(e => e.Value == category).Select(e => e.Key);
            table.Append(string.Join(", ", types));
            table.Append("</td>");
        }

        table.Append("</tr>");
        return table.ToString();
    }

    #endregion
}

public class HtmlBuilder() {
    private Stack<string> _tagStack = new();
    private System.Text.StringBuilder _htmlBuilder = new();

    public void OpenDocument(string title, string? stylesheet = null) {
        string stylesheetLink = stylesheet != null ? $"<link rel=\"stylesheet\" href=\"{stylesheet}\">" : string.Empty;
        _htmlBuilder.Append(
            $"<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"UTF-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\"><title>{title}</title>{stylesheetLink}</head><body>");
    }

    public void CloseDocument() {
        _htmlBuilder.Append("</body></html>");
    }

    public void OpenTag(string tag) {
        _tagStack.Push(tag.Split(' ')[0]);
        _htmlBuilder.Append($"<{tag}>");
    }

    public void InsertText(string text) {
        _htmlBuilder.Append(text);
    }

    public void CloseTag() {
        var tag = _tagStack.Pop();
        _htmlBuilder.Append($"</{tag}>");
    }

    public override string ToString() {
        return _htmlBuilder.ToString();
    }
}