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

Map<String, dynamic> _entry(int id, String text, String kind) => {
  'item': {
    'id': id,
    'name': 'Vosak $id',
    'price': 10,
    'stock': 3,
    'isLowStock': false,
    'isActive': true,
    'images': [],
  },
  'reason': {'kind': kind, 'sourceName': 'Ulje', 'text': text},
};

RecommendationsApi _api(_Server s) => RecommendationsApi(
  ApiClient(
    baseUrl: 'http://test',
    dio: Dio()..httpClientAdapter = s,
    readToken: () async => 't',
  ),
);

void main() {
  test(
    'every suggestion arrives with the reason the server gave for it',
    () async {
      final s = _Server([
        _entry(
          1,
          'Jer ste kupili „Ulje“ - ista kategorija (Voskovi)',
          'SimilarToInterest',
        ),
        _entry(2, 'Popularno u salonu.', 'Popular'),
      ]);

      final list = await _api(s).products(count: 2);

      expect(s.last!.path, '/api/v1/recommendations/products');
      expect(s.last!.queryParameters, {'count': 2});
      expect(list.map((r) => r.item.id), [1, 2]);
      expect(list.first.reason, contains('Jer ste kupili'));
      expect(list.first.kind, 'SimilarToInterest');
      expect(list.last.kind, 'Popular');
    },
  );

  test(
    'similar products use the related endpoint and keep their reason',
    () async {
      final s = _Server([
        _entry(5, 'Slično kao „Vosak“ - isti brend (Dandy)', 'SimilarToItem'),
      ]);

      final list = await _api(s).relatedProducts(9, count: 3);

      expect(s.last!.path, '/api/v1/products/9/related');
      expect(list.single.reason, startsWith('Slično kao'));
    },
  );
}
