import '../network/api_client.dart';
import '../reservations/reservation_models.dart';
import 'shop_models.dart';

Paged<T> _paged<T>(
  Map<String, dynamic> data,
  T Function(Map<String, dynamic>) f,
) => Paged(
  (data['items'] as List).map((e) => f(e as Map<String, dynamic>)).toList(),
  data['totalCount'] as int,
);

/// Products, brands/categories and orders.
class ShopApi {
  ShopApi(this._api);

  final ApiClient _api;

  // ---- products

  /// [sort]: Name, PriceAsc, PriceDesc, Newest. [includeInactive] and [lowStock] only work for admins.
  Future<Paged<Product>> products({
    String? search,
    int? categoryId,
    int? brandId,
    String sort = 'Name',
    bool includeInactive = false,
    bool lowStock = false,
    bool onSale = false,
    int page = 1,
    int pageSize = 20,
  }) async => _paged(
    await _api.get<Map<String, dynamic>>(
      '/api/v1/products',
      query: {
        if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
        'categoryId': ?categoryId,
        'brandId': ?brandId,
        'sort': sort,
        if (includeInactive) 'includeInactive': true,
        if (onSale) 'onSale': true,
        if (lowStock) 'lowStock': true,
        'page': page,
        'pageSize': pageSize,
      },
    ),
    Product.fromJson,
  );

  Future<Product> saveProduct({
    int? id,
    required String name,
    String? description,
    int? brandId,
    int? categoryId,
    required double price,
    required int stock,
    int? lowStockThreshold,
    bool isActive = true,
    double? salePrice,
  }) async {
    final body = {
      'name': name,
      'description': description,
      'brandId': brandId,
      'categoryId': categoryId,
      'price': price,
      'salePrice': salePrice,
      'stock': stock,
      'lowStockThreshold': lowStockThreshold,
      'isActive': isActive,
    };
    final data = id == null
        ? await _api.post<Map<String, dynamic>>('/api/v1/products', body: body)
        : await _api.put<Map<String, dynamic>>(
            '/api/v1/products/$id',
            body: body,
          );
    return Product.fromJson(data);
  }

  /// Positive restocks, negative writes off. The server refuses to go below zero.
  Future<Product> adjustStock(int id, int delta) async => Product.fromJson(
    await _api.post<Map<String, dynamic>>(
      '/api/v1/products/$id/stock',
      body: {'delta': delta},
    ),
  );

  // ---- entry page

  /// Categories and brands with counts and pictures, and the "on sale" shortcut: what the shop offers at a glance.
  Future<ShopOverview> overview() async => ShopOverview.fromJson(
    await _api.get<Map<String, dynamic>>('/api/v1/shop/overview'),
  );

  // ---- front-page sections

  /// Admin: every row as a rule, hidden ones included.
  Future<List<ShopSectionRule>> sectionRules() async =>
      (await _api.get<List<dynamic>>('/api/v1/shop/sections/manage'))
          .map((e) => ShopSectionRule.fromJson(e as Map<String, dynamic>))
          .toList();

  Future<ShopSectionRule> saveSection({
    int? id,
    required String title,
    required String kind,
    int? categoryId,
    int? brandId,
    int maxItems = 10,
    bool isActive = true,
    List<int> productIds = const [],
    bool showAsCard = false,
    String layout = ShopSectionLayout.row,
    String? eyebrow,
    List<int> entryIds = const [],
    ShopSectionStyle style = const ShopSectionStyle(),
  }) async {
    final body = {
      'title': title,
      'kind': kind,
      'categoryId': categoryId,
      'brandId': brandId,
      'maxItems': maxItems,
      'isActive': isActive,
      'productIds': productIds,
      'showAsCard': showAsCard,
      'layout': layout,
      'eyebrow': eyebrow,
      'entryIds': entryIds,
      'style': style.toJson(),
    };
    return ShopSectionRule.fromJson(
      id == null
          ? await _api.post<Map<String, dynamic>>(
              '/api/v1/shop/sections',
              body: body,
            )
          : await _api.put<Map<String, dynamic>>(
              '/api/v1/shop/sections/$id',
              body: body,
            ),
    );
  }

  /// Uploads the picture of a section card (JPEG, PNG or WebP, up to 5 MB). It replaces a picked product picture.
  Future<ShopSectionRule> setSectionImage(
    int id,
    List<int> bytes,
    String filename,
  ) async => ShopSectionRule.fromJson(
    await _api.put<Map<String, dynamic>>(
      '/api/v1/shop/sections/$id/image',
      body: imageForm(bytes, filename),
    ),
  );

  /// Uses one of the pictures of a product as the card picture.
  Future<ShopSectionRule> pickSectionImage(int id, int productImageId) async =>
      ShopSectionRule.fromJson(
        await _api.put<Map<String, dynamic>>(
          '/api/v1/shop/sections/$id/image/product',
          body: {'productImageId': productImageId},
        ),
      );

  /// Removes the card picture; the card goes back to the automatic cover.
  Future<ShopSectionRule> clearSectionImage(int id) async =>
      ShopSectionRule.fromJson(
        await _api.delete<Map<String, dynamic>>(
          '/api/v1/shop/sections/$id/image',
        ),
      );

