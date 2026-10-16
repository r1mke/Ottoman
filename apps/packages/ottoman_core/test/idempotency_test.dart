import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';

class _Server implements HttpClientAdapter {
  RequestOptions? last;

  @override
  Future<ResponseBody> fetch(
    RequestOptions o,
    Stream<Uint8List>? s,
    Future<void>? c,
  ) async {
    last = o;
    return ResponseBody.fromString(
      jsonEncode({}),
      200,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

ApiClient _client(_Server s) => ApiClient(
  baseUrl: 'http://test',
  dio: Dio()..httpClientAdapter = s,
  readToken: () async => 't',
);

void main() {
  test(
    'a key becomes the Idempotency-Key header, and no key sends none',
    () async {
      final s = _Server();
      final api = _client(s);

      await api.post<dynamic>('/x', body: {}, idempotencyKey: 'abc123');
      expect(s.last!.headers['Idempotency-Key'], 'abc123');

      await api.post<dynamic>('/x', body: {});
      expect(s.last!.headers.containsKey('Idempotency-Key'), isFalse);
    },
  );

  test('generated keys are 32 hex characters, accepted by the server rules, and never repeat', () {
    final keys = {for (var i = 0; i < 200; i++) newIdempotencyKey()};

    expect(keys, hasLength(200));
    for (final k in keys) {
      expect(k, matches(RegExp(r'^[0-9a-f]{32}$')));
    }
  });

  test('creating an order or a booking forwards the key', () async {
    final s = _Server();
    final api = _client(s);

    try {
      await ShopApi(api).createOrder(
        quantities: {1: 2},
        fulfilment: 'Pickup',
        paymentMethod: 'OnReceipt',
        idempotencyKey: 'order-key',
      );
    } on Object {
      // the empty fake answer cannot be parsed; only the request matters here
    }
    expect(s.last!.path, '/api/v1/orders');
    expect(s.last!.headers['Idempotency-Key'], 'order-key');

    try {
      await ReservationsApi(api).create(
        employeeId: 'e1',
        serviceIds: [1],
        startUtc: DateTime.utc(2030, 1, 7, 9),
        idempotencyKey: 'booking-key',
      );
    } on Object {
      // same: the request is what is being checked
    }
    expect(s.last!.path, '/api/v1/reservations');
    expect(s.last!.headers['Idempotency-Key'], 'booking-key');
  });
}
