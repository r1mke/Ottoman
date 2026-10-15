import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';
import 'package:ottoman_mobile/app.dart';

Map<String, dynamic> _product(int id, String name, double price, int stock) => {
  'id': id,
  'name': name,
  'description': 'Opis $name',
  'brandId': 1,
  'brandName': 'Dandy',
  'categoryId': 1,
  'categoryName': 'Voskovi',
  'price': price,
  'stock': stock,
  'lowStockThreshold': 2,
  'isLowStock': stock <= 2,
  'isActive': true,
};

Map<String, dynamic> _order({
  int id = 7,
  String status = 'Placed',
  String method = 'OnReceipt',
}) => {
  'id': id,
  'clientId': 'c1',
  'clientName': null,
  'status': status,
  'fulfilment': 'Pickup',
  'paymentMethod': method,
  'paymentStatus': null,
  'deliveryAddress': null,
  'note': null,
  'totalPrice': 22.5,
  'createdAtUtc': DateTime.utc(2030, 1, 7, 10).toIso8601String(),
  'expiresUtc': null,
  'items': [
    {
      'productId': 1,
      'productName': 'Vosak',
      'unitPrice': 10,
      'quantity': 2,
      'lineTotal': 20,
    },
  ],
  'paymentClientSecret': null,
};

class _Api implements HttpClientAdapter {
  final requests = <RequestOptions>[];
  int createStatus = 201;
  Map<String, dynamic>? createError;
  List<Map<String, dynamic>> orders = [];

  RequestOptions? last(String method, String part) => requests.reversed
      .where((r) => r.method == method && r.path.contains(part))
      .firstOrNull;

