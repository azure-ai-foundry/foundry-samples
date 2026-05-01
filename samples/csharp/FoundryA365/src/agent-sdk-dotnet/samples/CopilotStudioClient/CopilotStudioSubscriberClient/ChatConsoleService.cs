// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

/*************************************************
 * 
 * THIS SAMPLE IS NON-FUNCTIONAL OUTSIDE OF THE MICROSOFT NETWORK. 
 * IT IS INTENDED TO BE USED AS A REFERENCE FOR INTERNAL DEVELOPERS LOOKING TO UNDERSTAND HOW TO INTERACT WITH THE COPILOT STUDIO CLIENT LIBRARY,  AND IS NOT INTENDED TO BE A FULLY FUNCTIONAL SAMPLE.
 * 
 */


using Microsoft.Agents.CopilotStudio.Client;
using Microsoft.Agents.CopilotStudio.Client.Models;

namespace CopilotStudioClientSample;

/// <summary>
/// This class is responsible for handling the Chat Console service and managing the conversation between the user and the Copilot Studio hosted Agent.
/// </summary>
/// <param name="copilotClient">Connection Settings for connecting to Copilot Studio</param>
internal class ChatConsoleService(CopilotClient copilotClient) : IHostedService
{
    string? convo;

    /// <summary>
    /// This is the main thread loop that manages the communication from the Copilot Studio Agent. 
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="System.InvalidOperationException"></exception>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("Please provide the conversation ID to connect to an existing conversation"); 
        convo = Console.ReadLine();

        if (string.IsNullOrEmpty(convo))
        {
            Console.WriteLine("No conversation ID provided. Exiting.");
            return; 
        }
        Console.WriteLine($"Listening to conversation ID: {convo}");
        Console.Write("\nagent> ");

        // Once we are connected and have initiated the conversation,  begin the message loop with the Console. 
        while (!cancellationToken.IsCancellationRequested)
        {
            sw.Restart();
            await foreach (SubscribeEvent act in copilotClient.SubscribeAsync(conversationId:convo, lastReceivedEventId:string.Empty, cancellationToken:cancellationToken))
            {
                System.Diagnostics.Trace.WriteLine($">>>>MessageLoop Duration: {sw.Elapsed.ToDurationString()}");
                // for each response,  report to the UX
                PrintActivity(act);
                sw.Restart();
            }
        }
        sw.Stop();
    }

    /// <summary>
    /// This method is responsible for writing formatted data to the console.
    /// This method does not handle all of the possible activity types and formats, it is focused on just a few common types. 
    /// </summary>
    /// <param name="evnt"></param>
    static void PrintActivity(SubscribeEvent evnt)
    {
        Console.WriteLine(">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>");

        if (!string.IsNullOrEmpty(evnt.EventId))
        {
            Console.WriteLine($"\n[EventId: {evnt.EventId}]");
        }
        var act = evnt.Activity;
        switch (act.Type)
        {
            case "message":
                if (act.TextFormat == "markdown")
                {

                    Console.WriteLine(act.Text);
                    if (act.SuggestedActions?.Actions.Count > 0)
                    {
                        Console.WriteLine("Suggested actions:\n");
                        act.SuggestedActions.Actions.ToList().ForEach(action => Console.WriteLine("\t" + action.Text));
                    }
                }
                else
                {
                    Console.Write($"\n{act.Text}\n");
                }
                break;
            case "typing":
                Console.Write(".");
                break;
            case "event":
                Console.Write("+");
                break;
            default:
                Console.Write($"[{act.Type}]");
                break;
        }
        Console.WriteLine("<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        System.Diagnostics.Trace.TraceInformation("Stopping");
        return Task.CompletedTask;
    }
}
