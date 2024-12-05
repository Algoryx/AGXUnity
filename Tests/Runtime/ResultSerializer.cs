using AGXUnity.IO;
using NUnit.Framework.Interfaces;
using System.IO;
using System.Xml;
using UnityEngine;
using UnityEngine.TestRunner;

[assembly: TestRunCallback( typeof( ResultSerializer ) )]
public class ResultSerializer : ITestRunCallback
{
  public void RunStarted( ITest testsToRun ) { }
  public void TestFinished( ITestResult result ) { }
  public void TestStarted( ITest test ) { }

  public void RunFinished( ITestResult testResults )
  {
    if ( Environment.CommandLine.HasArg( "testResults" ) ) {
      if ( !testResults.Test.IsSuite ) {
        var path = Environment.CommandLine.GetValues( "testResults" )?[ 0 ] ?? "results.xml";
        var fullPath = Path.GetFullPath(path);
        using ( var xmlWriter = XmlWriter.Create( fullPath, new XmlWriterSettings { Indent = true } ) )
          WriteResultsToXml( testResults, xmlWriter );

        System.Console.WriteLine( $"\n Test results written to: {fullPath}\n" );
      }
    }
    Application.Quit( testResults.FailCount > 0 ? 1 : 0 );
  }

  // The code below this point is taken and modified from the UnityEditor.TestTools.TestRunner.Api.ResultsWriter class to 
  // output valid NUnit reports
  private const string k_nUnitVersion = "3.5.0.0";

  private const string k_TestRunNode = "test-run";
  private const string k_Id = "id";
  private const string k_Testcasecount = "testcasecount";
  private const string k_Result = "result";
  private const string k_Total = "total";
  private const string k_Passed = "passed";
  private const string k_Failed = "failed";
  private const string k_Inconclusive = "inconclusive";
  private const string k_Skipped = "skipped";
  private const string k_Asserts = "asserts";
  private const string k_EngineVersion = "engine-version";
  private const string k_ClrVersion = "clr-version";
  private const string k_StartTime = "start-time";
  private const string k_EndTime = "end-time";
  private const string k_Duration = "duration";

  private const string k_TimeFormat = "u";

  private void WriteResultsToXml( ITestResult result, XmlWriter xmlWriter )
  {
    // XML format as specified at https://github.com/nunit/docs/wiki/Test-Result-XML-Format

    var testRunNode = new TNode(k_TestRunNode);

    testRunNode.AddAttribute( k_Id, "2" );
    testRunNode.AddAttribute( k_Testcasecount, ( result.PassCount + result.FailCount + result.SkipCount + result.InconclusiveCount ).ToString() );
    testRunNode.AddAttribute( k_Result, result.ResultState.Label );
    testRunNode.AddAttribute( k_Total, ( result.PassCount + result.FailCount + result.SkipCount + result.InconclusiveCount ).ToString() );
    testRunNode.AddAttribute( k_Passed, result.PassCount.ToString() );
    testRunNode.AddAttribute( k_Failed, result.FailCount.ToString() );
    testRunNode.AddAttribute( k_Inconclusive, result.InconclusiveCount.ToString() );
    testRunNode.AddAttribute( k_Skipped, result.SkipCount.ToString() );
    testRunNode.AddAttribute( k_Asserts, result.AssertCount.ToString() );
    testRunNode.AddAttribute( k_EngineVersion, k_nUnitVersion );
    testRunNode.AddAttribute( k_ClrVersion, System.Environment.Version.ToString() );
    testRunNode.AddAttribute( k_StartTime, result.StartTime.ToString( k_TimeFormat ) );
    testRunNode.AddAttribute( k_EndTime, result.EndTime.ToString( k_TimeFormat ) );
    testRunNode.AddAttribute( k_Duration, result.Duration.ToString() );

    var resultNode = result.ToXml( true );
    testRunNode.ChildNodes.Add( resultNode );

    testRunNode.WriteTo( xmlWriter );
  }
}
