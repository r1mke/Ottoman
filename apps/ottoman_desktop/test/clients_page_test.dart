import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';
import 'package:ottoman_desktop/features/clients/clients_page.dart';

Map<String, dynamic> _client(
  String id,
  String name, {
  bool blocked = false,
  bool locked = false,
}) => {
  'id': id,
  'fullName': name,
  'email': '$id@x.ba',
  'phone': '061 111 222',
  'registeredAtUtc': '2030-01-01T10:00:00Z',
  'isBlocked': blocked,
  // Ana chose to zoom her picture: the server sends it already cropped.
  if (id == 'c1') 'photoUrl': _anaPhoto,
  if (id == 'c1') 'photoZoom': 2.0,
  'lockedUntilUtc': locked ? '2030-01-01T10:15:00Z' : null,
};

const _anaPhoto =
    'https://res.cloudinary.com/demo/image/upload/c_thumb,g_face,w_512,h_512,z_0.5,q_auto,f_auto/ottoman/accounts/ana';

class _Api implements HttpClientAdapter {
  final requests = <RequestOptions>[];
  bool blocked = false;
  bool locked = false;
  int failBlockWith = 0;
  int total = 2;

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
    final p = o.path;
    if (p == '/api/v1/users/c1/unlock') {
      locked = false;
      body = {'id': 'c1', 'lockedUntilUtc': null};
    } else if (p.endsWith('/block') || p.endsWith('/unblock')) {
      if (failBlockWith != 0) {
        status = failBlockWith;
        body = {'status': status, 'title': 'Nije pronađeno.'};
      } else {
        blocked = p.endsWith('/block');
        body = _client('c1', 'Ana Anić', blocked: blocked);
      }
    } else if (p == '/api/v1/clients/c1') {
      body = {
        ..._client('c1', 'Ana Anić', blocked: blocked, locked: locked),
        'stats': {
          'visits': 3,
          'noShows': 1,
          'cancelled': 0,
          'upcoming': 2,
          'servicesSpent': 75,
          'orders': 1,
          'productsSpent': 12.5,
          'lastVisitUtc': '2030-02-01T09:00:00Z',
        },
        'reservations': [
          {
            'id': 5,
            'startUtc': '2030-02-01T09:00:00Z',
            'status': 'Completed',
            'employeeName': 'Kerim Begić',
            'services': 'Šišanje',
            'total': 25,
          },
        ],
        'orders': [
          {
            'id': 9,
            'createdAtUtc': '2030-02-02T09:00:00Z',
            'status': 'Fulfilled',
            'total': 12.5,
            'items': '1x Vosak',
          },
        ],
      };
    } else {
      body = {
        'items': [
          _client('c1', 'Ana Anić', blocked: blocked, locked: locked),
          _client('c2', 'Bego Begić'),
        ],
        'totalCount': total,
      };
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

Future<_Api> _open(
  WidgetTester tester, {
  bool blocked = false,
  bool locked = false,
  int total = 2,
  double width = 1400,
}) async {
  tester.view.devicePixelRatio = 1.0;
  tester.view.physicalSize = Size(width, 1000);
  addTearDown(tester.view.reset);
  final api = _Api()
    ..blocked = blocked
    ..locked = locked
    ..total = total;
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
        body: ClientsPage(
          api: ClientsApi(
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
  testWidgets('lists the clients', (tester) async {
    await _open(tester);

    expect(find.text('Ana Anić'), findsOneWidget);
    expect(find.text('Bego Begić'), findsOneWidget);
    expect(find.byKey(const Key('blocked_c1')), findsNothing);
    // Every client has an avatar: their own picture, or a drawn one.
    expect(find.byKey(const Key('client_avatar_c1')), findsOneWidget);
    // The list shows the client's own crop (zoom 2 -> z_0.5).
    expect(
      tester
          .widget<PersonAvatar>(find.byKey(const Key('client_avatar_c1')))
          .photoUrl,
      contains('z_0.5,'),
    );
  });

  testWidgets('a blocked client is flagged in the list', (tester) async {
    await _open(tester, blocked: true);

    expect(find.byKey(const Key('blocked_c1')), findsOneWidget);
    expect(find.byKey(const Key('blocked_c2')), findsNothing);
  });

  testWidgets('searching sends the term and starts from the first page', (
    tester,
  ) async {
    final api = await _open(tester);

    await tester.enterText(find.byKey(const Key('client_search')), 'ana');
    await tester.testTextInput.receiveAction(TextInputAction.search);
    await tester.pumpAndSettle();

    final last = api.to('GET', '/api/v1/clients').last;
    expect(last.queryParameters['search'], 'ana');
    expect(last.queryParameters['page'], 1);

    await tester.tap(find.byKey(const Key('client_search_clear')));
    await tester.pumpAndSettle();
    expect(
      api
          .to('GET', '/api/v1/clients')
          .last
          .queryParameters
          .containsKey('search'),
      isFalse,
    );
  });

  testWidgets(
    'opening a client shows the counted numbers and the latest rows',
    (tester) async {
      final api = await _open(tester);

      await tester.tap(find.byKey(const Key('client_cell_c1')));
      await tester.pumpAndSettle();

      expect(api.to('GET', '/api/v1/clients/c1'), hasLength(1));
      expect(find.byKey(const Key('client_name')), findsOneWidget);
      expect(
        tester
            .widget<PersonAvatar>(find.byKey(const Key('client_detail_avatar')))
            .photoUrl,
        _anaPhoto,
      );
      expect(
        tester.widget<Text>(find.byKey(const Key('stat_value_visits'))).data,
        '3',
      );
      expect(
        tester.widget<Text>(find.byKey(const Key('stat_value_noshows'))).data,
        '1',
      );
      expect(find.byKey(const Key('reservation_5')), findsOneWidget);
      expect(find.byKey(const Key('order_9')), findsOneWidget);
    },
  );

  testWidgets('blocking asks first, then blocks and shows the state', (
    tester,
  ) async {
    final api = await _open(tester);
    await tester.tap(find.byKey(const Key('client_cell_c1')));
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('toggle_block')));
    await tester.pumpAndSettle();
    expect(
      api.to('POST', '/block'),
      isEmpty,
      reason: 'nothing happens before the admin confirms',
    );

    await tester.tap(find.byKey(const Key('confirm_block')));
    await tester.pumpAndSettle();

    expect(api.to('POST', '/api/v1/clients/c1/block'), hasLength(1));
    expect(find.byKey(const Key('client_blocked_chip')), findsOneWidget);

    await tester.tap(find.byKey(const Key('toggle_block')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('confirm_block')));
    await tester.pumpAndSettle();

    expect(api.to('POST', '/api/v1/clients/c1/unblock'), hasLength(1));
    expect(find.byKey(const Key('client_blocked_chip')), findsNothing);
  });

  testWidgets('declining the question changes nothing', (tester) async {
    final api = await _open(tester);
    await tester.tap(find.byKey(const Key('client_cell_c1')));
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('toggle_block')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Ne'));
    await tester.pumpAndSettle();

    expect(api.to('POST', '/block'), isEmpty);
  });

  testWidgets('a refused block shows the server message and keeps the dialog', (
    tester,
  ) async {
    final api = await _open(tester);
    api.failBlockWith = 404;
    await tester.tap(find.byKey(const Key('client_cell_c1')));
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('toggle_block')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('confirm_block')));
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('client_error')), findsOneWidget);
    expect(find.byKey(const Key('client_blocked_chip')), findsNothing);
  });

  testWidgets('paging buttons follow the total', (tester) async {
    final api = await _open(tester, total: 45);

    expect(
      tester
          .widget<IconButton>(find.byKey(const Key('clients_next')))
          .onPressed,
      isNotNull,
    );
    expect(
      tester
          .widget<IconButton>(find.byKey(const Key('clients_prev')))
          .onPressed,
      isNull,
    );
    await tester.tap(find.byKey(const Key('clients_next')));
    await tester.pumpAndSettle();
    expect(api.to('GET', '/api/v1/clients').last.queryParameters['page'], 2);
  });

  testWidgets('a single page has no next button', (tester) async {
    await _open(tester);

    expect(
      tester
          .widget<IconButton>(find.byKey(const Key('clients_next')))
          .onPressed,
      isNull,
    );
  });

  testWidgets('a locked client is flagged with an unlock button that asks, '
      'unlocks, says so and refreshes the list', (tester) async {
    final api = await _open(tester, locked: true);
    expect(find.byKey(const Key('locked_c1')), findsOneWidget);
    expect(find.textContaining('Zaključan do'), findsOneWidget);
    // Only the locked client has the button.
    expect(find.byKey(const Key('unlock_c1')), findsOneWidget);
    expect(find.byKey(const Key('unlock_c2')), findsNothing);
    final listLoads = api.to('GET', '/api/v1/clients').length;

    // Saying no changes nothing.
    await tester.tap(find.byKey(const Key('unlock_c1')));
    await tester.pumpAndSettle();
    expect(
      find.textContaining('Otključati račun korisnika Ana Anić'),
      findsOneWidget,
    );
    await tester.tap(find.byKey(const Key('confirm_action_cancel')));
    await tester.pumpAndSettle();
    expect(api.to('POST', '/unlock'), isEmpty);

    await tester.tap(find.byKey(const Key('unlock_c1')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('confirm_action')));
    await tester.pumpAndSettle();

    expect(api.to('POST', '/api/v1/users/c1/unlock'), hasLength(1));
    expect(find.text('Račun je otključan.'), findsOneWidget);
    expect(api.to('GET', '/api/v1/clients').length, greaterThan(listLoads));
    expect(find.byKey(const Key('locked_c1')), findsNothing);
    expect(find.byKey(const Key('unlock_c1')), findsNothing);
  });

  testWidgets('an unlocked client has no lock and no unlock button', (
    tester,
  ) async {
    await _open(tester);
    expect(find.byKey(const Key('locked_c1')), findsNothing);
    expect(find.byKey(const Key('unlock_c1')), findsNothing);
  });

  testWidgets('the client detail shows the lock and unlocks from there', (
    tester,
  ) async {
    final api = await _open(tester, locked: true);
    await tester.tap(find.byKey(const Key('client_cell_c1')));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('client_locked_chip')), findsOneWidget);

    await tester.tap(find.byKey(const Key('unlock_account')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('confirm_action')));
    await tester.pumpAndSettle();

    expect(api.to('POST', '/api/v1/users/c1/unlock'), hasLength(1));
    expect(find.text('Račun je otključan.'), findsOneWidget);
    expect(find.byKey(const Key('client_locked_chip')), findsNothing);
    expect(find.byKey(const Key('unlock_account')), findsNothing);
  });

  testWidgets('a locked and blocked row still fits the smallest window', (
    tester,
  ) async {
    // 1024 px window minus the 248 px sidebar.
    await _open(tester, locked: true, blocked: true, width: 776);
    expect(find.byKey(const Key('unlock_c1')), findsOneWidget);
    expect(tester.takeException(), isNull);
  });
}
