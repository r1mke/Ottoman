class Brand {
  const Brand({required this.id, required this.name, this.logoUrl});
  final int id;
  final String name;

  /// The logo the admin uploaded; null when the shop shows a product photo for this brand.
  final String? logoUrl;

  factory Brand.fromJson(Map<String, dynamic> j) => Brand(
    id: j['id'] as int,
    name: j['name'] as String,
    logoUrl: j['logoUrl'] as String?,
  );
}

class Category {
  const Category({required this.id, required this.name, this.description});
  final int id;
  final String name;
  final String? description;

  factory Category.fromJson(Map<String, dynamic> j) => Category(
    id: j['id'] as int,
    name: j['name'] as String,
    description: j['description'] as String?,
  );
}

class Product {
  const Product({
    required this.id,
    required this.name,
    required this.price,
    required this.stock,
    required this.isLowStock,
    required this.isActive,
    this.description,
    this.brandId,
    this.brandName,
    this.categoryId,
    this.categoryName,
    this.lowStockThreshold,
    this.imageUrl,
    this.images = const [],
    this.salePrice,
  });

  final int id;
  final String name;
  final String? description;
  final int? brandId;
  final String? brandName;
  final int? categoryId;
  final String? categoryName;

  /// The regular price. What the customer pays is [payPrice].
  final double price;

  /// The discounted price while the product is on sale (below [price]); null when it is not.
  final double? salePrice;
  final int stock;
  final int? lowStockThreshold;
  final bool isLowStock;
  final String? imageUrl;
  final List<ProductImage> images;
  final bool isActive;

  bool get inStock => stock > 0;
  bool get onSale => salePrice != null && salePrice! < price;

  /// What the customer pays. Display only: the server prices every order itself.
  double get payPrice => onSale ? salePrice! : price;

  /// Whole percent off the regular price (0 when not on sale).
  int get discountPercent =>
      onSale ? ((1 - salePrice! / price) * 100).round() : 0;

  factory Product.fromJson(Map<String, dynamic> j) => Product(
    id: j['id'] as int,
    name: j['name'] as String,
    description: j['description'] as String?,
    brandId: j['brandId'] as int?,
    brandName: j['brandName'] as String?,
    categoryId: j['categoryId'] as int?,
    categoryName: j['categoryName'] as String?,
    price: (j['price'] as num).toDouble(),
    salePrice: (j['salePrice'] as num?)?.toDouble(),
    stock: j['stock'] as int,
    lowStockThreshold: j['lowStockThreshold'] as int?,
    isLowStock: j['isLowStock'] as bool,
    isActive: j['isActive'] as bool,
    imageUrl: j['imageUrl'] as String?,
    images: ((j['images'] as List?) ?? const [])
        .map((e) => ProductImage.fromJson(e as Map<String, dynamic>))
        .toList(),
  );
}

/// Status values as sent by the API.
abstract final class OrderStatus {
  static const pendingPayment = 'PendingPayment';
  static const placed = 'Placed';
  static const fulfilled = 'Fulfilled';
  static const cancelled = 'Cancelled';
  static const expired = 'Expired';
}

abstract final class FulfilmentType {
  static const pickup = 'Pickup';
  static const delivery = 'Delivery';
}

abstract final class PaymentMethod {
  static const card = 'Card';
  static const onReceipt = 'OnReceipt';
}

class OrderItem {
  const OrderItem({
    required this.productId,
    required this.productName,
    required this.unitPrice,
    required this.quantity,
    required this.lineTotal,
  });

  final int productId;
  final String productName;
  final double unitPrice;
  final int quantity;
  final double lineTotal;

  factory OrderItem.fromJson(Map<String, dynamic> j) => OrderItem(
    productId: j['productId'] as int,
    productName: j['productName'] as String,
    unitPrice: (j['unitPrice'] as num).toDouble(),
    quantity: j['quantity'] as int,
    lineTotal: (j['lineTotal'] as num).toDouble(),
  );
}

class ShopOrder {
  const ShopOrder({
    required this.id,
    required this.clientId,
    required this.status,
    required this.fulfilment,
    required this.paymentMethod,
    required this.totalPrice,
    required this.createdAtUtc,
    required this.items,
    this.clientName,
    this.paymentStatus,
    this.deliveryAddress,
    this.note,
    this.expiresUtc,
    this.paymentClientSecret,
    this.isPaid = false,
  });

  final int id;
  final String clientId;
  final String? clientName;
  final String status;
  final String fulfilment;
  final String paymentMethod;
  final String? paymentStatus;
  final String? deliveryAddress;
  final String? note;
  final double totalPrice;
  final DateTime createdAtUtc;
  final DateTime? expiresUtc;
  final List<OrderItem> items;

