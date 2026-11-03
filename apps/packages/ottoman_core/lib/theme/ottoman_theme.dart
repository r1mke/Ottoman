import 'package:flutter/material.dart';

/// Brand tokens. Black/white base from the mockups with a warm "fez" accent.
abstract final class OttomanColors {
  static const ink = Color(0xFF0E0E10);
  static const paper = Color(0xFFFFFFFF);
  static const accent = Color(0xFFB4372B);
  static const success = Color(0xFF2E7D4F);
  static const warning = Color(0xFFB7791F);
  static const danger = Color(0xFFC0392B);

  // The mobile look: near-black, white text, and one warm amber between yellow and orange for
  // buttons and sub-headings. Text on amber is black: white on it is too faint to read.
  static const night = Color(0xFF0E0E14);
  static const nightCard = Color(0xFF1C1D27);
  static const nightRaised = Color(0xFF262734);
  static const nightLine = Color(0xFF2A2B36);
  static const nightBar = Color(0xFF0A0A0F);
  static const amber = Color(0xFFFFB000);
  static const alert = Color(0xFFE0434B);
}

abstract final class OttomanTheme {
  static ThemeData light() => _build(Brightness.light);
  static ThemeData dark() => _build(Brightness.dark);

  /// Black and white with an amber accent: the look of the mobile app.
  static ThemeData barbershop() {
    final scheme =
        ColorScheme.fromSeed(
          seedColor: OttomanColors.amber,
          brightness: Brightness.dark,
        ).copyWith(
          primary: OttomanColors.amber,
          onPrimary: const Color(0xFF111111),
          primaryContainer: const Color(0xFF3D2C07),
          onPrimaryContainer: const Color(0xFFFFD98A),
          secondary: OttomanColors.amber,
          onSecondary: const Color(0xFF111111),
          secondaryContainer: OttomanColors.nightRaised,
          onSecondaryContainer: Colors.white,
          surface: OttomanColors.night,
          onSurface: Colors.white,
          onSurfaceVariant: const Color(0xFFC4C4C4),
          surfaceContainerLowest: Colors.black,
          surfaceContainerLow: const Color(0xFF14151D),
          surfaceContainer: OttomanColors.nightCard,
          surfaceContainerHigh: OttomanColors.nightRaised,
          surfaceContainerHighest: const Color(0xFF30313F),
          outline: const Color(0xFF8E8FA0),
          outlineVariant: OttomanColors.nightLine,
          error: OttomanColors.alert,
          onError: const Color(0xFF1A0000),
        );
    final base = _build(Brightness.dark);
    final radius = BorderRadius.circular(20);
    final cardRadius = BorderRadius.circular(24);
    return base.copyWith(
      colorScheme: scheme,
      scaffoldBackgroundColor: OttomanColors.night,
      appBarTheme: AppBarTheme(
        backgroundColor: OttomanColors.night,
        foregroundColor: Colors.white,
        elevation: 0,
        scrolledUnderElevation: 0,
        centerTitle: false,
      ),
      cardTheme: CardThemeData(
        color: OttomanColors.nightCard,
        elevation: 0,
        margin: EdgeInsets.zero,
        shape: RoundedRectangleBorder(
          borderRadius: cardRadius,
          side: BorderSide(color: scheme.outlineVariant),
        ),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          minimumSize: const Size.fromHeight(56),
          shape: RoundedRectangleBorder(borderRadius: radius),
          textStyle: const TextStyle(fontWeight: FontWeight.w800, fontSize: 17),
          // Disabled: the same amber, dimmed, instead of a flat grey.
          disabledBackgroundColor: OttomanColors.amber.withValues(alpha: 0.22),
          disabledForegroundColor: OttomanColors.amber.withValues(alpha: 0.45),
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: OttomanColors.nightCard,
        border: OutlineInputBorder(borderRadius: BorderRadius.circular(16)),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(16),
          borderSide: BorderSide(color: scheme.outlineVariant),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(16),
          borderSide: const BorderSide(color: OttomanColors.amber, width: 1.5),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          minimumSize: const Size.fromHeight(52),
          foregroundColor: OttomanColors.amber,
          side: const BorderSide(color: OttomanColors.amber),
          shape: RoundedRectangleBorder(borderRadius: radius),
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(foregroundColor: OttomanColors.amber),
      ),
      chipTheme: ChipThemeData(
        // Pills, like the time slots and category filters.
        shape: const StadiumBorder(),
        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
        side: BorderSide(color: scheme.outlineVariant),
        backgroundColor: OttomanColors.nightCard,
        selectedColor: OttomanColors.amber,
        secondarySelectedColor: OttomanColors.amber,
        labelStyle: const TextStyle(color: Colors.white),
        secondaryLabelStyle: const TextStyle(color: Color(0xFF111111)),
        checkmarkColor: const Color(0xFF111111),
      ),
      navigationBarTheme: NavigationBarThemeData(
        backgroundColor: OttomanColors.nightBar,
        indicatorColor: Colors.transparent,
        elevation: 0,
        iconTheme: WidgetStateProperty.resolveWith(
          (states) => IconThemeData(
            color: states.contains(WidgetState.selected)
                ? OttomanColors.amber
                : const Color(0xFF9A9BAA),
          ),
        ),
        labelTextStyle: WidgetStateProperty.resolveWith(
          (states) => TextStyle(
            fontSize: 12,
            fontWeight: FontWeight.w600,
            color: states.contains(WidgetState.selected)
                ? OttomanColors.amber
                : const Color(0xFF9A9BAA),
          ),
        ),
      ),
      switchTheme: SwitchThemeData(
        thumbColor: WidgetStateProperty.resolveWith(
          (s) => s.contains(WidgetState.selected)
              ? const Color(0xFF111111)
              : Colors.white70,
        ),
        trackColor: WidgetStateProperty.resolveWith(
          (s) => s.contains(WidgetState.selected)
              ? OttomanColors.amber
              : const Color(0xFF3A3A3A),
        ),
      ),
      progressIndicatorTheme: const ProgressIndicatorThemeData(
        color: OttomanColors.amber,
      ),
      snackBarTheme: SnackBarThemeData(
        backgroundColor: OttomanColors.nightRaised,
        actionTextColor: OttomanColors.amber,
        contentTextStyle: const TextStyle(color: Colors.white),
        behavior: SnackBarBehavior.floating,
        shape: RoundedRectangleBorder(borderRadius: radius),
      ),
      dividerTheme: DividerThemeData(color: scheme.outlineVariant, space: 1),
    );
  }

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
