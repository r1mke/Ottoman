import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';
import 'package:ottoman_desktop/features/employees/employee_dialogs.dart';
import 'package:ottoman_desktop/features/products/product_dialogs.dart';
import 'package:ottoman_desktop/features/services/services_page.dart';

ApiClient _client() =>
    ApiClient(baseUrl: 'http://test', dio: Dio(), readToken: () async => 't');

Future<void> _open(
  WidgetTester tester,
  Widget Function(BuildContext) dialog,
) async {
  tester.view.devicePixelRatio = 1.0;
  tester.view.physicalSize = const Size(1200, 1000);
  addTearDown(tester.view.reset);
  await tester.pumpWidget(
    MaterialApp(
      localizationsDelegates: const [
        AppLocalizations.delegate,
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      supportedLocales: AppLocalizations.supportedLocales,
      locale: const Locale('bs'),
      home: Builder(
        builder: (context) => Scaffold(
          body: Center(
            child: FilledButton(
              key: const Key('open'),
              onPressed: () =>
                  showDialog<void>(context: context, builder: dialog),
              child: const Text('otvori'),
            ),
          ),
        ),
      ),
    ),
  );
  await tester.tap(find.byKey(const Key('open')));
  await tester.pumpAndSettle();
}

void main() {
  final shop = ShopApi(_client());
  final catalog = CatalogApi(_client());

  for (final entry in <String, Widget Function(BuildContext)>{
    'new product': (_) =>
        ProductDialog(shop: shop, brands: const [], categories: const []),
    'new service': (_) => ServiceDialog(catalog: catalog),
    'new employee': (_) => EmployeeDialog(catalog: catalog),
  }.entries) {
    testWidgets(
      'the ${entry.key} form has a close button (X) in its title that closes it',
      (tester) async {
        await _open(tester, entry.value);
        expect(find.byKey(const Key('dialog_close')), findsOneWidget);
        expect(find.byType(AlertDialog), findsOneWidget);

        await tester.tap(find.byKey(const Key('dialog_close')));
        await tester.pumpAndSettle();

        expect(find.byType(AlertDialog), findsNothing);
      },
    );
  }
}
