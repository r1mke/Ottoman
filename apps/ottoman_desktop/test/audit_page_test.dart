import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';
import 'package:ottoman_desktop/features/audit/audit_page.dart';

Map<String, dynamic> _entry(
  int id,
  String action, {
  String target = 'id=7',
  String? label,
}) => {
  'id': id,
  'atUtc': '2030-02-01T09:00:00Z',
  'userId': 'a1',
  'userName': 'Admin Ottoman',
  'role': 'Admin',
  'method': 'POST',
  'action': action,
  'route': 'api/v1/products/{id}/stock',
  'target': target,
  'statusCode': 200,
  'ip': '127.0.0.1',
  'targetLabel': label,
};

class _Api implements HttpClientAdapter {
  final requests = <RequestOptions>[];
  int total = 2;

  @override
  Future<ResponseBody> fetch(
    RequestOptions o,
    Stream<Uint8List>? s,
    Future<void>? c,
  ) async {
    requests.add(o);
    return ResponseBody.fromString(
      jsonEncode({
        'items': [
          _entry(2, 'Products.AdjustStock', label: 'Proizvod: Vosak'),
          _entry(1, 'Clients.Block', target: 'id=c1'),
        ],
        'totalCount': total,
      }),
      200,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

Future<_Api> _open(WidgetTester tester, {int total = 2}) async {
  tester.view.devicePixelRatio = 1.0;
  tester.view.physicalSize = const Size(1400, 900);
  addTearDown(tester.view.reset);
  final api = _Api()..total = total;
  await tester.pumpWidget(
    MaterialApp(
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
        body: AuditPage(
          api: AuditApi(
            ApiClient(
              baseUrl: 'http://test',
              dio: Dio()..httpClientAdapter = api,
              readToken: () async => 't',
            ),
          ),
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
  return api;
}

void main() {
  testWidgets('shows who did what, newest first, with readable area names', (
    tester,
  ) async {
    await _open(tester);

    expect(find.byKey(const Key('audit_who_2')), findsOneWidget);
    expect(find.textContaining('Admin Ottoman (Admin)'), findsNWidgets(2));
    expect(find.text('Proizvodi · AdjustStock'), findsOneWidget);
    expect(find.text('Klijenti · Block'), findsOneWidget);
    // The target is read in words; a raw id is never shown, even when the server could not name it.
    expect(find.text('Proizvod: Vosak'), findsOneWidget);
    expect(find.byKey(const Key('audit_target_1')), findsOneWidget);
    expect(find.textContaining('id='), findsNothing);
    expect(find.textContaining('c1'), findsNothing);
  });

  testWidgets('searching sends the term from the first page', (tester) async {
    final api = await _open(tester, total: 100);
    await tester.tap(find.byKey(const Key('audit_next')));
    await tester.pumpAndSettle();
    expect(api.requests.last.queryParameters['page'], 2);

    await tester.enterText(find.byKey(const Key('audit_search')), 'Clients');
    await tester.testTextInput.receiveAction(TextInputAction.search);
    await tester.pumpAndSettle();

    expect(api.requests.last.queryParameters['search'], 'Clients');
    expect(api.requests.last.queryParameters['page'], 1);
  });

  testWidgets('there is no way to change an entry from this page', (
    tester,
  ) async {
    await _open(tester);

    expect(find.byIcon(Icons.delete_outline), findsNothing);
    expect(find.byIcon(Icons.edit_outlined), findsNothing);
    expect(find.byType(Checkbox), findsNothing);
  });

  test('unknown areas keep their code name and the action is split', () {
    expect(auditAreaLabel('Products'), 'Proizvodi');
    expect(auditAreaLabel('Mystery'), 'Mystery');
    final e = AuditEntry.fromJson(_entry(1, 'Reservations.Complete'));
    expect(e.controller, 'Reservations');
    expect(e.verb, 'Complete');
  });
}
