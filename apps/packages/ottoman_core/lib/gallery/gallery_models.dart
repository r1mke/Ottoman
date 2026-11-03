/// A haircut style the work gallery is filtered by.
class HaircutCategory {
  const HaircutCategory({
    required this.id,
    required this.name,
    required this.count,
  });

  final int id;
  final String name;

  /// How many works use it.
  final int count;

  factory HaircutCategory.fromJson(Map<String, dynamic> j) => HaircutCategory(
    id: j['id'] as int,
    name: j['name'] as String,
    count: j['count'] as int,
  );
}

/// One finished haircut in the gallery.
class GalleryItem {
  const GalleryItem({
    required this.id,
    required this.imageUrl,
    required this.categoryId,
    required this.categoryName,
    this.employeeId,
    this.employeeName,
    this.caption,
    required this.isFeatured,
  });

  final int id;
  final String imageUrl;
  final int categoryId;
  final String categoryName;
  final String? employeeId;
  final String? employeeName;
  final String? caption;

  /// Shown in "Our work" on the home screen.
  final bool isFeatured;

  factory GalleryItem.fromJson(Map<String, dynamic> j) => GalleryItem(
    id: j['id'] as int,
    imageUrl: j['imageUrl'] as String,
    categoryId: j['categoryId'] as int,
    categoryName: j['categoryName'] as String,
    employeeId: j['employeeId'] as String?,
    employeeName: j['employeeName'] as String?,
    caption: j['caption'] as String?,
    isFeatured: j['isFeatured'] as bool,
  );
}
