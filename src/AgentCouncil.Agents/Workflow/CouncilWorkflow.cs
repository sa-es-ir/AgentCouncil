using AgentCouncil.Agents.Agents;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

namespace AgentCouncil.Agents.Workflow;

/// <summary>
/// Builds the council workflows for the agents the user invited. Both are built per session, since the
/// line-up (and for group chat, the rounds) are session-specific.
///
/// Handoff (default): the Moderator can hand off to any invited persona, and every persona hands back to
/// the Moderator (mesh-via-moderator topology); the session's streaming run preserves the conversation.
///
/// Group chat: a round-robin manager gives every invited agent the floor in turn. It terminates after each
/// agent has spoken <c>roundsPerAgent</c> times, which is the "ask the user" moment.
/// </summary>
public sealed class CouncilWorkflow(CouncilAgents agents)
{
    public Microsoft.Agents.AI.Workflows.Workflow BuildHandoff(IReadOnlyList<AIAgent> personas) => AgentWorkflowBuilder
        .CreateHandoffBuilderWith(agents.Moderator)
        .WithHandoffs(agents.Moderator, personas)
        .WithHandoffs(personas, agents.Moderator)
        // Lets a persona ask the user a follow-up and return to the asking agent.
        .EnableReturnToPrevious()
        .Build();

    public Microsoft.Agents.AI.Workflows.Workflow BuildGroupChat(IReadOnlyList<AIAgent> members, int roundsPerAgent) => AgentWorkflowBuilder
        .CreateGroupChatBuilderWith(participants => new RoundRobinGroupChatManager(participants)
        {
            // Total turns = every agent speaks exactly roundsPerAgent times.
            MaximumIterationCount = roundsPerAgent * participants.Count,
        })
        .AddParticipants([.. members])
        .Build();
}
