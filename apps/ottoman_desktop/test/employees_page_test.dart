import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';
import 'package:ottoman_desktop/features/employees/employees_page.dart';

Map<String, dynamic> _employee(
  String id,
  String first, {
  bool locked = false,
}) => {
  'id': id,
  'firstName': first,
  'lastName': 'Brijač',
  'fullName': '$first Brijač',
  'title': 'Barber',
  'titleId': 1,
  'isActive': true,
  'serviceIds': <int>[],
  'email': '$id@ottoman.local',
  'lockedUntilUtc': locked ? '2030-01-01T10:15:00Z' : null,
};

/// The admin's employee list, with one barber locked out by wrong passwords until unlocked.
class _Api implements HttpClientAdapter {
  final requests = <RequestOptions>[];
  bool locked = true;
  int failUnlockWith = 0;

  Iterable<RequestOptions> to(String method, String pathEnd) =>
      requests.where((r) => r.method == method && r.path.endsWith(pathEnd));

  @override
  Future<ResponseBody> fetch(
    RequestOptions o,
    Stream<Uint8List>? s,
    Future<void>? c,
  ) async {
    requests.add(o);
    Object? body;
    var status = 200;
    if (o.path == '/api/v1/users/e1/unlock') {
      if (failUnlockWith != 0) {
        status = failUnlockWith;
        body = {'status': status, 'title': 'Nije pronađeno.'};
      } else {
        locked = false;
        body = {'id': 'e1', 'lockedUntilUtc': null};
      }
    } else {
      body = [
        _employee('e1', 'Kerim', locked: locked),
        _employee('e2', 'Tarik'),
      ];
    }
    return ResponseBody.fromString(
      jsonEncode(body),
      status,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

Future<_Api> _open(WidgetTester tester, {int failUnlockWith = 0}) async {
  tester.view.devicePixelRatio = 1.0;
  tester.view.physicalSize = const Size(1200, 900);
  addTearDown(tester.view.reset);
  final api = _Api()..failUnlockWith = failUnlockWith;
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
        body: EmployeesPage(
          catalog: CatalogApi(
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
  testWidgets('only a locked barber shows the lock and the unlock button', (
    tester,
  ) async {
    await _open(tester);

    expect(find.byKey(const Key('employee_locked_e1')), findsOneWidget);
    expect(find.byKey(const Key('employee_unlock_e1')), findsOneWidget);
    expect(find.byKey(const Key('employee_locked_e2')), findsNothing);
    expect(find.byKey(const Key('employee_unlock_e2')), findsNothing);
  });

  testWidgets('unlocking asks first, then unlocks, says so and refreshes', (
    tester,
  ) async {
    final api = await _open(tester);
    final loads = api.to('GET', '/api/v1/employees').length;

    await tester.tap(find.byKey(const Key('employee_unlock_e1')));
    await tester.pumpAndSettle();
    expect(
      find.textContaining('Otključati račun korisnika Kerim Brijač'),
      findsOneWidget,
    );
    await tester.tap(find.byKey(const Key('confirm_action_cancel')));
    await tester.pumpAndSettle();
    expect(api.to('POST', '/unlock'), isEmpty);

    await tester.tap(find.byKey(const Key('employee_unlock_e1')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('confirm_action')));
    await tester.pumpAndSettle();

    expect(api.to('POST', '/api/v1/users/e1/unlock'), hasLength(1));
    expect(find.text('Račun je otključan.'), findsOneWidget);
    expect(api.to('GET', '/api/v1/employees').length, greaterThan(loads));
    expect(find.byKey(const Key('employee_locked_e1')), findsNothing);
    expect(find.byKey(const Key('employee_unlock_e1')), findsNothing);
  });

  testWidgets('a refused unlock shows the server message and keeps the lock', (
    tester,
  ) async {
    final api = await _open(tester, failUnlockWith: 404);

    await tester.tap(find.byKey(const Key('employee_unlock_e1')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('confirm_action')));
    await tester.pumpAndSettle();

    expect(api.to('POST', '/api/v1/users/e1/unlock'), hasLength(1));
    expect(find.text('Račun je otključan.'), findsNothing);
    expect(find.byKey(const Key('employee_locked_e1')), findsOneWidget);
  });
}