  @override
  Future<ResponseBody> fetch(
    RequestOptions o,
    Stream<Uint8List>? s,
    Future<void>? c,
  ) async {
    requests.add(o);
    Object? body;
    var status = 200;
    final path = o.path;
    if (path.endsWith('/login')) {
      body = {
        'accessToken': 'a',
        'refreshToken': 'r',
        'accessTokenExpiresUtc': DateTime.utc(2030).toIso8601String(),
        'user': {
          'id': 'c1',
          'email': 'x@y.z',
          'firstName': 'Ana',
          'lastName': 'Anić',
          'phone': null,
          'roles': ['Client'],
        },
      };
    } else if (path.endsWith('/employees') || path.endsWith('/services')) {
      body = [];
    } else if (path.endsWith('/categories')) {
      body = [
        {'id': 1, 'name': 'Voskovi', 'description': null},
      ];
    } else if (path.endsWith('/products')) {
      body = {
        'items': [
          _product(1, 'Vosak', 10, 5),
          _product(2, 'Ulje za bradu', 2.5, 1),
          _product(3, 'Nema ga', 8, 0),
        ],
        'totalCount': 3,
      };
    } else if (path.endsWith('/orders') && o.method == 'POST') {
      status = createStatus;
      body = createError ?? _order();
    } else if (path.endsWith('/orders')) {
      body = {'items': orders, 'totalCount': orders.length};
    } else if (path.endsWith('/cancel')) {
      orders = [_order(status: 'Cancelled')];
      body = orders.first;
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

Future<_Api> _openShop(WidgetTester tester) async {
  tester.view.devicePixelRatio = 1.0;
  tester.view.physicalSize = const Size(420, 900);
  addTearDown(tester.view.reset);

  final api = _Api();
  late final AuthController auth;
  auth = AuthController(
    baseUrl: 'http://test',
    storage: MemoryTokenStorage(),
    api: ApiClient(
      baseUrl: 'http://test',
      dio: Dio()..httpClientAdapter = api,
      readToken: () async => auth.accessToken,
    ),
  );
  await auth.restore();
  await tester.pumpWidget(OttomanMobileApp(auth: auth));
  await tester.pumpAndSettle();
  await tester.enterText(find.byKey(const Key('login_email')), 'x@y.z');
  await tester.enterText(find.byKey(const Key('login_password')), 'pw');
  await tester.tap(find.byKey(const Key('login_submit')));
  await tester.pumpAndSettle();
  await tester.tap(find.text('Proizvodi').last);
  await tester.pumpAndSettle();
  return api;
}

void main() {
  testWidgets('the shop lists products and flags what is out of stock', (
    tester,
  ) async {
    await _openShop(tester);

    expect(find.text('Vosak'), findsOneWidget);
    expect(find.text('10.00 KM'), findsOneWidget);
    expect(find.text('Nema na zalihi'), findsOneWidget);
    expect(find.text('Na zalihi: 1'), findsOneWidget); // low stock hint
    // The out-of-stock product cannot be added.
    final add = tester.widget<IconButton>(find.byKey(const Key('add_3')));
    expect(add.onPressed, isNull);
  });

  testWidgets('searching and picking a category are sent to the server', (
    tester,
  ) async {
    final api = await _openShop(tester);

    await tester.enterText(find.byKey(const Key('product_search')), 'vosak');
    await tester.testTextInput.receiveAction(TextInputAction.search);
    await tester.pumpAndSettle();
    expect(api.last('GET', '/products')!.queryParameters['search'], 'vosak');

    await tester.tap(find.byKey(const Key('category_1')));
    await tester.pumpAndSettle();
    expect(api.last('GET', '/products')!.queryParameters['categoryId'], 1);
  });

  testWidgets(
    'cart: the badge counts pieces, stock caps the quantity, total is shown',
    (tester) async {
      await _openShop(tester);

      await tester.tap(find.byKey(const Key('add_1')));
      await tester.tap(find.byKey(const Key('add_1')));
      await tester.tap(find.byKey(const Key('add_2')));
      await tester.pump();
      // Ulje has stock 1: the second add must be refused.
      await tester.tap(find.byKey(const Key('add_2')));
      await tester.pump();
      expect(find.text('Nema više komada na zalihi.'), findsOneWidget);

      expect(find.text('3'), findsOneWidget); // badge: 2 x Vosak + 1 x Ulje

      await tester.tap(find.byKey(const Key('open_cart')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('cart_line_1')), findsOneWidget);
      expect(find.byKey(const Key('cart_total')), findsOneWidget);
      expect(find.text('22.50 KM'), findsOneWidget); // 2 x 10 + 2.5

      await tester.tap(find.byKey(const Key('dec_1')));
      await tester.pump();
      expect(find.text('12.50 KM'), findsOneWidget);
    },
  );

  testWidgets('checkout sends only ids and quantities and clears the cart', (
    tester,
  ) async {
    final api = await _openShop(tester);
    await tester.tap(find.byKey(const Key('add_1')));
    await tester.tap(find.byKey(const Key('add_1')));
    await tester.tap(find.byKey(const Key('open_cart')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('go_checkout')));
    await tester.pumpAndSettle();

    // Card and delivery are unavailable without a payment SDK.
    expect(
      tester
          .widget<RadioListTile<String>>(find.byKey(const Key('pay_card')))
          .enabled,
      isFalse,
    );
    expect(
      find.text(
        'Plaćanje karticom trenutno nije dostupno u ovoj aplikaciji. Odaberite plaćanje pri preuzimanju.',
      ),
      findsOneWidget,
    );

    await tester.tap(find.byKey(const Key('place_order')));
    await tester.pumpAndSettle();

    final sent = api.last('POST', '/orders')!.data as Map<String, dynamic>;
    expect(sent['items'], [
      {'productId': 1, 'quantity': 2},
    ]);
    expect(sent['paymentMethod'], 'OnReceipt');
    expect(sent['fulfilment'], 'Pickup');
    expect(sent.keys, isNot(contains('totalPrice')));

    expect(find.text('Narudžba je zaprimljena.'), findsOneWidget);
    // Back on the shop with an empty basket (no badge).
    expect(find.byKey(const Key('product_search')), findsOneWidget);
    expect(find.byType(Badge), findsOneWidget);
    expect(tester.widget<Badge>(find.byType(Badge)).isLabelVisible, isFalse);
  });

  testWidgets(
    'a conflict from the server (stock gone) is shown and the cart is kept',
    (tester) async {
      final api = await _openShop(tester);
      api
        ..createStatus = 409
        ..createError = {
          'status': 409,
          'title': 'Sukob podataka.',
          'detail': 'Vosak nema dovoljno na zalihi.',
        };
      await tester.tap(find.byKey(const Key('add_1')));
      await tester.tap(find.byKey(const Key('open_cart')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('go_checkout')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('place_order')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('checkout_error')), findsOneWidget);
      expect(find.textContaining('nema dovoljno na zalihi'), findsOneWidget);
      expect(
        find.byKey(const Key('place_order')),
        findsOneWidget,
      ); // still on checkout
    },
  );

  testWidgets(
    'my orders lists them and lets the client withdraw an unpaid one',
    (tester) async {
      final api = await _openShop(tester);
      api.orders = [_order()];

      await tester.tap(find.byKey(const Key('open_orders')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('order_7')), findsOneWidget);
      expect(find.text('Zaprimljena'), findsOneWidget);
      expect(find.text('2× Vosak'), findsOneWidget);

      await tester.tap(find.byKey(const Key('cancel_order_7')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('confirm_cancel_order')));
      await tester.pumpAndSettle();

      expect(api.last('POST', '/cancel'), isNotNull);
      expect(find.text('Otkazana'), findsOneWidget);
      expect(find.byKey(const Key('cancel_order_7')), findsNothing);
    },
  );
}
