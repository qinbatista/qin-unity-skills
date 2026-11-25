using NUnit.Framework.Interfaces;
using UnityEngine;
using UnityEngine.TestRunner;

[assembly: TestRunCallback(typeof(GraphTrialProgress))]

public class GraphTrialProgress : ITestRunCallback
{
    public void RunStarted(ITest testsToRun) { }
    public void RunFinished(ITestResult result) { }
    public void TestStarted(ITest test) { }

    public void TestFinished(ITestResult result)
    {
        if (!result.Test.IsSuite && result.FullName.Contains("GraphTrialReplay.ShowTrials"))
            Debug.Log($"Graph trial {result.FullName}: {result.ResultState}, {result.Duration:F2}s. {result.Message}");
    }
}
