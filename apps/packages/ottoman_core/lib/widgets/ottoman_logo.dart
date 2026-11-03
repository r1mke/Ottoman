import 'package:flutter/material.dart';

/// Which of the salon's logos to draw.
enum LogoVariant {
  /// The emblem (fez, moustache, scissors) with the name below it.
  vertical('logo_vertical'),

  /// The emblem and the name side by side, for headers.
  horizontal('logo_horizontal'),

  /// The emblem alone, for small places and round frames.
  emblem('emblem');

  const LogoVariant(this.file);

  final String file;
}

/// The Ottoman logo from the bundled artwork. Black for light backgrounds, white for dark ones; when [light] is
/// not given it follows the theme's brightness.
class OttomanLogo extends StatelessWidget {
  const OttomanLogo({
    super.key,
    this.variant = LogoVariant.vertical,
    this.light,
    this.height,
    this.width,
  });

  final LogoVariant variant;

  /// True for the white logo (on a dark background).
  final bool? light;
  final double? height;
  final double? width;

  @override
  Widget build(BuildContext context) {
    final white = light ?? Theme.of(context).brightness == Brightness.dark;
    return Image.asset(
      'assets/logo/${variant.file}_${white ? 'white' : 'black'}.png',
      package: 'ottoman_core',
      height: height,
      width: width,
      fit: BoxFit.contain,
      filterQuality: FilterQuality.high,
      semanticLabel: 'Ottoman',
    );
  }
}
