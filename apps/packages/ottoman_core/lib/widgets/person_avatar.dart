import 'package:flutter/material.dart';

/// "Male" / "Female" as the server sends them; anything else gets the neutral avatar.
enum AvatarGender {
  male,
  female,
  unknown;

  static const variants = 3;

  static AvatarGender parse(String? value) => switch (value) {
    'Male' => male,
    'Female' => female,
    _ => unknown,
  };

  /// Which of the drawn avatars of this gender a person gets: stable for the same [seed] (the barber's id) in
  /// every app and every run, so a barber does not change face between the list and the sign-in card.
  String asset(String? seed) {
    final n = (seed ?? '').codeUnits.fold<int>(
      0,
      (a, b) => (a * 31 + b) & 0x7fffffff,
    );
    final i = n % variants + 1;
    return switch (this) {
      male => 'assets/avatars/male_$i.png',
      female => 'assets/avatars/female_$i.png',
      unknown => 'assets/avatars/neutral.png',
    };
  }

  /// Only shown if the drawn picture cannot be loaded.
  IconData get icon => switch (this) {
    male => Icons.face_6,
    female => Icons.face_3,
    unknown => Icons.person,
  };

  Color get background => switch (this) {
    male => const Color(0xFFD7E6F7),
    female => const Color(0xFFF7DCE6),
    unknown => const Color(0xFFE3E7EC),
  };

  Color get foreground => switch (this) {
    male => const Color(0xFF2F6CA8),
    female => const Color(0xFFB04A72),
    unknown => const Color(0xFF5C6672),
  };
}

/// The drawn default avatar of a gender; falls back to an icon if the asset is missing.
class _DefaultAvatarImage extends StatelessWidget {
  const _DefaultAvatarImage({
    required this.gender,
    required this.seed,
    required this.size,
  });

  final AvatarGender gender;
  final String? seed;
  final double size;

  @override
  Widget build(BuildContext context) => Image.asset(
    gender.asset(seed),
    key: const Key('avatar-default'),
    package: 'ottoman_core',
    width: size,
    height: size,
    fit: BoxFit.cover,
    excludeFromSemantics: true,
    errorBuilder: (_, _, _) => Container(
      width: size,
      height: size,
      color: gender.background,
      alignment: Alignment.center,
      child: Icon(gender.icon, size: size * 0.6, color: gender.foreground),
    ),
  );
}

Widget _photo(String url) => Image.network(
  url,
  key: const Key('avatar-photo'),
  fit: BoxFit.cover,
  excludeFromSemantics: true,
  errorBuilder: (_, _, _) => const SizedBox.shrink(),
  frameBuilder: (_, child, frame, sync) =>
      frame == null && !sync ? const SizedBox.shrink() : child,
);

/// A barber's photo, or - when there is none or it fails to load - a drawn default avatar chosen by gender.
class PersonAvatar extends StatelessWidget {
  const PersonAvatar({
    super.key,
    this.photoUrl,
    this.gender,
    this.seed,
    this.radius = 20,
  });

  final String? photoUrl;

  /// "Male" or "Female".
  final String? gender;

  /// Picks among the avatars of that gender; use the barber's id.
  final String? seed;
  final double radius;

  @override
  Widget build(BuildContext context) {
    final g = AvatarGender.parse(gender);
    final url = photoUrl;
    return SizedBox(
      width: radius * 2,
      height: radius * 2,
      child: ClipOval(
        child: Stack(
          fit: StackFit.expand,
          children: [
            _DefaultAvatarImage(gender: g, seed: seed, size: radius * 2),
            if (url != null && url.isNotEmpty) _photo(url),
          ],
        ),
      ),
    );
  }
}

/// The same avatar as a rounded square (the sign-in cards).
class PersonAvatarTile extends StatelessWidget {
  const PersonAvatarTile({
    super.key,
    this.photoUrl,
    this.gender,
    this.seed,
    this.size = 120,
  });

  final String? photoUrl;
  final String? gender;
  final String? seed;
  final double size;

  @override
  Widget build(BuildContext context) {
    final g = AvatarGender.parse(gender);
    final url = photoUrl;
    return ClipRRect(
      borderRadius: BorderRadius.circular(size * 0.08),
      child: SizedBox(
        width: size,
        height: size,
        child: Stack(
          fit: StackFit.expand,
          children: [
            _DefaultAvatarImage(gender: g, seed: seed, size: size),
            if (url != null && url.isNotEmpty) _photo(url),
          ],
        ),
      ),
    );
  }
}
