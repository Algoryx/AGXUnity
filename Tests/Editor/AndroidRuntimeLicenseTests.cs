using AGXUnity.IO;
using AGXUnityEditor.BuildTools;
using NUnit.Framework;
using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;

namespace AGXUnityTesting.Editor
{
  public class AndroidRuntimeLicenseTests
  {
    private const string Identifier = "com.algoryx.runtime.test";
    private string m_directory;

    [SetUp]
    public void SetUp()
    {
      m_directory = Path.Combine( Path.GetTempPath(), "AGXUnity-AndroidLicenseTest-" + Guid.NewGuid().ToString( "N" ) );
      Directory.CreateDirectory( m_directory );
    }

    [TearDown]
    public void TearDown()
    {
      Directory.Delete( m_directory, true );
    }

    [Test]
    public void FirstLaunchExtractsExactReferenceAndRequest()
    {
      byte[] reference = { 0, 1, 13, 10, 26, 127, 255 };
      AndroidRuntimeLicense.Prepare( m_directory, Bundle( reference: reference ), Identifier );

      Assert.That( File.ReadAllBytes( LicensePath( AndroidRuntimeLicense.ReferenceFilename ) ), Is.EqualTo( reference ) );
      Assert.That( File.ReadAllText( LicensePath( AndroidRuntimeLicense.ActivationFilename ) ), Is.EqualTo( "RT=test-request" ) );
    }

    [Test]
    public void AppUpdatePreservesDeviceLicenseAndDoesNotRecreateConsumedRequest()
    {
      string service = LicensePath( AndroidRuntimeLicense.ServiceFilename );
      File.WriteAllText( service, "activated-device-license" );
      AndroidRuntimeLicense.Prepare( m_directory, Bundle(), Identifier );
      int activations = 0;

      Assert.That( LoadOrActivate( filename => {
                                    Assert.That( filename, Is.EqualTo( service ) );
                                    return File.ReadAllText( filename ) == "activated-device-license";
                                  },
                                  (request, target) => { activations++; return false; } ), Is.True );
      Assert.That( File.ReadAllText( service ), Is.EqualTo( "activated-device-license" ) );
      Assert.That( File.Exists( LicensePath( AndroidRuntimeLicense.ActivationFilename ) ), Is.False );
      Assert.That( activations, Is.Zero );
    }

    [Test]
    public void RejectedDeviceLicenseIsPreservedWithoutAnotherAutomaticActivation()
    {
      string service = LicensePath( AndroidRuntimeLicense.ServiceFilename );
      File.WriteAllText( service, "expired-or-rejected-license" );
      File.WriteAllText( LicensePath( AndroidRuntimeLicense.ActivationFilename ), "RT=leftover-request" );
      int activations = 0;

      Assert.That( LoadOrActivate( filename => false, (request, target) => { activations++; return true; } ), Is.False );
      Assert.That( activations, Is.Zero );
      Assert.That( File.ReadAllText( service ), Is.EqualTo( "expired-or-rejected-license" ) );
      Assert.That( File.Exists( LicensePath( AndroidRuntimeLicense.ActivationFilename ) ), Is.True );
    }

    [Test]
    public void FailedActivationRetainsRequestAndRetriesOnNextLaunchInWritableDirectory()
    {
      int activations = 0;
      for ( int launch = 0; launch < 2; ++launch ) {
        AndroidRuntimeLicense.Prepare( m_directory, Bundle(), Identifier );
        Assert.That( LoadOrActivate( filename => throw new AssertionException( "No saved license to load" ), (request, target) => {
          Assert.That( target, Is.EqualTo( m_directory ) );
          Assert.That( request, Is.EqualTo( LicensePath( AndroidRuntimeLicense.ActivationFilename ) ) );
          activations++;
          return false;
        } ), Is.False );
        Assert.That( File.Exists( LicensePath( AndroidRuntimeLicense.ActivationFilename ) ), Is.True );
      }
      Assert.That( activations, Is.EqualTo( 2 ) );
    }

    [Test]
    public void SuccessfulActivationIsReusedOnNextLaunch()
    {
      byte[] bundle = Bundle();
      int activations = 0;
      Func<string, bool> load = filename => File.Exists( filename );
      Func<string, string, bool> activate = (request, target) => {
        activations++;
        File.WriteAllText( Path.Combine( target, AndroidRuntimeLicense.ServiceFilename ), "device-license" );
        // LicenseManager.ActivateEncryptedRuntime consumes the request on success.
        File.Delete( request );
        return true;
      };

      AndroidRuntimeLicense.Prepare( m_directory, bundle, Identifier );
      Assert.That( LoadOrActivate( load, activate ), Is.True );
      AndroidRuntimeLicense.Prepare( m_directory, bundle, Identifier );
      Assert.That( LoadOrActivate( load, activate ), Is.True );
      Assert.That( activations, Is.EqualTo( 1 ) );
      Assert.That( File.Exists( LicensePath( AndroidRuntimeLicense.ActivationFilename ) ), Is.False );
    }

