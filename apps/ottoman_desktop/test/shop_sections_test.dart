import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';
import 'package:ottoman_desktop/features/images/picture_editor.dart';
import 'package:ottoman_desktop/features/products/product_dialogs.dart';
import 'package:ottoman_desktop/features/products/shop_sections_page.dart';

class _Api implements HttpClientAdapter {
  final requests = <RequestOptions>[];
  List<Map<String, dynamic>> rules = [
    {
      'id': 1,
      'title': 'Novo u ponudi',
      'kind': 'NewArrivals',
      'categoryId': null,
      'categoryName': null,
      'maxItems': 10,
      'sortOrder': 0,
      'isActive': true,
      'productIds': <int>[],
      'productCount': 5,
    },
    {
      'id': 2,
      'title': 'Akcije',
      'kind': 'OnSale',
      'categoryId': null,
      'categoryName': null,
      'maxItems': 10,
      'sortOrder': 1,
      'isActive': true,
      'productIds': <int>[],
      'productCount': 0,
    },
    {
      'id': 3,
      'title': 'Kategorije',
      'eyebrow': 'Asortiman',
      'kind': 'CategoryTiles',
      'categoryId': null,
      'categoryName': null,
      'maxItems': 6,
      'sortOrder': 2,
      'isActive': true,
      'productIds': <int>[],
      'productCount': 6,
      'entryIds': <int>[],
    },
  ];

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
    if (o.path.endsWith('/overview')) {
      body = {
        'totalProducts': 9,
        'onSale': {'count': 0, 'maxDiscountPercent': 0},
        'categories': [
          {'id': 1, 'name': 'Voskovi', 'productCount': 5},
          {'id': 2, 'name': 'Brada', 'productCount': 3},
          {'id': 3, 'name': 'Šamponi', 'productCount': 1},
        ],
        'brands': [
          {'id': 7, 'name': 'Reuzel', 'productCount': 4},
        ],
        'cards': [],
        'blocks': [],
      };
    } else if (o.path.endsWith('/image/product')) {
      body = {
        ...rules.first,
        'imageUrl': 'https://img/picked.jpg',
        'imageProductImageId': 77,
      };
    } else if (o.path.endsWith('/image') && o.method == 'PUT') {
      body = {...rules.first, 'imageUrl': 'https://img/uploaded.jpg'};
    } else if (o.path.endsWith('/image') && o.method == 'DELETE') {
      body = {...rules.first, 'imageUrl': null};
    } else if (o.path.endsWith('/products')) {
      body = {
        'items': [
          {
            'id': 5,
            'name': 'Vosak',
            'description': null,
            'brandId': 1,
            'brandName': 'Dandy',
            'categoryId': 1,
            'categoryName': 'Voskovi',
            'price': 10,
            'stock': 5,
            'lowStockThreshold': 2,
            'isLowStock': false,
            'isActive': true,
            'images': [
              {'id': 77, 'url': 'https://img/p77.jpg'},
            ],
          },
        ],
        'totalCount': 1,
      };
    } else if (o.path.endsWith('/brands') && o.method == 'GET') {
      body = [
        {'id': 1, 'name': 'Dandy', 'logoUrl': null},
        {'id': 2, 'name': 'Proraso', 'logoUrl': 'https://img/proraso.png'},
      ];
    } else if (o.path.endsWith('/logo')) {
      body = {
        'id': 1,
        'name': 'Dandy',
        'logoUrl': o.method == 'PUT' ? 'https://img/dandy.png' : null,
      };
    } else if (o.method == 'DELETE' && o.path.endsWith('/brands/1')) {
      status = 409;
      body = {
        'title': 'Sukob',
        'status': 409,
        'detail': 'Brend se koristi kod 3 proizvoda. Prvo im promijenite brend.',
      };
    } else if (o.path.endsWith('/manage')) {
      body = rules;
    } else if (o.method == 'POST' && o.path.endsWith('/sections')) {
      status = 201;
      body = {...(o.data as Map<String, dynamic>), 'id': 3, 'sortOrder': 2};
    } else if (o.method == 'PUT') {
      body = {...rules.first, ...(o.data as Map<String, dynamic>)};
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

class _Chooser implements ImageChooser {
  @override
  Future<ChosenImage?> choose() async =>
      ChosenImage('a.png', Uint8List.fromList([1, 2, 3]));
}

Future<_Api> _open(WidgetTester tester) => _openWith(tester, _Api());

Future<_Api> _openWith(WidgetTester tester, _Api api) async {
  tester.view.devicePixelRatio = 1.0;
  tester.view.physicalSize = const Size(1200, 1800);
  addTearDown(tester.view.reset);
  final shop = ShopApi(
    ApiClient(
      baseUrl: 'http://test',
      dio: Dio()..httpClientAdapter = api,
      readToken: () async => 't',
    ),
  );
  await tester.pumpWidget(
    MaterialApp(
      localizationsDelegates: AppLocalizations.localizationsDelegates,
      supportedLocales: AppLocalizations.supportedLocales,
      locale: const Locale('bs'),
      home: Scaffold(
        body: ShopSectionsPage(shop: shop, chooser: _Chooser()),
      ),
    ),
  );
  await tester.pumpAndSettle();
  return api;
}

void main() {
  testWidgets('the admin sees the rows and can hide one from customers', (
    tester,
  ) async {
    final api = await _open(tester);

    expect(find.text('Novo u ponudi'), findsOneWidget);
    expect(find.text('Akcije'), findsOneWidget);

    await tester.tap(find.byKey(const Key('section_visible_2')));
    await tester.pumpAndSettle();

    final sent = api.last('PUT', '/sections/2')!.data as Map<String, dynamic>;
    expect(sent['isActive'], false);
    expect(sent['kind'], 'OnSale');
  });

  testWidgets('hiding a block keeps its hand-picked tiles and its style', (
    tester,
  ) async {
    final api = _Api();
    api.rules[2] = {
      ...api.rules[2],
      'maxItems': 3, // odd: only valid because the tiles are picked by hand
      'entryIds': <int>[2, 1],
      'style': {
        'background': 'Dark',
        'tileSize': 'Large',
        'cardSpan': 'Full',
        'headingAlign': 'Center',
        'showSeeAll': true,
      },
    };
    await _openWith(tester, api);

    await tester.tap(find.byKey(const Key('section_visible_3')));
    await tester.pumpAndSettle();

    final sent = api.last('PUT', '/sections/3')!.data as Map<String, dynamic>;
    expect(sent['isActive'], false);
    expect(sent['entryIds'], [2, 1]);
    expect(sent['maxItems'], 3);
    expect(sent['eyebrow'], 'Asortiman');
    expect(sent['style'], {
      'background': 'Dark',
      'tileSize': 'Large',
      'cardSpan': 'Full',
      'headingAlign': 'Center',
      'showSeeAll': true,
    });
  });

  testWidgets('adding a section sends its title and what it shows', (
    tester,
  ) async {
    final api = await _open(tester);

    await tester.tap(find.byKey(const Key('add_section')));
    await tester.pumpAndSettle();
    await tester.enterText(find.byKey(const Key('section_title')), 'Preporuka');
    await tester.tap(find.byKey(const Key('section_save')));
    await tester.pumpAndSettle();

    final sent = api.last('POST', '/sections')!.data as Map<String, dynamic>;
    expect(sent['title'], 'Preporuka');
    expect(sent['kind'], 'NewArrivals');
    expect(sent['maxItems'], 10);
  });

  testWidgets('a section needs a title', (tester) async {
    final api = await _open(tester);

    await tester.tap(find.byKey(const Key('add_section')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('section_save')));
    await tester.pumpAndSettle();

    expect(api.last('POST', '/sections'), isNull);
    expect(find.text('Obavezno polje'), findsOneWidget);
  });

  testWidgets('deleting asks first and then removes the section', (
    tester,
  ) async {
    final api = await _open(tester);

    await tester.tap(find.byKey(const Key('section_delete_1')));
    await tester.pumpAndSettle();
    expect(api.last('DELETE', '/sections/1'), isNull);
    await tester.tap(find.byKey(const Key('confirm_delete_section')));
    await tester.pumpAndSettle();

    expect(api.last('DELETE', '/sections/1'), isNotNull);
  });

  testWidgets('a new section says the picture comes after saving; an existing '
      'one offers upload, pick and remove once it is shown as a card', (
    tester,
  ) async {
    final api = await _open(tester);

    await tester.tap(find.byKey(const Key('add_section')));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('section_picture_upload')), findsNothing);
    await tester.tap(find.byKey(const Key('section_as_card')));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('section_picture_hint')), findsOneWidget);
    await tester.tap(find.text('Otkaži'));
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('section_edit_2')));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('section_picture_upload')), findsNothing);
    await tester.tap(find.byKey(const Key('section_as_card')));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('section_picture_none')), findsOneWidget);

    await tester.tap(find.byKey(const Key('section_picture_upload')));
    await tester.pumpAndSettle();
    expect(api.last('PUT', '/sections/2/image'), isNotNull);
    expect(find.byKey(const Key('section_picture')), findsOneWidget);

    await tester.tap(find.byKey(const Key('section_picture_remove')));
    await tester.pumpAndSettle();
    expect(api.last('DELETE', '/sections/2/image'), isNotNull);
    expect(find.byKey(const Key('section_picture_none')), findsOneWidget);
  });

  testWidgets('a product picture can be picked for the card', (tester) async {
    final api = await _open(tester);

    await tester.tap(find.byKey(const Key('section_edit_2')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('section_as_card')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('section_picture_pick')));
    await tester.pumpAndSettle();

    expect(find.text('Vosak'), findsOneWidget);
    await tester.tap(find.byKey(const Key('picker_image_77')));
    await tester.pumpAndSettle();

    final sent =
        api.last('PUT', '/sections/2/image/product')!.data
            as Map<String, dynamic>;
    expect(sent['productImageId'], 77);
    expect(find.byKey(const Key('section_picture')), findsOneWidget);
  });

  testWidgets('saving a card section sends the card flag', (tester) async {
    final api = await _open(tester);

    await tester.tap(find.byKey(const Key('section_edit_1')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('section_as_card')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('section_save')));
    await tester.pumpAndSettle();

    final sent = api.last('PUT', '/sections/1')!.data as Map<String, dynamic>;
    expect(sent['showAsCard'], true);
  });

  testWidgets('a brand logo is uploaded and removed from the brands dialog', (
    tester,
  ) async {
    tester.view.devicePixelRatio = 1.0;
    tester.view.physicalSize = const Size(1200, 1800);
    addTearDown(tester.view.reset);
    final api = _Api();
    final shop = ShopApi(
      ApiClient(
        baseUrl: 'http://test',
        dio: Dio()..httpClientAdapter = api,
        readToken: () async => 't',
      ),
    );
    await tester.pumpWidget(
      MaterialApp(
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        supportedLocales: AppLocalizations.supportedLocales,
        locale: const Locale('bs'),
        home: Scaffold(
          body: NamesDialog.brands(shop: shop, chooser: _Chooser()),
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('logo_none_1')), findsOneWidget);
    expect(find.byKey(const Key('logo_2')), findsOneWidget);
    expect(find.byKey(const Key('logo_clear_1')), findsNothing);

    await tester.tap(find.byKey(const Key('logo_upload_1')));
    await tester.pumpAndSettle();
    expect(api.last('PUT', '/brands/1/logo'), isNotNull);

    await tester.tap(find.byKey(const Key('logo_clear_2')));
    await tester.pumpAndSettle();
    expect(api.last('DELETE', '/brands/2/logo'), isNotNull);
  });

  testWidgets('deleting a brand in use shows the server message and keeps it', (
    tester,
  ) async {
    tester.view.devicePixelRatio = 1.0;
    tester.view.physicalSize = const Size(1200, 1800);
    addTearDown(tester.view.reset);
    final api = _Api();
    final shop = ShopApi(
      ApiClient(
        baseUrl: 'http://test',
        dio: Dio()..httpClientAdapter = api,
        readToken: () async => 't',
      ),
    );
    await tester.pumpWidget(
      MaterialApp(
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        supportedLocales: AppLocalizations.supportedLocales,
        locale: const Locale('bs'),
        home: Scaffold(
          body: NamesDialog.brands(shop: shop, chooser: _Chooser()),
        ),
      ),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('name_delete_1')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('confirm_action')));
    await tester.pumpAndSettle();

    expect(api.last('DELETE', '/brands/1'), isNotNull);
    expect(
      find.text('Brend se koristi kod 3 proizvoda. Prvo im promijenite brend.'),
      findsOneWidget,
    );
    expect(find.byKey(const Key('name_row_1')), findsOneWidget);
  });

  testWidgets('a section title that is too short is refused before it is sent, '
      'and the field stops typing at the limit', (tester) async {
    final api = await _open(tester);

    await tester.tap(find.byKey(const Key('add_section')));
    await tester.pumpAndSettle();

    await tester.enterText(find.byKey(const Key('section_title')), 'A');
    await tester.tap(find.byKey(const Key('section_save')));
    await tester.pumpAndSettle();
    expect(find.textContaining('najmanje 2'), findsOneWidget);
    expect(api.last('POST', '/sections'), isNull);

    final field = tester.widget<TextField>(
      find.descendant(
        of: find.byKey(const Key('section_title')),
        matching: find.byType(TextField),
      ),
    );
    expect(field.maxLength, 24);
  });

  testWidgets('the preview follows the title, the card switch and the layout', (
    tester,
  ) async {
    await _open(tester);

    await tester.tap(find.byKey(const Key('add_section')));
    await tester.pumpAndSettle();

    // Nothing typed yet: the placeholder, a row, and no card.
    expect(find.byKey(const Key('section_preview')), findsOneWidget);
    expect(find.text('Naslov sekcije'), findsOneWidget);
    expect(find.byKey(const Key('preview_row')), findsOneWidget);
    expect(find.byKey(const Key('preview_card')), findsNothing);

    await tester.enterText(
      find.byKey(const Key('section_title')),
      'Ljetna  ponuda',
    );
    await tester.pump();
    expect(
      find.text('Ljetna ponuda'),
      findsWidgets,
    ); // spaces tidied, as the server does

    await tester.tap(find.byKey(const Key('section_as_card')));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('preview_card')), findsOneWidget);

    await tester.tap(find.text('Mreža'));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('preview_grid')), findsOneWidget);
    expect(find.byKey(const Key('preview_row')), findsNothing);
    expect(find.byKey(const Key('section_layout_hint')), findsOneWidget);

    await tester.tap(find.text('Veliki baner'));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('preview_banner')), findsOneWidget);
    // The banner's own title sits on the picture, so there is no separate heading.
    expect(find.byKey(const Key('preview_title')), findsNothing);
  });

  testWidgets('a banner needs a picture even when it is not a card', (
    tester,
  ) async {
    await _open(tester);

    await tester.tap(find.byKey(const Key('section_edit_2')));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('section_picture_upload')), findsNothing);

    await tester.tap(find.text('Veliki baner'));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('section_picture_upload')), findsOneWidget);
  });

  testWidgets('saving sends the chosen layout', (tester) async {
    final api = await _open(tester);

    await tester.tap(find.byKey(const Key('section_edit_1')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Mreža'));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('section_save')));
    await tester.pumpAndSettle();

    final sent = api.last('PUT', '/sections/1')!.data as Map<String, dynamic>;
    expect(sent['layout'], 'Grid');
  });

  testWidgets('the yellow text is its own field and shows in the preview in '
      'capitals above the white title', (tester) async {
    final api = await _open(tester);

    await tester.tap(find.byKey(const Key('add_section')));
    await tester.pumpAndSettle();
    expect(find.text('Mali tekst iznad naslova (žuti)'), findsOneWidget);
    expect(find.text('Naslov (bijeli tekst)'), findsOneWidget);

    await tester.enterText(
      find.byKey(const Key('section_eyebrow')),
      'Nova  kolekcija',
    );
    await tester.enterText(
      find.byKey(const Key('section_title')),
      'Ljeto 2026',
    );
    await tester.pump();
    expect(find.text('NOVA KOLEKCIJA'), findsOneWidget);
    expect(find.text('Ljeto 2026'), findsWidgets);

    await tester.tap(find.byKey(const Key('section_save')));
    await tester.pumpAndSettle();
    final sent = api.last('POST', '/sections')!.data as Map<String, dynamic>;
    expect(sent['eyebrow'], 'Nova  kolekcija'.trim());
    expect(sent['title'], 'Ljeto 2026');
  });

  testWidgets('the yellow text is optional and the field stops at the limit', (
    tester,
  ) async {
    final api = await _open(tester);

    await tester.tap(find.byKey(const Key('add_section')));
    await tester.pumpAndSettle();
    final field = tester.widget<TextField>(
      find.descendant(
        of: find.byKey(const Key('section_eyebrow')),
        matching: find.byType(TextField),
      ),
    );
    expect(field.maxLength, 24);

    await tester.enterText(
      find.byKey(const Key('section_title')),
      'Samo naslov',
    );
    await tester.tap(find.byKey(const Key('section_save')));
    await tester.pumpAndSettle();
    expect((api.last('POST', '/sections')!.data as Map)['eyebrow'], isNull);
  });

  testWidgets('a section with no products is flagged in the list and in its '
      'dialog, a full one is not', (tester) async {
    await _open(tester);

    expect(find.byKey(const Key('section_empty_2')), findsOneWidget);
    expect(find.byKey(const Key('section_empty_1')), findsNothing);

    await tester.tap(find.byKey(const Key('section_edit_2')));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('section_empty_warning')), findsOneWidget);
    await tester.tap(find.text('Otkaži'));
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('section_edit_1')));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('section_empty_warning')), findsNothing);
  });

  testWidgets('a built-in block shows how many tiles it shows and has a lock '
      'instead of a delete button', (tester) async {
    await _open(tester);

    expect(find.byKey(const Key('section_delete_3')), findsNothing);
    expect(find.byIcon(Icons.lock_outline), findsOneWidget);
    expect(find.textContaining('Prikazuje: 6'), findsOneWidget);
    expect(find.byKey(const Key('section_delete_1')), findsOneWidget);
  });

  testWidgets('the category block has no layout, card or picture controls; '
      'its count is an even number; hand-picked categories must be even too', (
    tester,
  ) async {
    final api = await _open(tester);

    await tester.tap(find.byKey(const Key('section_edit_3')));
    await tester.pumpAndSettle();

    // Nothing that only a list of products has.
    expect(find.byKey(const Key('section_layout')), findsNothing);
    expect(find.byKey(const Key('section_as_card')), findsNothing);
    expect(find.byKey(const Key('section_tile_size')), findsNothing);
    expect(find.byKey(const Key('section_see_all')), findsNothing);
    // The kind cannot be changed.
    final kind = tester.widget<DropdownButtonFormField<String>>(
      find.byKey(const Key('section_kind')),
    );
    expect(kind.onChanged, isNull);
    // The count is a choice of even numbers.
    expect(find.byKey(const Key('section_tile_count')), findsOneWidget);
    expect(find.byKey(const Key('preview_categories')), findsOneWidget);

    await tester.tap(find.byKey(const Key('section_tiles_manual')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('pick_tile_2')));
    await tester.pumpAndSettle();
    expect(find.textContaining('mora biti paran'), findsWidgets);

    // One pick is odd: saving is refused before anything is sent.
    await tester.tap(find.byKey(const Key('section_save')));
    await tester.pumpAndSettle();
    expect(api.last('PUT', '/sections/3'), isNull);

    await tester.tap(find.byKey(const Key('pick_tile_1')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('section_save')));
    await tester.pumpAndSettle();
    final sent = api.last('PUT', '/sections/3')!.data as Map<String, dynamic>;
    expect(sent['entryIds'], [2, 1]); // the order they were picked in
    expect(sent['kind'], 'CategoryTiles');
  });

  testWidgets('an odd automatic category count is saved as the even number '
      'the dropdown shows', (tester) async {
    final api = _Api();
    api.rules[2] = {...api.rules[2], 'maxItems': 5};
    await _openWith(tester, api);

    await tester.tap(find.byKey(const Key('section_edit_3')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('section_save')));
    await tester.pumpAndSettle();

    final sent = api.last('PUT', '/sections/3')!.data as Map<String, dynamic>;
    expect(sent['maxItems'], 6);
  });

  Map<String, dynamic> manual(List<int> productIds) => {
    'id': 4,
    'title': 'Izbor',
    'kind': 'Manual',
    'categoryId': null,
    'categoryName': null,
    'maxItems': 10,
    'sortOrder': 3,
    'isActive': true,
    'productIds': productIds,
    'productCount': productIds.length,
  };

  testWidgets('a hand-picked section needs at least one product and shows '
      'all of its picks', (tester) async {
    final api = _Api();
    api.rules.add(manual(const []));
    await _openWith(tester, api);

    await tester.tap(find.byKey(const Key('section_edit_4')));
    await tester.pumpAndSettle();
    await tester.ensureVisible(find.byKey(const Key('section_save')));
    await tester.tap(find.byKey(const Key('section_save')));
    await tester.pumpAndSettle();
    expect(api.last('PUT', '/sections/4'), isNull);
    expect(find.textContaining('od 1 do 30 proizvoda'), findsOneWidget);
  });

  testWidgets('saving a hand-picked section sends how many were picked as '
      'its count', (tester) async {
    final api = _Api();
    api.rules.add(manual(List.generate(12, (i) => 100 + i)));
    await _openWith(tester, api);

    await tester.tap(find.byKey(const Key('section_edit_4')));
    await tester.pumpAndSettle();
    await tester.ensureVisible(find.byKey(const Key('section_save')));
    await tester.tap(find.byKey(const Key('section_save')));
    await tester.pumpAndSettle();
    final sent = api.last('PUT', '/sections/4')!.data as Map<String, dynamic>;
    expect(sent['maxItems'], 12);
    expect((sent['productIds'] as List).length, 12);
  });

  testWidgets('a built-in block that exists is not offered again when '
      'adding a section, one that does not is', (tester) async {
    await _open(tester);

    await tester.tap(find.byKey(const Key('add_section')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('section_kind')));
    await tester.pumpAndSettle();

    expect(find.text('Blok brendova (kartice)'), findsWidgets);
    expect(find.text('Blok kategorija (pločice)'), findsNothing);
  });

  testWidgets('the look controls change the preview and are sent on saving', (
    tester,
  ) async {
    final api = await _open(tester);

    await tester.tap(find.byKey(const Key('section_edit_1')));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('preview_panel_soft')), findsNothing);

    await tester.tap(find.text('Tamna ploča'));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('preview_panel_soft')), findsOneWidget);

    await tester.tap(find.text('Jantarna ploča'));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('preview_panel_amber')), findsOneWidget);

    await tester.tap(find.text('Sredina'));
    await tester.tap(find.text('Veliki'));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('section_see_all')));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('preview_see_all')), findsOneWidget);

    // The width of a card only matters once the section is a card.
    expect(find.byKey(const Key('section_card_span')), findsNothing);
    await tester.tap(find.byKey(const Key('section_as_card')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Cijela širina'));
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('section_save')));
    await tester.pumpAndSettle();
    final sent = api.last('PUT', '/sections/1')!.data as Map<String, dynamic>;
    expect(sent['style'], {
      'background': 'Amber',
      'tileSize': 'Large',
      'cardSpan': 'Full',
      'headingAlign': 'Center',
      'showSeeAll': true,
    });
  });

  testWidgets('a banner says it always has a panel, and the preview draws it', (
    tester,
  ) async {
    await _open(tester);

    await tester.tap(find.byKey(const Key('section_edit_1')));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('section_banner_panel_hint')), findsNothing);

    await tester.tap(find.text('Veliki baner'));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('section_banner_panel_hint')), findsOneWidget);
    // No panel was chosen, yet the banner and its products stand on one.
    expect(find.byKey(const Key('preview_panel_soft')), findsOneWidget);
  });
}
