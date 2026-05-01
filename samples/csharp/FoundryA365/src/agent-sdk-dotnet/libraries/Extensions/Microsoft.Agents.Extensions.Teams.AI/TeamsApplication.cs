using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Extensions.Teams.App;
using Microsoft.Agents.Extensions.Teams.App.MessageExtensions;
using Microsoft.Agents.Extensions.Teams.App.Meetings;
using Microsoft.Agents.Extensions.Teams.App.TaskModules;
using Microsoft.Agents.Extensions.Teams.AI;
using Microsoft.Agents.Builder.State;

namespace Microsoft.Agents.Extensions.Teams.AI;

/// <summary>
/// Represents a Teams application that can be used to build and deploy intelligent agents within Microsoft Teams.
/// </summary>
public class TeamsApplication : AgentApplication
{
    private readonly AISystem? _ai = null;
#if !NETSTANDARD
    private TeamsAgentExtension _teamsAgentExtension { get; init; }
#else
    private TeamsAgentExtension _teamsAgentExtension { get; set; } = new TeamsAgentExtension(null!); // Initialize with null, will be set in constructor
#endif

    /// <summary>
    /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.AI.TeamsApplication"/> class with the specified options.
    /// </summary>
    /// <param name="options">The agent application options to leverage.</param>
    public TeamsApplication(TeamsAgentApplicationOptions options) : base(options)
    {
        // Create a teams agent extension and set it as a property.
        _teamsAgentExtension = new TeamsAgentExtension(this);

        // Check if AI options were configured and set the AI property accordingly.
        if (options.AI != null)
        {
            _ai = new AISystem(options.AI);
        }

        this.OnBeforeTurn((turnContext, turnState, cancellationToken) =>
        {
            string? input = turnState.Temp.GetValue<string?>("input");
            // Call the OnBeforeTurn method of the TeamsAgentExtension.
            if ((input == null || input.Length == 0) && turnContext.Activity.Text != null)
            {
                // Use the received activity text
                turnState.Temp.SetValue("input", turnContext.Activity.Text);
            }
            return Task.FromResult(true);
        });

    }

    /// <summary>
    /// Gets the message extensions for the Teams application.
    /// </summary>
    public MessageExtension MessageExtensions => _teamsAgentExtension.MessageExtensions;

    /// <summary>
    /// Gets the task modules for the Teams application.
    /// </summary>
    public TaskModule TaskModules => _teamsAgentExtension.TaskModules;

    /// <summary>
    /// Gets the meeting for the Teams application.
    /// </summary>
    public Meeting Meetings => _teamsAgentExtension.Meetings;

    /// <summary>
    /// Fluent interface for accessing AI specific features.
    /// </summary>
    /// <remarks>
    /// This property is only available if the Application was configured with 'ai' options. An
    /// exception will be thrown if you attempt to access it otherwise.
    /// </remarks>
    public AISystem AI
    {
        get
        {
            if (_ai == null)
            {
                throw new ArgumentException("The Application.AI property is unavailable because no AI options were configured.");
            }

            return _ai;
        }
    }
}
