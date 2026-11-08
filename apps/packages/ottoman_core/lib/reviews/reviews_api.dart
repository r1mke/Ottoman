import '../catalog/catalog_models.dart';
import '../l10n/app_localizations.dart';
import '../network/api_client.dart';
import '../reservations/reservation_models.dart';
import '../shop/shop_models.dart';

/// What a review is about; matches the API names.
abstract final class ReviewTarget {
  static const employee = 'Employee';
  static const service = 'Service';
  static const product = 'Product';
}

/// "Frizer", "Usluga" or "Proizvod" for a target type (the API name for anything else).
String reviewTargetLabel(AppLocalizations t, String type) => switch (type) {
  ReviewTarget.employee => t.reviewTargetEmployee,
  ReviewTarget.service => t.reviewTargetService,
  ReviewTarget.product => t.reviewTargetProduct,
  _ => type,
};

class Review {
  const Review({
    required this.id,
    required this.targetType,
    required this.targetKey,
    required this.targetName,
    required this.authorName,
    required this.rating,
    required this.createdAtUtc,
    required this.isHidden,
    required this.isMine,
    this.comment,
    this.replyText,
  });

  final int id;
  final String targetType;
  final String targetKey;
  final String targetName;
  final String authorName;
  final int rating;
  final String? comment;
  final DateTime createdAtUtc;
  final bool isHidden;
  final String? replyText;
  final bool isMine;

  factory Review.fromJson(Map<String, dynamic> j) => Review(
    id: j['id'] as int,
    targetType: j['targetType'] as String,
    targetKey: j['targetKey'] as String,
    targetName: j['targetName'] as String,
    authorName: j['authorName'] as String,
    rating: j['rating'] as int,
    comment: j['comment'] as String?,
    createdAtUtc: DateTime.parse(j['createdAtUtc'] as String),
    isHidden: j['isHidden'] as bool,
    replyText: j['replyText'] as String?,
    isMine: j['isMine'] as bool,
  );
}

class ReviewList {
  const ReviewList(this.items, this.totalCount, this.average, this.count);
  final List<Review> items;
  final int totalCount;
  final double average;
  final int count;
}

class ReviewSummary {
  const ReviewSummary(this.average, this.count);
  final double average;
  final int count;
}

class Eligibility {
  const Eligibility({required this.canReview, this.reason, this.existingId});
  final bool canReview;
  final String? reason;
  final int? existingId;
}

class ReviewsApi {
  ReviewsApi(this._api);

  final ApiClient _api;

  Future<ReviewList> list(String type, String key, {int pageSize = 20}) async {
    final data = await _api.get<Map<String, dynamic>>(
      '/api/v1/reviews',
      query: {'targetType': type, 'targetKey': key, 'pageSize': pageSize},
    );
    return ReviewList(
      (data['items'] as List)
          .map((e) => Review.fromJson(e as Map<String, dynamic>))
          .toList(),
      data['totalCount'] as int,
      (data['average'] as num).toDouble(),
      data['count'] as int,
    );
  }

  /// Average and count for every target of one kind, keyed by target key.
  Future<Map<String, ReviewSummary>> summary(String type) async {
    final data = await _api.get<List<dynamic>>(
      '/api/v1/reviews/summary',
      query: {'targetType': type},
    );
    return {
      for (final e in data.cast<Map<String, dynamic>>())
        e['targetKey'] as String: ReviewSummary(
          (e['average'] as num).toDouble(),
          e['count'] as int,
        ),
    };
  }

  Future<Eligibility> eligibility(String type, String key) async {
    final data = await _api.get<Map<String, dynamic>>(
      '/api/v1/reviews/eligibility',
      query: {'targetType': type, 'targetKey': key},
    );
    return Eligibility(
      canReview: data['canReview'] as bool,
      reason: data['reason'] as String?,
      existingId: data['existingReviewId'] as int?,
    );
  }

  Future<Review> create(
    String type,
    String key,
    int rating, {
    String? comment,
  }) async => Review.fromJson(
    await _api.post<Map<String, dynamic>>(
      '/api/v1/reviews',
      body: {
        'targetType': type,
        'targetKey': key,
        'rating': rating,
        'comment': comment,
      },
    ),
  );

  Future<Review> update(int id, int rating, {String? comment}) async =>
      Review.fromJson(
        await _api.put<Map<String, dynamic>>(
          '/api/v1/reviews/$id',
          body: {'rating': rating, 'comment': comment},
        ),
      );

  Future<void> delete(int id) => _api.delete<dynamic>('/api/v1/reviews/$id');

  // ---- admin

  Future<Paged<Review>> all({
    bool onlyHidden = false,
    int page = 1,
    int pageSize = 30,
  }) async {
    final data = await _api.get<Map<String, dynamic>>(
      '/api/v1/reviews/all',
      query: {
        if (onlyHidden) 'onlyHidden': true,
        'page': page,
        'pageSize': pageSize,
      },
    );
    return Paged(
      (data['items'] as List)
          .map((e) => Review.fromJson(e as Map<String, dynamic>))
          .toList(),
      data['totalCount'] as int,
    );
  }

  Future<Review> setHidden(int id, bool hidden) async => Review.fromJson(
    await _api.put<Map<String, dynamic>>(
      '/api/v1/reviews/$id/hide',
      body: {'hidden': hidden},
    ),
  );

  Future<Review> reply(int id, String text) async => Review.fromJson(
    await _api.post<Map<String, dynamic>>(
      '/api/v1/reviews/$id/reply',
      body: {'text': text},
    ),
  );
}

/// "You might like" lists and similar products.
/// A suggestion together with the reason the server gives for it ("Jer ste kupili ..."). The reason is built from
/// what the engine actually compared, so it can be shown as it is.
class Recommended<T> {
  const Recommended(this.item, {required this.reason, required this.kind});

  final T item;

  /// One sentence for the client.
  final String reason;

  /// SimilarToInterest, SimilarToItem or Popular.
  final String kind;

  factory Recommended.fromJson(
    Map<String, dynamic> j,
    T Function(Map<String, dynamic>) parse,
  ) {
    final reason = j['reason'] as Map<String, dynamic>;
    return Recommended(
      parse(j['item'] as Map<String, dynamic>),
      reason: reason['text'] as String,
      kind: reason['kind'] as String,
    );
  }
}

class RecommendationsApi {
  RecommendationsApi(this._api);

  final ApiClient _api;

  Future<List<Recommended<Product>>> products({int count = 6}) =>
      _list('/api/v1/recommendations/products', count, Product.fromJson);

  Future<List<Recommended<Service>>> services({int count = 6}) =>
      _list('/api/v1/recommendations/services', count, Service.fromJson);

  Future<List<Recommended<Employee>>> employees({int count = 4}) =>
      _list('/api/v1/recommendations/employees', count, Employee.fromJson);

  Future<List<Recommended<Product>>> relatedProducts(
    int productId, {
    int count = 4,
  }) => _list('/api/v1/products/$productId/related', count, Product.fromJson);

  Future<List<Recommended<T>>> _list<T>(
    String path,
    int count,
    T Function(Map<String, dynamic>) parse,
  ) async => (await _api.get<List<dynamic>>(path, query: {'count': count}))
      .map((e) => Recommended.fromJson(e as Map<String, dynamic>, parse))
      .toList();
}
