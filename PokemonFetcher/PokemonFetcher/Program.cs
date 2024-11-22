using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;

namespace PokemonFetcher;

class Program {
    static void Main(string[] args) {
        if (!ArgsValid(args, out string mainPagePath, out string subPageDirectory, out string stylesheetPath, out string subPageStylesheet)) {
            return;
        }

        // Build main page
        var jsonObject = FetchPokemon(0, 10 /* Value is TEMP */);
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

        // TODO: calculate weaknesses/resistances based on types
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
                                  <tr>
                                      <td></td>
                                      <td></td>
                                      <td></td>
                                      <td></td>
                                      <td></td>
                                      <td></td>
                                  </tr>
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