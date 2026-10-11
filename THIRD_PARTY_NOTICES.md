# Third-party notices

## Microsoft.CodeAnalysis.CSharp

The standalone-only `DDFLanguageEditor.Compiler` assembly uses the
`Microsoft.CodeAnalysis.CSharp` 5.9.0 NuGet package. Roslyn is provided by
Microsoft under the MIT License. Core, WinForms and the embedded SDK do not
depend on Roslyn.

## Microsoft.Data.Sqlite

The `DDFLanguageEditor.Data.Tests` integration gate and the compiler's
cross-backend adapter gate use
`Microsoft.Data.Sqlite` 10.0.12 and its SQLitePCLRaw dependencies under their
respective permissive licenses. The provider is not a dependency of
`DDFLanguageEditor.Core` or the embedded editor SDK; production hosts select
and distribute their own ADO.NET provider.

- NuGet package: https://www.nuget.org/packages/Microsoft.Data.Sqlite/10.0.12
- Source and license: https://github.com/dotnet/efcore

## System.IO.Ports

`DDFLanguageEditor.Hardware` uses the `System.IO.Ports` 10.0.12 NuGet
package for explicitly configured serial endpoints. The package is provided by
Microsoft under the MIT License. DDF does not enumerate ports on behalf of a
script; availability of a physical endpoint still depends on the operating
system and installed driver.

- NuGet package: https://www.nuget.org/packages/System.IO.Ports/10.0.12
- Source and license: https://github.com/dotnet/runtime

## System.Device.Gpio

`DDFLanguageEditor.Hardware` uses the `System.Device.Gpio` 4.2.0 NuGet
package as an optional physical bridge for host-authorized GPIO, I2C, SPI and
PWM endpoints. The package is provided by Microsoft under the MIT License.
Actual availability depends on the operating system, board, controller,
driver and wiring; DDF does not discover or authorize hardware implicitly.

- NuGet package: https://www.nuget.org/packages/System.Device.Gpio/4.2.0
- Source and license: https://github.com/dotnet/iot

## Microsoft .NET

It targets Microsoft .NET 10 and uses Windows Forms, System.Drawing
and other framework assemblies supplied by the .NET Desktop Runtime. The official
self-contained package bundles the redistributable runtime files required by the application.
Those components are owned and licensed by Microsoft
under their respective terms and are not relicensed by this project's MIT
License.

- .NET documentation: https://learn.microsoft.com/dotnet/
- Microsoft licensing information: https://www.microsoft.com/licensing/
