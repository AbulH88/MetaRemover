# Camera metadata defaults

Checked 2026-10-11. Apertures and equivalent focal lengths use Apple specifications. Main is the native wide/fusion camera; Telephoto selects the dedicated optical camera, not the main-sensor 2x crop. Front and SE equivalent lengths remain optional. Physical focal length is never derived from a 35mm equivalent. Lens model strings are descriptive editable labels, not copied Apple MakerNotes.

- [iPhone 16 Pro](https://support.apple.com/en-us/121031): Main 24mm f/1.78, Ultra Wide 13mm f/2.2, Telephoto 120mm f/2.8; front f/1.9. Same camera defaults for Pro Max.
- [iPhone 16](https://support.apple.com/en-us/121029): Main 26mm f/1.6, Ultra Wide 13mm f/2.2; front f/1.9. Same defaults for Plus.
- [iPhone 16e](https://support.apple.com/en-us/122208): Main 26mm f/1.6, front f/1.9; no Ultra Wide or dedicated Telephoto.
- [iPhone 15 Pro](https://support.apple.com/en-us/111829): Main 24mm f/1.78, Ultra Wide 13mm f/2.2, Telephoto 77mm f/2.8. Pro Max uses 120mm telephoto. Front f/1.9.
- [iPhone 15](https://support.apple.com/en-us/111831): Main 26mm f/1.6, Ultra Wide 13mm f/2.4; front f/1.9. Same defaults for Plus.
- [iPhone 14 Pro](https://support.apple.com/en-us/111849): Main 24mm f/1.78, Ultra Wide 13mm f/2.2, Telephoto 77mm f/2.8; front f/1.9. Same defaults for Pro Max.
- [iPhone 14](https://support.apple.com/en-us/111850): Main 26mm f/1.5, Ultra Wide 13mm f/2.4; front f/1.9. Same defaults for Plus.
- [iPhone 13 Pro](https://support.apple.com/en-us/111871): Main f/1.5, Ultra Wide f/1.8, Telephoto f/2.8; front f/2.2. [Apple announcement](https://www.apple.com/newsroom/2021/09/apple-unveils-iphone-13-pro-and-iphone-13-pro-max-more-pro-than-ever-before/) documents 77mm telephoto. Same defaults for Pro Max.
- [iPhone 13](https://support.apple.com/en-us/111872): Main f/1.6, Ultra Wide f/2.4; front f/2.2. Same defaults for mini.
- [iPhone 12](https://support.apple.com/en-is/111876): Main f/1.6, Ultra Wide f/2.4; front f/2.2. Same defaults for mini. [Apple Pro announcement](https://www.apple.com/eg/newsroom/2020/10/apple-introduces-iphone-12-pro-and-iphone-12-pro-max-with-5g/) documents 52mm Pro and 65mm Pro Max telephoto; [Pro Max specifications](https://support.apple.com/en-us/111874) document f/2.2 telephoto.
- [iPhone 11](https://support.apple.com/en-us/111865): Main f/1.8, Ultra Wide f/2.4; front f/2.2. [Pro specifications](https://support.apple.com/en-us/111879) document f/2.0 telephoto. Legacy Main/Ultra Wide equivalent presets use 26/13mm, Telephoto 52mm.
- [iPhone SE first generation](https://support.apple.com/en-us/112005): Main f/2.2, front f/2.4; later SE generations use Main f/1.8 and front f/2.2.

ExifTool's bundled `QuickTime.pm` VideoKeys table supports LensModel and FocalLengthIn35mmFormat. Photo exposure fields are EXIF-only. The EXIF verifier checks numeric values with tolerance for rational normalization. Exposure and software version are user-defined; no per-shot values are guessed.

## Complete example profiles in v1.3

One-click profiles supply editable daylight examples (ISO100, 1/250 s, 0 EV, flash off, auto white balance) and fixed example software. These are expressly sample configurations, not recovered capture measurements. Physical lens values are independently entered defaults, not derived from 35mm equivalents. The earlier hardware catalog still supplies documented aperture/equivalent values where available; complete profile front/SE equivalents and some physical mm are example assumptions.

[ExifTool's Apple metadata samples](https://exiftool.org/sample_images.html), downloaded from `https://exiftool.org/Apple.tar.gz`, provided main-lens examples: 11 4.25mm, 12 Pro 4.2mm, 12 Pro Max/13 5.1mm, 13 Pro Max/14 5.7mm, 15/15 Plus 5.96mm, 15 Pro 6.86mm, SE 4.15mm and SE2 3.99mm; telephoto examples 13 Pro 9mm and 15 Pro Max 15.66mm. Source extraction is retained locally at `.build/apple-sample-lenses.json`. [Apple's video lens metadata documentation](https://developer.apple.com/documentation/avfoundation/avmetadataidentifier/quicktimemetadatacameralensmodel) supplies the example 16 Pro 6.765mm f/1.78. Shared/later model values and front/ultrawide values in the example profile are preset assumptions and remain editable. No Apple MakerNotes are copied.
