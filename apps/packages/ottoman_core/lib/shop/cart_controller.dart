import 'package:flutter/foundation.dart';

import 'shop_models.dart';

class CartLine {
  const CartLine(this.product, this.quantity);
  final Product product;
  final int quantity;

  /// Display only. The server recomputes every price at checkout.
  double get total => product.price * quantity;
}

/// The shopping basket lives on the device; the server only ever receives product ids and quantities.
class CartController extends ChangeNotifier {
  static const maxPerLine = 20;

  final Map<int, CartLine> _lines = {};

  List<CartLine> get lines => _lines.values.toList(growable: false);
  bool get isEmpty => _lines.isEmpty;
  int get itemCount => _lines.values.fold(0, (n, l) => n + l.quantity);
  double get total => _lines.values.fold(0.0, (s, l) => s + l.total);

  int quantityOf(int productId) => _lines[productId]?.quantity ?? 0;

  /// The most of this product that can sit in the basket: what is in stock, capped per line.
  int limitFor(Product p) => p.stock < maxPerLine ? p.stock : maxPerLine;

  /// Adds one piece. Returns false when the stock (or the per-line cap) would be exceeded.
  bool add(Product p) {
    final current = quantityOf(p.id);
    if (current + 1 > limitFor(p)) return false;
    _lines[p.id] = CartLine(p, current + 1);
    notifyListeners();
    return true;
  }

  void setQuantity(Product p, int quantity) {
    final q = quantity > limitFor(p) ? limitFor(p) : quantity;
    if (q <= 0) {
      remove(p.id);
      return;
    }
    _lines[p.id] = CartLine(p, q);
    notifyListeners();
  }

  void remove(int productId) {
    if (_lines.remove(productId) != null) notifyListeners();
  }

  void clear() {
    if (_lines.isEmpty) return;
    _lines.clear();
    notifyListeners();
  }

  /// What goes to the server.
  Map<int, int> toQuantities() => {
    for (final l in _lines.values) l.product.id: l.quantity,
  };
}
