using AGXUnity.Utils;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace AGXUnityTesting.Editor
{
  public class LogOnceTests
  {
    private class CapturingLogHandler : ILogHandler
    {
      public readonly List<(LogType Type, string Message, UnityEngine.Object Context)> Entries =
        new List<(LogType, string, UnityEngine.Object)>();

      public void LogFormat( LogType logType, UnityEngine.Object context, string format, params object[] args )
        => Entries.Add( (logType, string.Format( format, args ), context) );

      public void LogException( Exception exception, UnityEngine.Object context )
        => Entries.Add( (LogType.Exception, exception.Message, context) );
    }

    private static readonly MethodInfo s_clear =
      typeof( LogOnce ).GetMethod( "Clear", BindingFlags.Static | BindingFlags.NonPublic );

    private CapturingLogHandler m_handler;
    private ILogHandler m_originalHandler;
    private bool m_originalLogEnabled;
    private LogType m_originalFilterLogType;

    [SetUp]
    public void SetUp()
    {
      Assert.That( s_clear, Is.Not.Null );
      ClearLogState();

      var logger = Debug.unityLogger;
      m_originalHandler = logger.logHandler;
      m_originalLogEnabled = logger.logEnabled;
      m_originalFilterLogType = logger.filterLogType;
      m_handler = new CapturingLogHandler();
      logger.logHandler = m_handler;
      logger.logEnabled = true;
      logger.filterLogType = LogType.Log;
    }

    [TearDown]
    public void TearDown()
    {
      var logger = Debug.unityLogger;
      logger.logHandler = m_originalHandler;
      logger.logEnabled = m_originalLogEnabled;
      logger.filterLogType = m_originalFilterLogType;
      ClearLogState();
    }

    [TestCase( LogType.Log )]
    [TestCase( LogType.Warning )]
    [TestCase( LogType.Error )]
    public void ExplicitIdLogsOnlyTheFirstMessage( LogType type )
    {
      LogExplicit( type, "First message", "shared-id" );
      LogExplicit( type, "Changed message", "shared-id" );
      LogExplicit( type, "First message", "shared-id" );

      Assert.That( m_handler.Entries.Count, Is.EqualTo( 1 ) );
      Assert.That( m_handler.Entries[ 0 ].Type, Is.EqualTo( type ) );
      Assert.That( m_handler.Entries[ 0 ].Message, Is.EqualTo( "First message" ) );
      Assert.That( m_handler.Entries[ 0 ].Context, Is.Null );
    }

    [TestCase( LogType.Log )]
    [TestCase( LogType.Warning )]
    [TestCase( LogType.Error )]
    public void DifferentIdsAreIndependent( LogType type )
    {
      var context = new object();
      LogExplicit( type, "Same message", "first-id", context );
      LogExplicit( type, "Same message", "second-id", context );
      LogExplicit( type, "Duplicate", "first-id", context );

      Assert.That( m_handler.Entries.Count, Is.EqualTo( 2 ) );
    }

    [TestCase( LogType.Log )]
    [TestCase( LogType.Warning )]
    [TestCase( LogType.Error )]
    public void ExplicitIdIsIndependentForEachContextIncludingNull( LogType type )
    {
      var contexts = new[] { new object(), new object(), null };
      for ( int repetition = 0; repetition < 2; ++repetition )
        foreach ( var context in contexts )
          LogExplicit( type, "Same message", "shared-id", context );

      Assert.That( m_handler.Entries.Count, Is.EqualTo( contexts.Length ) );
      Assert.That( m_handler.Entries.TrueForAll( entry => entry.Context == null ), Is.True );
    }

    [TestCase( LogType.Log )]
    [TestCase( LogType.Warning )]
    [TestCase( LogType.Error )]
    public void UnityContextIsForwardedToLogger( LogType type )
    {
      var first = new GameObject( "First LogOnce context" );
      var second = new GameObject( "Second LogOnce context" );
      try {
        LogExplicit( type, "First", "shared-id", first );
        LogExplicit( type, "Second", "shared-id", second );
        LogExplicit( type, "Duplicate", "shared-id", first );

        Assert.That( m_handler.Entries.Count, Is.EqualTo( 2 ) );
        Assert.That( m_handler.Entries[ 0 ].Context, Is.SameAs( first ) );
        Assert.That( m_handler.Entries[ 1 ].Context, Is.SameAs( second ) );
      }
      finally {
        UnityEngine.Object.DestroyImmediate( first );
        UnityEngine.Object.DestroyImmediate( second );
      }
    }

    [TestCase( LogType.Log )]
    [TestCase( LogType.Warning )]
    [TestCase( LogType.Error )]
    public void CallerInformationLogsOncePerCallSiteAndContext( LogType type )
    {
      var contexts = new[] { new object(), new object(), null };
      for ( int repetition = 0; repetition < 2; ++repetition )
        foreach ( var context in contexts )
          LogAutomatic( type, "Same message", context );

      Assert.That( m_handler.Entries.Count, Is.EqualTo( contexts.Length ) );
      Assert.That( m_handler.Entries.TrueForAll( entry => entry.Type == type ), Is.True );
    }

    [Test]
    public void CallerInformationDistinguishesDifferentSourceLines()
    {
      var context = new object();
      for ( int repetition = 0; repetition < 2; ++repetition ) {
        LogOnce.Warning( "First call site", context );
        LogOnce.Warning( "Second call site", context );
      }

      Assert.That( m_handler.Entries.Count, Is.EqualTo( 2 ) );
      Assert.That( m_handler.Entries[ 0 ].Message, Is.EqualTo( "First call site" ) );
      Assert.That( m_handler.Entries[ 1 ].Message, Is.EqualTo( "Second call site" ) );
    }

    [Test]
    public void NamedNullContextSelectsCallerInformationOverload()
    {
      LogOnce.Warning( "First call site", context: null );
      LogOnce.Warning( "Second call site", context: null );

      Assert.That( m_handler.Entries.Count, Is.EqualTo( 2 ) );
    }

    [Test]
    public void PositionalNullSelectsAutomaticIdOverload()
    {
      LogOnce.Warning( "First call site", null );
      LogOnce.Warning( "Second call site", null );

      Assert.That( m_handler.Entries.Count, Is.EqualTo( 2 ) );
    }

    [Test]
    public void NamedStringContextSelectsCallerInformationOverload()
    {
      for ( int repetition = 0; repetition < 2; ++repetition ) {
        LogOnce.Warning( "First call site", context: "shared-context" );
        LogOnce.Warning( "Second call site", context: "shared-context" );
      }

      Assert.That( m_handler.Entries.Count, Is.EqualTo( 2 ) );
    }

    [Test]
    public void ContextsUseReferenceEquality()
    {
      object first = new string( 'x', 1 );
      object second = new string( 'x', 1 );
      Assert.That( first, Is.Not.SameAs( second ) );

      LogOnce.Warning( "First context", first, "shared-id" );
      LogOnce.Warning( "Equal context", second, "shared-id" );

      Assert.That( m_handler.Entries.Count, Is.EqualTo( 2 ) );
    }

    [Test]
    public void ExplicitIdsAreSharedAcrossLogLevels()
    {
      var context = new object();
      LogOnce.Info( "First message", context, "shared-id" );
      LogOnce.Warning( "Suppressed warning", context, "shared-id" );
      LogOnce.Error( "Suppressed error", context, "shared-id" );

      Assert.That( m_handler.Entries.Count, Is.EqualTo( 1 ) );
      Assert.That( m_handler.Entries[ 0 ].Type, Is.EqualTo( LogType.Log ) );
    }

    [Test]
    public void NullIdIsDeduplicatedWithinEachContext()
    {
      var context = new object();
      for ( int repetition = 0; repetition < 2; ++repetition ) {
        LogOnce.Warning( "Global", null, null );
        LogOnce.Warning( "Instance", context, null );
      }

      Assert.That( m_handler.Entries.Count, Is.EqualTo( 2 ) );
    }

    [TestCase( LogType.Log )]
    [TestCase( LogType.Warning )]
    [TestCase( LogType.Error )]
    public void ClearAllowsPreviouslyLoggedIdsAndCallSitesAgain( LogType type )
    {
      var context = new object();
      for ( int session = 0; session < 2; ++session ) {
        ClearLogState();
        for ( int repetition = 0; repetition < 2; ++repetition ) {
          LogExplicit( type, "Global", "shared-id" );
          LogExplicit( type, "Instance", "shared-id", context );
          LogAutomatic( type, "Automatic", context );
        }
      }

      Assert.That( m_handler.Entries.Count, Is.EqualTo( 6 ) );
    }

    [Test]
    public void LogStateResetsAtSubsystemRegistration()
    {
      var attribute = s_clear.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();

      Assert.That( attribute, Is.Not.Null );
      Assert.That( attribute.loadType, Is.EqualTo( RuntimeInitializeLoadType.SubsystemRegistration ) );
    }

    private static void ClearLogState() => s_clear.Invoke( null, null );

    private static void LogExplicit( LogType type, string message, string id, object context = null )
    {
      switch ( type ) {
        case LogType.Log: LogOnce.Info( message, context, id ); break;
        case LogType.Warning: LogOnce.Warning( message, context, id ); break;
        case LogType.Error: LogOnce.Error( message, context, id ); break;
        default: throw new ArgumentOutOfRangeException( nameof( type ) );
      }
    }

    private static void LogAutomatic( LogType type, string message, object context )
    {
      switch ( type ) {
        case LogType.Log: LogOnce.Info( message, context ); break;
        case LogType.Warning: LogOnce.Warning( message, context ); break;
        case LogType.Error: LogOnce.Error( message, context ); break;
        default: throw new ArgumentOutOfRangeException( nameof( type ) );
      }
    }
  }
}
