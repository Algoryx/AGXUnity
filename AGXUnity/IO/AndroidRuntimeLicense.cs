using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace AGXUnity.IO
{
  /// <summary>Stages Android activation data separately from the persistent device license.</summary>
  public static class AndroidRuntimeLicense
  {
    public const string StreamingAssetPath = "AGXUnity/agx-runtime-activation.bytes";
    public const string ActivationFilename = "agx.rtlfx";
    public const string ReferenceFilename = "agx-runtime-reference";
    public const string ApplicationIdFilename = "application-id";
    public const string ServiceFilename = "agx.lfx";

    public static void ValidateActivationBundle( byte[] bundle, string applicationIdentifier )
    {
      ReadActivationBundle( bundle, applicationIdentifier );
    }

    /// <summary>Extracts the request without replacing a previously activated device license.</summary>
    public static void Prepare( string directory, byte[] activationBundle, string applicationIdentifier )
    {
      Directory.CreateDirectory( directory );
      if ( File.Exists( Path.Combine( directory, ServiceFilename ) ) )
        return;
      if ( activationBundle == null )
        throw new InvalidDataException( "The Android runtime activation bundle is missing. Generate it before building the player." );
      var data = ReadActivationBundle( activationBundle, applicationIdentifier );
      File.WriteAllBytes( Path.Combine( directory, ReferenceFilename ), data.Reference );
      File.WriteAllBytes( Path.Combine( directory, ActivationFilename ), data.Activation );
    }

    // Keep filesystem decisions independent of the native licensing backend so
    // failure/relaunch behavior can be verified without consuming activations.
    internal static bool LoadOrActivate( string directory, Func<string, bool> loadLicense, Func<string, string, bool> activate )
    {
      // An expired/rejected license needs attention, not another automatic
      // activation. Preserve it and the native error for diagnosis.
      string service = Path.Combine( directory, ServiceFilename );
      if ( File.Exists( service ) )
        return loadLicense( service );
      string request = Path.Combine( directory, ActivationFilename );
      return File.Exists( request ) && activate( request, directory );
    }

    private static (byte[] Reference, byte[] Activation) ReadActivationBundle( byte[] bundle, string applicationIdentifier )
    {
      using ( var memory = new MemoryStream( bundle, false ) )
      using ( var archive = new ZipArchive( memory, ZipArchiveMode.Read ) ) {
        // Only known entries are extracted, using fixed paths rather than paths
        // supplied by the archive. Reject duplicates and incomplete bundles.
        if ( archive.Entries.Count != 3 )
          throw new InvalidDataException( "The Android runtime activation bundle is incomplete. Regenerate it before building." );
        byte[] reference = ReadEntry( archive, ReferenceFilename );
        byte[] activation = ReadEntry( archive, ActivationFilename );
        string identifier = Encoding.UTF8.GetString( ReadEntry( archive, ApplicationIdFilename ) );
        if ( !string.Equals( identifier, applicationIdentifier, StringComparison.Ordinal ) )
          throw new InvalidDataException( "The Android runtime activation was generated for a different application identifier. Regenerate it before building." );
        if ( !Encoding.UTF8.GetString( activation ).StartsWith( "RT=", StringComparison.Ordinal ) )
          throw new InvalidDataException( "The Android runtime activation request is invalid. Regenerate it before building." );
        return (reference, activation);
      }
    }

    private static byte[] ReadEntry( ZipArchive archive, string name )
    {
      var entry = archive.GetEntry( name );
      if ( entry == null || entry.Length == 0 )
        throw new InvalidDataException( "Missing Android runtime activation data: " + name );
      using ( var source = entry.Open() )
      using ( var memory = new MemoryStream() ) {
        source.CopyTo( memory );
        return memory.ToArray();
      }
    }
  }
}