  /// The products a section currently holds (what its card opens).
  Future<List<Product>> sectionProducts(int id) async =>
      (await _api.get<List<dynamic>>('/api/v1/shop/sections/$id/products'))
          .map((e) => Product.fromJson(e as Map<String, dynamic>))
          .toList();

  /// Sets the logo of a brand (admin). It is shown on the shop entry page.
  Future<Brand> setBrandLogo(int id, List<int> bytes, String filename) async =>
      Brand.fromJson(
        await _api.put<Map<String, dynamic>>(
          '/api/v1/brands/$id/logo',
          body: imageForm(bytes, filename),
        ),
      );

  Future<Brand> clearBrandLogo(int id) async => Brand.fromJson(
    await _api.delete<Map<String, dynamic>>('/api/v1/brands/$id/logo'),
  );

  Future<void> deleteSection(int id) =>
      _api.delete<dynamic>('/api/v1/shop/sections/$id');

  Future<void> reorderSections(List<int> ids) =>
      _api.put<dynamic>('/api/v1/shop/sections/order', body: {'ids': ids});

  Future<void> deactivateProduct(int id) =>
      _api.delete<dynamic>('/api/v1/products/$id');

  /// Adds a picture (JPEG, PNG or WebP, up to 5 MB). Admin only; returns the updated product.
  Future<Product> addProductImage(
    int id,
    List<int> bytes,
    String filename,
  ) async => Product.fromJson(
    await _api.post<Map<String, dynamic>>(
      '/api/v1/products/$id/images',
      body: imageForm(bytes, filename),
    ),
  );

  Future<Product> removeProductImage(int id, int imageId) async =>
      Product.fromJson(
        await _api.delete<Map<String, dynamic>>(
          '/api/v1/products/$id/images/$imageId',
        ),
      );

  // ---- brands & categories

  Future<List<Brand>> brands() async =>
      (await _api.get<List<dynamic>>('/api/v1/brands'))
          .map((e) => Brand.fromJson(e as Map<String, dynamic>))
          .toList();

  Future<Brand> saveBrand({int? id, required String name}) async {
    final body = {'name': name};
    return Brand.fromJson(
      id == null
          ? await _api.post<Map<String, dynamic>>('/api/v1/brands', body: body)
          : await _api.put<Map<String, dynamic>>(
              '/api/v1/brands/$id',
              body: body,
            ),
    );
  }

  Future<void> deleteBrand(int id) =>
      _api.delete<dynamic>('/api/v1/brands/$id');

  Future<List<Category>> categories() async =>
      (await _api.get<List<dynamic>>('/api/v1/categories'))
          .map((e) => Category.fromJson(e as Map<String, dynamic>))
          .toList();

  Future<Category> saveCategory({
    int? id,
    required String name,
    String? description,
  }) async {
    final body = {'name': name, 'description': description};
    return Category.fromJson(
      id == null
          ? await _api.post<Map<String, dynamic>>(
              '/api/v1/categories',
              body: body,
            )
          : await _api.put<Map<String, dynamic>>(
              '/api/v1/categories/$id',
              body: body,
            ),
    );
  }

  Future<void> deleteCategory(int id) =>
      _api.delete<dynamic>('/api/v1/categories/$id');

  // ---- orders

  /// Sends only product ids and quantities; the server prices the order.
  Future<ShopOrder> createOrder({
    required Map<int, int> quantities,
    required String fulfilment,
    required String paymentMethod,
    String? note,
    String? idempotencyKey,
  }) async => ShopOrder.fromJson(
    await _api.post<Map<String, dynamic>>(
      '/api/v1/orders',
      idempotencyKey: idempotencyKey,
      body: {
        'items': [
          for (final e in quantities.entries)
            {'productId': e.key, 'quantity': e.value},
        ],
        'fulfilment': fulfilment,
        'paymentMethod': paymentMethod,
        'note': note,
      },
    ),
  );

  Future<Paged<ShopOrder>> orders({
    String? status,
    int page = 1,
    int pageSize = 50,
  }) async => _paged(
    await _api.get<Map<String, dynamic>>(
      '/api/v1/orders',
      query: {'status': ?status, 'page': page, 'pageSize': pageSize},
    ),
    ShopOrder.fromJson,
  );

  Future<ShopOrder> order(int id) async => ShopOrder.fromJson(
    await _api.get<Map<String, dynamic>>('/api/v1/orders/$id'),
  );

  /// After the card sheet: the server asks the processor itself and finalises the order. The app never records a
  /// payment on its own.
  Future<ShopOrder> confirmPayment(int id) async => ShopOrder.fromJson(
    await _api.post<Map<String, dynamic>>('/api/v1/orders/$id/confirm-payment'),
  );

  /// Whether the server could verify the order as paid now. Never throws: if it cannot tell yet, the webhook
  /// finishes the job and the order shows as paid a little later.
  Future<bool> verifyPaid(int id) async {
    try {
      return (await confirmPayment(id)).isPaid;
    } on Object {
      return false; // offline, not confirmed yet, an odd answer: the webhook settles it
    }
  }

  Future<ShopOrder> cancelOrder(int id) async => ShopOrder.fromJson(
    await _api.post<Map<String, dynamic>>('/api/v1/orders/$id/cancel'),
  );

  Future<ShopOrder> fulfilOrder(int id) async => ShopOrder.fromJson(
    await _api.post<Map<String, dynamic>>('/api/v1/orders/$id/fulfil'),
  );
}