  /// Only present on the owner's own unpaid card order.
  final String? paymentClientSecret;

  /// The money is in, as decided by the server (card payment verified, or paid at pickup).
  final bool isPaid;

  bool get awaitsPayment => status == OrderStatus.pendingPayment && !isPaid;

  /// What the order's owner may still do: withdraw unpaid orders (and pay-on-receipt ones).
  bool get clientCanCancel =>
      status == OrderStatus.pendingPayment ||
      (status == OrderStatus.placed &&
          paymentMethod == PaymentMethod.onReceipt);

  String get itemSummary =>
      items.map((i) => '${i.quantity}× ${i.productName}').join(', ');

  factory ShopOrder.fromJson(Map<String, dynamic> j) => ShopOrder(
    id: j['id'] as int,
    clientId: j['clientId'] as String,
    clientName: j['clientName'] as String?,
    status: j['status'] as String,
    fulfilment: j['fulfilment'] as String,
    paymentMethod: j['paymentMethod'] as String,
    paymentStatus: j['paymentStatus'] as String?,
    deliveryAddress: j['deliveryAddress'] as String?,
    note: j['note'] as String?,
    totalPrice: (j['totalPrice'] as num).toDouble(),
    createdAtUtc: DateTime.parse(j['createdAtUtc'] as String),
    expiresUtc: j['expiresUtc'] == null
        ? null
        : DateTime.parse(j['expiresUtc'] as String),
    items: (j['items'] as List)
        .map((e) => OrderItem.fromJson(e as Map<String, dynamic>))
        .toList(),
    paymentClientSecret: j['paymentClientSecret'] as String?,
    isPaid: (j['isPaid'] as bool?) ?? false,
  );
}

class ProductImage {
  const ProductImage({required this.id, required this.url});

  final int id;
  final String url;

  factory ProductImage.fromJson(Map<String, dynamic> j) =>
      ProductImage(id: j['id'] as int, url: j['url'] as String);
}

/// How a front-page row of the shop chooses its products (as sent by the API).
abstract final class ShopSectionKind {
  static const newArrivals = 'NewArrivals';
  static const onSale = 'OnSale';
  static const category = 'Category';
  static const manual = 'Manual';

  /// The built-in blocks: they are not lists of products.
  static const categoryTiles = 'CategoryTiles';
  static const brandTiles = 'BrandTiles';
  static const recommended = 'Recommended';

  /// The kinds an admin picks for a list of products.
  static const all = [newArrivals, onSale, category, manual];

  /// Every kind that can be created.
  static const allWithBlocks = [
    newArrivals,
    onSale,
    category,
    manual,
    categoryTiles,
    brandTiles,
    recommended,
  ];

  static bool isBuiltIn(String kind) =>
      kind == categoryTiles || kind == brandTiles || kind == recommended;
}

/// The look of a section or of its card (as sent by the API).
abstract final class ShopLook {
  static const backgrounds = ['None', 'Soft', 'Amber'];
  static const tileSizes = ['Compact', 'Normal', 'Large'];
  static const spans = ['Half', 'Full'];
  static const aligns = ['Left', 'Center'];
}

class ShopSectionStyle {
  const ShopSectionStyle({
    this.background = 'None',
    this.tileSize = 'Normal',
    this.cardSpan = 'Half',
    this.headingAlign = 'Left',
    this.showSeeAll = false,
  });

  final String background;
  final String tileSize;
  final String cardSpan;
  final String headingAlign;
  final bool showSeeAll;

  bool get centered => headingAlign == 'Center';
  bool get fullCard => cardSpan == 'Full';

  ShopSectionStyle copyWith({
    String? background,
    String? tileSize,
    String? cardSpan,
    String? headingAlign,
    bool? showSeeAll,
  }) => ShopSectionStyle(
    background: background ?? this.background,
    tileSize: tileSize ?? this.tileSize,
    cardSpan: cardSpan ?? this.cardSpan,
    headingAlign: headingAlign ?? this.headingAlign,
    showSeeAll: showSeeAll ?? this.showSeeAll,
  );

  Map<String, dynamic> toJson() => {
    'background': background,
    'tileSize': tileSize,
    'cardSpan': cardSpan,
    'headingAlign': headingAlign,
    'showSeeAll': showSeeAll,
  };

  factory ShopSectionStyle.fromJson(Map<String, dynamic>? j) => j == null
      ? const ShopSectionStyle()
      : ShopSectionStyle(
          background: (j['background'] as String?) ?? 'None',
          tileSize: (j['tileSize'] as String?) ?? 'Normal',
          cardSpan: (j['cardSpan'] as String?) ?? 'Half',
          headingAlign: (j['headingAlign'] as String?) ?? 'Left',
          showSeeAll: (j['showSeeAll'] as bool?) ?? false,
        );
}

