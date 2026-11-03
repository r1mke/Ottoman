import 'package:dio/dio.dart';

import '../network/api_client.dart';
import '../reservations/reservation_models.dart';
import 'gallery_models.dart';

/// The work gallery. Browsing is open to everyone signed in; the writes are admin only.
class GalleryApi {
  GalleryApi(this._api);

  final ApiClient _api;

  Future<List<HaircutCategory>> categories() async =>
      (await _api.get<List<dynamic>>('/api/v1/gallery/categories'))
          .map((e) => HaircutCategory.fromJson(e as Map<String, dynamic>))
          .toList();

  /// Newest first. [featured] limits to the works shown on the home screen.
  Future<Paged<GalleryItem>> items({
    int? categoryId,
    bool? featured,
    String? employeeId,
    int page = 1,
    int pageSize = 24,
  }) async {
    final data = await _api.get<Map<String, dynamic>>(
      '/api/v1/gallery',
      query: {
        'categoryId': ?categoryId,
        'featured': ?featured,
        'employeeId': ?employeeId,
        'page': page,
        'pageSize': pageSize,
      },
    );
    return Paged(
      (data['items'] as List)
          .map((e) => GalleryItem.fromJson(e as Map<String, dynamic>))
          .toList(),
      data['totalCount'] as int,
    );
  }

  /// Adds a picture (JPEG, PNG or WebP, up to 5 MB). The server refuses the same picture twice.
  Future<GalleryItem> add({
    required List<int> bytes,
    required String filename,
    required int categoryId,
    String? employeeId,
    String? caption,
    bool featured = false,
  }) async => GalleryItem.fromJson(
    await _api.post<Map<String, dynamic>>(
      '/api/v1/gallery',
      body: FormData.fromMap({
        'file': MultipartFile.fromBytes(bytes, filename: filename),
        'categoryId': categoryId,
        'employeeId': ?employeeId,
        if (caption != null && caption.trim().isNotEmpty)
          'caption': caption.trim(),
        'isFeatured': featured,
      }),
    ),
  );

  Future<GalleryItem> update(
    int id, {
    required int categoryId,
    String? employeeId,
    String? caption,
    required bool featured,
  }) async => GalleryItem.fromJson(
    await _api.put<Map<String, dynamic>>(
      '/api/v1/gallery/$id',
      body: {
        'categoryId': categoryId,
        'employeeId': employeeId,
        'caption': caption,
        'isFeatured': featured,
      },
    ),
  );

  Future<void> delete(int id) => _api.delete<dynamic>('/api/v1/gallery/$id');

  Future<HaircutCategory> saveCategory({int? id, required String name}) async {
    final body = {'name': name};
    return HaircutCategory.fromJson(
      id == null
          ? await _api.post<Map<String, dynamic>>(
              '/api/v1/gallery/categories',
              body: body,
            )
          : await _api.put<Map<String, dynamic>>(
              '/api/v1/gallery/categories/$id',
              body: body,
            ),
    );
  }

  Future<void> deleteCategory(int id) =>
      _api.delete<dynamic>('/api/v1/gallery/categories/$id');
}
