import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';
import 'package:ottoman_desktop/features/employees/employee_dialogs.dart';

class _Server implements HttpClientAdapter {
  final requests = <RequestOptions>[];
  final titles = <Map<String, dynamic>>[
    {'id': 1, 'name': 'Barber', 'inUse': 1},
    {'id': 2, 'name': 'Junior Barber', 'inUse': 0},
    {'id': 3, 'name': 'Master Barber', 'inUse': 2},
  ];
  int nextId = 10;
  Map<String, dynamic>? refuse; // problem details for the next POST

  Iterable<RequestOptions> sent(String method, String part) =>
      requests.where((r) => r.method == method && r.path.contains(part));

  @override
  Future<ResponseBody> fetch(
    RequestOptions o,
    Stream<Uint8List>? s,
    Future<void>? c,
  ) async {
    requests.add(o);
    Object? body;
    var status = 200;
    if (o.path.endsWith('/employee-titles') && o.method == 'GET') {
      body = titles;
    } else if (o.path.endsWith('/employee-titles') && o.method == 'POST') {
      if (refuse != null) {
        status = 409;
        body = refuse;
        refuse = null;
      } else {
        final row = {
          'id': nextId++,
          'name': (o.data as Map)['name'],
          'inUse': 0,
        };
        titles.add(row);
        status = 201;
        body = row;
      }
    } else if (o.method == 'DELETE') {
      titles.removeWhere((t) => o.path.endsWith('/${t['id']}'));
      status = 204;
    } else if (o.method == 'PUT' && o.path.contains('/employee-titles/')) {
      final row = titles.firstWhere((t) => o.path.endsWith('/${t['id']}'));
      row['name'] = (o.data as Map)['name'];
      body = row;
    } else if (o.method == 'PUT') {
      body = {
        'id': 'e1',
        'firstName': 'Kerim',
        'lastName': 'Begić',
        'fullName': 'Kerim Begić',
        'title': 'Barber',
        'titleId': (o.data as Map)['titleId'],
        'isActive': true,
        'serviceIds': <int>[],
      };
    } else {
      status = 204;
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

Future<_Server> _open(
  WidgetTester tester, {
  String title = 'Barber',
  int titleId = 1,
}) async {
  tester.view.devicePixelRatio = 1.0;
  tester.view.physicalSize = const Size(1400, 1000);
  addTearDown(tester.view.reset);
  final server = _Server();
  final catalog = CatalogApi(
    ApiClient(
      baseUrl: 'http://test',
      dio: Dio()..httpClientAdapter = server,
      readToken: () async => 'token',
    ),
  );
  final employee = Employee.fromJson({
    'id': 'e1',
    'firstName': 'Kerim',
    'lastName': 'Begić',
    'title': title,
    'titleId': titleId,
    'isActive': true,
    'serviceIds': <int>[],
  });
  await tester.pumpWidget(
    MaterialApp(
      theme: WorkspaceTheme.build(),
      locale: const Locale('bs'),
      localizationsDelegates: AppLocalizations.localizationsDelegates,
      supportedLocales: AppLocalizations.supportedLocales,
      home: Scaffold(
        body: Builder(
          builder: (context) => TextButton(
            onPressed: () => showDialog<void>(
              context: context,
              builder: (_) =>
                  EmployeeDialog(catalog: catalog, employee: employee),
            ),
            child: const Text('open'),
          ),
        ),
      ),
    ),
  );
  await tester.tap(find.text('open'));
  await tester.pumpAndSettle();
  return server;
}

Future<void> _openDropdown(WidgetTester tester) async {
  await tester.ensureVisible(find.byKey(const Key('emp_title')));
  await tester.tap(find.byKey(const Key('emp_title')));
  await tester.pumpAndSettle();
}

void main() {
  genderTests();
  testWidgets('the title is picked from the list the server holds', (
    tester,
  ) async {
    final server = await _open(tester);

    expect(server.sent('GET', 'employee-titles'), hasLength(1));
    // The current title is shown as selected.
    expect(
      find.descendant(
        of: find.byKey(const Key('emp_title')),
        matching: find.text('Barber'),
      ),
      findsOneWidget,
    );

    await _openDropdown(tester);
    expect(find.text('Junior Barber'), findsOneWidget);
    expect(find.text('Master Barber'), findsOneWidget);
    expect(find.text('Uredi listu titula'), findsOneWidget);

    await tester.tap(find.text('Master Barber').last);
    await tester.pumpAndSettle();

    await tester.ensureVisible(find.text('Spremi'));
    await tester.tap(find.text('Spremi'));
    await tester.pumpAndSettle();

    final saved = server.sent('PUT', '/employees/e1').single;
    expect((saved.data as Map)['titleId'], 3);
    expect((saved.data as Map).containsKey('title'), isFalse);
  });

  testWidgets(
    'a title that is not on the list is not offered: one must be picked',
    (tester) async {
      final server = await _open(tester, title: 'Legacy majstor', titleId: 99);

      expect(find.text('Legacy majstor'), findsNothing);
      await tester.ensureVisible(find.text('Spremi'));
      await tester.tap(find.text('Spremi'));
      await tester.pumpAndSettle();
      expect(server.sent('PUT', '/employees/e1'), isEmpty);
      expect(find.text('Obavezno polje'), findsWidgets);
    },
  );

  testWidgets('a new title is added from the form and gets selected', (
    tester,
  ) async {
    final server = await _open(tester);
    await _openDropdown(tester);
    await tester.tap(find.text('Uredi listu titula'));
    await tester.pumpAndSettle();

    await tester.enterText(find.byKey(const Key('title_new')), '  Stilist  ');
    await tester.tap(find.byKey(const Key('title_add')));
    await tester.pumpAndSettle();

    expect(
      (server.sent('POST', 'employee-titles').single.data as Map)['name'],
      '  Stilist  '.trim(),
    );
    expect(
      find.text('Stilist'),
      findsOneWidget,
    ); // now in the list of the editor

    await tester.tap(find.byKey(const Key('titles_done')));
    await tester.pumpAndSettle();

    expect(
      find.descendant(
        of: find.byKey(const Key('emp_title')),
        matching: find.text('Stilist'),
      ),
      findsOneWidget,
    );
  });

  testWidgets(
    'a refused title shows the reason; used titles cannot be deleted',
    (tester) async {
      final server = await _open(tester);
      await _openDropdown(tester);
      await tester.tap(find.text('Uredi listu titula'));
      await tester.pumpAndSettle();

      server.refuse = {
        'title': 'Sukob podataka.',
        'detail': 'Ta titula već postoji na listi.',
        'status': 409,
      };
      await tester.enterText(find.byKey(const Key('title_new')), 'barber');
      await tester.tap(find.byKey(const Key('title_add')));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('title_error')), findsOneWidget);
      expect(find.textContaining('već postoji'), findsOneWidget);

      // Title 1 and 3 are in use: their delete buttons are disabled. Title 2 is free.
      IconButton button(int id) =>
          tester.widget<IconButton>(find.byKey(Key('title_delete_$id')));
      expect(button(1).onPressed, isNull);
      expect(button(3).onPressed, isNull);
      expect(button(2).onPressed, isNotNull);
      expect(find.text('Koristi: 2'), findsOneWidget);

      await tester.tap(find.byKey(const Key('title_delete_2')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('confirm_action')));
      await tester.pumpAndSettle();
      expect(server.sent('DELETE', '/employee-titles/2'), hasLength(1));
      expect(find.text('Junior Barber'), findsNothing);
    },
  );

  testWidgets('a title is renamed from the list, even one that is in use', (
    tester,
  ) async {
    final server = await _open(tester);
    await _openDropdown(tester);
    await tester.tap(find.text('Uredi listu titula'));
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('title_edit_3')));
    await tester.pumpAndSettle();
    await tester.enterText(
      find.byKey(const Key('title_rename_field')),
      '  Glavni barber ',
    );
    await tester.tap(find.byKey(const Key('title_rename_save')));
    await tester.pumpAndSettle();

    final put = server.sent('PUT', '/employee-titles/3').single;
    expect((put.data as Map)['name'], 'Glavni barber');
    expect(find.text('Glavni barber'), findsOneWidget);
    expect(find.text('Master Barber'), findsNothing);
  });
}

