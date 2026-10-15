import 'package:flutter/material.dart';

/// Brand tokens. Black/white base from the mockups with a warm "fez" accent.
abstract final class OttomanColors {
  static const ink = Color(0xFF0E0E10);
  static const paper = Color(0xFFFFFFFF);
  static const accent = Color(0xFFB4372B);
  static const success = Color(0xFF2E7D4F);
  static const warning = Color(0xFFB7791F);
  static const danger = Color(0xFFC0392B);
}

abstract final class OttomanTheme {
  static ThemeData light() => _build(Brightness.light);
  static ThemeData dark() => _build(Brightness.dark);

  static ThemeData _build(Brightness brightness) {
    final isLight = brightness == Brightness.light;
    final scheme =
        ColorScheme.fromSeed(
          seedColor: OttomanColors.accent,
          brightness: brightness,
        ).copyWith(
          primary: isLight ? OttomanColors.ink : OttomanColors.paper,
          onPrimary: isLight ? OttomanColors.paper : OttomanColors.ink,
          secondary: OttomanColors.accent,
          error: OttomanColors.danger,
          surface: isLight ? OttomanColors.paper : const Color(0xFF151517),
        );

    final radius = BorderRadius.circular(12);
    return ThemeData(
      useMaterial3: true,
      colorScheme: scheme,
      scaffoldBackgroundColor: isLight
          ? const Color(0xFFF6F6F7)
          : const Color(0xFF0E0E10),
      appBarTheme: AppBarTheme(
        backgroundColor: scheme.surface,
        foregroundColor: scheme.onSurface,
        elevation: 0,
        scrolledUnderElevation: 0.5,
        centerTitle: false,
      ),
      cardTheme: CardThemeData(
        color: scheme.surface,
        elevation: 0,
        margin: EdgeInsets.zero,
        shape: RoundedRectangleBorder(
          borderRadius: radius,
          side: BorderSide(color: scheme.outlineVariant),
        ),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          minimumSize: const Size.fromHeight(48),
          shape: RoundedRectangleBorder(borderRadius: radius),
          textStyle: const TextStyle(fontWeight: FontWeight.w600, fontSize: 16),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          minimumSize: const Size.fromHeight(48),
          shape: RoundedRectangleBorder(borderRadius: radius),
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: scheme.surface,
        border: OutlineInputBorder(borderRadius: radius),
        enabledBorder: OutlineInputBorder(
          borderRadius: radius,
          borderSide: BorderSide(color: scheme.outlineVariant),
        ),
      ),
      chipTheme: ChipThemeData(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
        side: BorderSide(color: scheme.outlineVariant),
      ),
      dividerTheme: DividerThemeData(color: scheme.outlineVariant, space: 1),
    );
  }
}