/// How a section lays out its products (as sent by the API).
abstract final class ShopSectionLayout {
  /// One row that scrolls sideways.
  static const row = 'Row';

  /// Two columns, product under product.
  static const grid = 'Grid';

  /// A wide picture with the title over it, and the row of products under it.
  static const banner = 'Banner';
  static const all = [row, grid, banner];
}

/// A row of the shop front page, already resolved to products (what customers see).
class ShopSection {
  const ShopSection({
    required this.id,
    required this.title,
    required this.kind,
    required this.products,
    this.layout = ShopSectionLayout.row,
    this.imageUrl,
    this.eyebrow,
    this.style = const ShopSectionStyle(),
  });

  final int id;
  final String title;
  final ShopSectionStyle style;

  /// The small amber text above the title; null when the admin left it out.
  final String? eyebrow;
  final String kind;
  final List<Product> products;
  final String layout;

  /// The wide picture of a banner section.
  final String? imageUrl;

  factory ShopSection.fromJson(Map<String, dynamic> j) => ShopSection(
    id: j['id'] as int,
    title: j['title'] as String,
    kind: j['kind'] as String,
    products: ((j['products'] as List?) ?? const [])
        .map((e) => Product.fromJson(e as Map<String, dynamic>))
        .toList(),
    layout: (j['layout'] as String?) ?? ShopSectionLayout.row,
    imageUrl: j['imageUrl'] as String?,
    eyebrow: j['eyebrow'] as String?,
    style: ShopSectionStyle.fromJson(j['style'] as Map<String, dynamic>?),
  );
}

/// A section as the admin edits it: the rule, not the resulting products.
class ShopSectionRule {
  /// The most characters a section or card title may have (the server enforces the same limit).
  static const maxTitleLength = 24;
  static const minTitleLength = 2;
  static const maxEyebrowLength = 24;

  const ShopSectionRule({
    required this.id,
    required this.title,
    required this.kind,
    required this.maxItems,
    required this.sortOrder,
    required this.isActive,
    this.categoryId,
    this.categoryName,
    this.brandId,
    this.brandName,
    this.productIds = const [],
    this.showAsCard = false,
    this.imageUrl,
    this.imageProductImageId,
    this.layout = ShopSectionLayout.row,
    this.eyebrow,
    this.productCount = 0,
    this.entryIds = const [],
    this.style = const ShopSectionStyle(),
  });

  final int id;
  final String title;
  final String kind;
  final int? categoryId;
  final String? categoryName;
  final int? brandId;
  final String? brandName;
  final int maxItems;
  final int sortOrder;
  final bool isActive;
  final List<int> productIds;

  /// Also shown as a card on the shop entry page.
  final bool showAsCard;

  /// The picture of the card (uploaded or picked), if the admin chose one.
  final String? imageUrl;

  /// Set when the picture is one of a product's pictures (so the picker can mark it).
  final int? imageProductImageId;

  /// How the section lays out its products ([ShopSectionLayout]).
  final String layout;

  /// The small amber text above the title (optional).
  final String? eyebrow;

  /// How many items it shows right now. Zero means customers do not see the section at all.
  final int productCount;

  /// The categories or brands picked for a tile block, in order (empty = automatic).
  final List<int> entryIds;
  final ShopSectionStyle style;

  factory ShopSectionRule.fromJson(Map<String, dynamic> j) => ShopSectionRule(
    id: j['id'] as int,
    title: j['title'] as String,
    kind: j['kind'] as String,
    categoryId: j['categoryId'] as int?,
    categoryName: j['categoryName'] as String?,
    brandId: j['brandId'] as int?,
    brandName: j['brandName'] as String?,
    maxItems: j['maxItems'] as int,
    sortOrder: j['sortOrder'] as int,
    isActive: j['isActive'] as bool,
    productIds: ((j['productIds'] as List?) ?? const []).cast<int>(),
    showAsCard: (j['showAsCard'] as bool?) ?? false,
    imageUrl: j['imageUrl'] as String?,
    imageProductImageId: j['imageProductImageId'] as int?,
    layout: (j['layout'] as String?) ?? ShopSectionLayout.row,
    eyebrow: j['eyebrow'] as String?,
    productCount: (j['productCount'] as int?) ?? 0,
    entryIds: ((j['entryIds'] as List?) ?? const []).cast<int>(),
    style: ShopSectionStyle.fromJson(j['style'] as Map<String, dynamic>?),
  );
}

