using DenoHost.Runtime.Downloader;
using System.Security.Cryptography;
using System.Text.Json;

namespace DenoHost.Tests;

public sealed class DownloaderLogicTests
{
  [Fact]
  public void ExtractExpectedSha256_MatchesByExactFileName()
  {
    const string hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    var checksum = $"{hash}  deno-x86_64-unknown-linux-gnu.zip";

    var actual = DownloaderLogic.ExtractExpectedSha256FromContent(checksum, "deno-x86_64-unknown-linux-gnu.zip");

    Assert.Equal(hash, actual);
  }

  [Fact]
  public void ExtractExpectedSha256_MatchesPowerShellFormat()
  {
    const string hash = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
    var checksum = $"Hash      : {hash}\nAlgorithm : SHA256";

    var actual = DownloaderLogic.ExtractExpectedSha256FromContent(checksum, "deno-aarch64-pc-windows-msvc.zip");

    Assert.Equal(hash, actual);
  }

  [Fact]
  public void ExtractExpectedSha256_FallsBackToFirstHash()
  {
    const string hash = "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210";
    var checksum = $"{hash}  *unexpected-name.zip";

    var actual = DownloaderLogic.ExtractExpectedSha256FromContent(checksum, "deno.zip");

    Assert.Equal(hash, actual);
  }

  [Fact]
  public void ResolveRuntimeRid_UsesProvidedRidFirst()
  {
    var rid = DownloaderLogic.ResolveRuntimeRid("/tmp/DenoHost.Runtime.linux-x64/deno", "linux-arm64");

    Assert.Equal("linux-arm64", rid);
  }

  [Fact]
  public void ResolveRuntimeRid_DerivesRidFromRuntimeDirectory()
  {
    var rid = DownloaderLogic.ResolveRuntimeRid("/tmp/DenoHost.Runtime.osx-arm64/deno", null);

    Assert.Equal("osx-arm64", rid);
  }

  [Fact]
  public void ResolveRuntimeRid_ReturnsUnknownWhenDirectoryDoesNotMatch()
  {
    var rid = DownloaderLogic.ResolveRuntimeRid("/tmp/something-else/deno", null);

    Assert.Equal("unknown", rid);
  }

  [Fact]
  public void SerializeRuntimeMetadata_ProducesExpectedJsonShape()
  {
    var metadata = DownloaderLogic.CreateRuntimeMetadata(
      "/tmp/DenoHost.Runtime.linux-x64/deno",
      "2.8.1",
      "linux-x64",
      "deno-x86_64-unknown-linux-gnu.zip",
      "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
      "2026-05-29T00:00:00.0000000Z",
      DownloaderWorkflow.BaseUrlForTests);

    var json = DownloaderLogic.SerializeRuntimeMetadata(metadata);
    using var document = JsonDocument.Parse(json);

    Assert.Equal(1, document.RootElement.GetProperty("metadataVersion").GetInt32());
    Assert.Equal("deno", document.RootElement.GetProperty("fileName").GetString());
    Assert.Equal("linux-x64", document.RootElement.GetProperty("rid").GetString());
    Assert.Equal("2.8.1", document.RootElement.GetProperty("denoVersion").GetString());
    Assert.Equal("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", document.RootElement.GetProperty("sha256").GetString());
    Assert.Equal("https://github.com/denoland/deno/releases/download/v2.8.1/deno-x86_64-unknown-linux-gnu.zip", document.RootElement.GetProperty("source").GetString());
    Assert.Equal("2026-05-29T00:00:00.0000000Z", document.RootElement.GetProperty("createdAtUtc").GetString());
  }

  [Fact]
  public void SignMetadata_RoundTripsWithGeneratedKey()
  {
    using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    var privateKeyPem = ExportPemPrivateKey(ecdsa);

    var metadataBytes = "{\"example\":true}"u8.ToArray();
    var signatureBase64 = DownloaderLogic.SignMetadata(metadataBytes, privateKeyPem);
    var signatureBytes = Convert.FromBase64String(signatureBase64);

    Assert.True(ecdsa.VerifyData(metadataBytes, signatureBytes, HashAlgorithmName.SHA256));
  }

  private static string ExportPemPrivateKey(ECDsa ecdsa)
  {
    var pkcs8 = ecdsa.ExportPkcs8PrivateKey();
    var base64 = Convert.ToBase64String(pkcs8, Base64FormattingOptions.InsertLineBreaks);
    return $"-----BEGIN PRIVATE KEY-----\n{base64}\n-----END PRIVATE KEY-----";
  }
}
