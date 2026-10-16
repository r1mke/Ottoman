import 'package:flutter/material.dart';

/// A picture from the server with a quiet placeholder when there is none, while it loads, or when it fails.
/// Used everywhere a product, service or barber can have a photo, so the fallback looks the same.
class NetworkPicture extends StatelessWidget {
  const NetworkPicture({
    super.key,
    required this.url,
    this.fallbackIcon = Icons.image_outlined,
    this.fit = BoxFit.cover,
    this.width,
    this.height,
  });

  final String? url;
  final IconData fallbackIcon;
  final BoxFit fit;
  final double? width;
  final double? height;

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    Widget placeholder() => Container(
      key: const Key('picture-placeholder'),
      width: width,
      height: height,
      color: colors.surfaceContainerHighest,
      alignment: Alignment.center,
      child: Icon(fallbackIcon, color: colors.onSurfaceVariant),
    );

    final address = url;
    if (address == null || address.isEmpty) return placeholder();
    return Image.network(
      address,
      key: const Key('picture-image'),
      width: width,
      height: height,
      fit: fit,
      excludeFromSemantics: true,
      errorBuilder: (_, _, _) => placeholder(),
      loadingBuilder: (_, child, progress) =>
          progress == null ? child : placeholder(),
    );
  }
}
