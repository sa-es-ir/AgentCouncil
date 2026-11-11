using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentCouncil.Agents.Agents;

/// <summary>
/// Before an agent's turn, turns the other members' replies into name-tagged user lines, and drops tool content it can't use: another agent's approval exchanges, and any
/// approval request, approval response or tool result whose other half isn't in the conversation.
///
/// Needed in 1.27: after an approved call inside a handoff workflow, the shared conversation hands the
/// next agent the caller's approval request and response. Approval binding only honours responses to
/// requests the agent itself surfaced, so the response is dropped and function invocation throws
/// ("ToolApprovalRequestContent found with FunctionCall.CallId(s) ... that have no matching
/// ToolApprovalResponseContent"). The agent that made the call keeps both halves, so it still resumes.
/// </summary>
internal sealed class OrphanToolContentFilter : AIContextProvider
{
    protected override ValueTask<AIContext> InvokingCoreAsync(InvokingContext context, CancellationToken cancellationToken = default)
    {
        AIContext input = context.AIContext;

        // The Responses API has no author field, so another member's turn would read as this agent's own.
        // It becomes a user line tagged with the speaker; only its words survive, its tool traffic is theirs.
        List<ChatMessage> messages = (input.Messages ?? [])
            .Select(m => m.Role == ChatRole.Assistant && m.AuthorName is { } author && author != context.Agent.Name
                ? (string.IsNullOrWhiteSpace(m.Text) ? null : new ChatMessage(ChatRole.User, $"[{author}] {m.Text}") { AuthorName = author })
                : m)
            .OfType<ChatMessage>()
            .ToList();
        List<AIContent> all = messages.SelectMany(m => m.Contents).ToList();
        HashSet<string> calls = [.. all.OfType<FunctionCallContent>().Select(c => c.CallId)];
        HashSet<string> responses = [.. all.OfType<ToolApprovalResponseContent>().Select(r => r.RequestId)];
        HashSet<string> requests = [];
        HashSet<string> foreign = [];
        foreach (ChatMessage message in messages)
        {
            foreach (ToolApprovalRequestContent request in message.Contents.OfType<ToolApprovalRequestContent>())
            {
                requests.Add(request.RequestId);
                if (message.AuthorName is { } author && author != context.Agent.Name)
                {
                    foreign.Add(request.RequestId);
                }
            }
        }

        bool Drop(AIContent content) => content switch
        {
            FunctionResultContent result => !calls.Contains(result.CallId),
            ToolApprovalRequestContent request => foreign.Contains(request.RequestId) || !responses.Contains(request.RequestId),
            ToolApprovalResponseContent response => foreign.Contains(response.RequestId) || !requests.Contains(response.RequestId),
            _ => false,
        };

        List<ChatMessage> kept = [];
        foreach (ChatMessage message in messages)
        {
            if (!message.Contents.Any(Drop))
            {
                kept.Add(message);
            }
            else if (message.Contents.Where(c => !Drop(c)).ToList() is { Count: > 0 } rest)
            {
                ChatMessage copy = message.Clone();
                copy.Contents = rest;
                kept.Add(copy);
            }
        }

        return ValueTask.FromResult(new AIContext { Instructions = input.Instructions, Messages = kept, Tools = input.Tools });
    }
}
