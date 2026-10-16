import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';
import 'package:ottoman_desktop/features/shell/sidebar.dart';

Widget _host({
  required List<SidebarItem> items,
  int selected = 0,
  ValueChanged<int>? onSelect,
  VoidCallback? onLogout,
}) => MaterialApp(
  theme: WorkspaceTheme.build(),
  localizationsDelegates: const [
    AppLocalizations.delegate,
    GlobalMaterialLocalizations.delegate,
    GlobalWidgetsLocalizations.delegate,
    GlobalCupertinoLocalizations.delegate,
  ],
  supportedLocales: AppLocalizations.supportedLocales,
  locale: const Locale('bs'),
  home: Scaffold(
    body: Row(
      children: [
        Sidebar(
          items: items,
          selected: selected,
          onSelect: onSelect ?? (_) {},
          userName: 'Admin Ottoman',
          onLogout: onLogout ?? () {},
        ),
      ],
    ),
  ),
);

void main() {
  testWidgets(
    'shows every entry, the user, and a badge only where there is a count',
    (tester) async {
      await tester.pumpWidget(
        _host(
          items: const [
            SidebarItem(icon: Icons.dashboard, label: 'Pregled'),
            SidebarItem(icon: Icons.inventory_2, label: 'Proizvodi', badge: 3),
            SidebarItem(
              icon: Icons.receipt_long,
              label: 'Narudžbe',
              badge: 120,
            ),
          ],
        ),
      );

      expect(find.text('Pregled'), findsOneWidget);
      expect(find.text('Proizvodi'), findsOneWidget);
      expect(find.text('Admin Ottoman'), findsOneWidget);
      expect(find.byKey(const Key('nav_badge')), findsNWidgets(2));
      expect(find.text('3'), findsOneWidget);
      expect(
        find.text('99+'),
        findsOneWidget,
        reason: 'huge counts do not break the layout',
      );
    },
  );

  testWidgets('tapping an entry reports its index; logout is reachable', (
    tester,
  ) async {
    int? picked;
    var loggedOut = false;
    await tester.pumpWidget(
      _host(
        items: const [
          SidebarItem(icon: Icons.dashboard, label: 'Pregled'),
          SidebarItem(icon: Icons.list, label: 'Usluge'),
        ],
        onSelect: (i) => picked = i,
        onLogout: () => loggedOut = true,
      ),
    );

    await tester.tap(find.byKey(const Key('nav_1')));
    await tester.tap(find.byKey(const Key('logout')));

    expect(picked, 1);
    expect(loggedOut, isTrue);
  });

  testWidgets('a long name does not overflow the narrow column', (
    tester,
  ) async {
    await tester.pumpWidget(
      _host(
        items: const [
          SidebarItem(
            icon: Icons.dashboard,
            label: 'Neki vrlo dugačak naziv sekcije koji ne staje',
            badge: 5,
          ),
        ],
      ),
    );
    expect(tester.takeException(), isNull);
  });

  test('the workspace theme uses the variant B palette and does not stretch buttons', () {
    final theme = WorkspaceTheme.build(fontFamily: 'Manrope');

    expect(theme.scaffoldBackgroundColor, WorkspaceColors.canvas);
    expect(theme.colorScheme.primary, WorkspaceColors.ink);
    expect(theme.colorScheme.secondary, WorkspaceColors.amber);
    expect(theme.textTheme.bodyMedium?.fontFamily, 'Manrope');
    final size = theme.filledButtonTheme.style!.minimumSize!.resolve({});
    expect(
      size!.width,
      0,
      reason: 'a full-width default broke dialogs and rows before',
    );
    expect(size.height, 44);
  });
}
