# AgentToAgent

This Sample contains two Agents: `Agent1` and `Agent2`, that represent the two legs of a multi-agent communication sequence. Each Agent has its own readme to setup and configure the Agent.  

There is a third Agent, `StreamAgent1`, that demonstrates streaming replies from Agent2.  

These should be used as `Agent1 <-> Agent2`, or `StreamingAgent1 <-> Agent2`.  Agent2 is the same in either case.

## Key concepts in this sample

The solution includes a parent (`Agent1`) and another Agent (`Agent2`) and shows how the parent agent can send Activities to another and return the responses to the user.

- `Agent1`: this project shows how to consume another Agent and includes:
  - A [HostAgent](Agent1/HostAgent.cs) that calls the Agent2.Echo and keeps the conversation active until the user says "end". [HostAgent](Agent1/HostAgent.cs) also keeps track of the conversation with  `Agent2` and handles the `EndOfConversation` Activity received from `Agent2` to terminate the conversation.
- `Agent2`:  Agent2 is just an Agent.  There is nothing done differently to enable being called by another Agent.  In this case, Agent2 just echoes what Agent2 sends it, until "end" is received.
- `StreamingAgent1` is the same as Agent1, but shows how to consume replies from `Agent2` with a stream.  Here is the difference between `Agent1` and `StreamingAgent1`
  - `Agent1` uses `DeliveryModes.Normal` when sending the message.  This the is default method for Agents to reply to the caller.  With this, replies are posted back to the caller asyncronously.  The replies could happen immediately (and often do).  They could also happen some indeterminate time later.
  - `StreamingAgent1` uses `DeliveryModes.Stream`.  In this case, the replies from `Agent2` are streamed back in the HTTP response.  The same thing will happen in `Agent2` either way.  But for `StreamingAgent1`, this means the replies from `Agent2` are always handled in the same Turn.
  - The same thing happens either way.  The consideration of one vs the other is that `DeliveryModes.Normal` is best suited for possibly long running operations when you don't know how long the reply could take.  `DeliveryModes.Stream` is more appropriate (and slightly easier to code) for short chat-style interactions.

## Next steps to get going
- Pick one combination:  `Agent1 <-> Agent2`, or `StreamAgent1 <-> Agent2`
  - Once you set up one pair, `Agent1` and `StreamingAgent1` can be swapped out.
- **Setup Agent2 first.**
- [Agent2](./Agent2/README.md)
- [Agent1](./Agent1/README.md)
- [StreamingAgent1](./StreamingAgent1/README.md)