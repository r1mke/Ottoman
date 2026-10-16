import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

/// Swipeable pictures of a product with a dot indicator; a quiet placeholder when it has none.
class ProductGallery extends StatefulWidget {
  const ProductGallery({super.key, required this.images, this.height = 220});

  final List<ProductImage> images;
  final double height;

  @override
  State<ProductGallery> createState() => _ProductGalleryState();
}

class _ProductGalleryState extends State<ProductGallery> {
  int _page = 0;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final images = widget.images;
    return ClipRRect(
      borderRadius: BorderRadius.circular(16),
      child: SizedBox(
        height: widget.height,
        child: images.isEmpty
            ? const NetworkPicture(
                url: null,
                fallbackIcon: Icons.inventory_2_outlined,
              )
            : Stack(
                children: [
                  PageView.builder(
                    key: const Key('gallery'),
                    itemCount: images.length,
                    onPageChanged: (i) => setState(() => _page = i),
                    itemBuilder: (_, i) => NetworkPicture(
                      url: images[i].url,
                      fallbackIcon: Icons.inventory_2_outlined,
                    ),
                  ),
                  if (images.length > 1)
                    Positioned(
                      left: 0,
                      right: 0,
                      bottom: 8,
                      child: Row(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          for (var i = 0; i < images.length; i++)
                            Container(
                              width: 8,
                              height: 8,
                              margin: const EdgeInsets.symmetric(horizontal: 3),
                              decoration: BoxDecoration(
                                shape: BoxShape.circle,
                                color: i == _page
                                    ? scheme.primary
                                    : scheme.surface.withValues(alpha: 0.7),
                              ),
                            ),
                        ],
                      ),
                    ),
                ],
              ),
      ),
    );
  }
}
