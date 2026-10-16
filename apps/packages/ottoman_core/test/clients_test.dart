import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';

class _Server implements HttpClientAdapter {
  _Server(this.body);
  final Object body;
  RequestOptions? last;

  @override
  Future<ResponseBody> fetch(
    RequestOptions o,
    Stream<Uint8List>? s,
    Future<void>? c,
  ) async {
    last = o;
    return ResponseBody.fromString(
      jsonEncode(body),
      200,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

ClientsApi _api(_Server s) => ClientsApi(
  ApiClient(
    baseUrl: 'http://test',
    dio: Dio()..httpClientAdapter = s,
    readToken: () async => 't',
  ),
);

Map<String, dynamic> _detail({bool blocked = false}) => {
  'id': 'c1',
  'fullName': 'Ana Anić',
  'email': 'ana@x.ba',
  'phone': null,
  'registeredAtUtc': '2030-01-01T10:00:00Z',
  'isBlocked': blocked,
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

void main() {
  test(
    'list sends the search and paging and parses the blocked flag',
    () async {
      final s = _Server({
        'items': [
          {
            'id': 'c1',
            'fullName': 'Ana Anić',
            'email': 'ana@x.ba',
            'phone': null,
            'registeredAtUtc': '2030-01-01T10:00:00Z',
            'isBlocked': true,
          },
        ],
        'totalCount': 41,
      });

      final page = await _api(s).list(search: '  ana ', page: 2, pageSize: 20);

      expect(s.last!.path, '/api/v1/clients');
      expect(s.last!.queryParameters, {
        'search': 'ana',
        'page': 2,
        'pageSize': 20,
      });
      expect(page.totalCount, 41);
      expect(page.items.single.isBlocked, isTrue);
      expect(page.items.single.registeredAtUtc, DateTime.utc(2030, 1, 1, 10));
    },
  );

  test('the older list shape without the new fields still parses', () {
    final c = ClientItem.fromJson({
      'id': 'c1',
      'fullName': 'A B',
      'email': 'a@b.c',
    });
    expect(c.isBlocked, isFalse);
    expect(c.registeredAtUtc, isNull);
  });

  test(
    'detail parses the server counted numbers and the latest rows',
    () async {
      final s = _Server(_detail());

      final d = await _api(s).detail('c1');

      expect(s.last!.path, '/api/v1/clients/c1');
      expect(d.stats.visits, 3);
      expect(d.stats.totalSpent, 87.5);
      expect(d.stats.lastVisitUtc, DateTime.utc(2030, 2, 1, 9));
      expect(d.reservations.single.employeeName, 'Kerim Begić');
      expect(d.orders.single.items, '1x Vosak');
    },
  );

  test('block and unblock use their own endpoints', () async {
    final s = _Server({
      'id': 'c1',
      'fullName': 'A B',
      'email': 'a@b.c',
      'isBlocked': true,
    });
    final api = _api(s);

    await api.setBlocked('c1', blocked: true);
    expect(s.last!.method, 'POST');
    expect(s.last!.path, '/api/v1/clients/c1/block');

    await api.setBlocked('c1', blocked: false);
    expect(s.last!.path, '/api/v1/clients/c1/unblock');
  });
}
