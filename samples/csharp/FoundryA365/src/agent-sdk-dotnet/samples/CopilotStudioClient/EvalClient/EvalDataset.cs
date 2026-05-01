// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace EvalClient;

/// <summary>
/// This class is responsible for deserializing the evaluation dataset
/// </summary>
public class EvalDataset
{
    public string? Name { get; set; }
    public string? TestUtterance { get; set; }
    public string? ExpectedResponse { get; set; }
    public string? Sources { get; set; }
    public string? AgentResponse { get; set; }
    public string? AnswerScore { get; set; }
    public string? SourcesScore { get; set; }
}

