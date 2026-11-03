import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';

class _Adapter implements HttpClientAdapter {
  _Adapter(this.body);
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

ApiClient _client(_Adapter a) => ApiClient(
  baseUrl: 'http://test',
  dio: Dio()..httpClientAdapter = a,
  readToken: () async => 't',
);

Map<String, dynamic> _product({
  String? imageUrl,
  List<Map<String, dynamic>>? images,
}) => {
  'id': 7,
  'name': 'Vosak',
  'price': 10,
  'stock': 3,
  'isLowStock': false,
  'isActive': true,
  'imageUrl': imageUrl,
  'images': images ?? [],
};

void main() {
  test(
    'a product with pictures parses them in order, one without parses to none',
    () {
      final with2 = Product.fromJson(
        _product(
          imageUrl: 'https://x/a',
          images: [
            {'id': 1, 'url': 'https://x/a'},
            {'id': 2, 'url': 'https://x/b'},
          ],
        ),
      );
      expect(with2.imageUrl, 'https://x/a');
      expect(with2.images.map((i) => i.id), [1, 2]);

      final without = Product.fromJson(_product());
      expect(without.imageUrl, isNull);
      expect(without.images, isEmpty);

      final old = _product()..remove('images');
      expect(Product.fromJson(old).images, isEmpty);
    },
  );

  test('the employee initials are upper case and survive missing names', () {
    Employee e(String f, String l) => Employee.fromJson({
      'id': 'e',
      'firstName': f,
      'lastName': l,
      'title': 't',
      'isActive': true,
      'serviceIds': <int>[],
    });
    expect(e('kerim', 'hodzic').initials, 'KH');
    expect(e('Ana', '').initials, 'A');
    expect(e('', '').initials, '');
  });

  test(
    'uploading a product picture sends multipart to the images endpoint',
    () async {
      final adapter = _Adapter(
        _product(
          imageUrl: 'https://x/a',
          images: [
            {'id': 5, 'url': 'https://x/a'},
          ],
        ),
      );

      final p = await ShopApi(_client(adapter))
          .addProductImage(7, [1, 2, 3], 'foto.png');

      expect(adapter.last!.method, 'POST');
      expect(adapter.last!.path, '/api/v1/products/7/images');
      expect(adapter.last!.data, isA<FormData>());
      final form = adapter.last!.data as FormData;
      expect(form.files.single.key, 'file');
      expect(form.files.single.value.filename, 'foto.png');
      expect(p.images.single.id, 5);
    },
  );

  test('removing, and the service and employee picture endpoints use the right routes', () async {
    final product = _Adapter(_product());
    await ShopApi(_client(product)).removeProductImage(7, 5);
    expect(product.last!.method, 'DELETE');
    expect(product.last!.path, '/api/v1/products/7/images/5');

    final service = _Adapter({
      'id': 1,
      'name': 'Šišanje',
      'price': 10,
      'durationMinutes': 30,
      'isActive': true,
      'imageUrl': 'https://x/s',
    });
    final catalog = CatalogApi(_client(service));
    final saved = await catalog.setServiceImage(1, [1], 'a.jpg');
    expect(service.last!.method, 'PUT');
    expect(service.last!.path, '/api/v1/services/1/image');
    expect(saved.imageUrl, 'https://x/s');
    await catalog.clearServiceImage(1);
    expect(service.last!.method, 'DELETE');
    expect(service.last!.path, '/api/v1/services/1/image');

    final employee = _Adapter({
      'id': 'e1',
      'firstName': 'A',
      'lastName': 'B',
      'title': 't',
      'isActive': true,
      'serviceIds': <int>[],
      'photoUrl': 'https://x/p',
    });
    final staff = CatalogApi(_client(employee));
    final e = await staff.setEmployeePhoto('e1', [1], 'a.webp');
    expect(employee.last!.path, '/api/v1/employees/e1/photo');
    expect(e.photoUrl, 'https://x/p');
    await staff.clearEmployeePhoto('e1');
    expect(employee.last!.method, 'DELETE');
  });

  testWidgets('NetworkPicture shows a placeholder without an address', (
    tester,
  ) async {
    await tester.pumpWidget(
      const MaterialApp(home: NetworkPicture(url: null, width: 40, height: 40)),
    );
    expect(find.byKey(const Key('picture-placeholder')), findsOneWidget);
    expect(find.byKey(const Key('picture-image')), findsNothing);
  });

  testWidgets(
    'NetworkPicture falls back to the placeholder when the picture fails',
    (tester) async {
      await tester.pumpWidget(
        const MaterialApp(
          home: NetworkPicture(
            url: 'http://127.0.0.1:1/none.png',
            width: 40,
            height: 40,
          ),
        ),
      );
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 50));
      expect(tester.takeException(), isNull);
      expect(find.byKey(const Key('picture-placeholder')), findsOneWidget);
    },
  );

  testWidgets('PersonAvatar without a photo shows a default chosen by gender', (
    tester,
  ) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: Scaffold(
          body: Row(
            children: [
              PersonAvatar(gender: 'Male'),
              PersonAvatar(gender: 'Female'),
              PersonAvatar(),
            ],
          ),
        ),
      ),
    );
    await tester.pump();
    final assets = tester
        .widgetList<Image>(find.byKey(const Key('avatar-default')))
        .map((i) => (i.image as AssetImage).assetName)
        .toList();
    expect(assets, hasLength(3));
    expect(assets[0], startsWith('assets/avatars/male_'));
    expect(assets[1], startsWith('assets/avatars/female_'));
    expect(assets[2], 'assets/avatars/neutral.png');
    expect(find.byKey(const Key('avatar-photo')), findsNothing);
  });

  test('the same seed always gets the same drawn avatar, other seeds spread over all of them', () {
    expect(AvatarGender.male.asset('abc'), AvatarGender.male.asset('abc'));
    expect(AvatarGender.male.asset(null), 'assets/avatars/male_1.png');
    final used = {
      for (var i = 0; i < 40; i++) AvatarGender.female.asset('barber-$i'),
    };
    expect(used, hasLength(AvatarGender.variants));
    expect(AvatarGender.unknown.asset('x'), AvatarGender.unknown.asset('y'));
  });

  test('only Male and Female pick a gendered avatar', () {
    expect(AvatarGender.parse('Male'), AvatarGender.male);
    expect(AvatarGender.parse('Female'), AvatarGender.female);
    expect(AvatarGender.parse(null), AvatarGender.unknown);
    expect(AvatarGender.parse('male'), AvatarGender.unknown);
  });
}
