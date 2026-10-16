import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

/// A horizontal strip of product suggestions ("Možda vam se sviđa" / "Slično").
/// Hidden while loading, on error and when there is nothing to suggest: a suggestion row must never get in the way.
class RecommendedRow extends StatelessWidget {
  const RecommendedRow({
    super.key,
    required this.title,
    required this.load,
    required this.cart,
    required this.onOpen,
  });

  final String title;
  final Future<List<Product>> Function() load;
  final CartController cart;
  final void Function(Product) onOpen;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final scheme = Theme.of(context).colorScheme;
    return FutureBuilder<List<Product>>(
      future: load(),
      builder: (context, snap) {
        final items = snap.data;
        if (items == null || items.isEmpty) return const SizedBox.shrink();
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 8, 16, 4),
              child: Text(
                title,
                style: Theme.of(context).textTheme.titleMedium
                    ?.copyWith(fontWeight: FontWeight.w700),
              ),
            ),
            SizedBox(
              height: 118,
              child: ListView.separated(
                scrollDirection: Axis.horizontal,
                padding: const EdgeInsets.symmetric(horizontal: 16),
                itemCount: items.length,
                separatorBuilder: (_, _) => const SizedBox(width: 10),
                itemBuilder: (context, i) {
                  final p = items[i];
                  return SizedBox(
                    width: 150,
                    child: Card(
                      key: Key('reco_${p.id}'),
                      clipBehavior: Clip.antiAlias,
                      child: InkWell(
                        onTap: () => onOpen(p),
                        child: Padding(
                          padding: const EdgeInsets.all(10),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                p.name,
                                maxLines: 2,
                                overflow: TextOverflow.ellipsis,
                                style: const TextStyle(
                                  fontWeight: FontWeight.w700,
                                  fontSize: 13,
                                ),
                              ),
                              if (p.brandName != null)
                                Text(
                                  p.brandName!,
                                  style: TextStyle(
                                    color: scheme.outline,
                                    fontSize: 11,
                                  ),
                                ),
                              const Spacer(),
                              Row(
                                children: [
                                  Expanded(
                                    child: FittedBox(
                                      alignment: Alignment.centerLeft,
                                      fit: BoxFit.scaleDown,
                                      child: Text(
                                        money(t, p.price),
                                        style: const TextStyle(
                                          fontWeight: FontWeight.w800,
                                        ),
                                      ),
                                    ),
                                  ),
                                  InkResponse(
                                    key: Key('reco_add_${p.id}'),
                                    onTap: () => cart.add(p),
                                    child: const Icon(
                                      Icons.add_shopping_cart,
                                      size: 20,
                                    ),
                                  ),
                                ],
                              ),
                            ],
                          ),
                        ),
                      ),
                    ),
                  );
                },
              ),
            ),
          ],
        );
      },
    );
  }
}
