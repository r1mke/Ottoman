import 'package:flutter/material.dart';

/// Palette of the desktop "workspace" look (variant B): a light work surface, a dark sidebar and one amber accent.
abstract final class WorkspaceColors {
  static const canvas = Color(0xFFEEF1F4);
  static const surface = Color(0xFFFFFFFF);
  static const ink = Color(0xFF14181D);
  static const line = Color(0xFFDDE2E8);
  static const muted = Color(0xFF5C6672);
  static const tableHead = Color(0xFFF6F8FA);

  static const sidebar = Color(0xFF14181D);
  static const sidebarActive = Color(0xFF262C34);
  static const sidebarText = Color(0xFFAEB6C0);
  static const sidebarMuted = Color(0xFF8A929C);

  static const amber = Color(0xFFE0A43A);
  static const amberSoft = Color(0xFFFBECCB);
  static const amberInk = Color(0xFF8A5A00);

  static const success = Color(0xFF16794A);
  static const danger = Color(0xFFB3361D);
  static const info = Color(0xFF3B82C4);
  static const neutral = Color(0xFF9AA5B1);
}

/// The theme of the desktop app. Mobile keeps [OttomanTheme]; this one is dense, quiet and table friendly.
abstract final class WorkspaceTheme {
  static ThemeData build({String? fontFamily}) {
    const c = WorkspaceColors.ink;
    final scheme = ColorScheme.fromSeed(
      seedColor: WorkspaceColors.amber,
      brightness: Brightness.light,
    ).copyWith(
      primary: c,
      onPrimary: Colors.white,
      secondary: WorkspaceColors.amber,
      onSecondary: c,
      secondaryContainer: WorkspaceColors.amberSoft,
      onSecondaryContainer: WorkspaceColors.amberInk,
      surface: WorkspaceColors.surface,
      onSurface: c,
      onSurfaceVariant: WorkspaceColors.muted,
      surfaceContainerHighest: WorkspaceColors.tableHead,
      surfaceContainerHigh: WorkspaceColors.tableHead,
      surfaceContainer: WorkspaceColors.canvas,
      surfaceContainerLow: WorkspaceColors.canvas,
      outline: WorkspaceColors.muted,
      outlineVariant: WorkspaceColors.line,
      error: WorkspaceColors.danger,
      tertiary: WorkspaceColors.success,
    );

    final card = BorderRadius.circular(12);
    final control = BorderRadius.circular(10);
    final base = ThemeData(
      useMaterial3: true,
      colorScheme: scheme,
      fontFamily: fontFamily,
      brightness: Brightness.light,
    );

    return base.copyWith(
      scaffoldBackgroundColor: WorkspaceColors.canvas,
      canvasColor: WorkspaceColors.canvas,
      textTheme: base.textTheme.apply(
        bodyColor: c,
        displayColor: c,
        fontFamily: fontFamily,
      ),
      appBarTheme: const AppBarTheme(
        backgroundColor: WorkspaceColors.surface,
        foregroundColor: c,
        elevation: 0,
        scrolledUnderElevation: 0,
        centerTitle: false,
      ),
      cardTheme: CardThemeData(
        color: WorkspaceColors.surface,
        elevation: 0,
        margin: EdgeInsets.zero,
        surfaceTintColor: Colors.transparent,
        shape: RoundedRectangleBorder(
          borderRadius: card,
          side: const BorderSide(color: WorkspaceColors.line),
        ),
      ),
      dialogTheme: DialogThemeData(
        backgroundColor: WorkspaceColors.surface,
        surfaceTintColor: Colors.transparent,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
        titleTextStyle: TextStyle(
          fontFamily: fontFamily,
          fontSize: 20,
          fontWeight: FontWeight.w800,
          color: c,
        ),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          backgroundColor: c,
          foregroundColor: Colors.white,
          minimumSize: const Size(0, 44),
          shape: RoundedRectangleBorder(borderRadius: control),
          textStyle: const TextStyle(fontWeight: FontWeight.w700, fontSize: 14),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: c,
          minimumSize: const Size(0, 44),
          side: const BorderSide(color: WorkspaceColors.line),
          shape: RoundedRectangleBorder(borderRadius: control),
          textStyle: const TextStyle(fontWeight: FontWeight.w700, fontSize: 14),
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(
          foregroundColor: c,
          minimumSize: const Size(0, 40),
          shape: RoundedRectangleBorder(borderRadius: control),
          textStyle: const TextStyle(fontWeight: FontWeight.w700),
        ),
      ),
      iconButtonTheme: IconButtonThemeData(
        style: IconButton.styleFrom(foregroundColor: WorkspaceColors.muted),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: WorkspaceColors.surface,
        isDense: true,
        contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
        border: OutlineInputBorder(borderRadius: control),
        enabledBorder: OutlineInputBorder(
          borderRadius: control,
          borderSide: const BorderSide(color: WorkspaceColors.line),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: control,
          borderSide: const BorderSide(color: c, width: 1.6),
        ),
        errorBorder: OutlineInputBorder(
          borderRadius: control,
          borderSide: const BorderSide(color: WorkspaceColors.danger),
        ),
        labelStyle: const TextStyle(color: WorkspaceColors.muted),
      ),
      // The period switch and filters read as one pill group: ink when selected.
      chipTheme: ChipThemeData(
        backgroundColor: WorkspaceColors.surface,
        selectedColor: c,
        checkmarkColor: Colors.white,
        showCheckmark: false,
        side: const BorderSide(color: WorkspaceColors.line),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(99)),
        labelStyle: const TextStyle(
          fontWeight: FontWeight.w700,
          fontSize: 13,
          color: WorkspaceColors.muted,
        ),
        secondaryLabelStyle: const TextStyle(
          fontWeight: FontWeight.w700,
          fontSize: 13,
          color: Colors.white,
        ),
        padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 4),
      ),
      dataTableTheme: DataTableThemeData(
        headingRowColor: const WidgetStatePropertyAll(WorkspaceColors.tableHead),
        headingTextStyle: const TextStyle(
          color: WorkspaceColors.muted,
          fontSize: 12,
          fontWeight: FontWeight.w700,
        ),
        dataTextStyle: const TextStyle(fontSize: 14, color: c),
        dividerThickness: 1,
        dataRowMinHeight: 52,
        dataRowMaxHeight: 64,
        headingRowHeight: 44,
        horizontalMargin: 20,
        columnSpacing: 24,
      ),
      dividerTheme: const DividerThemeData(
        color: WorkspaceColors.line,
        space: 1,
        thickness: 1,
      ),
      listTileTheme: const ListTileThemeData(
        iconColor: WorkspaceColors.muted,
        textColor: c,
      ),
      switchTheme: SwitchThemeData(
        thumbColor: WidgetStateProperty.resolveWith(
          (s) => s.contains(WidgetState.selected) ? Colors.white : WorkspaceColors.neutral,
        ),
        trackColor: WidgetStateProperty.resolveWith(
          (s) => s.contains(WidgetState.selected) ? WorkspaceColors.success : WorkspaceColors.line,
        ),
        trackOutlineColor: const WidgetStatePropertyAll(Colors.transparent),
      ),
      snackBarTheme: SnackBarThemeData(
        backgroundColor: c,
        contentTextStyle: const TextStyle(color: Colors.white),
        behavior: SnackBarBehavior.floating,
        shape: RoundedRectangleBorder(borderRadius: control),
      ),
      tooltipTheme: TooltipThemeData(
        decoration: BoxDecoration(color: c, borderRadius: BorderRadius.circular(6)),
        textStyle: const TextStyle(color: Colors.white, fontSize: 12),
      ),
      progressIndicatorTheme: const ProgressIndicatorThemeData(color: c),
    );
  }
}