void genderTests() {
  testWidgets('a barber has a gender, shown in the form and sent on save', (
    tester,
  ) async {
    final server = await _open(tester);

    final field = find.byKey(const Key('emp_gender'));
    await tester.ensureVisible(field);
    expect(field, findsOneWidget);
    await tester.tap(field);
    await tester.pumpAndSettle();
    await tester.tap(find.text('Ženski').last);
    await tester.pumpAndSettle();

    await tester.ensureVisible(find.text('Spremi'));
    await tester.tap(find.text('Spremi'));
    await tester.pumpAndSettle();

    final saved = server.sent('PUT', '/employees/e1').single;
    expect((saved.data as Map)['gender'], 'Female');
  });

  testWidgets(
    'editing never asks for a password; Izmijeni lozinku sets one without the old',
    (tester) async {
      final server = await _open(tester);

      // No password field in the edit form, and saving works without one.
      expect(find.byKey(const Key('emp_password')), findsNothing);

      final open = find.byKey(const Key('emp_change_password'));
      await tester.ensureVisible(open);
      await tester.tap(open);
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('pw_current')), findsNothing);

      // Empty fields are validated only inside this dialog, and only when it is saved.
      await tester.tap(find.byKey(const Key('setpw_save')));
      await tester.pumpAndSettle();
      expect(server.sent('POST', '/users/e1/password'), isEmpty);

      await tester.enterText(
        find.byKey(const Key('setpw_new')),
        'Nova_Lozinka1!',
      );
      await tester.enterText(
        find.byKey(const Key('setpw_repeat')),
        'Nova_Lozinka1!',
      );
      await tester.tap(find.byKey(const Key('setpw_save')));
      await tester.pumpAndSettle();

      final sent = server.sent('POST', '/users/e1/password').single;
      expect((sent.data as Map)['newPassword'], 'Nova_Lozinka1!');
      expect(find.byKey(const Key('emp_save')), findsOneWidget);
      expect(server.sent('PUT', '/employees/e1'), isEmpty);
    },
  );
}
