using AgentCouncil.Agents.Agents;
using Microsoft.Agents.AI.Workflows;

namespace AgentCouncil.Agents.Workflow;

/// <summary>
/// Builds the council workflows.
///
/// Handoff (default): the Moderator can hand off to any persona, and every persona hands back to
/// the Moderator (mesh-via-moderator topology). Built once and reused across sessions; each
/// session opens its own streaming run that preserves the full conversation.
///
/// Group chat: a round-robin manager gives every agent the floor in turn (Moderator first).
/// The rounds-per-agent setting is session-specific, so the workflow is built per session;
/// it terminates after each agent has spoken that many times, which is the "ask the user" moment.
/// </summary>
public sealed class CouncilWorkflow(CouncilAgents agents)
{
    public Microsoft.Agents.AI.Workflows.Workflow Handoff { get; } = AgentWorkflowBuilder
        .CreateHandoffBuilderWith(agents.Moderator)
        .WithHandoffs(agents.Moderator, agents.Personas)
        .WithHandoffs(agents.Personas, agents.Moderator)
        // Lets a persona ask the user a follow-up and return to the asking agent.
        .EnableReturnToPrevious()
        .Build();

    public Microsoft.Agents.AI.Workflows.Workflow BuildGroupChat(int roundsPerAgent) => AgentWorkflowBuilder
        .CreateGroupChatBuilderWith(participants => new RoundRobinGroupChatManager(participants)
        {
            // Total turns = every agent speaks exactly roundsPerAgent times.
            MaximumIterationCount = roundsPerAgent * agents.All.Count,
        })
        .AddParticipants([.. agents.All])
        .Build();
}
