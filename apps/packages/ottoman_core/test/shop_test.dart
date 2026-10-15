import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';

class _Adapter implements HttpClientAdapter {
  _Adapter(this.body, {this.status = 200});
  final Object body;
  final int status;
  RequestOptions? last;
  Object? lastBody;

  @override
  Future<ResponseBody> fetch(
    RequestOptions o,
    Stream<Uint8List>? s,
    Future<void>? c,
  ) async {
    last = o;
    lastBody = o.data;
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

ShopApi _api(_Adapter a) => ShopApi(
  ApiClient(
    baseUrl: 'http://test',
    dio: Dio()..httpClientAdapter = a,
    readToken: () async => 't',
  ),
);

Map<String, dynamic> _product({int id = 1, int stock = 5, double price = 10}) =>
    {
      'id': id,
      'name': 'Vosak',
      'description': null,
      'brandId': null,
      'brandName': null,
      'categoryId': null,
      'categoryName': null,
      'price': price,
      'stock': stock,
      'lowStockThreshold': 2,
      'isLowStock': stock <= 2,
      'isActive': true,
    };

Product _p({int id = 1, int stock = 5, double price = 10}) =>
    Product.fromJson(_product(id: id, stock: stock, price: price));

void main() {
  group('CartController', () {
    test('adds, counts and totals', () {
      final cart = CartController();
      final a = _p(id: 1, price: 10);
      final b = _p(id: 2, price: 2.5);

      expect(cart.isEmpty, isTrue);
      cart
        ..add(a)
        ..add(a)
        ..add(b);

      expect(cart.itemCount, 3);
      expect(cart.total, 22.5);
      expect(cart.quantityOf(1), 2);
      expect(cart.toQuantities(), {1: 2, 2: 1});
    });

    test('never exceeds the stock', () {
      final cart = CartController();
      final p = _p(stock: 2);

      expect(cart.add(p), isTrue);
      expect(cart.add(p), isTrue);
      expect(cart.add(p), isFalse);
      expect(cart.quantityOf(1), 2);
    });

    test('never exceeds the per-line cap even with plenty of stock', () {
      final cart = CartController();
      final p = _p(stock: 500);
      for (var i = 0; i < 30; i++) {
        cart.add(p);
      }
      expect(cart.quantityOf(1), CartController.maxPerLine);
    });

    test('an out-of-stock product cannot be added', () {
      expect(CartController().add(_p(stock: 0)), isFalse);
    });

    test('setQuantity clamps, and zero removes the line', () {
      final cart = CartController();
      final p = _p(stock: 4);
      cart.setQuantity(p, 99);
      expect(cart.quantityOf(1), 4);
      cart.setQuantity(p, 0);
      expect(cart.isEmpty, isTrue);
    });

    test('notifies listeners only on real changes', () {
      final cart = CartController();
      var calls = 0;
      cart.addListener(() => calls++);
      final p = _p();

      cart.add(p);
      cart.remove(99); // nothing there
      cart.clear();
      cart.clear(); // already empty

      expect(calls, 2);
    });
  });

  group('ShopApi', () {
    test(
      'products sends search, filters, sort and paging; parses the page',
      () async {
        final adapter = _Adapter({
          'items': [_product()],
          'totalCount': 7,
        });

        final page = await _api(adapter).products(
          search: '  vosak ',
          categoryId: 3,
          sort: 'PriceDesc',
          page: 2,
          pageSize: 10,
        );

        final uri = adapter.last!.uri;
        expect(uri.path, '/api/v1/products');
        expect(uri.queryParameters['search'], 'vosak');
        expect(uri.queryParameters['categoryId'], '3');
        expect(uri.queryParameters['sort'], 'PriceDesc');
        expect(uri.queryParameters['page'], '2');
        expect(uri.queryParameters.containsKey('brandId'), isFalse);
        expect(page.totalCount, 7);
        expect(page.items.single.name, 'Vosak');
      },
    );

    test('createOrder sends ids and quantities only - never a price', () async {
      final adapter = _Adapter({
        'id': 5,
        'clientId': 'c',
        'status': 'PendingPayment',
        'fulfilment': 'Pickup',
        'paymentMethod': 'Card',
        'paymentStatus': 'Pending',
        'totalPrice': 20,
        'createdAtUtc': '2030-01-01T10:00:00Z',
        'expiresUtc': '2030-01-01T10:30:00Z',
        'items': [
          {
            'productId': 1,
            'productName': 'Vosak',
            'unitPrice': 10,
            'quantity': 2,
            'lineTotal': 20,
          },
        ],
        'paymentClientSecret': 'pi_x_secret',
      }, status: 201);

      final order = await _api(adapter).createOrder(
        quantities: {1: 2},
        fulfilment: FulfilmentType.pickup,
        paymentMethod: PaymentMethod.card,
      );

      final sent = adapter.lastBody as Map<String, dynamic>;
      expect(sent['items'], [
        {'productId': 1, 'quantity': 2},
      ]);
      expect(sent.keys, isNot(contains('totalPrice')));
      expect(sent.keys, isNot(contains('price')));
      expect(order.awaitsPayment, isTrue);
      expect(order.paymentClientSecret, 'pi_x_secret');
      expect(order.clientCanCancel, isTrue);
      expect(order.itemSummary, '2× Vosak');
    });

    test('adjustStock posts the delta', () async {
      final adapter = _Adapter(_product(stock: 8));
      final p = await _api(adapter).adjustStock(1, 3);
      expect(adapter.last!.path, '/api/v1/products/1/stock');
      expect((adapter.lastBody as Map)['delta'], 3);
      expect(p.stock, 8);
    });
  });

  group('ShopOrder rules', () {
    ShopOrder order(String status, String method) => ShopOrder.fromJson({
      'id': 1,
      'clientId': 'c',
      'status': status,
      'fulfilment': 'Pickup',
      'paymentMethod': method,
      'totalPrice': 1,
      'createdAtUtc': '2030-01-01T10:00:00Z',
      'items': [],
    });

    test('a client may withdraw only unpaid orders', () {
      expect(order('PendingPayment', 'Card').clientCanCancel, isTrue);
      expect(order('Placed', 'OnReceipt').clientCanCancel, isTrue);
      expect(order('Placed', 'Card').clientCanCancel, isFalse);
      expect(order('Fulfilled', 'OnReceipt').clientCanCancel, isFalse);
      expect(order('Cancelled', 'Card').clientCanCancel, isFalse);
    });
  });
}
