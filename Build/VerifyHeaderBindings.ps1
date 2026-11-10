#Requires -Version 7.2
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$repo = Split-Path $PSScriptRoot -Parent
$editorConfig = Join-Path $repo '.editorconfig'
if (!(Test-Path -LiteralPath $editorConfig)) {
    Copy-Item -LiteralPath (Join-Path $repo 'externals/TedToolkit/.editorconfig') -Destination $editorConfig
}
$rid = $IsWindows ? 'win-x64' : 'linux-x64'
$root = Join-Path $repo ('out/verification/header-' + [Guid]::NewGuid().ToString('N').Substring(0, 12))
$generate = (New-Item -ItemType Directory -Path (Join-Path $root 'generate') -Force).FullName
$managed = (New-Item -ItemType Directory -Path (Join-Path $root 'managed') -Force).FullName
$cpp = Join-Path $root 'cpp'

@'
struct Point { long value; wchar_t code; };
extern "C" {
    inline long Add(long value, const wchar_t* code) noexcept { return value + *code; }
    inline Point Translate(Point point, long delta) noexcept { return {point.value + delta, point.code}; }
    inline Point& Borrow(Point& point) noexcept { return point; }
    inline const Point& Read(const Point& point) noexcept { return point; }
}
'@ | Set-Content -LiteralPath (Join-Path $root 'Fixture.h') -Encoding utf8

foreach ($entry in @(@{ Folder = $generate; Project = 'Generator' }, @{ Folder = $managed; Project = 'Runtime' })) {
    $projectPath = "../../../../src/shared/TedToolkit.CppBindings.$($entry.Project)/TedToolkit.CppBindings.$($entry.Project).csproj"
    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="$projectPath" />
  </ItemGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $entry.Folder 'Smoke.csproj') -Encoding utf8
}

@'
using ModularPipelines;
using TedToolkit.CppBindings.Generator;
using TedToolkit.FileSystem;

var root = new DirectoryPath(args[0]);
var builder = Pipeline.CreateBuilder();
builder.Options.ThrowOnPipelineFailure = true;
var pipeline = await builder.AddCppGenerators(new GenerationOptions
{
    CSharpNamespace = "Fixture.Bindings",
    NativeLibraryBaseName = "fixture_bridge",
    RuntimeIdentifier = args[1],
    CSharpFolder = root / "managed" / "Generated",
    CppFolder = root / "cpp",
    Headers = [new(root / new FileName("Fixture.h"))],
}).BuildAsync();
await pipeline.RunAsync();
'@ | Set-Content -LiteralPath (Join-Path $generate 'Program.cs') -Encoding utf8

@'
using Fixture.Bindings;

unsafe
{
    var code = (CODE_TYPE)97;
    var value = (VALUE_TYPE)TEST_VALUE;
    if (FixtureWrapper.Add(value, &code) != value + code) throw new Exception("scalar ABI");
    var point = new Point { value = value, code = code };
    var translated = FixtureWrapper.Translate(point, 1);
    if (translated.value != value + 1 || translated.code != code) throw new Exception("record ABI");
    ref Point borrowed = ref FixtureWrapper.Borrow(ref point);
    ref readonly Point read = ref FixtureWrapper.Read(in point);
    borrowed.value++;
    if (read.value != value + 1) throw new Exception("reference ABI");
}
'@.Replace('CODE_TYPE', ($IsWindows ? 'ushort' : 'int')).
    Replace('VALUE_TYPE', ($IsWindows ? 'int' : 'long')).
    Replace('TEST_VALUE', ($IsWindows ? '1000' : '0x100000000L')) |
    Set-Content -LiteralPath (Join-Path $managed 'Program.cs') -Encoding utf8

dotnet run --project (Join-Path $generate 'Smoke.csproj') -c Release -p:NuGetAudit=false -- $root $rid
$build = Join-Path $root 'native-build'
cmake -S $cpp -B $build -DCMAKE_BUILD_TYPE=Release
cmake --build $build --config Release
$bin = Join-Path $root 'run'
dotnet build (Join-Path $managed 'Smoke.csproj') -c Release -o $bin -p:NuGetAudit=false
$name = $IsWindows ? 'fixture_bridge.dll' : 'libfixture_bridge.so'
$library = Get-ChildItem -LiteralPath $build -Recurse -Filter $name -File
Copy-Item -LiteralPath $library.FullName -Destination $bin
dotnet (Join-Path $bin 'Smoke.dll')
Write-Output "Shared header binding smoke passed ($rid)"
