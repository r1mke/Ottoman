import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';
import 'package:flutter_stripe/flutter_stripe.dart' show FailureCode;
import 'package:ottoman_mobile/app.dart';
import 'package:ottoman_mobile/features/shop/stripe_card_payment.dart';

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

  /// What GET /orders/{id} returns; by default an unpaid card order.
  Map<String, dynamic>? orderDetail;
  int orderDetailStatus = 200;
  List<Map<String, dynamic>> sections = [];
  int onSaleCount = 0;
  bool showCategoryBlock = true;
  Map<String, Map<String, dynamic>> blockStyles = {};
  List<Map<String, dynamic>> cards = [];
  Map<String, dynamic> brand = {
    'id': 1,
    'name': 'Dandy Brand',
    'productCount': 3,
    'imageUrl': null,
  };

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
    } else if (path.endsWith('/overview')) {
      body = {
        'totalProducts': 3,
        'newestImageUrl': null,
        'onSale': {
          'count': onSaleCount,
          'maxDiscountPercent': 30,
          'imageUrl': null,
        },
        'categories': [
          {'id': 1, 'name': 'Voskovi', 'productCount': 3, 'imageUrl': null},
          {'id': 2, 'name': 'Brada', 'productCount': 1, 'imageUrl': null},
        ],
        'brands': [brand],
        'cards': cards,
        // The blocks below the cards, in the admin's order: the three built-in ones, then the sections.
        'blocks': [
          if (showCategoryBlock)
            {
              'sectionId': 100,
              'kind': 'CategoryTiles',
              'title': 'Kategorije',
              'eyebrow': 'Asortiman',
              'style': ?blockStyles['CategoryTiles'],
              'tiles': [
                {
                  'id': 1,
                  'name': 'Voskovi',
                  'productCount': 3,
                  'imageUrl': null,
                },
                {'id': 2, 'name': 'Brada', 'productCount': 1, 'imageUrl': null},
              ],
            },
          {
            'sectionId': 101,
            'kind': 'BrandTiles',
            'title': 'Brendovi',
            'eyebrow': 'Marke',
            'style': ?blockStyles['BrandTiles'],
            'tiles': [brand],
          },
          {
            'sectionId': 102,
            'kind': 'Recommended',
            'title': 'Možda vam se sviđa',
            'eyebrow': 'Za vas',
            'style': ?blockStyles['Recommended'],
          },
          // The section fixtures use `id`; a block calls it `sectionId`.
          for (final s in sections) {...s, 'sectionId': s['id']},
        ],
      };
    } else if (path.contains('/shop/sections/') && path.endsWith('/products')) {
      body = [
        _product(7, 'Sezonski vosak', 12, 4),
        _product(8, 'Šampon za bradu', 5, 4),
        _product(9, 'Ulje za bradu', 20, 4),
      ];
    } else if (path.endsWith('/sections')) {
      body = sections;
    } else if (RegExp(r'/orders/\d+$').hasMatch(path) && o.method == 'GET') {
      status = orderDetailStatus;
      body =
          orderDetail ??
          {
            ..._order(status: 'PendingPayment', method: 'Card'),
            'paymentClientSecret': 'pi_2_secret_y',
          };
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

/// A payment sheet that stays open until the test closes it.
class _SlowCard extends CardPaymentLauncher {
  final opened = <ShopOrder>[];
  final done = Completer<CardPaymentResult>();

  @override
  bool get isAvailable => true;

  @override
  Future<CardPaymentResult> pay(ShopOrder order) {
    opened.add(order);
    return done.future;
  }
}

class _FakeCard extends CardPaymentLauncher {
  _FakeCard(this.result);
  final CardPaymentResult result;
  final paid = <ShopOrder>[];

  @override
  bool get isAvailable => true;

  @override
  Future<CardPaymentResult> pay(ShopOrder order) async {
    paid.add(order);
    return result;
  }
}

Future<_Api> _openShopWith(
  WidgetTester tester, {
  required List<Map<String, dynamic>> sections,
}) => _openShop(tester, sections: sections, openList: false);

Future<_Api> _openShop(
  WidgetTester tester, {
  CardPaymentLauncher? cardPayment,
  List<Map<String, dynamic>> sections = const [],
  int onSaleCount = 0,
  List<Map<String, dynamic>> cards = const [],
  Map<String, dynamic>? brand,
  bool showCategoryBlock = true,
  Map<String, Map<String, dynamic>> blockStyles = const {},

  /// The shop opens on its entry page; most tests want the list of all products behind it.
  bool openList = true,
}) async {
  tester.view.devicePixelRatio = 1.0;
  tester.view.physicalSize = const Size(420, 900);
  addTearDown(tester.view.reset);

  final api = _Api()
    ..sections = sections.toList()
    ..onSaleCount = onSaleCount
    ..cards = cards.toList()
    ..showCategoryBlock = showCategoryBlock
    ..blockStyles = {...blockStyles};
  if (brand != null) api.brand = brand;
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
  await tester.pumpWidget(
    OttomanMobileApp(
      auth: auth,
      cardPayment: cardPayment ?? const UnavailableCardPayment(),
    ),
  );
  await tester.pumpAndSettle();
  await tester.enterText(find.byKey(const Key('login_email')), 'x@y.z');
  await tester.enterText(find.byKey(const Key('login_password')), 'pw');
  await tester.tap(find.byKey(const Key('login_submit')));
  await tester.pumpAndSettle();
  await tester.tap(find.text('Shop').last);
  await tester.pumpAndSettle();
  if (openList) {
    await tester.tap(find.byKey(const Key('all_products')));
    await tester.pumpAndSettle();
  }
  return api;
}

/// The main list of the entry page (the search field has a scrollable of its own, so `.first` would be wrong).
Finder _hubScrollable() => find
    .descendant(
      of: find.byKey(const Key('shop_hub')),
      matching: find.byType(Scrollable),
    )
    .first;

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
    await tester.tap(find.byKey(const Key('confirm_order')));
    await tester.pumpAndSettle();

    final sent = api.last('POST', '/orders')!.data as Map<String, dynamic>;
    expect(sent['items'], [
      {'productId': 1, 'quantity': 2},
    ]);
    expect(sent['paymentMethod'], 'OnReceipt');
    expect(sent['fulfilment'], 'Pickup');
    expect(sent.keys, isNot(contains('totalPrice')));
    expect(
      api.last('POST', '/orders')!.headers['Idempotency-Key'],
      matches(RegExp(r'^[0-9a-f]{32}$')),
      reason: 'one key per intention to order, so a repeat cannot order twice',
    );

    expect(find.text('Narudžba je zaprimljena.'), findsOneWidget);
    // Back on the shop's entry page with an empty basket (no badge).
    expect(find.byKey(const Key('shop_search')), findsOneWidget);
    final cartBadge = find.descendant(
      of: find.byKey(const Key('open_cart')),
      matching: find.byType(Badge),
    );
    expect(tester.widget<Badge>(cartBadge).isLabelVisible, isFalse);
  });

  Future<_Api> checkoutWithCard(
    WidgetTester tester,
    CardPaymentResult result,
    _FakeCard card,
  ) async {
    final api = await _openShop(tester, cardPayment: card);
    api.createError = {
      ..._order(status: 'PendingPayment', method: 'Card'),
      'paymentClientSecret': 'pi_1_secret_x',
    };
    await tester.tap(find.byKey(const Key('add_1')));
    await tester.tap(find.byKey(const Key('open_cart')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('go_checkout')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('place_order')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('confirm_order')));
    await tester.pumpAndSettle();
    return api;
  }

  testWidgets('with cards available checkout defaults to card and opens the '
      'payment sheet with the server secret', (tester) async {
    final card = _FakeCard(CardPaymentResult.paid);
    final api = await checkoutWithCard(tester, CardPaymentResult.paid, card);

    final sent = api.last('POST', '/orders')!.data as Map<String, dynamic>;
    expect(sent['paymentMethod'], 'Card');
    expect(sent['fulfilment'], 'Pickup');
    expect(card.paid.single.paymentClientSecret, 'pi_1_secret_x');
    expect(find.text('Narudžba je zaprimljena.'), findsOneWidget);
  });

  testWidgets(
    'closing the card sheet withdraws the order and keeps the basket',
    (tester) async {
      final card = _FakeCard(CardPaymentResult.cancelled);
      final api = await checkoutWithCard(
        tester,
        CardPaymentResult.cancelled,
        card,
      );

      // The order made for the sheet is withdrawn at once...
      expect(api.last('POST', '/cancel'), isNotNull);
      expect(find.textContaining('Narudžba nije kreirana'), findsOneWidget);
      // ...and the customer is still on checkout with the items in the basket.
      expect(find.byKey(const Key('place_order')), findsOneWidget);
      await tester.tap(find.byType(BackButton));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('go_checkout')), findsOneWidget);
    },
  );

  test('only an explicit cancel of the Stripe sheet counts as cancelled', () {
    expect(cardResultFor(FailureCode.Canceled), CardPaymentResult.cancelled);
    for (final code in [
      FailureCode.Failed,
      FailureCode.Timeout,
      FailureCode.Unknown,
    ]) {
      expect(cardResultFor(code), CardPaymentResult.failed, reason: '$code');
    }
  });

  testWidgets(
    'a sheet error that is not a cancel asks the server: a paid order is kept '
    'and the basket emptied',
    (tester) async {
      final card = _FakeCard(CardPaymentResult.failed);
      final api = await _openShop(tester, cardPayment: card);
      api.orderDetail = _order(status: 'Placed', method: 'Card');
      api.createError = {
        ..._order(status: 'PendingPayment', method: 'Card'),
        'paymentClientSecret': 'pi_1_secret_x',
      };
      await tester.tap(find.byKey(const Key('add_1')));
      await tester.tap(find.byKey(const Key('open_cart')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('go_checkout')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('place_order')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('confirm_order')));
      await tester.pumpAndSettle();

      expect(api.last('GET', '/orders/7'), isNotNull);
      expect(api.last('POST', '/cancel'), isNull);
      expect(find.text('Narudžba je zaprimljena.'), findsOneWidget);
    },
  );

  testWidgets(
    'a sheet error on an order the server has not seen paid never withdraws it',
    (tester) async {
      final card = _FakeCard(CardPaymentResult.failed);
      final api = await checkoutWithCard(
        tester,
        CardPaymentResult.failed,
        card,
      );

      expect(
        api.last('POST', '/cancel'),
        isNull,
        reason: 'the card may have been charged',
      );
      expect(find.byKey(const Key('payment_not_confirmed')), findsOneWidget);
      expect(find.byKey(const Key('place_order')), findsOneWidget);
    },
  );

  testWidgets('the quantity buttons in the cart say what they do', (
    tester,
  ) async {
    await _openShop(tester);
    await tester.tap(find.byKey(const Key('add_1')));
    await tester.tap(find.byKey(const Key('open_cart')));
    await tester.pumpAndSettle();
    expect(find.byTooltip('Jedan komad manje'), findsOneWidget);
    expect(find.byTooltip('Jedan komad više'), findsOneWidget);
  });

  testWidgets('checkout cannot be left while the order is being paid', (
    tester,
  ) async {
    final card = _SlowCard();
    final api = await _openShop(tester, cardPayment: card);
    api.createError = {
      ..._order(status: 'PendingPayment', method: 'Card'),
      'paymentClientSecret': 'pi_1_secret_x',
    };
    await tester.tap(find.byKey(const Key('add_1')));
    await tester.tap(find.byKey(const Key('open_cart')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('go_checkout')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('place_order')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('confirm_order')));
    // The busy spinner never settles: pump a little instead.
    await tester.pump(const Duration(milliseconds: 500));
    expect(card.opened, hasLength(1));

    // The system back gesture is refused and there is no back arrow.
    await tester.binding.handlePopRoute();
    await tester.pump(const Duration(milliseconds: 500));
    expect(find.byKey(const Key('place_order')), findsOneWidget);
    expect(find.byType(BackButton), findsNothing);

    card.done.complete(CardPaymentResult.cancelled);
    await tester.pumpAndSettle();
    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('place_order')), findsNothing);
  });

  testWidgets(
    'after a closed sheet the next try is a new order, not the old one',
    (tester) async {
      final card = _FakeCard(CardPaymentResult.cancelled);
      final api = await checkoutWithCard(
        tester,
        CardPaymentResult.cancelled,
        card,
      );
      String? orderKey() =>
          api.requests.reversed
                  .firstWhere(
                    (r) => r.method == 'POST' && r.path.endsWith('/orders'),
                  )
                  .headers['Idempotency-Key']
              as String?;
      final firstKey = orderKey();

      await tester.tap(find.byKey(const Key('place_order')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('confirm_order')));
      await tester.pumpAndSettle();

      expect(orderKey(), isNot(firstKey));
      expect(card.paid, hasLength(2));
    },
  );

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
      await tester.tap(find.byKey(const Key('confirm_order')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('checkout_error')), findsOneWidget);
      expect(find.textContaining('nema dovoljno na zalihi'), findsOneWidget);
      expect(
        find.byKey(const Key('place_order')),
        findsOneWidget,
      ); // still on checkout
    },
  );

  Future<_Api> openUnpaidOrder(
    WidgetTester tester,
    CardPaymentLauncher card,
  ) async {
    final api = await _openShop(tester, cardPayment: card);
    api.orders = [_order(status: 'PendingPayment', method: 'Card')];
    await tester.tap(find.byKey(const Key('open_orders')));
    await tester.pumpAndSettle();
    return api;
  }

  testWidgets('paying from my orders says how it went and reloads', (
    tester,
  ) async {
    await openUnpaidOrder(tester, _FakeCard(CardPaymentResult.paid));
    await tester.tap(find.byKey(const Key('pay_7')));
    await tester.pumpAndSettle();
    expect(find.text('Plaćanje je uspjelo. Hvala!'), findsOneWidget);
  });

  testWidgets('closing the sheet from my orders says it can be paid later', (
    tester,
  ) async {
    await openUnpaidOrder(tester, _FakeCard(CardPaymentResult.cancelled));
    await tester.tap(find.byKey(const Key('pay_7')));
    await tester.pumpAndSettle();
    expect(find.textContaining('platiti kasnije'), findsOneWidget);
  });

  testWidgets('a second tap while the sheet is open opens no second sheet', (
    tester,
  ) async {
    final card = _SlowCard();
    await openUnpaidOrder(tester, card);
    await tester.tap(find.byKey(const Key('pay_7')));
    await tester.pumpAndSettle();
    // While the sheet is open the button is gone, so it cannot be tapped again.
    expect(find.byKey(const Key('pay_7')), findsNothing);
    expect(card.opened, hasLength(1));

    card.done.complete(CardPaymentResult.cancelled);
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('pay_result')), findsOneWidget);
  });

  testWidgets('a server error while opening the payment is shown', (
    tester,
  ) async {
    final card = _FakeCard(CardPaymentResult.paid);
    final api = await openUnpaidOrder(tester, card);
    api
      ..orderDetailStatus = 409
      ..orderDetail = {
        'status': 409,
        'title': 'Sukob podataka.',
        'detail': 'Narudžba je u međuvremenu promijenjena.',
      };
    await tester.tap(find.byKey(const Key('pay_7')));
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);
    expect(card.paid, isEmpty);
    expect(find.byKey(const Key('pay_result')), findsOneWidget);
  });

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

  testWidgets('the product page lets the customer choose how many to add', (
    tester,
  ) async {
    final api = await _openShop(tester);
    await tester.tap(find.byKey(const Key('product_1')));
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('qty_plus')));
    await tester.tap(find.byKey(const Key('qty_plus')));
    await tester.pump();
    expect(find.byKey(const Key('qty_value')), findsOneWidget);
    expect(tester.widget<Text>(find.byKey(const Key('qty_value'))).data, '3');
    await tester.tap(find.byKey(const Key('detail_add')));
    await tester.pump();
    expect(tester.widget<Text>(find.byKey(const Key('qty_value'))).data, '1');

    await tester.tap(find.byType(BackButton));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('open_cart')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('go_checkout')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('place_order')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('confirm_order')));
    await tester.pumpAndSettle();
    final items = (api.last('POST', '/orders')!.data as Map)['items'] as List;
    expect(items.single, {'productId': 1, 'quantity': 3});
  });

  testWidgets('the quantity cannot go above the stock', (tester) async {
    await _openShop(tester);
    await tester.tap(find.byKey(const Key('product_2'))); // stock 1
    await tester.pumpAndSettle();

    final plus = tester.widget<IconButton>(find.byKey(const Key('qty_plus')));
    expect(plus.onPressed, isNull);
  });

  testWidgets('arranged sections show on the shop with the sale price '
      'struck through', (tester) async {
    tester.view.devicePixelRatio = 1.0;
    final sale = _product(9, 'Akcijski vosak', 20, 4)..['salePrice'] = 15.0;
    // Opening the shop builds the fake; the sections are served from it.
    final api = await _openShopWith(
      tester,
      sections: [
        {
          'id': 1,
          'title': 'Akcije',
          'kind': 'OnSale',
          'products': [sale],
        },
      ],
    );
    expect(api, isNotNull);

    await tester.scrollUntilVisible(
      find.byKey(const Key('section_1')),
      300,
      scrollable: _hubScrollable(),
    );
    expect(find.byKey(const Key('section_1')), findsOneWidget);
    expect(find.text('Akcije'), findsOneWidget);
    expect(find.byKey(const Key('tile_9')), findsOneWidget);
    expect(find.text('15.00 KM'), findsOneWidget);
    expect(find.byKey(const Key('old_price_9')), findsOneWidget);
    expect(find.text('-25%'), findsOneWidget);
  });

  testWidgets('the entry page shows what the shop offers: categories and '
      'brands, no cards the admin did not make, and no product list', (
    tester,
  ) async {
    await _openShop(tester, onSaleCount: 2, openList: false);

    // Nothing at the top that the admin cannot control: no automatic "sale" or "new" cards.
    expect(find.byKey(const Key('tile_on_sale')), findsNothing);
    expect(find.byKey(const Key('tile_new')), findsNothing);
    expect(find.byKey(const Key('category_tile_1')), findsOneWidget);
    expect(find.text('Voskovi'), findsOneWidget);
    expect(find.text('3 proizvoda'), findsWidgets);
    expect(find.byKey(const Key('all_products')), findsOneWidget);
    // The products themselves are one tap away, not on this page.
    expect(find.byKey(const Key('add_1')), findsNothing);
  });

  testWidgets('a category tile opens the products of that category', (
    tester,
  ) async {
    final api = await _openShop(tester, openList: false);

    await tester.tap(find.byKey(const Key('category_tile_2')));
    await tester.pumpAndSettle();

    final q = api.last('GET', '/products')!.queryParameters;
    expect(q['categoryId'], 2);
    expect(find.text('Brada'), findsWidgets); // the page title
    expect(find.byKey(const Key('add_1')), findsOneWidget);
  });

  testWidgets('a brand tile opens the products of that brand', (tester) async {
    final api = await _openShop(tester, openList: false);

    await tester.scrollUntilVisible(
      find.byKey(const Key('brand_tile_1')),
      200,
      scrollable: _hubScrollable(),
    );
    await tester.tap(find.byKey(const Key('brand_tile_1')));
    await tester.pumpAndSettle();
    expect(api.last('GET', '/products')!.queryParameters['brandId'], 1);
  });

  testWidgets('searching on the entry page opens the results', (tester) async {
    final api = await _openShop(tester, openList: false);

    await tester.enterText(find.byKey(const Key('shop_search')), 'vosak');
    await tester.testTextInput.receiveAction(TextInputAction.search);
    await tester.pumpAndSettle();

    expect(api.last('GET', '/products')!.queryParameters['search'], 'vosak');
    expect(find.text('Rezultati pretrage'), findsOneWidget);
  });

  testWidgets('sections the admin made cards replace the automatic shortcuts '
      'and open their own products', (tester) async {
    final api = await _openShop(
      tester,
      onSaleCount: 2,
      openList: false,
      cards: [
        {
          'sectionId': 4,
          'title': 'Ljetna ponuda',
          'kind': 'Manual',
          'productCount': 1,
          'maxDiscountPercent': 0,
          'imageUrl': null,
        },
        {
          'sectionId': 5,
          'title': 'Akcije',
          'kind': 'OnSale',
          'productCount': 3,
          'maxDiscountPercent': 25,
          'imageUrl': null,
        },
      ],
    );

    expect(find.byKey(const Key('card_4')), findsOneWidget);
    expect(find.text('Ljetna ponuda'), findsOneWidget);
    expect(find.text('1 proizvod'), findsWidgets); // also the Brada category
    expect(find.text('do -25%'), findsOneWidget);
    // The automatic ones are not shown next to them.
    expect(find.byKey(const Key('tile_on_sale')), findsNothing);
    expect(find.byKey(const Key('tile_new')), findsNothing);

    await tester.tap(find.byKey(const Key('card_4')));
    await tester.pumpAndSettle();

    expect(api.last('GET', '/shop/sections/4/products'), isNotNull);
    expect(find.text('Sezonski vosak'), findsOneWidget);
    expect(find.byKey(const Key('product_search')), findsOneWidget);
    expect(find.byKey(const Key('product_sort')), findsOneWidget);
  });

  testWidgets('the list a card opens can be searched and sorted on the device, '
      'without asking the server again', (tester) async {
    final api = await _openShop(
      tester,
      openList: false,
      cards: [
        {
          'sectionId': 4,
          'title': 'Ljetna ponuda',
          'kind': 'Manual',
          'productCount': 3,
          'maxDiscountPercent': 0,
          'imageUrl': null,
        },
      ],
    );
    await tester.tap(find.byKey(const Key('card_4')));
    await tester.pumpAndSettle();
    int asked() => api.requests
        .where((r) => r.path.contains('/shop/sections/4/products'))
        .length;
    expect(asked(), 1);

    // Search ignores case and the diacritics: "sampon" finds "Šampon za bradu".
    await tester.enterText(find.byKey(const Key('product_search')), 'sampon');
    await tester.pumpAndSettle();
    expect(find.text('Šampon za bradu'), findsOneWidget);
    expect(find.text('Sezonski vosak'), findsNothing);

    // The brand is searched too (all fixtures are by the same brand).
    await tester.enterText(find.byKey(const Key('product_search')), 'dandy');
    await tester.pumpAndSettle();
    expect(find.text('Sezonski vosak'), findsOneWidget);
    expect(find.text('Ulje za bradu'), findsOneWidget);

    await tester.enterText(find.byKey(const Key('product_search')), 'nema ga');
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('section_list_empty')), findsOneWidget);

    // Sorting by price, cheapest first, then dearest first.
    await tester.enterText(find.byKey(const Key('product_search')), '');
    await tester.pumpAndSettle();
    double top(String name) => tester.getTopLeft(find.text(name)).dy;
    await tester.tap(find.byKey(const Key('product_sort')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Cijena: manja prvo'));
    await tester.pumpAndSettle();
    expect(top('Šampon za bradu') < top('Sezonski vosak'), isTrue);
    expect(top('Sezonski vosak') < top('Ulje za bradu'), isTrue);

    await tester.tap(find.byKey(const Key('product_sort')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Cijena: veća prvo'));
    await tester.pumpAndSettle();
    expect(top('Ulje za bradu') < top('Sezonski vosak'), isTrue);

    // None of this went back to the server.
    expect(asked(), 1);
  });

  testWidgets('a brand shows its logo whole on a light card, or a product '
      'photo, or the initials when it has no picture yet', (tester) async {
    await _openShop(
      tester,
      openList: false,
      brand: {
        'id': 1,
        'name': 'Dandy Brand',
        'productCount': 3,
        'imageUrl': 'https://img/dandy.png',
        'isLogo': true,
      },
    );
    await tester.scrollUntilVisible(
      find.byKey(const Key('brand_tile_1')),
      200,
      scrollable: _hubScrollable(),
    );
    expect(find.byKey(const Key('brand_logo_1')), findsOneWidget);
    expect(find.text('Dandy Brand'), findsOneWidget);
  });

  testWidgets('a brand without a picture falls back to its initials', (
    tester,
  ) async {
    await _openShop(tester, openList: false);
    await tester.scrollUntilVisible(
      find.byKey(const Key('brand_tile_1')),
      200,
      scrollable: _hubScrollable(),
    );

    expect(find.text('DB'), findsOneWidget); // Dandy Brand
    expect(find.byKey(const Key('brand_logo_1')), findsNothing);
  });

  testWidgets('a section can be laid out as a grid or as a banner with its '
      'picture, and a plain one stays a sideways row', (tester) async {
    final a = _product(11, 'Prvi', 10, 3);
    final b = _product(12, 'Drugi', 12, 3);
    final c = _product(13, 'Treći', 9, 3);
    final d = _product(14, 'Četvrti', 8, 3);
    await _openShop(
      tester,
      openList: false,
      sections: [
        {
          'id': 1,
          'title': 'Mreža proizvoda',
          'kind': 'Manual',
          'layout': 'Grid',
          'products': [a, b],
        },
        {
          'id': 2,
          'title': 'Veliki baner',
          'kind': 'Manual',
          'layout': 'Banner',
          'imageUrl': 'https://img/banner.jpg',
          'products': [c],
        },
        {
          'id': 3,
          'title': 'Običan red',
          'kind': 'Manual',
          'products': [d],
        },
      ],
    );

    await tester.scrollUntilVisible(
      find.byKey(const Key('grid_1')),
      300,
      scrollable: _hubScrollable(),
    );
    // A grid heading is a normal title; both products are laid out in it.
    expect(find.text('Mreža proizvoda'), findsOneWidget);
    expect(find.byKey(const Key('tile_11')), findsOneWidget);
    expect(find.byKey(const Key('tile_12')), findsOneWidget);

    await tester.scrollUntilVisible(
      find.byKey(const Key('banner_2')),
      300,
      scrollable: _hubScrollable(),
    );
    // The banner carries the title on the picture (not as a separate heading).
    expect(find.text('Veliki baner'), findsOneWidget);

    await tester.scrollUntilVisible(
      find.text('Običan red'),
      300,
      scrollable: _hubScrollable(),
    );
    expect(find.byKey(const Key('grid_3')), findsNothing);
    expect(find.byKey(const Key('banner_3')), findsNothing);
  });

  testWidgets('a section shows its small yellow text in capitals above the '
      'white title; without it only the title; a card shows it too', (
    tester,
  ) async {
    await _openShop(
      tester,
      openList: false,
      sections: [
        {
          'id': 1,
          'title': 'Ljetna ponuda',
          'eyebrow': 'Nova kolekcija',
          'kind': 'Manual',
          'products': [_product(21, 'Prvi', 10, 3)],
        },
        {
          'id': 2,
          'title': 'Bez žutog',
          'kind': 'Manual',
          'products': [_product(22, 'Drugi', 10, 3)],
        },
      ],
      cards: [
        {
          'sectionId': 1,
          'title': 'Ljetna ponuda',
          'eyebrow': 'Nova kolekcija',
          'kind': 'Manual',
          'productCount': 1,
          'maxDiscountPercent': 0,
          'imageUrl': null,
        },
      ],
    );

    // On the card and (scrolled to) in the section heading: the same two texts.
    expect(find.text('NOVA KOLEKCIJA'), findsOneWidget);
    await tester.scrollUntilVisible(
      find.text('Bez žutog'),
      300,
      scrollable: _hubScrollable(),
    );
    expect(find.text('Bez žutog'), findsOneWidget);
    await tester.scrollUntilVisible(
      find.byKey(const Key('section_1')),
      -300,
      scrollable: _hubScrollable(),
    );
    expect(find.text('NOVA KOLEKCIJA'), findsWidgets);
  });

  testWidgets('the tab is called Shop', (tester) async {
    await _openShop(tester, openList: false);

    expect(find.text('Shop'), findsWidgets);
    expect(find.text('Proizvodi'), findsNothing);
  });

  Map<String, dynamic> section(
    int id,
    String title,
    int productId, {
    String layout = 'Row',
    Map<String, dynamic>? style,
  }) => {
    'id': id,
    'title': title,
    'kind': 'Manual',
    'layout': layout,
    'products': [_product(productId, 'Proizvod $productId', 10, 3)],
    'style': ?style,
  };

  testWidgets('the entry page draws its blocks in order; the category grid has '
      'no "all products" tile, only the link in its heading', (tester) async {
    await _openShop(
      tester,
      openList: false,
      sections: [section(5, 'Moja sekcija', 31)],
    );
    tester.view.physicalSize = const Size(420, 4000);
    await tester.pumpAndSettle();

    double top(Finder f) => tester.getTopLeft(f).dy;
    final categories = top(find.byKey(const Key('block_100')));
    final brands = top(find.byKey(const Key('block_101')));
    final mine = top(find.byKey(const Key('block_5')));
    expect(categories < brands, isTrue);
    expect(brands < mine, isTrue);

    // Two category tiles (an even number) and no tile that says "all products": the link is in the heading.
    expect(find.byKey(const Key('category_tile_1')), findsOneWidget);
    expect(find.byKey(const Key('category_tile_2')), findsOneWidget);
    expect(find.text('ASORTIMAN'), findsOneWidget);
    expect(find.byKey(const Key('all_products')), findsOneWidget);
    expect(
      find.descendant(
        of: find.byKey(const Key('block_100')),
        matching: find.byKey(const Key('all_products')),
      ),
      findsOneWidget,
    );
  });

  testWidgets('without a category block there is still a way to all products', (
    tester,
  ) async {
    await _openShop(tester, openList: false, showCategoryBlock: false);

    expect(find.byKey(const Key('block_100')), findsNothing);
    expect(find.byKey(const Key('all_products')), findsOneWidget);
    await tester.tap(find.byKey(const Key('all_products')));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('add_1')), findsOneWidget);
  });

  testWidgets('a banner stands on a panel with its products; a chosen panel '
      'and the "see all" link work', (tester) async {
    final api = await _openShop(
      tester,
      openList: false,
      sections: [
        section(6, 'Veliki baner', 32, layout: 'Banner'),
        section(
          7,
          'Na jantarnom',
          33,
          style: {'background': 'Amber', 'showSeeAll': true},
        ),
        section(8, 'Bez ploče', 34),
      ],
    );
    tester.view.physicalSize = const Size(420, 5000);
    await tester.pumpAndSettle();

    // The banner has a panel even though none was chosen, and its products are inside it.
    expect(
      find.descendant(
        of: find.byKey(const Key('panel_soft')),
        matching: find.byKey(const Key('banner_6')),
      ),
      findsOneWidget,
    );
    expect(
      find.descendant(
        of: find.byKey(const Key('panel_soft')),
        matching: find.byKey(const Key('tile_32')),
      ),
      findsOneWidget,
    );
    expect(
      find.descendant(
        of: find.byKey(const Key('panel_amber')),
        matching: find.byKey(const Key('tile_33')),
      ),
      findsOneWidget,
    );
    // A section without a panel has none around it.
    expect(
      find.ancestor(
        of: find.byKey(const Key('tile_34')),
        matching: find.byKey(const Key('panel_none')),
      ),
      findsNothing,
    );

    expect(find.byKey(const Key('see_all_8')), findsNothing);
    await tester.tap(find.byKey(const Key('see_all_7')));
    await tester.pumpAndSettle();
    expect(api.last('GET', '/shop/sections/7/products'), isNotNull);
    expect(find.text('Na jantarnom'), findsWidgets);
  });

  testWidgets('the tile size of a section changes how wide its products are', (
    tester,
  ) async {
    await _openShop(
      tester,
      openList: false,
      sections: [
        section(9, 'Mali', 41, style: {'tileSize': 'Compact'}),
        section(10, 'Veliki', 42, style: {'tileSize': 'Large'}),
        section(11, 'Obični', 43),
      ],
    );
    tester.view.physicalSize = const Size(420, 5000);
    await tester.pumpAndSettle();

    expect(tester.getSize(find.byKey(const Key('tile_41'))).width, 128);
    expect(tester.getSize(find.byKey(const Key('tile_42'))).width, 188);
    expect(tester.getSize(find.byKey(const Key('tile_43'))).width, 156);
  });

  testWidgets('a full-width card takes a row of its own and half-width cards '
      'go two by two', (tester) async {
    Map<String, dynamic> card(int id, String span) => {
      'sectionId': id,
      'title': 'Kartica $id',
      'kind': 'Manual',
      'productCount': 2,
      'maxDiscountPercent': 0,
      'imageUrl': null,
      'style': {'cardSpan': span},
    };
    await _openShop(
      tester,
      openList: false,
      cards: [card(1, 'Half'), card(2, 'Half'), card(3, 'Full')],
    );

    final half1 = tester.getSize(find.byKey(const Key('card_1')));
    final half2 = tester.getSize(find.byKey(const Key('card_2')));
    final full = tester.getSize(find.byKey(const Key('card_3')));
    expect(half1.width, half2.width);
    expect(full.width, greaterThan(half1.width * 1.8));
    // The two halves share a row; the full card sits under them.
    expect(
      tester.getTopLeft(find.byKey(const Key('card_1'))).dy,
      tester.getTopLeft(find.byKey(const Key('card_2'))).dy,
    );
    expect(
      tester.getTopLeft(find.byKey(const Key('card_3'))).dy,
      greaterThan(tester.getTopLeft(find.byKey(const Key('card_1'))).dy),
    );
  });

  testWidgets('the panel the admin chose also goes around the category, brand '
      'and suggestion blocks, not only around sections', (tester) async {
    await _openShop(
      tester,
      openList: false,
      blockStyles: {
        'BrandTiles': {'background': 'Amber'},
        'CategoryTiles': {'background': 'Soft', 'headingAlign': 'Center'},
      },
    );
    tester.view.physicalSize = const Size(420, 4000);
    await tester.pumpAndSettle();

    expect(
      find.descendant(
        of: find.byKey(const Key('panel_amber')),
        matching: find.byKey(const Key('brand_tile_1')),
      ),
      findsOneWidget,
    );
    expect(
      find.descendant(
        of: find.byKey(const Key('panel_soft')),
        matching: find.byKey(const Key('category_tile_1')),
      ),
      findsOneWidget,
    );
    // The suggestions were left without a panel.
    expect(
      find.ancestor(
        of: find.text('Možda vam se sviđa'),
        matching: find.byKey(const Key('panel_none')),
      ),
      findsNothing,
    );
    expect(find.byKey(const Key('panel_amber')), findsOneWidget);
    expect(find.byKey(const Key('panel_soft')), findsOneWidget);
  });

  testWidgets('sending an order asks first: cancelling sends nothing, '
      'confirming sends it', (tester) async {
    final api = await _openShop(tester);
    await tester.tap(find.byKey(const Key('add_1')));
    await tester.tap(find.byKey(const Key('open_cart')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('go_checkout')));
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('place_order')));
    await tester.pumpAndSettle();
    expect(find.text('Potvrda narudžbe'), findsOneWidget);
    expect(find.textContaining('10.00 KM'), findsWidgets);

    await tester.tap(find.byKey(const Key('confirm_order_cancel')));
    await tester.pumpAndSettle();
    expect(api.last('POST', '/orders'), isNull);
    expect(find.byKey(const Key('place_order')), findsOneWidget);

    await tester.tap(find.byKey(const Key('place_order')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('confirm_order')));
    await tester.pumpAndSettle();
    expect(api.last('POST', '/orders'), isNotNull);
  });
}
