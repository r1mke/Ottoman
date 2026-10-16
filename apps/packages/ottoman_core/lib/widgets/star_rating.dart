import 'package:flutter/material.dart';

/// Five stars. Read-only when [onChanged] is null; otherwise tapping a star sets the rating.
class StarRating extends StatelessWidget {
  const StarRating({
    super.key,
    required this.value,
    this.onChanged,
    this.size = 18,
  });

  /// 0-5, may be fractional (an average); shown rounded to the nearest half star.
  final double value;
  final ValueChanged<int>? onChanged;
  final double size;

  @override
  Widget build(BuildContext context) {
    final color = Colors.amber.shade700;
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        for (var i = 1; i <= 5; i++)
          GestureDetector(
            key: Key('star_$i'),
            behavior: HitTestBehavior.opaque,
            onTap: onChanged == null ? null : () => onChanged!(i),
            child: Padding(
              padding: EdgeInsets.all(onChanged == null ? 0 : 4),
              child: Icon(
                value >= i - 0.25
                    ? Icons.star
                    : value >= i - 0.75
                    ? Icons.star_half
                    : Icons.star_border,
                size: size,
                color: color,
              ),
            ),
          ),
      ],
    );
  }
}

/// "★★★★☆ 4.3 (12)" or nothing when there are no reviews yet.
class RatingSummary extends StatelessWidget {
  const RatingSummary({
    super.key,
    required this.average,
    required this.count,
    this.size = 14,
  });

  final double average;
  final int count;
  final double size;

  @override
  Widget build(BuildContext context) {
    if (count == 0) return const SizedBox.shrink();
    // Scales down instead of overflowing in narrow cards.
    return FittedBox(
      fit: BoxFit.scaleDown,
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          StarRating(value: average, size: size),
          const SizedBox(width: 4),
          Text(
            '${average.toStringAsFixed(1)} ($count)',
            style: TextStyle(fontSize: size - 1),
          ),
        ],
      ),
    );
  }
}