    [Test]
    public void MissingActivationBundleWithoutSavedLicenseStopsStartup()
    {
      Assert.Throws<InvalidDataException>( () => AndroidRuntimeLicense.Prepare( m_directory, null, Identifier ) );
      Assert.That( Directory.GetFiles( m_directory ), Is.Empty );
    }

    [Test]
    public void SavedLicenseCanBeLoadedWithoutBundledCredentials()
    {
      File.WriteAllText( LicensePath( AndroidRuntimeLicense.ServiceFilename ), "device-license" );
      AndroidRuntimeLicense.Prepare( m_directory, null, Identifier );
      Assert.That( LoadOrActivate( filename => true, (request, target) => throw new AssertionException( "Unexpected activation" ) ), Is.True );
    }

    [Test]
    public void MissingRequestDoesNotAttemptActivation()
    {
      Assert.That( LoadOrActivate( filename => throw new AssertionException( "No saved license to load" ),
                                  (request, target) => throw new AssertionException( "Unexpected activation" ) ), Is.False );
    }

    [TestCase( "different.app", AndroidRuntimeLicense.ReferenceFilename, "RT=test" )]
    [TestCase( Identifier, "../outside-reference", "RT=test" )]
    [TestCase( Identifier, AndroidRuntimeLicense.ReferenceFilename, "not-an-activation-request" )]
    public void InvalidBundleIsRejectedBeforeReplacingExistingFiles( string identifier, string referenceName, string request )
    {
      string reference = LicensePath( AndroidRuntimeLicense.ReferenceFilename );
      File.WriteAllText( reference, "keep this reference" );
      byte[] bundle = Bundle( identifier, referenceName, request );
      Assert.Throws<InvalidDataException>( () => AndroidRuntimeLicense.Prepare( m_directory, bundle, Identifier ) );
      Assert.That( File.ReadAllText( reference ), Is.EqualTo( "keep this reference" ) );
      Assert.That( Directory.GetFiles( m_directory ).Length, Is.EqualTo( 1 ) );
    }

    [Test]
    public void NativeGenerationPackagesPortableReferenceWithoutActivatingLicense()
    {
      // encryptRuntimeActivation only encrypts locally. These synthetic credentials
      // are never passed to an activation API or sent to the license server.
      byte[] bundle = AndroidRuntimeResourcePackager.CreateRuntimeActivationBundle( 1, "LOCAL-ENCRYPTION-TEST", Identifier );
      AndroidRuntimeLicense.Prepare( m_directory, bundle, Identifier );
      byte[] reference = File.ReadAllBytes( LicensePath( AndroidRuntimeLicense.ReferenceFilename ) );
      string request = File.ReadAllText( LicensePath( AndroidRuntimeLicense.ActivationFilename ) );
      Assert.That( reference, Has.No.Member( (byte)13 ) );
      Assert.That( reference, Has.No.Member( (byte)26 ) );
      Assert.That( Encoding.UTF8.GetString( reference ), Does.StartWith( Identifier + "\n" ) );
      Assert.That( request, Does.StartWith( "RT=" ) );
      byte[] encrypted = Convert.FromBase64String( request.Substring( 3 ) );
      // The native format stores a 32-byte IV followed by the reference path.
      int terminator = Array.IndexOf( encrypted, (byte)0, 32 );
      Assert.That( Encoding.UTF8.GetString( encrypted, 32, terminator - 32 ), Is.EqualTo( AndroidRuntimeLicense.ReferenceFilename ) );
    }

    private string LicensePath( string filename ) => Path.Combine( m_directory, filename );

    private bool LoadOrActivate( Func<string, bool> load, Func<string, string, bool> activate )
    {
      var method = typeof( AndroidRuntimeLicense ).GetMethod( "LoadOrActivate", BindingFlags.Static | BindingFlags.NonPublic );
      return (bool)method.Invoke( null, new object[] { m_directory, load, activate } );
    }

    private static byte[] Bundle( string identifier = Identifier,
                                  string referenceName = AndroidRuntimeLicense.ReferenceFilename,
                                  string request = "RT=test-request",
                                  byte[] reference = null )
    {
      using ( var memory = new MemoryStream() ) {
        using ( var zip = new ZipArchive( memory, ZipArchiveMode.Create, true ) ) {
          WriteEntry( zip, referenceName, reference ?? Encoding.UTF8.GetBytes( "static-app-reference" ) );
          WriteEntry( zip, AndroidRuntimeLicense.ActivationFilename, Encoding.UTF8.GetBytes( request ) );
          WriteEntry( zip, AndroidRuntimeLicense.ApplicationIdFilename, Encoding.UTF8.GetBytes( identifier ) );
        }
        return memory.ToArray();
      }
    }

    private static void WriteEntry( ZipArchive zip, string filename, byte[] bytes )
    {
      using ( var target = zip.CreateEntry( filename ).Open() )
        target.Write( bytes, 0, bytes.Length );
    }
  }
}
