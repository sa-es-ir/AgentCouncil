namespace AgentCouncil.Web.Components.Pages;

/// <summary>
/// Who lives in the village: the /village page's own cast, separate from the round table's five.
/// <c>Name</c> is the hub key and must match the agents' VillageCast; <c>Css</c> picks the colour in village.css.
/// </summary>
internal static class VillageCast
{
    internal static readonly Dictionary<string, Council.Member> Members = new Council.Member[]
    {
        new("Mayor", "🎩", "Mayor", "makes speeches", "mayor", "Always thinking of the next election.", "Friends, neighbours, voters!"),
        new("Baker", "🥖", "Baker", "sees the upside", "baker", "Up before dawn, never out of hope.", "There's always bread tomorrow."),
        new("Astronomer", "🔭", "Astronomer", "wants evidence", "astronomer", "Trusts only what can be observed.", "Show me the data, and the error bars."),
        new("Blacksmith", "🔨", "Blacksmith", "gets it done", "blacksmith", "Hammers talk into a plan.", "Less talk. More anvil."),
        new("Gravedigger", "⚰️", "Gravedigger", "expects the worst", "gravedigger", "Has buried every bright idea in town.", "I'll fetch my shovel."),
        new("Merchant", "💰", "Merchant", "names the price", "merchant", "Everything has a price.", "And what does that cost us?"),
        new("Elder", "📜", "Elder", "remembers", "elder", "Has seen it all before.", "Back in my day…"),
        new("Seer", "🔮", "Fortune-teller", "predicts", "seer", "Reads the year ahead in a crystal ball.", "I see… a great surprise."),
        new("Bard", "🪕", "Bard", "sums up in rhyme", "bard", "Turns every quarrel into a verse.", "Gather round, a tale I'll sing."),
        new("Crier", "🔔", "Town Crier", "proclaims", "crier", "Announces where the village stands.", "Hear ye, hear ye!"),
    }.ToDictionary(m => m.Name, StringComparer.Ordinal);

    internal static Council.Member Meta(string agent) => Members.GetValueOrDefault(agent) ?? Council.Meta(agent);
}
