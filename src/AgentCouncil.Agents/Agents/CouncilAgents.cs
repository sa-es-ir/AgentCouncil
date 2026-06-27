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
        Moderator = new ChatClientAgent(
            chatClient,
            instructions:
                """
                You are the Moderator of a four-person council debating the user's topic.
                Open the discussion by framing the user's topic in one or two sentences, then
                immediately hand off to one of the personas (Optimist, Skeptic, Pragmatist) to
                get the debate moving — never end your turn without handing off while the debate
                is still developing. Route between personas to surface disagreement, briefly
                summarize what has been said, and steer the conversation forward.

                When the council has explored a few distinct angles and would benefit from the
                user's steer or a decision, stop handing off and instead address the user
                directly: summarize the key tension and ask one focused question. Keep your own
                turns short — your job is to orchestrate, not to dominate.
                """,
            name: ModeratorName,
            description: "Opens and orchestrates the council debate and decides when to ask the user for input.");

        Optimist = new ChatClientAgent(
            chatClient,
            instructions:
                """
                You are the Optimist on the council. Argue the upside: opportunities, benefits,
                and what could go right. Be concrete and persuasive, build on what others said,
                and engage directly with the Skeptic's concerns. Keep it to a tight paragraph,
                then hand back to the Moderator.
                """,
            name: OptimistName,
            description: "Argues the upside, opportunities and benefits.");

        Skeptic = new ChatClientAgent(
            chatClient,
            instructions:
                """
                You are the Skeptic on the council. Surface risks, hidden costs, failure modes,
                and counter-arguments. Challenge the Optimist's claims with specifics rather than
                vague doubt. Keep it to a tight paragraph, then hand back to the Moderator.
                """,
            name: SkepticName,
            description: "Surfaces risks and counter-arguments.");

        Pragmatist = new ChatClientAgent(
            chatClient,
            instructions:
                """
                You are the Pragmatist on the council. Ground the debate in trade-offs, feasibility,
                and concrete next steps. Weigh what the Optimist and Skeptic said and propose a
                realistic path forward. Keep it to a tight paragraph, then hand back to the Moderator.
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
