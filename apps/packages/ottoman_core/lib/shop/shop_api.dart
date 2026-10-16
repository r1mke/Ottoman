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
  }) async {
    final body = {
      'name': name,
      'description': description,
      'brandId': brandId,
      'categoryId': categoryId,
      'price': price,
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
    String? deliveryAddress,
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
        'deliveryAddress': deliveryAddress,
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

  Future<ShopOrder> cancelOrder(int id) async => ShopOrder.fromJson(
    await _api.post<Map<String, dynamic>>('/api/v1/orders/$id/cancel'),
  );

  Future<ShopOrder> fulfilOrder(int id) async => ShopOrder.fromJson(
    await _api.post<Map<String, dynamic>>('/api/v1/orders/$id/fulfil'),
  );
}
