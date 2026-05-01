// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using CsvHelper.Configuration;

namespace EvalClient;


/// <summary>
/// This class is responsible for deserializing the evaluation dataset
/// </summary>
public class EvalDatasetResultCsvMap : ClassMap<EvalDataset>
{
    public EvalDatasetResultCsvMap()
    {
        Map(m => m.Name).Name("Name");
        Map(m => m.TestUtterance).Name("Test Utterance");
        Map(m => m.ExpectedResponse).Name("Expected Response");
        Map(m => m.Sources).Name("Sources");
        Map(m => m.AgentResponse).Name("Agent Response");
        Map(m => m.AnswerScore).Name("Answer Score");
        Map(m => m.SourcesScore).Name("Sources Score");
    }
}
