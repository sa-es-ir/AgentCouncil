# .NET Conf Session Proposal

## ✅ Final submission (copy-paste)

**Title:** From Debate to Done: Multi-Agent Apps That Argue, Plan, and Ask Permission in .NET

**Level:** Intermediate (300) · **Format:** Talk + live demo · **Try it:** https://agents.codesimple.dev

### Abstract

Most agent demos stop at conversation. This one keeps going, and you can try it yourself at **agents.codesimple.dev**.

**Argue.** Invite a panel of AI characters (a Moderator, an Optimist, a Skeptic and a Pragmatist) to a round table and give them a topic. Pick who sits down, tune each one's token budget and reasoning effort, and watch them debate live in Blazor, either as a **handoff** or a **round-robin group chat** built on **Microsoft Agent Framework**. Jump in whenever you want to steer.

**Plan.** When the talking stops, the Pragmatist turns the debate into a todo list. You confirm the plan, it switches to execute mode and sends a background Researcher to dig up facts.

**Ask permission.** Every file write waits behind a **human approval gate**. We'll build it, show where it still breaks inside workflows today, and walk through the workaround.

Along the way: the **DevUI** debugger, OpenTelemetry traces and per-model token cost in the **.NET Aspire** dashboard, agent skills, context compaction, and shipping the whole thing to **Azure Container Apps** with a custom domain using `azd up`. It's real, running code.

### Short version (~60 words)

Four AI agents debate your topic, one turns the result into a plan, then it acts, but only with your approval. Built with Microsoft Agent Framework and .NET Aspire and running live at agents.codesimple.dev: handoff and group-chat orchestration, a tool-using harness agent, human-in-the-loop approvals, DevUI, OpenTelemetry, and a one-command deploy to Azure Container Apps.

### Key takeaways

- How to pick between handoff and group-chat orchestration in Agent Framework
- How to move an agent from talking to acting safely: plan/execute modes, todos, approval gates
- Where human-in-the-loop approvals break down today, and a workaround that ships
- How to observe agents and control cost with DevUI, Aspire, OpenTelemetry and cheaper model routing
- How to take an Aspire agent app from `dotnet run` to a custom domain on Azure Container Apps

---

## Title options

1. **Assemble the Council: Multi-Agent Orchestration with Microsoft Agent Framework and .NET Aspire**
2. **Four Agents Walk into a Debate: Handoff, Group Chat, and Human-in-the-Loop in .NET**
3. **From Talk to Action: Building Agents That Debate, Plan, and Ask Permission** (earlier pick, superseded by the final title above)
4. **Agents at the Round Table: A Practical Tour of Microsoft Agent Framework**
5. **Let Them Argue, Then Let One Act: Multi-Agent Apps in .NET**

**Level:** Intermediate (300). **Format:** A 45–60 min talk with a live demo.

---

## Description A: product-focused (final)

### Abstract (~170 words)

A good team meeting has an optimist, a skeptic and someone practical, and in the end somebody actually does the work. What if your app worked that way?

In this session we build **AgentCouncil**, a place where you invite a panel of AI characters, give them a topic, and watch them debate it live. You choose who joins, how long each one talks and how hard each one thinks. You can step in at any point to steer the conversation.

Once the talking is done, the Pragmatist takes the result and turns it into a plan. You review the plan, and it starts working through it, sending a researcher to dig up facts along the way. It never changes anything without asking you first. You stay in charge the whole time.

We'll cover the product decisions that make an agent feel trustworthy rather than scary, show how little code it takes with **Microsoft Agent Framework** and **.NET Aspire**, and be honest about what still doesn't work.

### Short version (~55 words)

What if your app ran like a good team meeting? Invite a panel of AI characters, watch them debate your idea, then let one of them turn the result into a plan and carry it out, asking your permission before each step. Built with Microsoft Agent Framework and .NET Aspire, with you in charge throughout.

### Key takeaways

- How to design agent experiences where users stay in control
- Why several agents with different personalities give better answers than one
- How to turn a conversation into actions users can trust
- What it takes to build this in .NET today, and where the rough edges are

---

## Description B: Talk → Plan → Ask permission (technical, fits title 3)

### Abstract (~180 words)

Most agent demos stop at conversation. This one keeps going.

**Talk.** We start with four AI debaters, a Moderator, an Optimist, a Skeptic and a Pragmatist, built on the new **Microsoft Agent Framework** and hosted in **.NET Aspire**. Users choose who sits at the table and tune each agent's token budget and reasoning effort. They can run the debate as a **handoff** or as a **round-robin group chat** and watch it stream live into a Blazor UI.

**Plan.** Then the Pragmatist moves from opinions to a todo list. It drafts a plan, waits for you to confirm it, switches to execute mode, and sends a background Researcher off while it works.

**Ask permission.** Every file write goes through a **human approval gate**. You'll see how to build it, where it breaks inside workflows today, and the workaround we used.

Along the way we'll use the **DevUI** debugger, OpenTelemetry traces in the **Aspire dashboard** (including token cost per model), agent skills, and context compaction. It's all real code you can clone and run.

### Short version (~60 words)

Most agent demos stop at conversation. In this one, four AI agents debate your topic, one of them turns the result into a plan, and then it acts, but only with your approval. Built with Microsoft Agent Framework and .NET Aspire, it covers handoff and group-chat orchestration, tool-using agents, human-in-the-loop approvals, DevUI and OpenTelemetry.

---

## Description C: original technical tour (fits titles 1, 2, 4, 5)

### Abstract (~180 words)

What happens when you put an Optimist, a Skeptic, a Pragmatist, and a Moderator at the same table and hand them a topic? You get a working tour of the new **Microsoft Agent Framework**.

In this session we build *AgentCouncil*, a .NET Aspire app where users pick AI debaters, tune each one's token budget and reasoning effort, and watch them argue live in a Blazor UI. We compare two orchestration styles on the same agents, **handoff** and **round-robin group chat**, and look at when each one fits.

Then the Pragmatist stops talking and starts doing. We give it tools: a todo list, plan and execute modes, a background Researcher, and file writes to a shared workspace behind a **human approval gate**. You'll also see where the framework's limits are today, including the approval patterns that won't round-trip through a workflow and how we worked around them.

Along the way you'll see the **DevUI** debugger, OpenTelemetry traces in the **Aspire dashboard** (including the token cost per model), agent skills, context compaction, and streaming over SignalR. It's all real code you can clone and run.

### Short version (~60 words)

Four AI agents debate your topic, and then one of them gets to work. Using Microsoft Agent Framework and .NET Aspire, we build a live multi-agent app with handoff and group-chat orchestration, a tool-using agent behind human approval gates, the DevUI debugger, and full OpenTelemetry tracing. You'll see real code, real limits, and real workarounds.

### Key takeaways (technical)

- How to move an agent from talking to acting safely: plan mode, execute mode, and approval gates
- How to choose between handoff and group-chat orchestration in Agent Framework
- How to build a "harness" agent with todos, plan/execute modes, and background sub-agents
- How to add human-in-the-loop tool approvals, and where they break down today
- How to observe and debug agents with DevUI, Aspire, and OpenTelemetry
- How to keep costs down by routing agents to cheaper model deployments
