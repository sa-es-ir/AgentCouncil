using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentCouncil.Agents.Agents;

/// <summary>
/// Builds and holds the four council agents. They share a single <see cref="IChatClient"/>
/// and are reused both by the handoff workflow (driven from the SignalR hub) and by DevUI.
/// </summary>
public sealed class CouncilAgents
{
    // Agent names are used as DevUI identifiers and as the labels the Blazor UI shows per turn.
    public const string ModeratorName = "Moderator";
    public const string OptimistName = "Optimist";
    public const string SkepticName = "Skeptic";
    public const string PragmatistName = "Pragmatist";

    public CouncilAgents(IChatClient chatClient)
    {
        // All personas are hard-capped at 2 sentences: keeps the debate sharp and demo-friendly.
        Moderator = new ChatClientAgent(
            chatClient,
            instructions:
                """
                You are the Moderator of a four-person council debating the user's topic. Frame or
                sharpen the debate in at most 2 short sentences, then hand the floor to a persona
                (Optimist, Skeptic, Pragmatist); once a few distinct angles have been heard, instead
                ask the user one focused question. Never exceed 2 sentences.
                """,
            name: ModeratorName,
            description: "Opens and orchestrates the council debate and decides when to ask the user for input.");

        Optimist = new ChatClientAgent(
            chatClient,
            instructions:
                """
                You are the Optimist on the council: argue the upside with concrete, specific
                benefits, engaging the Skeptic's latest point head-on. Maximum 2 sentences per
                turn — sharp and punchy — then hand the floor back to the Moderator.
                """,
            name: OptimistName,
            description: "Argues the upside, opportunities and benefits.");

        Skeptic = new ChatClientAgent(
            chatClient,
            instructions:
                """
                You are the Skeptic on the council: surface the sharpest risk, hidden cost, or
                failure mode, countering the Optimist with specifics, not vague doubt. Maximum
                2 sentences per turn, then hand the floor back to the Moderator.
                """,
            name: SkepticName,
            description: "Surfaces risks and counter-arguments.");

        Pragmatist = new ChatClientAgent(
            chatClient,
            instructions:
                """
                You are the Pragmatist on the council: weigh the trade-off on the table and
                propose one concrete, realistic next step. Maximum 2 sentences per turn, then
                hand the floor back to the Moderator.
                """,
            name: PragmatistName,
            description: "Grounds the debate in trade-offs and next steps.");
    }

    public AIAgent Moderator { get; }
    public AIAgent Optimist { get; }
    public AIAgent Skeptic { get; }
    public AIAgent Pragmatist { get; }

    /// <summary>All council agents, with the Moderator first (the handoff start agent).</summary>
    public IReadOnlyList<AIAgent> All => [Moderator, Optimist, Skeptic, Pragmatist];

    public IReadOnlyList<AIAgent> Personas => [Optimist, Skeptic, Pragmatist];
}
