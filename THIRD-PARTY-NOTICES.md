# Third-party materials

MetaRemover is an independent application, not an Apple product.

## ExifTool 13.59
Phil Harvey, https://exiftool.org/
Redistributed as a separate executable with its original exiftool_files directory.
License and Windows runtime notices: tools/exiftool/exiftool_files/LICENSE and Licenses_Strawberry_Perl.zip.
ExifTool is distributed under the same terms as Perl (Artistic License or GNU GPL).
Upstream source: https://exiftool.org/ and https://github.com/exiftool/exiftool
The included Perl/runtime materials retain their original notices.

## FFmpeg and FFprobe
BtbN Windows x64 LGPL shared build, n9.0.2-25-g67b60c310b-20261011.
https://github.com/BtbN/FFmpeg-Builds
FFmpeg source commit: 67b60c310b
License: tools/ffmpeg/LICENSE.txt (LGPL v3).
Exact FFmpeg source snapshot and build recipe snapshot are included in tools/ffmpeg/source.
The build recipes name the source repositories and licenses of enabled dependencies, including static dependency components. The configuration is recorded in ffmpeg-version.txt.
The executable and shared DLLs are separate replaceable components. Users may replace compatible tools/DLLs with modified versions; the app invokes them through a process interface.
Keep source archives, provenance and licenses with copies of this distribution. Source repositories and pinned recipe versions remain available through the recipe snapshot.
Build inputs/source download helper: tools/ffmpeg/source/build-recipes.zip contains the builder's recipes; these record each upstream dependency source URL and revision.

## Microsoft .NET / WPF
Self-contained .NET 10 runtime from Microsoft.
https://github.com/dotnet/runtime and https://github.com/dotnet/wpf
Runtime license / third-party notices included under licenses/dotnet.
