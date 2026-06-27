using AgentCouncil.Agents.Agents;
using Microsoft.Agents.AI.Workflows;

namespace AgentCouncil.Agents.Workflow;

/// <summary>
/// Builds the council handoff workflow: the Moderator can hand off to any persona, and every
/// persona hands back to the Moderator (mesh-via-moderator topology). Full conversation history
/// is preserved by the handoff workflow. The built <see cref="Microsoft.Agents.AI.Workflows.Workflow"/>
/// is reused across sessions; each session opens its own streaming run.
/// </summary>
public sealed class CouncilWorkflow
{
    public CouncilWorkflow(CouncilAgents agents)
    {
        Workflow = AgentWorkflowBuilder
            .CreateHandoffBuilderWith(agents.Moderator)
            .WithHandoffs(agents.Moderator, agents.Personas)
            .WithHandoffs(agents.Personas, agents.Moderator)
            // Lets a persona ask the user a follow-up and return to the asking agent.
            .EnableReturnToPrevious()
            .Build();
    }

    public Microsoft.Agents.AI.Workflows.Workflow Workflow { get; }
}
