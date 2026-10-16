import 'package:flutter/material.dart';

/// Round photo with the person's initials underneath, so a missing or failing picture looks intentional.
class PersonAvatar extends StatelessWidget {
  const PersonAvatar({
    super.key,
    required this.initials,
    this.photoUrl,
    this.radius = 20,
  });

  final String initials;
  final String? photoUrl;
  final double radius;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final url = photoUrl;
    return CircleAvatar(
      radius: radius,
      backgroundColor: scheme.surfaceContainerHighest,
      child: Stack(
        alignment: Alignment.center,
        children: [
          Text(
            initials,
            style: TextStyle(
              fontSize: radius * 0.7,
              color: scheme.onSurfaceVariant,
              fontWeight: FontWeight.w600,
            ),
          ),
          if (url != null && url.isNotEmpty)
            ClipOval(
              child: Image.network(
                url,
                key: const Key('avatar-photo'),
                width: radius * 2,
                height: radius * 2,
                fit: BoxFit.cover,
                excludeFromSemantics: true,
                errorBuilder: (_, _, _) => const SizedBox.shrink(),
                frameBuilder: (_, child, frame, sync) =>
                    frame == null && !sync ? const SizedBox.shrink() : child,
              ),
            ),
        ],
      ),
    );
  }
}
