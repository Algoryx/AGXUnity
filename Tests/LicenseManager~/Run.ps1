$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$testOutput = Join-Path ([IO.Path]::GetTempPath()) ('agx-license-tests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testOutput | Out-Null
$sdk = (dotnet --list-sdks | Select-Object -Last 1)
if ($sdk -notmatch '^([^ ]+) \[(.+)\]$') { throw 'A .NET SDK is required.' }
$sdkDirectory = Join-Path $Matches[2] $Matches[1]
$dotnetRoot = [IO.Path]::GetFullPath((Join-Path $Matches[2] '..'))
$referencePack = Get-ChildItem (Join-Path $dotnetRoot 'packs/Microsoft.NETCore.App.Ref') -Directory |
  Sort-Object { [version]$_.Name } | Select-Object -Last 1
$framework = Get-ChildItem (Join-Path $referencePack.FullName 'ref') -Directory | Select-Object -First 1
$runtimeVersion = [version]$referencePack.Name
$outputDll = Join-Path $testOutput 'LicenseTests.dll'
$compilerArguments = @('-nologo', '-target:exe', '-define:UNITY_EDITOR', '-langversion:9', "-out:`"$outputDll`"")
$compilerArguments += Get-ChildItem $framework.FullName -Filter '*.dll' | ForEach-Object { "-r:`"$($_.FullName)`"" }
$compilerArguments += @(
  'AGXUnity/LicenseInfo.cs',
  'AGXUnity/LicenseManager.cs',
  'Editor/AGXUnityEditor/LicenseWarnings.cs',
  'Editor/AGXUnityEditor/Windows/LicenseManagerWindow.cs',
  'Tests/LicenseManager~/Stubs.cs',
  'Tests/LicenseManager~/Tests.cs'
) | ForEach-Object { '"' + (Join-Path $repoRoot $_) + '"' }
$responseFile = Join-Path $testOutput 'compile.rsp'
Set-Content -LiteralPath $responseFile -Value $compilerArguments
& dotnet (Join-Path $sdkDirectory 'Roslyn/bincore/csc.dll') "@$responseFile"
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
@{ runtimeOptions = @{ tfm = $framework.Name; framework = @{ name = 'Microsoft.NETCore.App'; version = "$($runtimeVersion.Major).$($runtimeVersion.Minor).0" } } } |
  ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $testOutput 'LicenseTests.runtimeconfig.json')
& dotnet $outputDll
if ($LASTEXITCODE -ne 0) { throw 'License lifecycle tests failed.' }
Write-Output "Test artifacts: $testOutput"
