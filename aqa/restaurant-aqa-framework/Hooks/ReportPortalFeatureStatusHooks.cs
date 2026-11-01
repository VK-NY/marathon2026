using System.Collections.Concurrent;
using ReportPortal.Client.Abstractions.Models;
using ReportPortal.ReqnrollPlugin;

namespace Restaurant_Aqa_Framework.Hooks;

/// <summary>
/// The ReportPortal agent always closes a feature as Skipped, and ReportPortal then shows the whole launch as FAILED
/// even when every scenario passed. This gives a feature its real status: Failed if one of its scenarios failed,
/// Passed otherwise. Without ReportPortal enabled the events are never raised.
/// </summary>
[Binding]
public static class ReportPortalFeatureStatusHooks
{
    private static readonly ConcurrentDictionary<string, bool> FeaturesWithFailures = new();

    [BeforeTestRun(Order = -30000)]
    public static void CorrectTheFeatureStatus()
    {
        ReportPortalAddin.BeforeScenarioFinished += (_, scenario) =>
        {
            if (scenario.FinishTestItemRequest.Status == Status.Failed && scenario.FeatureContext is { } feature)
                FeaturesWithFailures[feature.FeatureInfo.Title] = true;
        };

        ReportPortalAddin.BeforeFeatureFinished += (_, finished) =>
            finished.FinishTestItemRequest.Status = FeaturesWithFailures.ContainsKey(finished.FeatureContext.FeatureInfo.Title)
                ? Status.Failed
                : Status.Passed;
    }
}
