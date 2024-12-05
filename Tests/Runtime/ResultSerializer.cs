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
      var path = Environment.CommandLine.GetValues( "testResults" )?[ 0 ] ?? "results.xml";
      var fullPath = Path.GetFullPath(path);
      using ( var xmlWriter = XmlWriter.Create( fullPath, new XmlWriterSettings { Indent = true } ) )
        testResults.ToXml( true ).WriteTo( xmlWriter );

      System.Console.WriteLine( $"\n Test results written to: {fullPath}\n" );
    }
    Application.Quit( testResults.FailCount > 0 ? 1 : 0 );
  }
}
