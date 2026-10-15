import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

import 'checkout_page.dart';

class CartPage extends StatelessWidget {
  const CartPage({
    super.key,
    required this.shop,
    required this.cart,
    required this.cardPayment,
  });

  final ShopApi shop;
  final CartController cart;
  final CardPaymentLauncher cardPayment;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return Scaffold(
      appBar: AppBar(title: Text(t.cart)),
      body: ListenableBuilder(
        listenable: cart,
        builder: (context, _) {
          if (cart.isEmpty) {
            return EmptyState(
              icon: Icons.shopping_cart_outlined,
              message: t.cartEmpty,
            );
          }
          return Column(
            children: [
              Expanded(
                child: ListView.separated(
                  padding: const EdgeInsets.all(16),
                  itemCount: cart.lines.length,
                  separatorBuilder: (_, _) => const SizedBox(height: 12),
                  itemBuilder: (context, i) {
                    final line = cart.lines[i];
                    final p = line.product;
                    return Card(
                      key: Key('cart_line_${p.id}'),
                      child: Padding(
                        padding: const EdgeInsets.all(12),
                        child: Row(
                          children: [
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(
                                    p.name,
                                    style: const TextStyle(
                                      fontWeight: FontWeight.w700,
                                    ),
                                  ),
                                  Text(money(t, p.price)),
                                ],
                              ),
                            ),
                            IconButton(
                              key: Key('dec_${p.id}'),
                              icon: const Icon(Icons.remove_circle_outline),
                              onPressed: () =>
                                  cart.setQuantity(p, line.quantity - 1),
                            ),
                            Text(
                              '${line.quantity}',
                              style: const TextStyle(
                                fontWeight: FontWeight.w700,
                              ),
                            ),
                            IconButton(
                              key: Key('inc_${p.id}'),
                              icon: const Icon(Icons.add_circle_outline),
                              onPressed: line.quantity < cart.limitFor(p)
                                  ? () => cart.add(p)
                                  : null,
                            ),
                            SizedBox(
                              width: 76,
                              child: Text(
                                money(t, line.total),
                                textAlign: TextAlign.end,
                                style: const TextStyle(
                                  fontWeight: FontWeight.w700,
                                ),
                              ),
                            ),
                          ],
                        ),
                      ),
                    );
                  },
                ),
              ),
              SafeArea(
                top: false,
                child: Padding(
                  padding: const EdgeInsets.fromLTRB(16, 8, 16, 16),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Row(
                        children: [
                          Text(
                            t.total,
                            style: Theme.of(context).textTheme.titleMedium,
                          ),
                          const Spacer(),
                          Text(
                            money(t, cart.total),
                            key: const Key('cart_total'),
                            style: Theme.of(context).textTheme.titleLarge
                                ?.copyWith(fontWeight: FontWeight.w800),
                          ),
                        ],
                      ),
                      const SizedBox(height: 12),
                      FilledButton(
                        key: const Key('go_checkout'),
                        style: FilledButton.styleFrom(
                          minimumSize: const Size.fromHeight(52),
                        ),
                        onPressed: () => Navigator.of(context).push(
                          MaterialPageRoute<void>(
                            builder: (_) => CheckoutPage(
                              shop: shop,
                              cart: cart,
                              cardPayment: cardPayment,
                            ),
                          ),
                        ),
                        child: Text(t.checkout),
                      ),
                    ],
                  ),
                ),
              ),
            ],
          );
        },
      ),
    );
  }
}