/// A category or brand tile of the shop entry page.
class ShopTile {
  const ShopTile({
    required this.id,
    required this.name,
    required this.productCount,
    this.imageUrl,
    this.isLogo = false,
  });

  final int id;
  final String name;
  final int productCount;
  final String? imageUrl;

  /// The picture is a brand logo (to be shown whole on a light ground), not a product photo.
  final bool isLogo;

  factory ShopTile.fromJson(Map<String, dynamic> j) => ShopTile(
    id: j['id'] as int,
    name: j['name'] as String,
    productCount: j['productCount'] as int,
    imageUrl: j['imageUrl'] as String?,
    isLogo: (j['isLogo'] as bool?) ?? false,
  );
}

/// A shop section shown as a card on the entry page; tapping it opens the products behind it.
class ShopCard {
  const ShopCard({
    required this.sectionId,
    required this.title,
    required this.kind,
    required this.productCount,
    required this.maxDiscountPercent,
    this.imageUrl,
    this.eyebrow,
    this.style = const ShopSectionStyle(),
  });

  final int sectionId;
  final String title;
  final String? eyebrow;
  final ShopSectionStyle style;
  final String kind;
  final int productCount;
  final int maxDiscountPercent;
  final String? imageUrl;

  factory ShopCard.fromJson(Map<String, dynamic> j) => ShopCard(
    sectionId: j['sectionId'] as int,
    title: j['title'] as String,
    kind: j['kind'] as String,
    productCount: j['productCount'] as int,
    maxDiscountPercent: j['maxDiscountPercent'] as int,
    imageUrl: j['imageUrl'] as String?,
    eyebrow: j['eyebrow'] as String?,
    style: ShopSectionStyle.fromJson(j['style'] as Map<String, dynamic>?),
  );
}

/// One block of the shop entry page, in the order the admin arranged it.
class ShopBlock {
  const ShopBlock({
    required this.sectionId,
    required this.kind,
    required this.title,
    this.eyebrow,
    this.layout = ShopSectionLayout.row,
    this.imageUrl,
    this.products = const [],
    this.tiles = const [],
    this.style = const ShopSectionStyle(),
  });

  final int sectionId;
  final String kind;
  final String title;
  final String? eyebrow;
  final String layout;
  final String? imageUrl;

  /// The products of a product block.
  final List<Product> products;

  /// The categories or brands of a tile block.
  final List<ShopTile> tiles;
  final ShopSectionStyle style;

  /// A product block as the section widgets draw it.
  ShopSection toSection() => ShopSection(
    id: sectionId,
    title: title,
    kind: kind,
    products: products,
    layout: layout,
    imageUrl: imageUrl,
    eyebrow: eyebrow,
    style: style,
  );

  factory ShopBlock.fromJson(Map<String, dynamic> j) => ShopBlock(
    sectionId: j['sectionId'] as int,
    title: j['title'] as String,
    kind: j['kind'] as String,
    eyebrow: j['eyebrow'] as String?,
    layout: (j['layout'] as String?) ?? ShopSectionLayout.row,
    imageUrl: j['imageUrl'] as String?,
    products: ((j['products'] as List?) ?? const [])
        .map((e) => Product.fromJson(e as Map<String, dynamic>))
        .toList(),
    tiles: ((j['tiles'] as List?) ?? const [])
        .map((e) => ShopTile.fromJson(e as Map<String, dynamic>))
        .toList(),
    style: ShopSectionStyle.fromJson(j['style'] as Map<String, dynamic>?),
  );
}

/// Everything the shop entry page shows.
class ShopOverview {
  const ShopOverview({
    required this.categories,
    required this.brands,
    this.cards = const [],
    this.blocks = const [],
  });

  final List<ShopTile> categories;
  final List<ShopTile> brands;

  /// The blocks below the cards in the admin's order: tile blocks, "you may like" and the sections.
  final List<ShopBlock> blocks;

  /// Sections the admin chose to show as cards. When there are none the page offers the automatic
  /// "on sale" and "new" shortcuts instead.
  final List<ShopCard> cards;

  factory ShopOverview.fromJson(Map<String, dynamic> j) => ShopOverview(
    categories: ((j['categories'] as List?) ?? const [])
        .map((e) => ShopTile.fromJson(e as Map<String, dynamic>))
        .toList(),
    brands: ((j['brands'] as List?) ?? const [])
        .map((e) => ShopTile.fromJson(e as Map<String, dynamic>))
        .toList(),
    cards: ((j['cards'] as List?) ?? const [])
        .map((e) => ShopCard.fromJson(e as Map<String, dynamic>))
        .toList(),
    blocks: ((j['blocks'] as List?) ?? const [])
        .map((e) => ShopBlock.fromJson(e as Map<String, dynamic>))
        .toList(),
  );
}
