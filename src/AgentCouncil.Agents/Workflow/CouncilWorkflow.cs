using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

namespace AgentCouncil.Agents.Workflow;

/// <summary>
/// Builds the council workflows for the agents the user invited. All are built per session, since the
/// line-up (and the rounds) are session-specific.
///
/// Handoff (default): a full mesh. The Moderator can hand off to any invited persona, and every persona
/// can pass the floor to any other persona or back to the Moderator, so they answer each other directly.
///
/// Group chat: a round-robin manager gives every invited agent the floor in turn. It terminates after each
/// agent has spoken <c>roundsPerAgent</c> times, which is the "ask the user" moment.
///
/// Magentic One: the Moderator, as an LLM manager, plans the debate, picks each next speaker with an
/// instruction and delivers a verdict. One Magentic workflow runs one task, so it is built per user turn.
/// </summary>
public sealed class CouncilWorkflow
{
    public Microsoft.Agents.AI.Workflows.Workflow BuildHandoff(AIAgent moderator, IReadOnlyList<AIAgent> personas)
    {
        var builder = AgentWorkflowBuilder
            .CreateHandoffBuilderWith(moderator)
            .WithHandoffs(moderator, personas);
        foreach (AIAgent persona in personas)
        {
            // ponytail: nothing caps persona-to-persona passing; the instructions steer it back to the
            // Moderator. Add a WithTerminationCondition on turn count if a debate ever runs away.
            builder.WithHandoffs(persona, [moderator, .. personas.Where(other => other != persona)]);
        }

        return builder
            // Lets the user's next message go straight back to whoever asked them a question.
            .EnableReturnToPrevious()
            // Agents hear each other's words, not each other's tool calls. After an approved write the
            // shared conversation otherwise carries a tool result without its call, which Azure OpenAI
            // rejects on the next agent's turn ("role 'tool' must be a response to ... 'tool_calls'").
            .WithToolCallFilteringBehavior(HandoffToolCallFilteringBehavior.All)
            .Build();
    }

    public Microsoft.Agents.AI.Workflows.Workflow BuildGroupChat(IReadOnlyList<AIAgent> members, int roundsPerAgent) => AgentWorkflowBuilder
        .CreateGroupChatBuilderWith(participants => new RoundRobinGroupChatManager(participants)
        {
            // Total turns per user message = every agent speaks exactly roundsPerAgent times.
            MaximumIterationCount = roundsPerAgent * participants.Count,
        })
        .AddParticipants([.. members])
        .Build();

    public Microsoft.Agents.AI.Workflows.Workflow BuildMagentic(AIAgent manager, IReadOnlyList<AIAgent> personas, int roundsPerAgent) => AgentWorkflowBuilder
        .CreateMagenticBuilderWith(manager)
        .AddParticipants(personas)
        .WithMaxRounds(roundsPerAgent * personas.Count)
        // ponytail: no plan review step; RequirePlanSignoff(true) + answering MagenticPlanReviewRequest adds one.
        .RequirePlanSignoff(false)
        .Build();
}
