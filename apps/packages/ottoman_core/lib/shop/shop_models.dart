class Brand {
  const Brand({required this.id, required this.name});
  final int id;
  final String name;

  factory Brand.fromJson(Map<String, dynamic> j) =>
      Brand(id: j['id'] as int, name: j['name'] as String);
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
  });

  final int id;
  final String name;
  final String? description;
  final int? brandId;
  final String? brandName;
  final int? categoryId;
  final String? categoryName;
  final double price;
  final int stock;
  final int? lowStockThreshold;
  final bool isLowStock;
  final bool isActive;

  bool get inStock => stock > 0;

  factory Product.fromJson(Map<String, dynamic> j) => Product(
    id: j['id'] as int,
    name: j['name'] as String,
    description: j['description'] as String?,
    brandId: j['brandId'] as int?,
    brandName: j['brandName'] as String?,
    categoryId: j['categoryId'] as int?,
    categoryName: j['categoryName'] as String?,
    price: (j['price'] as num).toDouble(),
    stock: j['stock'] as int,
    lowStockThreshold: j['lowStockThreshold'] as int?,
    isLowStock: j['isLowStock'] as bool,
    isActive: j['isActive'] as bool,
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

  bool get awaitsPayment => status == OrderStatus.pendingPayment;

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
  );
}
