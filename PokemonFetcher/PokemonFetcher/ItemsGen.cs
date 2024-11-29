using PokeApiNet;

namespace PokemonFetcher;

public class ItemsGen
{
    public static async Task<List<NamedApiResource<Item>>> GetItems()
    {
        NamedApiResourceList<Item> items = await Program.Client.GetNamedResourcePageAsync<Item>(20, 0);
        return items.Results;
    }
}